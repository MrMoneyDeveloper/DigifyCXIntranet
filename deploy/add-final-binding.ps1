[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SiteName = "DigifyCX Intranet",
    [string]$InternalIp = "192.168.69.17",
    [string]$HostName = "intranet.digifycx.local",
    [ValidateSet("http", "https")]
    [string]$Protocol = "http",
    [int]$Port = 80,
    [string]$CertificateThumbprint = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ($SiteName -eq "Default Web Site") {
    throw "Refusing to modify Default Web Site."
}

if ([string]::IsNullOrWhiteSpace($HostName)) {
    throw "HostName is required for the final binding."
}

if ($Protocol -eq "https") {
    $Port = 443
    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        throw "HTTPS final binding requires -CertificateThumbprint."
    }
}

$bindingInformation = "$InternalIp`:$Port`:$HostName"
$portSuffix = ""
if (($Protocol -eq "http" -and $Port -ne 80) -or ($Protocol -eq "https" -and $Port -ne 443)) {
    $portSuffix = ":$Port"
}
$url = "${Protocol}://${HostName}${portSuffix}/"

Write-Host "Planned final go-live binding:"
Write-Host "- Site: $SiteName"
Write-Host "- Binding: $Protocol $bindingInformation"
Write-Host "- URL: $url"
Write-Host "- Default Web Site is not modified"

if (-not $Force) {
    $answer = Read-Host "Add this final binding? Type YES to continue"
    if ($answer -ne "YES") {
        Write-Host "Final binding setup cancelled."
        exit 0
    }
}

Import-Module WebAdministration -ErrorAction Stop

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    throw "Site not found: $SiteName"
}

$existingBinding = Get-WebBinding -Name $SiteName -Protocol $Protocol -ErrorAction SilentlyContinue |
    Where-Object { $_.bindingInformation -eq $bindingInformation }

if (-not $existingBinding) {
    if ($PSCmdlet.ShouldProcess($SiteName, "Add final binding $bindingInformation")) {
        New-WebBinding -Name $SiteName -Protocol $Protocol -IPAddress $InternalIp -Port $Port -HostHeader $HostName | Out-Null
    }
}

if ($Protocol -eq "https") {
    $binding = Get-WebBinding -Name $SiteName -Protocol https | Where-Object { $_.bindingInformation -eq $bindingInformation } | Select-Object -First 1
    if ($null -eq $binding) {
        throw "HTTPS binding was not found after creation."
    }
    if ($PSCmdlet.ShouldProcess($SiteName, "Attach certificate to final HTTPS binding")) {
        $binding.AddSslCertificate($CertificateThumbprint, "My")
    }
}

Write-Host "Final binding is configured: $url"
