using System;
using DigifyCXIntranet.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260620143000_AddOperationalHardening")]
    public partial class AddOperationalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing installations may already have these objects from the legacy
            // startup schema repair. Keep this migration safe for both clean and
            // previously repaired databases.
            migrationBuilder.Sql("""
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Announcements_IsDeleted_IsActive_IsPinned_PublishDateUtc_ExpirationDate' AND [object_id] = OBJECT_ID(N'Announcements'))
    CREATE INDEX [IX_Announcements_IsDeleted_IsActive_IsPinned_PublishDateUtc_ExpirationDate] ON [Announcements] ([IsDeleted], [IsActive], [IsPinned], [PublishDateUtc], [ExpirationDate]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_Actor_Action_TimestampUtc' AND [object_id] = OBJECT_ID(N'AuditLogs'))
    CREATE INDEX [IX_AuditLogs_Actor_Action_TimestampUtc] ON [AuditLogs] ([Actor], [Action], [TimestampUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_Entity_EntityId_TimestampUtc' AND [object_id] = OBJECT_ID(N'AuditLogs'))
    CREATE INDEX [IX_AuditLogs_Entity_EntityId_TimestampUtc] ON [AuditLogs] ([Entity], [EntityId], [TimestampUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AuditLogs_TimestampUtc' AND [object_id] = OBJECT_ID(N'AuditLogs'))
    CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs] ([TimestampUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BackgroundJobRuns_JobName_StartedUtc' AND [object_id] = OBJECT_ID(N'BackgroundJobRuns'))
    CREATE INDEX [IX_BackgroundJobRuns_JobName_StartedUtc] ON [BackgroundJobRuns] ([JobName], [StartedUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_BackgroundJobRuns_Status_StartedUtc' AND [object_id] = OBJECT_ID(N'BackgroundJobRuns'))
    CREATE INDEX [IX_BackgroundJobRuns_Status_StartedUtc] ON [BackgroundJobRuns] ([Status], [StartedUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CanteenOrders_OrderTimeUtc_Status_CanteenBatchRunId' AND [object_id] = OBJECT_ID(N'CanteenOrders'))
    CREATE INDEX [IX_CanteenOrders_OrderTimeUtc_Status_CanteenBatchRunId] ON [CanteenOrders] ([OrderTimeUtc], [Status], [CanteenBatchRunId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailOutboxAttachments_EmailOutboxMessageId' AND [object_id] = OBJECT_ID(N'EmailOutboxAttachments'))
    CREATE INDEX [IX_EmailOutboxAttachments_EmailOutboxMessageId] ON [EmailOutboxAttachments] ([EmailOutboxMessageId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EmailOutboxMessages_Status_CreatedUtc' AND [object_id] = OBJECT_ID(N'EmailOutboxMessages'))
    CREATE INDEX [IX_EmailOutboxMessages_Status_CreatedUtc] ON [EmailOutboxMessages] ([Status], [CreatedUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FinanceAuditLogs_Actor_Action_TimestampUtc' AND [object_id] = OBJECT_ID(N'FinanceAuditLogs'))
    CREATE INDEX [IX_FinanceAuditLogs_Actor_Action_TimestampUtc] ON [FinanceAuditLogs] ([Actor], [Action], [TimestampUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_JobPostings_IsDeleted_ClosingDate' AND [object_id] = OBJECT_ID(N'JobPostings'))
    CREATE INDEX [IX_JobPostings_IsDeleted_ClosingDate] ON [JobPostings] ([IsDeleted], [ClosingDate]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PolicyAcknowledgements_EmployeeDomainName_TimestampUtc' AND [object_id] = OBJECT_ID(N'PolicyAcknowledgements'))
    CREATE INDEX [IX_PolicyAcknowledgements_EmployeeDomainName_TimestampUtc] ON [PolicyAcknowledgements] ([EmployeeDomainName], [TimestampUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PolicyAcknowledgements_PolicyArticleId_TimestampUtc' AND [object_id] = OBJECT_ID(N'PolicyAcknowledgements'))
    CREATE INDEX [IX_PolicyAcknowledgements_PolicyArticleId_TimestampUtc] ON [PolicyAcknowledgements] ([PolicyArticleId], [TimestampUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ZendeskPolicyArticles_IsPublished_CategoryName_SectionName_UpdatedAtUtc' AND [object_id] = OBJECT_ID(N'ZendeskPolicyArticles'))
    CREATE INDEX [IX_ZendeskPolicyArticles_IsPublished_CategoryName_SectionName_UpdatedAtUtc] ON [ZendeskPolicyArticles] ([IsPublished], [CategoryName], [SectionName], [UpdatedAtUtc]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ZendeskSyncLogs_Succeeded_StartedUtc' AND [object_id] = OBJECT_ID(N'ZendeskSyncLogs'))
    CREATE INDEX [IX_ZendeskSyncLogs_Succeeded_StartedUtc] ON [ZendeskSyncLogs] ([Succeeded], [StartedUtc]);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("AuditLogs");
            migrationBuilder.DropTable("BackgroundJobRuns");
            migrationBuilder.DropTable("EmailOutboxAttachments");
            migrationBuilder.DropTable("EmailOutboxMessages");

            migrationBuilder.DropIndex("IX_Announcements_IsDeleted_IsActive_IsPinned_PublishDateUtc_ExpirationDate", "Announcements");
            migrationBuilder.DropIndex("IX_CanteenOrders_OrderTimeUtc_Status_CanteenBatchRunId", "CanteenOrders");
            migrationBuilder.DropIndex("IX_FinanceAuditLogs_Actor_Action_TimestampUtc", "FinanceAuditLogs");
            migrationBuilder.DropIndex("IX_JobPostings_IsDeleted_ClosingDate", "JobPostings");
            migrationBuilder.DropIndex("IX_PolicyAcknowledgements_EmployeeDomainName_TimestampUtc", "PolicyAcknowledgements");
            migrationBuilder.DropIndex("IX_PolicyAcknowledgements_PolicyArticleId_TimestampUtc", "PolicyAcknowledgements");
            migrationBuilder.DropIndex("IX_ZendeskPolicyArticles_IsPublished_CategoryName_SectionName_UpdatedAtUtc", "ZendeskPolicyArticles");
            migrationBuilder.DropIndex("IX_ZendeskSyncLogs_Succeeded_StartedUtc", "ZendeskSyncLogs");
        }
    }
}
