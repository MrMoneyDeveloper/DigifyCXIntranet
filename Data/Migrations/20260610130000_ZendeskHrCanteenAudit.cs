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
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "JobPostings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Announcements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CandidateName",
                table: "ReferralInvites",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CandidatePhone",
                table: "ReferralInvites",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ReferralInvites",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferrerEmployeeEmail",
                table: "ReferralInvites",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ZendeskTicketId",
                table: "ReferralInvites",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidatePhone",
                table: "ExternalApplications",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ZendeskTicketId",
                table: "ExternalApplications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZendeskTicketUrl",
                table: "ExternalApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ZendeskTicketId",
                table: "InternalJobApplications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZendeskTicketUrl",
                table: "InternalJobApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "CategoryId",
                table: "ZendeskPolicyArticles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "ZendeskPolicyArticles",
                type: "nvarchar(220)",
                maxLength: 220,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SectionId",
                table: "ZendeskPolicyArticles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionName",
                table: "ZendeskPolicyArticles",
                type: "nvarchar(220)",
                maxLength: 220,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FinanceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Actor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Entity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZendeskSyncLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ItemsProcessed = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZendeskSyncLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceAuditLogs_TimestampUtc",
                table: "FinanceAuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ZendeskSyncLogs_StartedUtc",
                table: "ZendeskSyncLogs",
                column: "StartedUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FinanceAuditLogs");
            migrationBuilder.DropTable(name: "ZendeskSyncLogs");

            migrationBuilder.DropColumn(name: "IsDeleted", table: "MenuItems");
            migrationBuilder.DropColumn(name: "IsDeleted", table: "JobPostings");
            migrationBuilder.DropColumn(name: "IsDeleted", table: "Announcements");
            migrationBuilder.DropColumn(name: "CandidateName", table: "ReferralInvites");
            migrationBuilder.DropColumn(name: "CandidatePhone", table: "ReferralInvites");
            migrationBuilder.DropColumn(name: "Notes", table: "ReferralInvites");
            migrationBuilder.DropColumn(name: "ReferrerEmployeeEmail", table: "ReferralInvites");
            migrationBuilder.DropColumn(name: "ZendeskTicketId", table: "ReferralInvites");
            migrationBuilder.DropColumn(name: "CandidatePhone", table: "ExternalApplications");
            migrationBuilder.DropColumn(name: "ZendeskTicketId", table: "ExternalApplications");
            migrationBuilder.DropColumn(name: "ZendeskTicketUrl", table: "ExternalApplications");
            migrationBuilder.DropColumn(name: "ZendeskTicketId", table: "InternalJobApplications");
            migrationBuilder.DropColumn(name: "ZendeskTicketUrl", table: "InternalJobApplications");
            migrationBuilder.DropColumn(name: "CategoryId", table: "ZendeskPolicyArticles");
            migrationBuilder.DropColumn(name: "CategoryName", table: "ZendeskPolicyArticles");
            migrationBuilder.DropColumn(name: "SectionId", table: "ZendeskPolicyArticles");
            migrationBuilder.DropColumn(name: "SectionName", table: "ZendeskPolicyArticles");
        }
    }
}