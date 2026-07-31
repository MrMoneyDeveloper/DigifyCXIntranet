# DigifyCX Intranet IIS Deployment

This guide is for internal IIS deployments only. Do not expose the site publicly, do not open firewall access to `Any` or `0.0.0.0/0`, and do not store real secrets in tracked files.

## Detected Server State

- Hostname: `DESKTOP-RBG4CLJ`.
- Windows version: `10.0.26200.8737`.
- Full IIS: not detected from this session. `WebAdministration`, `appcmd.exe`, and `HKLM:\SOFTWARE\Microsoft\InetStp` were missing.
- Windows feature inspection: blocked by elevation/access restrictions in this shell.
- Installed IIS-related components: IIS Express and ASP.NET Core Module for IIS Express only.
- ASP.NET Core Hosting Bundle for full IIS: not detected.
- .NET SDK: `10.0.301`.
- .NET runtime requirement for this app: ASP.NET Core Runtime `8.x`; `Microsoft.AspNetCore.App 8.0.28` is installed.
- Ports `80`, `443`, and `8080`: no TCP listeners found.
- Internal IP candidate: `192.168.69.17`.
- Suggested local subnet: `192.168.68.0/23`.
- Certificate for `intranet-test.digifycx.local`: not found.

Initial TEST URL after IIS setup:

```text
http://192.168.69.17:8080/
```

Use `https://intranet-test.digifycx.local` only after internal DNS and a matching certificate are available.

## Application Summary

- Solution: `DigifyCXIntranet.sln`.
- Startup web project: `DigifyCXIntranet.csproj`.
- App type: ASP.NET Core Razor Pages with API controllers.
- Target framework: `net8.0`.
- Database: Entity Framework Core SQL Server via `ConnectionStrings:DefaultConnection`.
- Authentication: non-development defaults to Windows/Negotiate authentication when `AuthMode:UseWindowsAuthenticationInNonDevelopment` is `true`; cookie/app login is used in Development or when that flag is `false`.

Recommended TEST auth setting: use Windows Authentication if users are on the internal domain. Keep IIS Anonymous Authentication enabled because the app has explicit anonymous pages such as `/External/Apply`, `/Account/Activate`, `/Account/ForgotPassword`, and `/Account/ResetPassword`; ASP.NET Core authorization still protects the rest through the fallback policy.

## Configuration

Real TEST values must come from Machine/User environment variables, IIS app pool environment configuration, or another server-side secret store. Do not create a real `appsettings.Test.json` in the repo.

Required variables:

```text
ASPNETCORE_ENVIRONMENT=Test
DOTNET_ENVIRONMENT=Test
ConnectionStrings__DefaultConnection
ZendeskSync__Subdomain
ZendeskSync__BaseUrl
ZendeskSync__Email
ZendeskSync__ApiToken
ZendeskWebhook__Secret
Activation__DefaultPassword
Activation__SheetApiUrl
UserRegistrySync__SheetApiUrl
```

Inspection found every listed variable missing at Machine and Process scope.

Safe manual format for typing a value without writing it to a script:

```powershell
$value = Read-Host "Enter value"
[Environment]::SetEnvironmentVariable("ZendeskWebhook__Secret", $value, "Machine")
Remove-Variable value
```

`deploy\set-env-test.example.ps1` is dry-run by default and contains placeholders only.

## Production First

Production defaults:

- Site: `DigifyCX Intranet`
- App pool: `DigifyCXIntranet-Prod`
- Physical path: `C:\Sites\DigifyCXIntranet\Production`
- Hostname: `intranet.digifycx.local`
- Environment: `Production`
- Preferred binding: internal HTTPS on port `443`

Before production IIS setup can succeed, run this from an elevated Administrator PowerShell prompt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\enable-iis-features.ps1
```

Then install the latest .NET 8 ASP.NET Core Hosting Bundle for full IIS and restart IIS:

```powershell
iisreset
```

Set production environment variables from local committed configuration values without printing secret values:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-prod-from-local-appsettings.ps1
```

Publish production:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\publish-prod.ps1
```

Create/update the production IIS site. Use HTTPS once an internal certificate exists:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis-setup-prod.ps1 -UseHttps -CertificateThumbprint "<thumbprint>"
```

Temporary internal HTTP production binding, if HTTPS is not ready yet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis-setup-prod.ps1
```

Create the production firewall rule only after confirming the company subnet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\firewall-prod.example.ps1 -RemoteAddress "192.168.68.0/23" -Apply
```

Copy production files after reviewing the printed backup/overwrite plan:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\copy-to-iis-prod.ps1
```

Production scripts print their plans and require typing `YES` before server or overwrite changes unless `-Force` is passed.

## Publish

Because this server blocks direct PowerShell script execution, use a process-scoped bypass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\publish-test.ps1
```

The script:

- scans root `appsettings.json` before publishing;
- restores unless `-SkipRestore` is passed;
- cleans stale Release output;
- builds Release;
- publishes to `artifacts\publish\DigifyCXIntranet-Test`;
- confirms `appsettings.Development.json` is absent;
- runs `deploy\check-publish-secrets.ps1`.

The verified publish output passed the secret scan. The publish output contains only `appsettings.json` among appsettings files; `appsettings.Development.json`, `appsettings.Test.example.json`, generated `App_Data\Outbox`, and nested `artifacts` are excluded.

## IIS Setup

Prerequisites before this can succeed:

- Install full IIS with Management Tools.
- Install Windows Authentication if using domain/Negotiate auth.
- Install the ASP.NET Core Hosting Bundle for .NET 8.
- Restart IIS after installing the Hosting Bundle.

Review and apply:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis-setup-test.ps1
```

The script prints a plan and asks for `YES` before applying. It creates/updates:

- Site: `DigifyCX Intranet Test`
- App pool: `DigifyCXIntranet-Test`
- Physical path: `C:\Sites\DigifyCXIntranet\Test`
- Binding: `http://192.168.69.17:8080/`
- App pool runtime: No Managed Code
- App pool identity: ApplicationPoolIdentity
- Authentication: Anonymous enabled; Windows enabled by default

## Firewall

Review and apply only after confirming the internal network range:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\firewall-test.example.ps1
```

Default planned rule:

```text
TCP 8080 inbound from 192.168.68.0/23 only
```

The script refuses `Any` and `0.0.0.0/0`.

## Database

The app requires `ConnectionStrings__DefaultConnection`. Prefer Integrated Security for TEST if SQL Server is local or domain-accessible.

For the shared Windows 11 internal test binding only, SQL Server Express may use a certificate chain that is not trusted by this machine. The app defaults to strict SQL transport validation, but can explicitly allow `TrustServerCertificate=True` only when `SqlServerSecurity__AllowTrustServerCertificateForInternalTest=true` is configured. Proper production should keep `Encrypt=True;TrustServerCertificate=False` and use a trusted SQL Server certificate.

For the temporary HTTP port 8080 binding, set
`AuthMode__AllowInsecureHttpForInternalTest=true`; otherwise Production cookies are
secure-only and login cannot persist over HTTP. Set
`AuthMode__SeedConfiguredTestUsers=true` to synchronize configured test profiles into
ASP.NET Core Identity. Both flags default to false and must be false for final HTTPS
production.

Example SQL for a local SQL Server database:

```sql
CREATE LOGIN [IIS APPPOOL\DigifyCXIntranet-Test] FROM WINDOWS;
USE [DigifyCXIntranetTest];
CREATE USER [IIS APPPOOL\DigifyCXIntranet-Test] FOR LOGIN [IIS APPPOOL\DigifyCXIntranet-Test];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\DigifyCXIntranet-Test];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\DigifyCXIntranet-Test];
```

Do not run migrations or grant schema permissions without explicit approval.

## Copy And Rollback

After publish passes and IIS prerequisites are installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\copy-to-iis-test.ps1
```

The copy script scans the publish folder again, prints the planned overwrite, asks for `YES`, backs up existing files to:

```text
C:\Sites\DigifyCXIntranet\Backups\Test-yyyyMMdd-HHmmss
```

Then it copies to:

```text
C:\Sites\DigifyCXIntranet\Test
```

Rollback: copy the chosen backup folder contents back to `C:\Sites\DigifyCXIntranet\Test`, then restart `DigifyCXIntranet-Test`.

## Local Test

After IIS setup and copy:

```powershell
Invoke-WebRequest http://192.168.69.17:8080/ -UseBasicParsing
Import-Module WebAdministration
Get-Website "DigifyCX Intranet Test"
Get-WebAppPoolState "DigifyCXIntranet-Test"
```

If the app fails, check:

```powershell
Get-EventLog -LogName Application -Newest 50
Get-ChildItem C:\inetpub\logs\LogFiles -Recurse -File | Sort-Object LastWriteTime -Descending | Select-Object -First 10
```

## Troubleshooting

- HTTP 500.30: check Event Viewer, app pool environment variables, database connectivity, and temporarily enable stdout logs only while diagnosing.
- Missing Hosting Bundle: install the ASP.NET Core Hosting Bundle for .NET 8 and restart IIS.
- Database connection failure: verify `ConnectionStrings__DefaultConnection`, SQL reachability, certificate/trust settings, and app pool login permissions.
- Authentication loop: confirm `AuthMode:UseWindowsAuthenticationInNonDevelopment`, IIS Windows Authentication, browser intranet-zone settings, and domain/Kerberos/NTLM behavior.
- 401/403: verify domain credentials, IIS auth features, app role mappings under `AdminAccess`, and anonymous page requirements.
- Static files not loading: confirm IIS physical path, `wwwroot` presence, and app pool read permissions.

## Secrets To Rotate

The previous tracked development configuration contained live-looking values. Rotate or replace these keys/sources:

- `ZendeskSync__ApiToken`
- `ZendeskWebhook__Secret`
- `Activation__DefaultPassword`
- `Activation__SheetApiUrl`
- `UserRegistrySync__SheetApiUrl`
- `AuthMode__DevelopmentUsers__*__Password`

The values were removed from tracked configuration and are intentionally not documented here.
