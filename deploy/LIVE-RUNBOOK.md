# DigifyCX Intranet Live-On-This-Machine Runbook

This runbook is for the shared company Windows 11 Pro machine. It preserves the existing `Default Web Site`, `DefaultAppPool`, and ports `80`/`443`.

## Current Live Setup

- IIS site: `DigifyCX Intranet`
- IIS app pool: `DigifyCXIntranet`
- Physical path: `C:\Sites\DigifyCXIntranet\Live`
- Backup path: `C:\Sites\DigifyCXIntranet\Backups`
- Initial binding: `http://192.168.69.17:8080/`
- Final hostname later: `intranet.digifycx.local`

## Safety Rules

- Do not modify or stop `Default Web Site`.
- Do not modify `DefaultAppPool`.
- Do not bind the new site to `80` or `443` during the initial test binding.
- Do not run `iisreset`.
- Restart only `DigifyCXIntranet` when needed.
- Do not apply firewall rules before reviewing the exact rule.
- Do not use firewall `RemoteAddress Any` or `0.0.0.0/0`.
- Do not copy to `C:\Sites\DigifyCXIntranet\Live` without a backup.
- Do not publish `appsettings.Development.json`.
- Store real deployment values in environment variables, not appsettings files.

## Publish

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\publish-live.ps1
```

## Deployment Order

1. Create the IIS site/app pool on the initial port `8080` binding:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis-setup-live.ps1
```

2. Set IIS app-pool-scoped environment variables for only `DigifyCXIntranet`:

Plan only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-live-apppool.ps1 -Plan
```

Apply by entering values interactively:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-live-apppool.ps1
```

To read values from local committed appsettings only after explicit approval:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-live-apppool.ps1 -ReadFromLocalCommittedAppSettings
```

If this IIS setup does not expose app-pool environment variables, copy the published files first, then use the deployed `web.config` fallback:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-live-apppool.ps1 -Method WebConfig
```

Verify configured app-pool environment variable names without printing values:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-live-apppool.ps1 -ListNames
```

3. Copy published files to `C:\Sites\DigifyCXIntranet\Live` with backup:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\copy-to-iis-live.ps1
```

4. Restart only the DigifyCX app pool:

```powershell
Restart-WebAppPool -Name "DigifyCXIntranet"
```

5. Test the initial internal URL:

```powershell
Invoke-WebRequest http://192.168.69.17:8080/ -UseBasicParsing
```

## Environment Variables

Review required variables without applying changes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\set-env-live.example.ps1
```

Do not use Machine-level environment variables on this shared computer. Use
`set-env-live-apppool.ps1`; its IIS app-pool method or deployed `web.config`
fallback is scoped to `DigifyCXIntranet` only and masks values.

The temporary HTTP test binding requires these explicit internal-test switches:

- `AuthMode__AllowInsecureHttpForInternalTest=true`
- `AuthMode__SeedConfiguredTestUsers=true`

The first switch allows the authentication cookie over the temporary HTTP port
8080. The second validates the configured test profiles, creates or updates their
ASP.NET Core Identity rows, hashes their configured passwords, and synchronizes
their display names and roles. Configured test credentials are accepted directly
only in Development; the Production test deployment authenticates them against the
database.

For this internal Windows 11 test deployment only, the app can allow SQL Server's untrusted local certificate when both are true:

- `ConnectionStrings__DefaultConnection` uses `Encrypt=True;TrustServerCertificate=True`
- `SqlServerSecurity__AllowTrustServerCertificateForInternalTest=true`

This is an explicit test/demo escape hatch. Proper production should keep `TrustServerCertificate=False` and use a SQL Server TLS certificate trusted by the app server.

## IIS Initial Test Binding

Review/apply after approval:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\iis-setup-live.ps1
```

## Copy To IIS

Review/apply after approval:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\copy-to-iis-live.ps1
```

## Firewall

Review first:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\firewall-live-testbinding.ps1
```

Apply only after approval:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\firewall-live-testbinding.ps1 -RemoteAddress "192.168.68.0/23" -Apply
```

## Local Test

```powershell
Invoke-WebRequest http://192.168.69.17:8080/ -UseBasicParsing
Import-Module WebAdministration
Get-Website "DigifyCX Intranet"
Get-WebAppPoolState "DigifyCXIntranet"
```

## Final Go-Live Later

After internal DNS is ready for `intranet.digifycx.local`, add the final binding separately.

HTTP final binding:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\add-final-binding.ps1
```

HTTPS final binding after an internal certificate is installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\add-final-binding.ps1 -Protocol https -CertificateThumbprint "<thumbprint>"
```

Before final production go-live, set both internal-test switches to `false`,
publish/copy with backup, restart only `DigifyCXIntranet`, and verify HTTPS login.
