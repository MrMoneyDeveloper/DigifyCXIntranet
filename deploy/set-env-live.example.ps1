[CmdletBinding()]
param(
    [ValidateSet("Machine", "User", "Process")]
    [string]$Scope = "Machine",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$envValues = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "DOTNET_ENVIRONMENT" = "Production"
    "ConnectionStrings__DefaultConnection" = "__SET_MANUALLY__"
    "ZendeskSync__Subdomain" = "__SET_MANUALLY__"
    "ZendeskSync__BaseUrl" = "__SET_MANUALLY__"
    "ZendeskSync__Email" = "__SET_MANUALLY__"
    "ZendeskSync__ApiToken" = "__SET_MANUALLY__"
    "ZendeskWebhook__Secret" = "__SET_MANUALLY__"
    "Activation__DefaultPassword" = "__SET_MANUALLY__"
    "Activation__SheetApiUrl" = "__SET_MANUALLY__"
    "UserRegistrySync__SheetApiUrl" = "__SET_MANUALLY__"
}

Write-Host "Live environment variables required at $Scope scope:"
$envValues.Keys | ForEach-Object { Write-Host "- $_" }
Write-Host "This example contains placeholders only. Do not commit or save real secrets in this file."
Write-Host 'Safe manual format: $value = Read-Host "Enter value"; [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $value, "Machine"); Remove-Variable value'

if (-not $Apply) {
    Write-Host "Dry run only. Re-run with -Apply only if you intentionally want to set placeholders."
    return
}

$answer = Read-Host "This will set placeholder values, not real secrets. Type YES to continue"
if ($answer -ne "YES") {
    Write-Host "Environment placeholder setup cancelled."
    exit 0
}

foreach ($entry in $envValues.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, $Scope)
}

Write-Host "Placeholder live environment variables were set at $Scope scope. Replace placeholders manually before starting the app."
