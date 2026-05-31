using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase123AutomationAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludedInPayrollReconciliation",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "OrderDate",
                table: "CanteenOrders");

            migrationBuilder.RenameColumn(
                name: "EmploymentMonthsAtOrder",
                table: "CanteenOrders",
                newName: "MenuItemId");

            migrationBuilder.AddColumn<string>(
                name: "AdBackgroundImagePath",
                table: "JobPostings",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdHeadline",
                table: "JobPostings",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdSubHeadline",
                table: "JobPostings",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UseVisualAd",
                table: "JobPostings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CanteenBatchRunId",
                table: "CanteenOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MealSlot",
                table: "CanteenOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrderTimeUtc",
                table: "CanteenOrders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateUtc",
                table: "Announcements",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanteenBatchRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    MealSlot = table.Column<int>(type: "int", nullable: false),
                    CutoffLocalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggeredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrdersCount = table.Column<int>(type: "int", nullable: false),
                    EmailTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArtifactPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SentSuccessfully = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenBatchRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternalJobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobPostingId = table.Column<int>(type: "int", nullable: false),
                    EmployeeUsername = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EmployeeEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalJobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalJobApplications_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IconClass = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MealSlot = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggeredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeesCount = table.Column<int>(type: "int", nullable: false),
                    EmailTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArtifactPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SentSuccessfully = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                });

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [MenuItems] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [MenuItems] ON;
    INSERT INTO [MenuItems] ([Id], [Name], [Price], [Emoji], [IconClass], [MealSlot], [IsActive], [DisplayOrder])
    VALUES (1, 'Legacy Menu Item', 0.01, 'Legacy', '', 2, 1, 0);
    SET IDENTITY_INSERT [MenuItems] OFF;
END");

            migrationBuilder.Sql(@"
UPDATE o
SET o.[MenuItemId] = 1
FROM [CanteenOrders] o
WHERE NOT EXISTS (
    SELECT 1
    FROM [MenuItems] m
    WHERE m.[Id] = o.[MenuItemId]
)");

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeDomainName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PolicyArticleId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferralInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobPostingId = table.Column<int>(type: "int", nullable: false),
                    ReferrerEmployeeUsername = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CandidateEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsConsumed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralInvites_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZendeskPolicyArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZendeskArticleId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    HtmlUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    VersionLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZendeskPolicyArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobPostingId = table.Column<int>(type: "int", nullable: false),
                    ReferralInviteId = table.Column<int>(type: "int", nullable: false),
                    CandidateName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CandidateEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalApplications_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalApplications_ReferralInvites_ReferralInviteId",
                        column: x => x.ReferralInviteId,
                        principalTable: "ReferralInvites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_CanteenBatchRunId",
                table: "CanteenOrders",
                column: "CanteenBatchRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_MenuItemId",
                table: "CanteenOrders",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenBatchRuns_RunKey",
                table: "CanteenBatchRuns",
                column: "RunKey",
                unique: true,
                filter: "[RunKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApplications_JobPostingId",
                table: "ExternalApplications",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApplications_ReferralInviteId",
                table: "ExternalApplications",
                column: "ReferralInviteId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalJobApplications_JobPostingId",
                table: "InternalJobApplications",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_RunKey",
                table: "PayrollRuns",
                column: "RunKey",
                unique: true,
                filter: "[RunKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_EmployeeDomainName_PolicyArticleId_PolicyVersion",
                table: "PolicyAcknowledgements",
                columns: new[] { "EmployeeDomainName", "PolicyArticleId", "PolicyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralInvites_JobPostingId",
                table: "ReferralInvites",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralInvites_Token",
                table: "ReferralInvites",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CanteenOrders_CanteenBatchRuns_CanteenBatchRunId",
                table: "CanteenOrders",
                column: "CanteenBatchRunId",
                principalTable: "CanteenBatchRuns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CanteenOrders_MenuItems_MenuItemId",
                table: "CanteenOrders",
                column: "MenuItemId",
                principalTable: "MenuItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanteenOrders_CanteenBatchRuns_CanteenBatchRunId",
                table: "CanteenOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_CanteenOrders_MenuItems_MenuItemId",
                table: "CanteenOrders");

            migrationBuilder.DropTable(
                name: "CanteenBatchRuns");

            migrationBuilder.DropTable(
                name: "ExternalApplications");

            migrationBuilder.DropTable(
                name: "InternalJobApplications");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "PayrollRuns");

            migrationBuilder.DropTable(
                name: "PolicyAcknowledgements");

            migrationBuilder.DropTable(
                name: "ZendeskPolicyArticles");

            migrationBuilder.DropTable(
                name: "ReferralInvites");

            migrationBuilder.DropIndex(
                name: "IX_CanteenOrders_CanteenBatchRunId",
                table: "CanteenOrders");

            migrationBuilder.DropIndex(
                name: "IX_CanteenOrders_MenuItemId",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "AdBackgroundImagePath",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "AdHeadline",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "AdSubHeadline",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "UseVisualAd",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "CanteenBatchRunId",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "MealSlot",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "OrderTimeUtc",
                table: "CanteenOrders");

            migrationBuilder.DropColumn(
                name: "CreatedDateUtc",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "Announcements");

            migrationBuilder.RenameColumn(
                name: "MenuItemId",
                table: "CanteenOrders",
                newName: "EmploymentMonthsAtOrder");

            migrationBuilder.AddColumn<bool>(
                name: "IncludedInPayrollReconciliation",
                table: "CanteenOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OrderDate",
                table: "CanteenOrders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }
    }
}
