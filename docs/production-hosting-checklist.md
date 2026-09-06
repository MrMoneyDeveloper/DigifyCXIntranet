# DigifyCX Intranet Production Hosting Checklist

Target: Windows Server, IIS reverse proxy, ASP.NET Core hosting bundle, SQL Server.

## IIS and TLS
- Bind the site to HTTPS only with a valid certificate and a documented renewal owner.
- Set the app pool to `No Managed Code`, 64-bit enabled, and a dedicated least-privilege identity.
- Enable `AlwaysRunning` plus application preload, disable idle timeout for the job-hosting pool, and schedule recycling outside critical job windows.
- Grant the app pool identity read/write only to the app content that needs it, `App_Data\Outbox`, `DataProtection:KeyRingPath`, and log folders.
- Keep `DataProtection:ApplicationName` identical on every node. On a single Windows/IIS host, leave `ProtectKeysWithDpapi=true`, enable **Load User Profile** for the app pool, and restrict the key-ring ACL to that app pool identity.
- For multiple IIS nodes, use a shared key ring and a certificate-backed key protector available to every node before enabling clustering; per-user DPAPI keys are not portable between machines.
- Keep one active app instance unless `JobScheduling:UsePersistentStore=true` and `JobScheduling:UseClustering=true` are configured with Quartz SQL tables.
- Forward `X-Forwarded-For` and `X-Forwarded-Proto` from IIS/reverse proxy; the app already enforces forwarded headers, HSTS outside development, and HTTPS redirection.

## Database
- Production connection strings must use `Encrypt=True;TrustServerCertificate=False`.
- Run EF migrations before switching traffic.
- Generate and review the idempotent deployment script with `dotnet tool restore` followed by `dotnet tool run dotnet-ef migrations script --idempotent --context ApplicationDbContext --configuration Release --output artifacts\migrations.sql`.
- Confirm migration `20260620143000_AddOperationalHardening` is applied and `dbo.AuditLogs`, `dbo.BackgroundJobRuns`, and the email outbox tables exist before accepting traffic.
- Run the SQL Agent backup jobs from `docs/sql-agent-backup-jobs.sql` and write backups to an off-server network share.
- Enable `BackupHealth:Enabled=true` only after SQL Agent backups are running and visible in `msdb.dbo.backupset`.
- Before setting `JobScheduling:UsePersistentStore=true`, create the standard Quartz SQL Server tables in the application database using the Quartz `tables_sqlServer.sql` script that matches the deployed Quartz package version.
- Grant the SQL Agent service account write access to the off-server share, define retention/cleanup jobs, and alert on failed backup job history.

## Health and Monitoring
- Liveness: `/health/live`
- Readiness: `/health/ready`
- Database and backup health: `/health/database`
- Runtime GC memory pressure and thread-pool backlog: `/health/runtime`
- Monitor Windows Event Viewer and IIS logs for failed jobs, repeated 429 responses, stale backups, failed SMTP, failed Zendesk sync, and SQL health failures.
- Start Content Security Policy with `BrowserSecurity:ReportOnly=true`. Review `ContentSecurityPolicy` warning events, allow only required origins, then switch `ReportOnly=false` after at least one normal operating cycle with no unexplained violations.
- Keep `RequestLimits` aligned with IIS `requestFiltering/requestLimits`; the application rejects bodies over 8 MB and multipart forms over 6 MB by default.
- Keep the tracked `web.config` host controls enabled: the IIS server header is removed, `X-Powered-By` is removed, TRACE is rejected, and `maxAllowedContentLength` matches `RequestLimits:MaxRequestBodyBytes`.
- Create the configured `Monitoring:EventLogSourceName` once as an administrator before the first production start, then grant the application identity permission to write events.
- Alert when `/health/ready` or `/health/database` is non-healthy for two consecutive checks; do not expose detailed health output publicly.
- Alert when `/health/runtime` is degraded for two consecutive checks. Tune `Monitoring:RuntimeMemoryLoadWarningPercent` and `Monitoring:RuntimeThreadPoolQueueWarningLength` only from an observed Release-build baseline.
- Track the IIS worker's private bytes and working set alongside .NET GC heap size, fragmentation, pinned objects, allocation rate, Gen 2 collections, thread-pool thread count, and queue length. A worker recycle is a recovery action, not the primary memory-control mechanism.
- Capture a process dump or GC dump before recycling a repeatedly degraded worker so retained object graphs can be identified. Keep dump files access-controlled because they can contain application and user data.
- The email outbox retains attachment filenames and content types after delivery but releases delivered binary payloads. Include failed and long-pending outbox rows in database-capacity monitoring because their payloads remain available for retry.

## Restore Drill
- Monthly, restore the latest full backup, latest differential backup, and available log backups to a validation database.
- Record restore start/end time, backup chain used, validation query result, RPO, and RTO.
- Treat a failed restore drill as a production incident until a successful backup chain is confirmed.

## LocalDB Recovery Safety
- If LocalDB reports system database paths under a nonexistent build directory, back up every application `.mdf` and `.ldf` before repairing or recreating the LocalDB instance.
- Do not delete `MSSQLLocalDB` as a first response. Repair SQL Server LocalDB from the installer or create a separate clean development instance, then attach verified application database files.
