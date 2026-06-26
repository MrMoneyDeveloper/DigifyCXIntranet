using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Services;

public static class DatabaseSchemaRepair
{
    public static Task EnsurePostMigrationSchemaAsync(DbContext db, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF COL_LENGTH('MenuItems', 'IsDeleted') IS NULL
    ALTER TABLE [MenuItems] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_MenuItems_IsDeleted] DEFAULT CAST(0 AS bit);

IF COL_LENGTH('JobPostings', 'IsDeleted') IS NULL
    ALTER TABLE [JobPostings] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_JobPostings_IsDeleted] DEFAULT CAST(0 AS bit);

IF COL_LENGTH('Announcements', 'IsDeleted') IS NULL
    ALTER TABLE [Announcements] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_Announcements_IsDeleted] DEFAULT CAST(0 AS bit);

IF COL_LENGTH('ReferralInvites', 'CandidateName') IS NULL
    ALTER TABLE [ReferralInvites] ADD [CandidateName] nvarchar(150) NOT NULL CONSTRAINT [DF_ReferralInvites_CandidateName] DEFAULT N'';

IF COL_LENGTH('ReferralInvites', 'CandidatePhone') IS NULL
    ALTER TABLE [ReferralInvites] ADD [CandidatePhone] nvarchar(60) NOT NULL CONSTRAINT [DF_ReferralInvites_CandidatePhone] DEFAULT N'';

IF COL_LENGTH('ReferralInvites', 'Notes') IS NULL
    ALTER TABLE [ReferralInvites] ADD [Notes] nvarchar(2000) NOT NULL CONSTRAINT [DF_ReferralInvites_Notes] DEFAULT N'';

IF COL_LENGTH('ReferralInvites', 'ReferrerEmployeeEmail') IS NULL
    ALTER TABLE [ReferralInvites] ADD [ReferrerEmployeeEmail] nvarchar(120) NOT NULL CONSTRAINT [DF_ReferralInvites_ReferrerEmployeeEmail] DEFAULT N'';

IF COL_LENGTH('ReferralInvites', 'ZendeskTicketId') IS NULL
    ALTER TABLE [ReferralInvites] ADD [ZendeskTicketId] bigint NULL;

IF COL_LENGTH('ExternalApplications', 'CandidatePhone') IS NULL
    ALTER TABLE [ExternalApplications] ADD [CandidatePhone] nvarchar(60) NOT NULL CONSTRAINT [DF_ExternalApplications_CandidatePhone] DEFAULT N'';

IF COL_LENGTH('ExternalApplications', 'ZendeskTicketId') IS NULL
    ALTER TABLE [ExternalApplications] ADD [ZendeskTicketId] bigint NULL;

IF COL_LENGTH('ExternalApplications', 'ZendeskTicketUrl') IS NULL
    ALTER TABLE [ExternalApplications] ADD [ZendeskTicketUrl] nvarchar(500) NOT NULL CONSTRAINT [DF_ExternalApplications_ZendeskTicketUrl] DEFAULT N'';

IF COL_LENGTH('InternalJobApplications', 'ZendeskTicketId') IS NULL
    ALTER TABLE [InternalJobApplications] ADD [ZendeskTicketId] bigint NULL;

IF COL_LENGTH('InternalJobApplications', 'ZendeskTicketUrl') IS NULL
    ALTER TABLE [InternalJobApplications] ADD [ZendeskTicketUrl] nvarchar(500) NOT NULL CONSTRAINT [DF_InternalJobApplications_ZendeskTicketUrl] DEFAULT N'';

IF COL_LENGTH('ZendeskPolicyArticles', 'CategoryId') IS NULL
    ALTER TABLE [ZendeskPolicyArticles] ADD [CategoryId] bigint NULL;

IF COL_LENGTH('ZendeskPolicyArticles', 'CategoryName') IS NULL
    ALTER TABLE [ZendeskPolicyArticles] ADD [CategoryName] nvarchar(220) NOT NULL CONSTRAINT [DF_ZendeskPolicyArticles_CategoryName] DEFAULT N'';

IF COL_LENGTH('ZendeskPolicyArticles', 'SectionId') IS NULL
    ALTER TABLE [ZendeskPolicyArticles] ADD [SectionId] bigint NULL;

IF COL_LENGTH('ZendeskPolicyArticles', 'SectionName') IS NULL
    ALTER TABLE [ZendeskPolicyArticles] ADD [SectionName] nvarchar(220) NOT NULL CONSTRAINT [DF_ZendeskPolicyArticles_SectionName] DEFAULT N'';

IF OBJECT_ID('FinanceAuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [FinanceAuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [Actor] nvarchar(120) NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [Entity] nvarchar(120) NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [Detail] nvarchar(4000) NOT NULL,
        CONSTRAINT [PK_FinanceAuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FinanceAuditLogs_TimestampUtc' AND [object_id] = OBJECT_ID(N'FinanceAuditLogs'))
    CREATE INDEX [IX_FinanceAuditLogs_TimestampUtc] ON [FinanceAuditLogs] ([TimestampUtc]);

IF OBJECT_ID('ZendeskSyncLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [ZendeskSyncLogs] (
        [Id] int NOT NULL IDENTITY,
        [Operation] nvarchar(80) NOT NULL,
        [StartedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NULL,
        [Succeeded] bit NOT NULL,
        [ItemsProcessed] int NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_ZendeskSyncLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ZendeskSyncLogs_StartedUtc' AND [object_id] = OBJECT_ID(N'ZendeskSyncLogs'))
    CREATE INDEX [IX_ZendeskSyncLogs_StartedUtc] ON [ZendeskSyncLogs] ([StartedUtc]);

-- -------------------------------------------------------
-- ForgotPasswordRequests — audit log for IT-support tickets
-- -------------------------------------------------------
IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NULL
BEGIN
    CREATE TABLE [ForgotPasswordRequests] (
        [Id]               int NOT NULL IDENTITY,
        [FullName]         nvarchar(120) NOT NULL,
        [Description]      nvarchar(500) NOT NULL,
        [IpAddress]        nvarchar(45)  NOT NULL,
        [SubmittedUtc]     datetime2     NOT NULL,
        [ZendeskTicketId]  bigint NULL,
        [ZendeskTicketUrl] nvarchar(500) NOT NULL CONSTRAINT [DF_ForgotPasswordRequests_ZendeskTicketUrl] DEFAULT N'',
        [Succeeded]        bit NOT NULL,
        CONSTRAINT [PK_ForgotPasswordRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ForgotPasswordRequests_SubmittedUtc' AND [object_id] = OBJECT_ID(N'ForgotPasswordRequests'))
    CREATE INDEX [IX_ForgotPasswordRequests_SubmittedUtc] ON [ForgotPasswordRequests] ([SubmittedUtc]);

-- -------------------------------------------------------
-- Clear fake @digifycx.internal emails from existing users.
-- These were auto-generated by the sync worker and are no
-- longer needed. Only touches rows that still have the
-- generated domain — real personal emails are left alone.
-- -------------------------------------------------------
UPDATE [AspNetUsers]
SET    [Email]           = NULL,
       [NormalizedEmail] = NULL
WHERE  [Email] LIKE N'%@digifycx.internal';
""";

        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
