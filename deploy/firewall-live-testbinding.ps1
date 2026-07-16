[CmdletBinding()]
param(
    [string]$DisplayName = "DigifyCX Intranet Live Test Binding 8080",
    [int]$Port = 8080,
    [string[]]$RemoteAddress = @("192.168.68.0/23"),
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

if ($Port -in @(80, 443)) {
    throw "Refusing to use port 80 or 443 for the initial live test binding firewall rule."
}

Write-Host "Planned firewall LIVE test-binding rule:"
Write-Host "  Name: $DisplayName"
Write-Host "  Direction: Inbound"
Write-Host "  Protocol/port: TCP/$Port"
Write-Host "  Remote addresses: $($RemoteAddress -join ', ')"

if (-not $Apply) {
    Write-Host "Dry run only. Re-run with -Apply only after approval."
    return
}

if ($RemoteAddress -contains "Any" -or $RemoteAddress -contains "0.0.0.0/0") {
    throw "Refusing to create a public firewall rule."
}

New-NetFirewallRule `
    -DisplayName $DisplayName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -RemoteAddress $RemoteAddress `
    -Profile Domain,Private | Out-Null

Write-Host "Live test-binding firewall rule created."
