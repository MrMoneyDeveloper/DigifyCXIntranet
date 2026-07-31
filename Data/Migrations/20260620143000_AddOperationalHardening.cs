using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Actor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Entity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RemoteIp = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_AuditLogs", x => x.Id));

            migrationBuilder.CreateTable(
                name: "BackgroundJobRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextFireUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    ItemsProcessed = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_BackgroundJobRuns", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    To = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailOutboxAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailOutboxMessageId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailOutboxAttachments_EmailOutboxMessages_EmailOutboxMessageId",
                        column: x => x.EmailOutboxMessageId,
                        principalTable: "EmailOutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_Announcements_IsDeleted_IsActive_IsPinned_PublishDateUtc_ExpirationDate", "Announcements", new[] { "IsDeleted", "IsActive", "IsPinned", "PublishDateUtc", "ExpirationDate" });
            migrationBuilder.CreateIndex("IX_AuditLogs_Actor_Action_TimestampUtc", "AuditLogs", new[] { "Actor", "Action", "TimestampUtc" });
            migrationBuilder.CreateIndex("IX_AuditLogs_Entity_EntityId_TimestampUtc", "AuditLogs", new[] { "Entity", "EntityId", "TimestampUtc" });
            migrationBuilder.CreateIndex("IX_AuditLogs_TimestampUtc", "AuditLogs", "TimestampUtc");
            migrationBuilder.CreateIndex("IX_BackgroundJobRuns_JobName_StartedUtc", "BackgroundJobRuns", new[] { "JobName", "StartedUtc" });
            migrationBuilder.CreateIndex("IX_BackgroundJobRuns_Status_StartedUtc", "BackgroundJobRuns", new[] { "Status", "StartedUtc" });
            migrationBuilder.CreateIndex("IX_CanteenOrders_OrderTimeUtc_Status_CanteenBatchRunId", "CanteenOrders", new[] { "OrderTimeUtc", "Status", "CanteenBatchRunId" });
            migrationBuilder.CreateIndex("IX_EmailOutboxAttachments_EmailOutboxMessageId", "EmailOutboxAttachments", "EmailOutboxMessageId");
            migrationBuilder.CreateIndex("IX_EmailOutboxMessages_Status_CreatedUtc", "EmailOutboxMessages", new[] { "Status", "CreatedUtc" });
            migrationBuilder.CreateIndex("IX_FinanceAuditLogs_Actor_Action_TimestampUtc", "FinanceAuditLogs", new[] { "Actor", "Action", "TimestampUtc" });
            migrationBuilder.CreateIndex("IX_JobPostings_IsDeleted_ClosingDate", "JobPostings", new[] { "IsDeleted", "ClosingDate" });
            migrationBuilder.CreateIndex("IX_PolicyAcknowledgements_EmployeeDomainName_TimestampUtc", "PolicyAcknowledgements", new[] { "EmployeeDomainName", "TimestampUtc" });
            migrationBuilder.CreateIndex("IX_PolicyAcknowledgements_PolicyArticleId_TimestampUtc", "PolicyAcknowledgements", new[] { "PolicyArticleId", "TimestampUtc" });
            migrationBuilder.CreateIndex("IX_ZendeskPolicyArticles_IsPublished_CategoryName_SectionName_UpdatedAtUtc", "ZendeskPolicyArticles", new[] { "IsPublished", "CategoryName", "SectionName", "UpdatedAtUtc" });
            migrationBuilder.CreateIndex("IX_ZendeskSyncLogs_Succeeded_StartedUtc", "ZendeskSyncLogs", new[] { "Succeeded", "StartedUtc" });
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
