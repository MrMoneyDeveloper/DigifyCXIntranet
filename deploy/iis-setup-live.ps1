[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SiteName = "DigifyCX Intranet",
    [string]$AppPoolName = "DigifyCXIntranet",
    [string]$PhysicalPath = "C:\Sites\DigifyCXIntranet\Live",
    [string]$InternalIp = "192.168.69.17",
    [int]$Port = 8080,
    [ValidateSet("Windows", "Cookie")]
    [string]$AuthMode = "Windows",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ($SiteName -eq "Default Web Site") {
    throw "Refusing to modify Default Web Site."
}

if ($AppPoolName -eq "DefaultAppPool") {
    throw "Refusing to modify DefaultAppPool."
}

if ($Port -in @(80, 443)) {
    throw "Refusing to use port 80 or 443 for the initial live test binding."
}

$bindingInformation = "$InternalIp`:$Port`:"
$url = "http://$InternalIp`:$Port/"

$plan = [ordered]@{
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    PhysicalPath = $PhysicalPath
    Binding = "http $bindingInformation"
    AppPoolRuntime = "No Managed Code"
    AppPoolIdentity = "ApplicationPoolIdentity"
    AnonymousAuthentication = "Enabled"
    WindowsAuthentication = if ($AuthMode -eq "Windows") { "Enabled" } else { "Disabled" }
    Url = $url
    ProtectedSites = "Default Web Site is not modified"
    ProtectedPools = "DefaultAppPool is not modified"
}

Write-Host "Planned IIS LIVE changes:"
$plan.GetEnumerator() | ForEach-Object { Write-Host ("- {0}: {1}" -f $_.Key, $_.Value) }

if ($AuthMode -eq "Windows") {
    Write-Host "Anonymous stays enabled because the app has explicit AllowAnonymous pages; the ASP.NET Core fallback policy still protects the rest."
}

if (-not $Force) {
    $answer = Read-Host "Apply these LIVE IIS changes? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Live IIS setup cancelled."
        exit 0
    }
}

Import-Module WebAdministration -ErrorAction Stop

if (Test-Path "IIS:\Sites\Default Web Site") {
    Write-Host "Default Web Site exists and will not be changed."
}

if ($PSCmdlet.ShouldProcess($PhysicalPath, "Create live deployment directory")) {
    New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
}

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    if ($PSCmdlet.ShouldProcess($AppPoolName, "Create IIS app pool")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
    }
}

if ($PSCmdlet.ShouldProcess($AppPoolName, "Configure IIS app pool")) {
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
}

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    if ($PSCmdlet.ShouldProcess($SiteName, "Create IIS site")) {
        New-Website -Name $SiteName -ApplicationPool $AppPoolName -PhysicalPath $PhysicalPath -Port $Port -IPAddress $InternalIp -HostHeader "" | Out-Null
    }
}
else {
    $site = Get-Website -Name $SiteName
    if ($site.Name -eq "Default Web Site") {
        throw "Resolved site is Default Web Site; refusing to continue."
    }

    if ($PSCmdlet.ShouldProcess($SiteName, "Update IIS site path and app pool")) {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    }
}

$existingBinding = Get-WebBinding -Name $SiteName -Protocol http -ErrorAction SilentlyContinue |
    Where-Object { $_.bindingInformation -eq $bindingInformation }

if (-not $existingBinding) {
    if ($PSCmdlet.ShouldProcess($SiteName, "Add initial test binding $bindingInformation")) {
        New-WebBinding -Name $SiteName -Protocol http -IPAddress $InternalIp -Port $Port -HostHeader "" | Out-Null
    }
}

if ($PSCmdlet.ShouldProcess($PhysicalPath, "Grant modify permission to IIS AppPool\$AppPoolName")) {
    & icacls $PhysicalPath /grant "IIS AppPool\$AppPoolName`:(OI)(CI)(M)" /T | Out-Host
}

if ($PSCmdlet.ShouldProcess($SiteName, "Configure IIS authentication")) {
    Set-WebConfigurationProperty -PSPath "IIS:\" -Filter "/system.webServer/security/authentication/anonymousAuthentication" -Location $SiteName -Name enabled -Value $true
    Set-WebConfigurationProperty -PSPath "IIS:\" -Filter "/system.webServer/security/authentication/windowsAuthentication" -Location $SiteName -Name enabled -Value ($AuthMode -eq "Windows")
}

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName
Write-Host "Live IIS site is configured for initial test binding: $url"
