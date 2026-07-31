[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SiteName = "DigifyCX Intranet Test",
    [string]$AppPoolName = "DigifyCXIntranet-Test",
    [string]$PhysicalPath = "C:\Sites\DigifyCXIntranet\Test",
    [string]$InternalIp = "192.168.69.17",
    [int]$Port = 8080,
    [string]$HostName = "intranet-test.digifycx.local",
    [ValidateSet("Windows", "Cookie")]
    [string]$AuthMode = "Windows",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$bindingInformation = "$InternalIp`:$Port`:"
$plan = [ordered]@{
    SiteName = $SiteName
    AppPoolName = $AppPoolName
    PhysicalPath = $PhysicalPath
    Binding = "http $bindingInformation"
    AppPoolRuntime = "No Managed Code"
    AppPoolIdentity = "ApplicationPoolIdentity"
    AnonymousAuthentication = "Enabled"
    WindowsAuthentication = if ($AuthMode -eq "Windows") { "Enabled" } else { "Disabled" }
    SuggestedUrl = "http://$InternalIp`:$Port/"
    OptionalDnsUrl = "http://$HostName`:$Port/"
}

Write-Host "Planned IIS TEST changes:"
$plan.GetEnumerator() | ForEach-Object { Write-Host ("- {0}: {1}" -f $_.Key, $_.Value) }

if ($AuthMode -eq "Windows") {
    Write-Host "Anonymous stays enabled because the app has explicit AllowAnonymous pages; the ASP.NET Core fallback policy still protects the rest."
}

if (-not $Force) {
    $answer = Read-Host "Apply these IIS changes? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "IIS setup cancelled."
        exit 0
    }
}

Import-Module WebAdministration -ErrorAction Stop

if ($PSCmdlet.ShouldProcess($PhysicalPath, "Create deployment directory")) {
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
} else {
    if ($PSCmdlet.ShouldProcess($SiteName, "Update IIS site path and app pool")) {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    }
}

$existingBinding = Get-WebBinding -Name $SiteName -Protocol http -ErrorAction SilentlyContinue |
    Where-Object { $_.bindingInformation -eq $bindingInformation }

if (-not $existingBinding) {
    if ($PSCmdlet.ShouldProcess($SiteName, "Add HTTP binding $bindingInformation")) {
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
Write-Host "IIS TEST site is configured: http://$InternalIp`:$Port/"
