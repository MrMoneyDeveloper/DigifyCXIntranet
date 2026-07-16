[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$AppPoolName = "DigifyCXIntranet",
    [string]$LiveRoot = "C:\Sites\DigifyCXIntranet\Live",
    [ValidateSet("Auto", "AppPool", "WebConfig")]
    [string]$Method = "Auto",
    [switch]$ReadFromLocalCommittedAppSettings,
    [switch]$Plan,
    [switch]$ListNames,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ($AppPoolName -eq "DefaultAppPool") {
    throw "Refusing to modify DefaultAppPool."
}

$requiredKeys = @(
    "ASPNETCORE_ENVIRONMENT",
    "DOTNET_ENVIRONMENT",
    "AuthMode__AllowInsecureHttpForInternalTest",
    "AuthMode__SeedConfiguredTestUsers",
    "ConnectionStrings__DefaultConnection",
    "ZendeskSync__Subdomain",
    "ZendeskSync__BaseUrl",
    "ZendeskSync__Email",
    "ZendeskSync__ApiToken",
    "ZendeskWebhook__Secret",
    "Activation__DefaultPassword",
    "Activation__SheetApiUrl",
    "UserRegistrySync__SheetApiUrl"
)

function New-IisServerManager {
    $assemblyPath = Join-Path $env:windir "System32\inetsrv\Microsoft.Web.Administration.dll"
    if (Test-Path -LiteralPath $assemblyPath) {
        Add-Type -Path $assemblyPath
    }
    else {
        Add-Type -AssemblyName Microsoft.Web.Administration
    }

    return [Microsoft.Web.Administration.ServerManager]::new()
}

function Get-IisApplicationPool {
    param(
        [Parameter(Mandatory = $true)]
        [Microsoft.Web.Administration.ServerManager]$ServerManager,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $ServerManager.ApplicationPools) {
        throw "IIS ApplicationPools collection is unavailable. Run this from an Administrator PowerShell on the IIS machine."
    }

    foreach ($pool in $ServerManager.ApplicationPools) {
        if ($pool.Name -eq $Name) {
            return $pool
        }
    }

    return $null
}

function Get-AppPoolEnvCollection {
    param(
        [Parameter(Mandatory = $true)]
        [Microsoft.Web.Administration.ServerManager]$ServerManager,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $config = $ServerManager.GetApplicationHostConfiguration()
    $section = $config.GetSection("system.applicationHost/applicationPools")
    foreach ($element in $section.GetCollection()) {
        if ([string]$element.GetAttributeValue("name") -ne $Name) {
            continue
        }

        $processModel = $element.GetChildElement("processModel")
        if ($null -eq $processModel) {
            return $null
        }

        try {
            return $processModel.GetCollection("environmentVariables")
        }
        catch {
            return $null
        }
    }

    throw "App pool does not exist: $Name. Create the IIS site/app pool first."
}

function Find-ConfigElementByName {
    param(
        [AllowNull()][object]$Collection,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Collection) {
        return $null
    }

    foreach ($item in $Collection) {
        $itemName = $null
        try {
            $itemName = [string]$item.GetAttributeValue("name")
        }
        catch {
            $itemName = [string]$item.name
        }

        if ($itemName -eq $Name) {
            return $item
        }
    }

    return $null
}

function Test-Placeholder {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $true
    }

    $text = [string]$Value
    return [string]::IsNullOrWhiteSpace($text) -or
        $text -match '^__[^_].*__$' -or
        $text -match '^REPLACE_' -or
        $text -match 'SET_MANUALLY'
}

function ConvertTo-Hashtable {
    param([AllowNull()][object]$Node)

    if ($null -eq $Node) {
        return $null
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        $table = @{}
        foreach ($property in $Node.PSObject.Properties) {
            $table[$property.Name] = ConvertTo-Hashtable $property.Value
        }
        return $table
    }

    if ($Node -is [System.Array]) {
        return @($Node | ForEach-Object { ConvertTo-Hashtable $_ })
    }

    return $Node
}

function Get-JsonFromGit {
    param([string]$Path)

    $listed = & git ls-tree -r --name-only HEAD -- $Path 2>$null
    if ($LASTEXITCODE -ne 0 -or $listed -notcontains $Path) {
        return $null
    }

    $raw = & git show "HEAD:$Path" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($raw -join "`n"))) {
        return $null
    }

    return ConvertTo-Hashtable (($raw -join "`n") | ConvertFrom-Json -ErrorAction Stop)
}

function Get-ConfigValue {
    param(
        [array]$Sources,
        [string[]]$Path
    )

    foreach ($source in $Sources) {
        if ($null -eq $source) {
            continue
        }

        $node = $source
        foreach ($part in $Path) {
            if ($node -is [hashtable] -and $node.ContainsKey($part)) {
                $node = $node[$part]
            }
            else {
                $node = $null
                break
            }
        }

        if (-not (Test-Placeholder $node)) {
            return [string]$node
        }
    }

    return $null
}

function Read-SecretValue {
    param([string]$Name)

    $secure = Read-Host "Enter value for $Name" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function Resolve-LiveWebConfigPath {
    param([string]$Root)

    $fullRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Root)
    $webConfig = Join-Path $fullRoot "web.config"

    if (-not ($webConfig.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to use web.config outside live root: $fullRoot"
    }

    return [pscustomobject]@{
        Root = $fullRoot
        WebConfig = $webConfig
    }
}

function Get-WebConfigEnvironmentVariableNames {
    param([string]$WebConfigPath)

    if (-not (Test-Path -LiteralPath $WebConfigPath)) {
        return @()
    }

    [xml]$xml = Get-Content -LiteralPath $WebConfigPath -Raw
    $aspNetCore = Get-AspNetCoreElement -Xml $xml
    if ($null -eq $aspNetCore) {
        return @()
    }

    $environmentVariables = $aspNetCore.SelectSingleNode("environmentVariables")
    if ($null -eq $environmentVariables) {
        return @()
    }

    return @($environmentVariables.SelectNodes("environmentVariable") | ForEach-Object { $_.name })
}

function Get-AspNetCoreElement {
    param([xml]$Xml)

    $aspNetCore = $Xml.SelectSingleNode("/configuration/system.webServer/aspNetCore")
    if ($null -ne $aspNetCore) {
        return $aspNetCore
    }

    $aspNetCore = $Xml.SelectSingleNode("/configuration/location[@path='.']/system.webServer/aspNetCore")
    if ($null -ne $aspNetCore) {
        return $aspNetCore
    }

    return $Xml.SelectSingleNode("/configuration/location/system.webServer/aspNetCore")
}

function Set-WebConfigEnvironmentVariables {
    param(
        [string]$WebConfigPath,
        [System.Collections.Specialized.OrderedDictionary]$Values,
        [string[]]$Keys
    )

    if (-not (Test-Path -LiteralPath $WebConfigPath)) {
        throw "Fallback web.config does not exist: $WebConfigPath. Copy the published site first, then re-run this script with -Method WebConfig."
    }

    [xml]$xml = Get-Content -LiteralPath $WebConfigPath -Raw
    $configuration = $xml.SelectSingleNode("/configuration")
    if ($null -eq $configuration) {
        throw "web.config is missing /configuration: $WebConfigPath"
    }

    $aspNetCore = Get-AspNetCoreElement -Xml $xml
    if ($null -eq $aspNetCore) {
        throw "web.config is missing an aspNetCore element under /configuration/system.webServer or /configuration/location/system.webServer. Refusing to create an aspNetCore handler from scratch."
    }

    $environmentVariables = $aspNetCore.SelectSingleNode("environmentVariables")
    if ($null -eq $environmentVariables) {
        $environmentVariables = $xml.CreateElement("environmentVariables")
        $null = $aspNetCore.AppendChild($environmentVariables)
    }

    $changed = $false
    foreach ($key in $Keys) {
        $node = $null
        foreach ($candidate in $environmentVariables.SelectNodes("environmentVariable")) {
            if ($candidate.name -eq $key) {
                $node = $candidate
                break
            }
        }

        if ($null -eq $node) {
            $node = $xml.CreateElement("environmentVariable")
            $nameAttribute = $xml.CreateAttribute("name")
            $nameAttribute.Value = $key
            $null = $node.Attributes.Append($nameAttribute)
            $valueAttribute = $xml.CreateAttribute("value")
            $valueAttribute.Value = [string]$Values[$key]
            $null = $node.Attributes.Append($valueAttribute)
            $null = $environmentVariables.AppendChild($node)
            $changed = $true
        }
        elseif ($node.value -ne [string]$Values[$key]) {
            $node.value = [string]$Values[$key]
            $changed = $true
        }
    }

    if ($changed) {
        $backupPath = "$WebConfigPath.$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
        Copy-Item -LiteralPath $WebConfigPath -Destination $backupPath -Force
        $xml.Save($WebConfigPath)
        Write-Host "Updated deployed web.config and created backup: $backupPath"
        Write-Host "Secrets are stored in deployed web.config. Protect this file with NTFS permissions and do not commit it."
    }

    return $changed
}

$serverManager = New-IisServerManager
$appPool = Get-IisApplicationPool -ServerManager $serverManager -Name $AppPoolName
if ($null -eq $appPool) {
    throw "App pool does not exist: $AppPoolName. Create the IIS site/app pool first."
}

$appPoolEnvCollection = $null
try {
    $appPoolEnvCollection = Get-AppPoolEnvCollection -ServerManager $serverManager -Name $AppPoolName
}
catch {
    throw
}

$webConfigInfo = Resolve-LiveWebConfigPath -Root $LiveRoot
$selectedMethod = $Method
if ($selectedMethod -eq "Auto") {
    if ($null -ne $appPoolEnvCollection) {
        $selectedMethod = "AppPool"
    }
    else {
        $selectedMethod = "WebConfig"
    }
}

if ($selectedMethod -eq "AppPool" -and $null -eq $appPoolEnvCollection) {
    throw "IIS app-pool environment variable collection is unavailable on this setup. Use -Method WebConfig after the site has been copied to $($webConfigInfo.WebConfig)."
}

if ($selectedMethod -eq "WebConfig" -and -not (Test-Path -LiteralPath $webConfigInfo.WebConfig)) {
    throw "WebConfig method requires deployed web.config first: $($webConfigInfo.WebConfig)"
}

if ($ListNames) {
    Write-Host "Configured environment variable names for '$AppPoolName' only. Values are not displayed."
    if ($selectedMethod -eq "AppPool") {
        foreach ($item in $appPoolEnvCollection) {
            Write-Host "- $($item.GetAttributeValue('name'))"
        }
    }
    else {
        foreach ($name in (Get-WebConfigEnvironmentVariableNames -WebConfigPath $webConfigInfo.WebConfig | Sort-Object)) {
            Write-Host "- $name"
        }
    }
    exit 0
}

$values = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "DOTNET_ENVIRONMENT" = "Production"
    "AuthMode__AllowInsecureHttpForInternalTest" = "true"
    "AuthMode__SeedConfiguredTestUsers" = "true"
}

if (-not $Plan -and -not $ReadFromLocalCommittedAppSettings) {
    $existingScopedValues = [ordered]@{}
    if ($selectedMethod -eq "AppPool") {
        foreach ($item in $appPoolEnvCollection) {
            $name = [string]$item.GetAttributeValue("name")
            if (-not [string]::IsNullOrWhiteSpace($name)) {
                $existingScopedValues[$name] = [string]$item.GetAttributeValue("value")
            }
        }
    }
    else {
        [xml]$existingWebConfig = Get-Content -LiteralPath $webConfigInfo.WebConfig -Raw
        $existingAspNetCore = Get-AspNetCoreElement -Xml $existingWebConfig
        if ($null -ne $existingAspNetCore.environmentVariables) {
            foreach ($item in @($existingAspNetCore.environmentVariables.environmentVariable)) {
                if (-not [string]::IsNullOrWhiteSpace([string]$item.name)) {
                    $existingScopedValues[[string]$item.name] = [string]$item.value
                }
            }
        }
    }

    foreach ($key in $requiredKeys) {
        if (-not $values.Contains($key) -and
            $existingScopedValues.Contains($key) -and
            -not [string]::IsNullOrWhiteSpace([string]$existingScopedValues[$key])) {
            $values[$key] = $existingScopedValues[$key]
        }
    }
}

Write-Host "Selected storage method: $selectedMethod"
if ($selectedMethod -eq "WebConfig") {
    Write-Host "WebConfig target: $($webConfigInfo.WebConfig)"
    Write-Host "web.config will be backed up before changes."
}

if ($ReadFromLocalCommittedAppSettings) {
    $sources = @(
        (Get-JsonFromGit "appsettings.Production.json"),
        (Get-JsonFromGit "appsettings.json"),
        (Get-JsonFromGit "appsettings.Development.json")
    )

    $values["ConnectionStrings__DefaultConnection"] = Get-ConfigValue -Sources $sources -Path @("ConnectionStrings", "DefaultConnection")
    $values["ZendeskSync__Subdomain"] = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "Subdomain")
    $values["ZendeskSync__BaseUrl"] = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "BaseUrl")
    $values["ZendeskSync__Email"] = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "Email")
    $values["ZendeskSync__ApiToken"] = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "ApiToken")
    $values["ZendeskWebhook__Secret"] = Get-ConfigValue -Sources $sources -Path @("ZendeskWebhook", "Secret")
    $values["Activation__DefaultPassword"] = Get-ConfigValue -Sources $sources -Path @("Activation", "DefaultPassword")
    $values["Activation__SheetApiUrl"] = Get-ConfigValue -Sources $sources -Path @("Activation", "SheetApiUrl")
    $values["UserRegistrySync__SheetApiUrl"] = Get-ConfigValue -Sources $sources -Path @("UserRegistrySync", "SheetApiUrl")
}
elseif (-not $Plan) {
    foreach ($key in $requiredKeys) {
        if (-not $values.Contains($key)) {
            $values[$key] = Read-SecretValue -Name $key
        }
    }
}

$missing = $requiredKeys | Where-Object {
    -not $values.Contains($_) -or [string]::IsNullOrWhiteSpace([string]$values[$_])
}

Write-Host "Planned environment variables for '$AppPoolName':"
foreach ($key in $requiredKeys) {
    $state = if ($missing -contains $key) { "MISSING" } elseif ($Plan -and $key -notin @("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT")) { "will prompt or read, value masked" } else { "set, value masked" }
    Write-Host "- $key : $state"
}
Write-Host "DefaultAppPool will not be modified."
Write-Host "Default Web Site will not be modified."
Write-Host "Machine-level environment variables will not be modified."
Write-Host "iisreset will not be run."

if ($Plan) {
    Write-Host "Plan mode only. No values were read, written, or displayed."
    exit 0
}

if ($missing.Count -gt 0) {
    throw "Missing required values. Re-run interactively or approve -ReadFromLocalCommittedAppSettings if appropriate."
}

if (-not $Force) {
    $answer = Read-Host "Apply these values for '$AppPoolName' using method '$selectedMethod' and restart only that app pool? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Environment setup cancelled."
        exit 0
    }
}

$changed = $false
if ($selectedMethod -eq "AppPool") {
    foreach ($key in $requiredKeys) {
        $existing = Find-ConfigElementByName -Collection $appPoolEnvCollection -Name $key
        if ($null -ne $existing) {
            if ($existing.GetAttributeValue("value") -ne [string]$values[$key]) {
                if ($PSCmdlet.ShouldProcess($AppPoolName, "Update app-pool environment variable $key")) {
                    $existing.SetAttributeValue("value", [string]$values[$key])
                    $changed = $true
                }
            }
        }
        else {
            if ($PSCmdlet.ShouldProcess($AppPoolName, "Add app-pool environment variable $key")) {
                $newElement = $appPoolEnvCollection.CreateElement("add")
                $newElement.SetAttributeValue("name", $key)
                $newElement.SetAttributeValue("value", [string]$values[$key])
                $appPoolEnvCollection.Add($newElement)
                $changed = $true
            }
        }
    }

    if ($changed -and $PSCmdlet.ShouldProcess($AppPoolName, "Commit app-pool environment variable changes")) {
        $serverManager.CommitChanges()
    }
}
else {
    if ($PSCmdlet.ShouldProcess($webConfigInfo.WebConfig, "Update aspNetCore environmentVariables in deployed web.config")) {
        $changed = Set-WebConfigEnvironmentVariables -WebConfigPath $webConfigInfo.WebConfig -Values $values -Keys $requiredKeys
    }
}

if ($changed) {
    Import-Module WebAdministration -ErrorAction Stop
    Restart-WebAppPool -Name $AppPoolName
    Write-Host "Applied environment variables and restarted only app pool '$AppPoolName'."
}
else {
    Write-Host "No environment variable changes were required. App pool was not restarted."
}
