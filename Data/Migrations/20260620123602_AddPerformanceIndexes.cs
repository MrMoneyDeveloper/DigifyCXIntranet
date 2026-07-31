using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ForgotPasswordRequests')
                BEGIN
                    CREATE TABLE [ForgotPasswordRequests] (
                        [Id]             int           NOT NULL IDENTITY(1,1),
                        [FullName]       nvarchar(max) NOT NULL,
                        [Description]   nvarchar(max) NOT NULL,
                        [IpAddress]     nvarchar(max) NOT NULL,
                        [SubmittedUtc]  datetime2     NOT NULL,
                        [ZendeskTicketId] bigint      NULL,
                        [ZendeskTicketUrl] nvarchar(max) NOT NULL,
                        [Succeeded]     bit           NOT NULL,
                        CONSTRAINT [PK_ForgotPasswordRequests] PRIMARY KEY ([Id])
                    );
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_ZendeskPolicyArticles_SectionName_Title'
                    AND object_id = OBJECT_ID(N'ZendeskPolicyArticles')
                )
                    CREATE INDEX [IX_ZendeskPolicyArticles_SectionName_Title]
                    ON [ZendeskPolicyArticles] ([SectionName], [Title]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_MenuItems_IsDeleted_IsActive_MealSlot_DisplayOrder'
                    AND object_id = OBJECT_ID(N'MenuItems')
                )
                    CREATE INDEX [IX_MenuItems_IsDeleted_IsActive_MealSlot_DisplayOrder]
                    ON [MenuItems] ([IsDeleted], [IsActive], [MealSlot], [DisplayOrder]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_JobPostings_IsDeleted_IsActive_ClosingDate'
                    AND object_id = OBJECT_ID(N'JobPostings')
                )
                    CREATE INDEX [IX_JobPostings_IsDeleted_IsActive_ClosingDate]
                    ON [JobPostings] ([IsDeleted], [IsActive], [ClosingDate]);
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_CanteenOrders_EmployeeUsername_OrderTimeUtc'
                    AND object_id = OBJECT_ID(N'CanteenOrders')
                )
                    CREATE INDEX [IX_CanteenOrders_EmployeeUsername_OrderTimeUtc]
                    ON [CanteenOrders] ([EmployeeUsername], [OrderTimeUtc]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ForgotPasswordRequests') DROP TABLE [ForgotPasswordRequests];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ZendeskPolicyArticles_SectionName_Title' AND object_id = OBJECT_ID(N'ZendeskPolicyArticles')) DROP INDEX [IX_ZendeskPolicyArticles_SectionName_Title] ON [ZendeskPolicyArticles];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MenuItems_IsDeleted_IsActive_MealSlot_DisplayOrder' AND object_id = OBJECT_ID(N'MenuItems')) DROP INDEX [IX_MenuItems_IsDeleted_IsActive_MealSlot_DisplayOrder] ON [MenuItems];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobPostings_IsDeleted_IsActive_ClosingDate' AND object_id = OBJECT_ID(N'JobPostings')) DROP INDEX [IX_JobPostings_IsDeleted_IsActive_ClosingDate] ON [JobPostings];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CanteenOrders_EmployeeUsername_OrderTimeUtc' AND object_id = OBJECT_ID(N'CanteenOrders')) DROP INDEX [IX_CanteenOrders_EmployeeUsername_OrderTimeUtc] ON [CanteenOrders];");
        }
    }
}
