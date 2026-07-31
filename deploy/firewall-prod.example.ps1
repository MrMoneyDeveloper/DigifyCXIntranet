[CmdletBinding()]
param(
    [string]$DisplayName = "DigifyCX Intranet Production",
    [int]$Port = 443,
    [string[]]$RemoteAddress = @("192.168.68.0/23"),
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

Write-Host "Planned firewall PRODUCTION rule:"
Write-Host "  Name: $DisplayName"
Write-Host "  Direction: Inbound"
Write-Host "  Protocol/port: TCP/$Port"
Write-Host "  Remote addresses: $($RemoteAddress -join ', ')"

if (-not $Apply) {
    Write-Host "Dry run only. Re-run with -Apply after confirming the internal network range."
    return
}

if ($RemoteAddress -contains "Any" -or $RemoteAddress -contains "0.0.0.0/0") {
    throw "Refusing to create a public firewall rule for production."
}

New-NetFirewallRule `
    -DisplayName $DisplayName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -RemoteAddress $RemoteAddress `
    -Profile Domain,Private | Out-Null

Write-Host "Production firewall rule created."
