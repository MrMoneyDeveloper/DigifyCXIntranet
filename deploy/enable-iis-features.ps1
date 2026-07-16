[CmdletBinding()]
param(
    [switch]$IncludeAspNet45,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$features = @(
    "IIS-WebServerRole",
    "IIS-WebServer",
    "IIS-ManagementConsole",
    "IIS-CommonHttpFeatures",
    "IIS-DefaultDocument",
    "IIS-StaticContent",
    "IIS-HttpErrors",
    "IIS-HttpLogging",
    "IIS-RequestFiltering",
    "IIS-WindowsAuthentication",
    "IIS-ISAPIExtensions",
    "IIS-ISAPIFilter"
)

if ($IncludeAspNet45) {
    $features += @("IIS-NetFxExtensibility45", "IIS-ASPNET45")
}

Write-Host "Planned Windows feature enablement:"
$features | ForEach-Object { Write-Host "- $_" }
Write-Host "A restart may be required after Windows finishes enabling IIS."

if (-not $Force) {
    $answer = Read-Host "Enable these IIS features now? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "IIS feature enablement cancelled."
        exit 0
    }
}

Enable-WindowsOptionalFeature -Online -FeatureName $features -All -NoRestart
Write-Host "IIS features enabled. Reboot if Windows reports that a restart is required."
