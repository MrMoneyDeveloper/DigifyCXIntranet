[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SiteName = "DigifyCX Intranet",
    [string]$AppPoolName = "DigifyCXIntranet-Prod",
    [string]$PhysicalPath = "C:\Sites\DigifyCXIntranet\Production",
    [string]$InternalIp = "192.168.69.17",
    [string]$HostName = "intranet.digifycx.local",
    [ValidateSet("Windows", "Cookie")]
    [string]$AuthMode = "Windows",
    [switch]$UseHttps,
    [string]$CertificateThumbprint = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$port = if ($UseHttps) { 443 } else { 80 }
$protocol = if ($UseHttps) { "https" } else { "http" }
$bindingInformation = "$InternalIp`:$port`:$HostName"
$url = "${protocol}://${HostName}/"

$plan = [ordered]@{
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    PhysicalPath = $PhysicalPath
    Binding = "$protocol $bindingInformation"
    AppPoolRuntime = "No Managed Code"
    AppPoolIdentity = "ApplicationPoolIdentity"
    AnonymousAuthentication = "Enabled"
    WindowsAuthentication = if ($AuthMode -eq "Windows") { "Enabled" } else { "Disabled" }
    Url = $url
}

Write-Host "Planned IIS PRODUCTION changes:"
$plan.GetEnumerator() | ForEach-Object { Write-Host ("- {0}: {1}" -f $_.Key, $_.Value) }

if ($UseHttps -and [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "UseHttps requires -CertificateThumbprint for the internal TLS certificate."
}

if ($AuthMode -eq "Windows") {
    Write-Host "Anonymous stays enabled because the app has explicit AllowAnonymous pages; the ASP.NET Core fallback policy still protects the rest."
}

if (-not $Force) {
    $answer = Read-Host "Apply these PRODUCTION IIS changes? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Production IIS setup cancelled."
        exit 0
    }
}

Import-Module WebAdministration -ErrorAction Stop

if ($PSCmdlet.ShouldProcess($PhysicalPath, "Create production deployment directory")) {
    New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
}

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    if ($PSCmdlet.ShouldProcess($AppPoolName, "Create production IIS app pool")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
    }
}

if ($PSCmdlet.ShouldProcess($AppPoolName, "Configure production IIS app pool")) {
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
}

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    if ($PSCmdlet.ShouldProcess($SiteName, "Create production IIS site")) {
        New-Website -Name $SiteName -ApplicationPool $AppPoolName -PhysicalPath $PhysicalPath -Port $port -IPAddress $InternalIp -HostHeader $HostName | Out-Null
    }
}
else {
    if ($PSCmdlet.ShouldProcess($SiteName, "Update production IIS site path and app pool")) {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    }
}

Get-WebBinding -Name $SiteName -ErrorAction SilentlyContinue |
    Where-Object { $_.protocol -eq $protocol -and $_.bindingInformation -ne $bindingInformation } |
    Remove-WebBinding

$existingBinding = Get-WebBinding -Name $SiteName -Protocol $protocol -ErrorAction SilentlyContinue |
    Where-Object { $_.bindingInformation -eq $bindingInformation }

if (-not $existingBinding) {
    if ($PSCmdlet.ShouldProcess($SiteName, "Add $protocol binding $bindingInformation")) {
        New-WebBinding -Name $SiteName -Protocol $protocol -IPAddress $InternalIp -Port $port -HostHeader $HostName | Out-Null
    }
}

if ($UseHttps) {
    $binding = Get-WebBinding -Name $SiteName -Protocol https | Where-Object { $_.bindingInformation -eq $bindingInformation } | Select-Object -First 1
    if ($null -eq $binding) {
        throw "HTTPS binding was not found after creation."
    }
    if ($PSCmdlet.ShouldProcess($SiteName, "Attach certificate to HTTPS binding")) {
        $binding.AddSslCertificate($CertificateThumbprint, "My")
    }
}

if ($PSCmdlet.ShouldProcess($PhysicalPath, "Grant modify permission to IIS AppPool\$AppPoolName")) {
    & icacls $PhysicalPath /grant "IIS AppPool\$AppPoolName`:(OI)(CI)(M)" /T | Out-Host
}

if ($PSCmdlet.ShouldProcess($SiteName, "Configure production IIS authentication")) {
    Set-WebConfigurationProperty -PSPath "IIS:\" -Filter "/system.webServer/security/authentication/anonymousAuthentication" -Location $SiteName -Name enabled -Value $true
    Set-WebConfigurationProperty -PSPath "IIS:\" -Filter "/system.webServer/security/authentication/windowsAuthentication" -Location $SiteName -Name enabled -Value ($AuthMode -eq "Windows")
}

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName
Write-Host "Production IIS site is configured: $url"
