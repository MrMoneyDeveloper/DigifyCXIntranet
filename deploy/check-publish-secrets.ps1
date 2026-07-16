[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [string]$IncludeFileName = "",

    [switch]$TopLevelOnly
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PublishPath -PathType Container)) {
    throw "PublishPath does not exist: $PublishPath"
}

$riskyTerms = @(
    "ApiToken",
    "Password",
    "Secret",
    "SheetApiUrl",
    "DefaultPassword",
    "AccessToken",
    "ClientSecret",
    "Bearer",
    "REPLACE_ME",
    "TODO_SECRET"
)

$textExtensions = @(
    ".json",
    ".config",
    ".xml",
    ".txt",
    ".ps1",
    ".cmd",
    ".bat",
    ".yml",
    ".yaml",
    ".env",
    ".html",
    ".css",
    ".js"
)

$root = (Resolve-Path -LiteralPath $PublishPath).Path
$findings = New-Object System.Collections.Generic.List[object]

function Test-SafeValue {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $true
    }

    if ($Value -is [bool] -or $Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal]) {
        return $true
    }

    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $true
    }

    if ($text -match '^__[^_].*__$' -or $text -like "REPLACE_WITH_*" -or $text -in @("REPLACE_IN_PRODUCTION", "REPLACE_IN_USER_SECRETS")) {
        return $true
    }

    return $false
}

function Add-Finding {
    param(
        [string]$File,
        [int]$Line,
        [string]$Term
    )

    $findings.Add([pscustomobject]@{
        File = $File
        Line = $Line
        Term = $Term
    })
}

function Add-JsonFindings {
    param(
        [AllowNull()][object]$Node,
        [string]$File,
        [string]$Path = ""
    )

    if ($null -eq $Node) {
        return
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            $nextPath = if ($Path) { "$Path`:$($property.Name)" } else { $property.Name }
            Add-JsonFindings -Node $property.Value -File $File -Path $nextPath
        }
        return
    }

    if ($Node -is [System.Array]) {
        for ($index = 0; $index -lt $Node.Count; $index++) {
            Add-JsonFindings -Node $Node[$index] -File $File -Path "$Path[$index]"
        }
        return
    }

    $leafKey = ($Path -split ":")[-1]
    $unsafeValue = -not (Test-SafeValue -Value $Node)
    $connectionString = $Path -like "ConnectionStrings:*"

    if ($connectionString -and $unsafeValue) {
        Add-Finding -File $File -Line 0 -Term "ConnectionStrings"
        return
    }

    foreach ($term in $riskyTerms) {
        $riskyLeafKey = $leafKey -cmatch [regex]::Escape($term)
        $riskyValue = ([string]$Node) -cmatch [regex]::Escape($term)

        if (($riskyLeafKey -or $riskyValue) -and $unsafeValue) {
            Add-Finding -File $File -Line 0 -Term $term
        }
    }
}

if ((-not $TopLevelOnly) -and (Test-Path -LiteralPath (Join-Path $root "appsettings.Development.json"))) {
    Add-Finding -File "appsettings.Development.json" -Line 0 -Term "Development settings file present"
}

$childItemParams = @{
    LiteralPath = $root
    File = $true
}

if (-not $TopLevelOnly) {
    $childItemParams.Recurse = $true
}

Get-ChildItem @childItemParams | Where-Object {
    $textExtensions -contains $_.Extension.ToLowerInvariant() -and
    ([string]::IsNullOrWhiteSpace($IncludeFileName) -or $_.Name -eq $IncludeFileName)
} | ForEach-Object {
    $file = $_
    $relative = $file.FullName.Substring($root.Length).TrimStart("\")

    if ($file.Extension.ToLowerInvariant() -eq ".json") {
        try {
            $json = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
            Add-JsonFindings -Node $json -File $relative
        }
        catch {
            Add-Finding -File $relative -Line 0 -Term "Invalid JSON"
        }

        return
    }

    $lineNumber = 0
    Get-Content -LiteralPath $file.FullName -ErrorAction Stop | ForEach-Object {
        $lineNumber++
        $line = $_

        foreach ($term in $riskyTerms) {
            if ($line -cmatch [regex]::Escape($term)) {
                Add-Finding -File $relative -Line $lineNumber -Term $term
            }
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Warning "Potential secrets or unsafe placeholder values were found. Values are intentionally not displayed."
    $findings | Sort-Object File, Line, Term -Unique | Format-Table -AutoSize
    throw "Secret scan failed. Move real values to IIS/server environment variables before deployment."
}

Write-Host "Secret scan passed for $root"
