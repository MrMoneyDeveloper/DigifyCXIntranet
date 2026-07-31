/*
DigifyCX Intranet SQL Agent backup template.

Run as a SQL Server administrator, review variables first, and create SQL Agent
jobs/schedules from the commands or equivalent maintenance-plan steps.

Backups must be written outside the web server, preferably to a protected
network share that is copied to offline or immutable storage.
*/

DECLARE @DatabaseName sysname = N'DigifyCXIntranet';
DECLARE @BackupRoot nvarchar(4000) = N'\\fileserver\sqlbackups\DigifyCXIntranet';
DECLARE @Stamp nvarchar(32) = FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmmss');

-- Full backup: schedule daily outside business hours.
DECLARE @FullPath nvarchar(4000) = CONCAT(@BackupRoot, N'\full\', @DatabaseName, N'_FULL_', @Stamp, N'.bak');
BACKUP DATABASE @DatabaseName
TO DISK = @FullPath
WITH COMPRESSION, CHECKSUM, INIT, STATS = 10;

-- Differential backup: schedule hourly or twice daily depending on RPO.
DECLARE @DiffPath nvarchar(4000) = CONCAT(@BackupRoot, N'\diff\', @DatabaseName, N'_DIFF_', @Stamp, N'.bak');
BACKUP DATABASE @DatabaseName
TO DISK = @DiffPath
WITH DIFFERENTIAL, COMPRESSION, CHECKSUM, INIT, STATS = 10;

-- Log backup: enable FULL recovery first, then schedule every 15-60 minutes.
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = @DatabaseName) = 'FULL'
BEGIN
    DECLARE @LogPath nvarchar(4000) = CONCAT(@BackupRoot, N'\log\', @DatabaseName, N'_LOG_', @Stamp, N'.trn');
    BACKUP LOG @DatabaseName
    TO DISK = @LogPath
    WITH COMPRESSION, CHECKSUM, INIT, STATS = 10;
END

-- Restore verification smoke test after restoring to a validation DB:
-- DBCC CHECKDB(N'DigifyCXIntranet_RestoreValidation') WITH NO_INFOMSGS;
