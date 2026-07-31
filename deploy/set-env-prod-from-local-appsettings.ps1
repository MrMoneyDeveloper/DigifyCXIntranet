[CmdletBinding()]
param(
    [ValidateSet("Machine", "User", "Process")]
    [string]$Scope = "Machine",

    [switch]$Force
)

$ErrorActionPreference = "Stop"

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

function Get-JsonFromFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return ConvertTo-Hashtable (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop)
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

$sources = @(
    (Get-JsonFromFile "appsettings.Production.json"),
    (Get-JsonFromFile "appsettings.json"),
    (Get-JsonFromGit "appsettings.Production.json"),
    (Get-JsonFromGit "appsettings.json"),
    (Get-JsonFromGit "appsettings.Development.json")
)

$variables = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "DOTNET_ENVIRONMENT" = "Production"
    "ConnectionStrings__DefaultConnection" = Get-ConfigValue -Sources $sources -Path @("ConnectionStrings", "DefaultConnection")
    "ZendeskSync__Subdomain" = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "Subdomain")
    "ZendeskSync__BaseUrl" = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "BaseUrl")
    "ZendeskSync__Email" = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "Email")
    "ZendeskSync__ApiToken" = Get-ConfigValue -Sources $sources -Path @("ZendeskSync", "ApiToken")
    "ZendeskWebhook__Secret" = Get-ConfigValue -Sources $sources -Path @("ZendeskWebhook", "Secret")
    "Activation__DefaultPassword" = Get-ConfigValue -Sources $sources -Path @("Activation", "DefaultPassword")
    "Activation__SheetApiUrl" = Get-ConfigValue -Sources $sources -Path @("Activation", "SheetApiUrl")
    "UserRegistrySync__SheetApiUrl" = Get-ConfigValue -Sources $sources -Path @("UserRegistrySync", "SheetApiUrl")
}

$missing = $variables.GetEnumerator() | Where-Object {
    $_.Key -notin @("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT") -and
    [string]::IsNullOrWhiteSpace([string]$_.Value)
}

Write-Host "Production environment variables to set at $Scope scope:"
$variables.Keys | ForEach-Object { Write-Host "- $_" }

if ($missing) {
    Write-Warning "Missing values were found. Values are not displayed."
    $missing | ForEach-Object { Write-Host "- Missing: $($_.Key)" }
    throw "Cannot set production environment variables until all required values are available."
}

if (-not $Force) {
    $answer = Read-Host "Set these production environment variables without displaying values? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Environment variable setup cancelled."
        exit 0
    }
}

foreach ($entry in $variables.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, $Scope)
}

Write-Host "Production environment variables were set at $Scope scope. Restart IIS/app pools before testing."
