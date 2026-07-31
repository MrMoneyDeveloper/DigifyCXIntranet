[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$PublishPath,
    [string]$TargetPath = "C:\Sites\DigifyCXIntranet\Production",
    [string]$BackupRoot = "C:\Sites\DigifyCXIntranet\Backups",
    [string]$AppPoolName = "DigifyCXIntranet-Prod",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $publishRoot = Join-Path $PSScriptRoot "..\artifacts\publish"
    $latestPublish = Get-ChildItem -LiteralPath $publishRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "DigifyCXIntranet-Prod-*" -and (Test-Path -LiteralPath (Join-Path $_.FullName "appsettings.json")) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestPublish) {
        throw "No production publish output found under $publishRoot. Run deploy\publish-prod.ps1 first or pass -PublishPath."
    }

    $PublishPath = $latestPublish.FullName
}

$publishFullPath = (Resolve-Path -LiteralPath $PublishPath).Path
$scanScript = Join-Path $PSScriptRoot "check-publish-secrets.ps1"
& $scanScript -PublishPath $publishFullPath

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $BackupRoot "Production-$timestamp"

Write-Host "Planned copy to IIS PRODUCTION folder:"
Write-Host "- Source: $publishFullPath"
Write-Host "- Target: $TargetPath"
Write-Host "- Backup: $backupPath"
Write-Host "- Restart app pool: $AppPoolName"

if (-not $Force) {
    $answer = Read-Host "Overwrite the IIS PRODUCTION folder after backup? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Production copy cancelled."
        exit 0
    }
}

if ($PSCmdlet.ShouldProcess($TargetPath, "Back up and overwrite IIS PRODUCTION files")) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

    if ((Get-ChildItem -LiteralPath $TargetPath -Force -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
        New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
        Copy-Item -Path (Join-Path $TargetPath "*") -Destination $backupPath -Recurse -Force
    }

    Copy-Item -Path (Join-Path $publishFullPath "*") -Destination $TargetPath -Recurse -Force
}

Import-Module WebAdministration -ErrorAction Stop
Restart-WebAppPool -Name $AppPoolName
Write-Host "Copied production publish output and restarted app pool $AppPoolName"
