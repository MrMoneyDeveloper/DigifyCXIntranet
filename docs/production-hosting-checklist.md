# DigifyCX Intranet Production Hosting Checklist

Target: Windows Server, IIS reverse proxy, ASP.NET Core hosting bundle, SQL Server.

## IIS and TLS
- Bind the site to HTTPS only with a valid certificate and a documented renewal owner.
- Set the app pool to `No Managed Code`, 64-bit enabled, and a dedicated least-privilege identity.
- Grant the app pool identity read/write only to the app content that needs it, `App_Data\Outbox`, and log folders.
- Keep one active app instance unless `JobScheduling:UsePersistentStore=true` and `JobScheduling:UseClustering=true` are configured with Quartz SQL tables.
- Forward `X-Forwarded-For` and `X-Forwarded-Proto` from IIS/reverse proxy; the app already enforces forwarded headers, HSTS outside development, and HTTPS redirection.

## Database
- Production connection strings must use `Encrypt=True;TrustServerCertificate=False`.
- Run EF migrations before switching traffic.
- Run the SQL Agent backup jobs from `docs/sql-agent-backup-jobs.sql` and write backups to an off-server network share.
- Enable `BackupHealth:Enabled=true` only after SQL Agent backups are running and visible in `msdb.dbo.backupset`.
- Before setting `JobScheduling:UsePersistentStore=true`, create the standard Quartz SQL Server tables in the application database using the Quartz `tables_sqlServer.sql` script that matches the deployed Quartz package version.

## Health and Monitoring
- Liveness: `/health/live`
- Readiness: `/health/ready`
- Database and backup health: `/health/database`
- Monitor Windows Event Viewer and IIS logs for failed jobs, repeated 429 responses, stale backups, failed SMTP, failed Zendesk sync, and SQL health failures.

## Restore Drill
- Monthly, restore the latest full backup, latest differential backup, and available log backups to a validation database.
- Record restore start/end time, backup chain used, validation query result, RPO, and RTO.
- Treat a failed restore drill as a production incident until a successful backup chain is confirmed.
