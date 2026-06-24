using System;
using DigifyCXIntranet.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260610130000_ZendeskHrCanteenAudit")]
    public partial class ZendeskHrCanteenAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guard each IsDeleted column — the column may already exist on databases
            // that had it added manually or via a prior partial run.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'MenuItems') AND name = 'IsDeleted'
                )
                    ALTER TABLE [MenuItems] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'JobPostings') AND name = 'IsDeleted'
                )
                    ALTER TABLE [JobPostings] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Announcements') AND name = 'IsDeleted'
                )
                    ALTER TABLE [Announcements] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'CandidateName'
                )
                    ALTER TABLE [ReferralInvites] ADD [CandidateName] nvarchar(150) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'CandidatePhone'
                )
                    ALTER TABLE [ReferralInvites] ADD [CandidatePhone] nvarchar(60) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'Notes'
                )
                    ALTER TABLE [ReferralInvites] ADD [Notes] nvarchar(2000) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'ReferrerEmployeeEmail'
                )
                    ALTER TABLE [ReferralInvites] ADD [ReferrerEmployeeEmail] nvarchar(120) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'ZendeskTicketId'
                )
                    ALTER TABLE [ReferralInvites] ADD [ZendeskTicketId] bigint NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ExternalApplications') AND name = 'CandidatePhone'
                )
                    ALTER TABLE [ExternalApplications] ADD [CandidatePhone] nvarchar(60) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ExternalApplications') AND name = 'ZendeskTicketId'
                )
                    ALTER TABLE [ExternalApplications] ADD [ZendeskTicketId] bigint NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ExternalApplications') AND name = 'ZendeskTicketUrl'
                )
                    ALTER TABLE [ExternalApplications] ADD [ZendeskTicketUrl] nvarchar(500) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'InternalJobApplications') AND name = 'ZendeskTicketId'
                )
                    ALTER TABLE [InternalJobApplications] ADD [ZendeskTicketId] bigint NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'InternalJobApplications') AND name = 'ZendeskTicketUrl'
                )
                    ALTER TABLE [InternalJobApplications] ADD [ZendeskTicketUrl] nvarchar(500) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'CategoryId'
                )
                    ALTER TABLE [ZendeskPolicyArticles] ADD [CategoryId] bigint NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'CategoryName'
                )
                    ALTER TABLE [ZendeskPolicyArticles] ADD [CategoryName] nvarchar(220) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'SectionId'
                )
                    ALTER TABLE [ZendeskPolicyArticles] ADD [SectionId] bigint NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'SectionName'
                )
                    ALTER TABLE [ZendeskPolicyArticles] ADD [SectionName] nvarchar(220) NOT NULL DEFAULT N'';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FinanceAuditLogs')
                BEGIN
                    CREATE TABLE [FinanceAuditLogs] (
                        [Id]           int           NOT NULL IDENTITY(1,1),
                        [Actor]        nvarchar(120) NOT NULL,
                        [Action]       nvarchar(120) NOT NULL,
                        [Entity]       nvarchar(120) NOT NULL,
                        [TimestampUtc] datetime2     NOT NULL,
                        [Detail]       nvarchar(4000) NOT NULL,
                        CONSTRAINT [PK_FinanceAuditLogs] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_FinanceAuditLogs_TimestampUtc] ON [FinanceAuditLogs] ([TimestampUtc]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ZendeskSyncLogs')
                BEGIN
                    CREATE TABLE [ZendeskSyncLogs] (
                        [Id]             int           NOT NULL IDENTITY(1,1),
                        [Operation]      nvarchar(80)  NOT NULL,
                        [StartedUtc]     datetime2     NOT NULL,
                        [CompletedUtc]   datetime2     NULL,
                        [Succeeded]      bit           NOT NULL,
                        [ItemsProcessed] int           NOT NULL,
                        [Message]        nvarchar(1000) NOT NULL,
                        CONSTRAINT [PK_ZendeskSyncLogs] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_ZendeskSyncLogs_StartedUtc] ON [ZendeskSyncLogs] ([StartedUtc]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FinanceAuditLogs') DROP TABLE [FinanceAuditLogs];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ZendeskSyncLogs') DROP TABLE [ZendeskSyncLogs];");

            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'MenuItems') AND name = 'IsDeleted') ALTER TABLE [MenuItems] DROP COLUMN [IsDeleted];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'JobPostings') AND name = 'IsDeleted') ALTER TABLE [JobPostings] DROP COLUMN [IsDeleted];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Announcements') AND name = 'IsDeleted') ALTER TABLE [Announcements] DROP COLUMN [IsDeleted];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'CandidateName') ALTER TABLE [ReferralInvites] DROP COLUMN [CandidateName];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'CandidatePhone') ALTER TABLE [ReferralInvites] DROP COLUMN [CandidatePhone];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'Notes') ALTER TABLE [ReferralInvites] DROP COLUMN [Notes];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'ReferrerEmployeeEmail') ALTER TABLE [ReferralInvites] DROP COLUMN [ReferrerEmployeeEmail];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ReferralInvites') AND name = 'ZendeskTicketId') ALTER TABLE [ReferralInvites] DROP COLUMN [ZendeskTicketId];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ExternalApplications') AND name = 'CandidatePhone') ALTER TABLE [ExternalApplications] DROP COLUMN [CandidatePhone];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ExternalApplications') AND name = 'ZendeskTicketId') ALTER TABLE [ExternalApplications] DROP COLUMN [ZendeskTicketId];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ExternalApplications') AND name = 'ZendeskTicketUrl') ALTER TABLE [ExternalApplications] DROP COLUMN [ZendeskTicketUrl];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'InternalJobApplications') AND name = 'ZendeskTicketId') ALTER TABLE [InternalJobApplications] DROP COLUMN [ZendeskTicketId];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'InternalJobApplications') AND name = 'ZendeskTicketUrl') ALTER TABLE [InternalJobApplications] DROP COLUMN [ZendeskTicketUrl];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'CategoryId') ALTER TABLE [ZendeskPolicyArticles] DROP COLUMN [CategoryId];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'CategoryName') ALTER TABLE [ZendeskPolicyArticles] DROP COLUMN [CategoryName];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'SectionId') ALTER TABLE [ZendeskPolicyArticles] DROP COLUMN [SectionId];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ZendeskPolicyArticles') AND name = 'SectionName') ALTER TABLE [ZendeskPolicyArticles] DROP COLUMN [SectionName];");
        }
    }
}
