[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$PublishPath,
    [string]$TargetPath = "C:\Sites\DigifyCXIntranet\Live",
    [string]$BackupRoot = "C:\Sites\DigifyCXIntranet\Backups",
    [string]$AppPoolName = "DigifyCXIntranet",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $publishRoot = Join-Path $PSScriptRoot "..\artifacts\publish"
    $latestPublish = Get-ChildItem -LiteralPath $publishRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "DigifyCXIntranet-Live-*" -and (Test-Path -LiteralPath (Join-Path $_.FullName "appsettings.json")) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestPublish) {
        throw "No live publish output found under $publishRoot. Run deploy\publish-live.ps1 first or pass -PublishPath."
    }

    $PublishPath = $latestPublish.FullName
}

$expectedTargetPath = [System.IO.Path]::GetFullPath("C:\Sites\DigifyCXIntranet\Live").TrimEnd('\')
$targetFullPath = [System.IO.Path]::GetFullPath($TargetPath).TrimEnd('\')
if (-not [string]::Equals($targetFullPath, $expectedTargetPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to copy outside the exact target C:\Sites\DigifyCXIntranet\Live."
}
$TargetPath = $targetFullPath

$publishFullPath = (Resolve-Path -LiteralPath $PublishPath).Path
$scanScript = Join-Path $PSScriptRoot "check-publish-secrets.ps1"
& $scanScript -PublishPath $publishFullPath

function Get-AspNetCoreNode {
    param([xml]$Document)

    $node = $Document.SelectSingleNode("/configuration/system.webServer/aspNetCore")
    if ($null -eq $node) {
        $node = $Document.SelectSingleNode("/configuration/location[@path='.']/system.webServer/aspNetCore")
    }
    if ($null -eq $node) {
        $node = $Document.SelectSingleNode("/configuration/location/system.webServer/aspNetCore")
    }
    return $node
}

function Save-XmlDocument {
    param(
        [xml]$Document,
        [string]$Path
    )

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Copy-PublishOutputPreservingUploads {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    Get-ChildItem -LiteralPath $SourcePath -Force |
        Where-Object { $_.Name -ne "wwwroot" } |
        Copy-Item -Destination $DestinationPath -Recurse -Force

    $sourceWwwroot = Join-Path $SourcePath "wwwroot"
    if (-not (Test-Path -LiteralPath $sourceWwwroot)) {
        return
    }

    $destinationWwwroot = Join-Path $DestinationPath "wwwroot"
    New-Item -ItemType Directory -Path $destinationWwwroot -Force | Out-Null

    Get-ChildItem -LiteralPath $sourceWwwroot -Force |
        Where-Object { $_.Name -ne "uploads" } |
        Copy-Item -Destination $destinationWwwroot -Recurse -Force
}

$publishWebConfig = Join-Path $publishFullPath "web.config"
if (-not (Test-Path -LiteralPath $publishWebConfig)) {
    throw "Publish output is missing web.config: $publishWebConfig"
}

[xml]$publishWebConfigXml = Get-Content -LiteralPath $publishWebConfig -Raw
$aspNetCore = Get-AspNetCoreNode -Document $publishWebConfigXml
if ($null -eq $aspNetCore) {
    throw "Publish web.config does not contain an aspNetCore section. Refusing to copy."
}

$preservedEnvironmentVariables = [ordered]@{}
$targetWebConfig = Join-Path $TargetPath "web.config"
if (Test-Path -LiteralPath $targetWebConfig) {
    [xml]$existingWebConfigXml = Get-Content -LiteralPath $targetWebConfig -Raw
    $existingAspNetCore = Get-AspNetCoreNode -Document $existingWebConfigXml
    if ($null -ne $existingAspNetCore -and $null -ne $existingAspNetCore.environmentVariables) {
        foreach ($item in @($existingAspNetCore.environmentVariables.environmentVariable)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$item.name)) {
                $preservedEnvironmentVariables[[string]$item.name] = [string]$item.value
            }
        }
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $BackupRoot "Live-$timestamp"

Write-Host "Planned copy to IIS LIVE folder:"
Write-Host "- Source: $publishFullPath"
Write-Host "- Target: $TargetPath"
Write-Host "- Backup: $backupPath"
Write-Host "- Restart app pool: $AppPoolName"
Write-Host "- Preserve existing app-scoped environment variables: $($preservedEnvironmentVariables.Count) value(s), all masked"
Write-Host "- Preserve existing uploaded files under wwwroot\uploads: yes"
Write-Host "- Copy uploaded files from publish output: no"
Write-Host "- Place only this site temporarily offline while locked files are replaced: yes"

if (-not $Force) {
    $answer = Read-Host "Overwrite the IIS LIVE folder after backup? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Live copy cancelled."
        exit 0
    }
}

$copied = $false
if ($PSCmdlet.ShouldProcess($TargetPath, "Back up and overwrite IIS LIVE files")) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

    if ((Get-ChildItem -LiteralPath $TargetPath -Force -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
        New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
        Copy-Item -Path (Join-Path $TargetPath "*") -Destination $backupPath -Recurse -Force
    }

    $appOfflinePath = Join-Path $TargetPath "app_offline.htm"
    try {
        [System.IO.File]::WriteAllText(
            $appOfflinePath,
            "DigifyCX Intranet is being updated. Please retry shortly.",
            [System.Text.UTF8Encoding]::new($false))
        Start-Sleep -Seconds 2

        Copy-PublishOutputPreservingUploads -SourcePath $publishFullPath -DestinationPath $TargetPath

        if ($preservedEnvironmentVariables.Count -gt 0) {
            [xml]$deployedWebConfigXml = Get-Content -LiteralPath $targetWebConfig -Raw
            $deployedAspNetCore = Get-AspNetCoreNode -Document $deployedWebConfigXml
            if ($null -eq $deployedAspNetCore) {
                throw "Copied web.config is missing aspNetCore. The live backup remains at $backupPath."
            }

            $environmentVariables = $deployedAspNetCore.environmentVariables
            if ($null -eq $environmentVariables) {
                $environmentVariables = $deployedWebConfigXml.CreateElement("environmentVariables")
                $null = $deployedAspNetCore.AppendChild($environmentVariables)
            }
            else {
                foreach ($item in @($environmentVariables.environmentVariable)) {
                    $null = $environmentVariables.RemoveChild($item)
                }
            }

            foreach ($entry in $preservedEnvironmentVariables.GetEnumerator()) {
                $item = $deployedWebConfigXml.CreateElement("environmentVariable")
                $item.SetAttribute("name", [string]$entry.Key)
                $item.SetAttribute("value", [string]$entry.Value)
                $null = $environmentVariables.AppendChild($item)
            }

            Save-XmlDocument -Document $deployedWebConfigXml -Path $targetWebConfig
        }

        $copied = $true
    }
    finally {
        if (Test-Path -LiteralPath $appOfflinePath) {
            Remove-Item -LiteralPath $appOfflinePath -Force
        }
    }
}

if ($copied) {
    Import-Module WebAdministration -ErrorAction Stop
    Restart-WebAppPool -Name $AppPoolName
    Write-Host "Copied live publish output and restarted only app pool $AppPoolName"
}
else {
    Write-Host "Copy was not applied. The app pool was not restarted."
}
