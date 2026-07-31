[CmdletBinding()]
param(
    [ValidateSet("Machine", "User", "Process")]
    [string]$Scope = "Machine",
    [switch]$ApplyPlaceholders
)

$ErrorActionPreference = "Stop"

Write-Host "This example intentionally contains placeholders only."
Write-Host "Do not commit real secrets. Prefer typing real values manually on the server."

$envValues = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = "Test"
    "DOTNET_ENVIRONMENT" = "Test"
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

Write-Host "Variables expected at $Scope scope:"
$envValues.GetEnumerator() | ForEach-Object { Write-Host ("- {0}: {1}" -f $_.Key, $_.Value) }

if (-not $ApplyPlaceholders) {
    Write-Host "Dry run only. Re-run with -ApplyPlaceholders only if you intentionally want placeholder variables."
    Write-Host 'Safe manual format: $value = Read-Host "Enter value"; [Environment]::SetEnvironmentVariable("ZendeskWebhook__Secret", $value, "Machine"); Remove-Variable value'
    exit 0
}

foreach ($name in $envValues.Keys) {
    [Environment]::SetEnvironmentVariable($name, $envValues[$name], $Scope)
}

Write-Host "Placeholder variables written to $Scope scope. Replace placeholders with real values typed manually, then restart the IIS app pool."
