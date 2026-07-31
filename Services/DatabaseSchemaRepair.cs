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

IF OBJECT_ID('AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [Actor] nvarchar(120) NOT NULL,
        [Action] nvarchar(120) NOT NULL,
        [Entity] nvarchar(120) NOT NULL,
        [EntityId] nvarchar(120) NOT NULL,
        [Succeeded] bit NOT NULL,
        [ErrorCode] nvarchar(80) NOT NULL,
        [CorrelationId] nvarchar(80) NOT NULL,
        [RemoteIp] nvarchar(80) NOT NULL,
        [UserAgent] nvarchar(512) NOT NULL,
        [Route] nvarchar(256) NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [Detail] nvarchar(4000) NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_TimestampUtc' AND [object_id] = OBJECT_ID(N'AuditLogs'))
    CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs] ([TimestampUtc]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_Actor_Action_TimestampUtc' AND [object_id] = OBJECT_ID(N'AuditLogs'))
    CREATE INDEX [IX_AuditLogs_Actor_Action_TimestampUtc] ON [AuditLogs] ([Actor], [Action], [TimestampUtc]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_Entity_EntityId_TimestampUtc' AND [object_id] = OBJECT_ID(N'AuditLogs'))
    CREATE INDEX [IX_AuditLogs_Entity_EntityId_TimestampUtc] ON [AuditLogs] ([Entity], [EntityId], [TimestampUtc]);

IF OBJECT_ID('BackgroundJobRuns', 'U') IS NULL
BEGIN
    CREATE TABLE [BackgroundJobRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [JobName] nvarchar(120) NOT NULL,
        [Status] nvarchar(80) NOT NULL,
        [StartedUtc] datetime2 NOT NULL,
        [CompletedUtc] datetime2 NULL,
        [NextFireUtc] datetime2 NULL,
        [Attempt] int NOT NULL,
        [ItemsProcessed] int NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_BackgroundJobRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BackgroundJobRuns_JobName_StartedUtc' AND [object_id] = OBJECT_ID(N'BackgroundJobRuns'))
    CREATE INDEX [IX_BackgroundJobRuns_JobName_StartedUtc] ON [BackgroundJobRuns] ([JobName], [StartedUtc]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BackgroundJobRuns_Status_StartedUtc' AND [object_id] = OBJECT_ID(N'BackgroundJobRuns'))
    CREATE INDEX [IX_BackgroundJobRuns_Status_StartedUtc] ON [BackgroundJobRuns] ([Status], [StartedUtc]);

IF OBJECT_ID('EmailOutboxMessages', 'U') IS NULL
BEGIN
    CREATE TABLE [EmailOutboxMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [To] nvarchar(320) NOT NULL,
        [Subject] nvarchar(250) NOT NULL,
        [BodyText] nvarchar(max) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [LastAttemptUtc] datetime2 NULL,
        [SentUtc] datetime2 NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_EmailOutboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailOutboxMessages_Status_CreatedUtc' AND [object_id] = OBJECT_ID(N'EmailOutboxMessages'))
    CREATE INDEX [IX_EmailOutboxMessages_Status_CreatedUtc] ON [EmailOutboxMessages] ([Status], [CreatedUtc]);

IF OBJECT_ID('EmailOutboxAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE [EmailOutboxAttachments] (
        [Id] bigint NOT NULL IDENTITY,
        [EmailOutboxMessageId] bigint NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [Bytes] varbinary(max) NOT NULL,
        CONSTRAINT [PK_EmailOutboxAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailOutboxAttachments_EmailOutboxMessages_EmailOutboxMessageId]
            FOREIGN KEY ([EmailOutboxMessageId]) REFERENCES [EmailOutboxMessages] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailOutboxAttachments_EmailOutboxMessageId' AND [object_id] = OBJECT_ID(N'EmailOutboxAttachments'))
    CREATE INDEX [IX_EmailOutboxAttachments_EmailOutboxMessageId] ON [EmailOutboxAttachments] ([EmailOutboxMessageId]);
""";

        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
