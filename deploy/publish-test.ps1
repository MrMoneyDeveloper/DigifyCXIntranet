[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$PublishPath,
    [string]$Configuration = "Release",
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot "..\DigifyCXIntranet.csproj"
}

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $PublishPath = Join-Path $PSScriptRoot ("..\artifacts\publish\DigifyCXIntranet-Test-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}

$projectFullPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$publishFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PublishPath)
$scanScript = Join-Path $PSScriptRoot "check-publish-secrets.ps1"
$projectDirectory = Split-Path -Parent $projectFullPath

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

& $scanScript -PublishPath $projectDirectory -IncludeFileName "appsettings.json" -TopLevelOnly

if (Test-Path -LiteralPath $publishFullPath) {
    Remove-Item -LiteralPath $publishFullPath -Recurse -Force
}

New-Item -ItemType Directory -Path $publishFullPath -Force | Out-Null

if (-not $SkipRestore) {
    Invoke-NativeChecked dotnet restore $projectFullPath
}

Invoke-NativeChecked dotnet build $projectFullPath --configuration $Configuration --no-restore
Invoke-NativeChecked dotnet publish $projectFullPath --configuration $Configuration --no-build --output $publishFullPath /p:EnvironmentName=Test

$developmentSettings = Join-Path $publishFullPath "appsettings.Development.json"
if (Test-Path -LiteralPath $developmentSettings) {
    throw "appsettings.Development.json was published. Stop before deployment."
}

& $scanScript -PublishPath $publishFullPath

Write-Host "Publish completed and passed checks: $publishFullPath"
