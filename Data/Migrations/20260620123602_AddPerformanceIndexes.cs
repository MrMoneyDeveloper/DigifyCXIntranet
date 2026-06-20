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
            migrationBuilder.CreateTable(
                name: "ForgotPasswordRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ZendeskTicketId = table.Column<long>(type: "bigint", nullable: true),
                    ZendeskTicketUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgotPasswordRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZendeskPolicyArticles_SectionName_Title",
                table: "ZendeskPolicyArticles",
                columns: new[] { "SectionName", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_IsDeleted_IsActive_MealSlot_DisplayOrder",
                table: "MenuItems",
                columns: new[] { "IsDeleted", "IsActive", "MealSlot", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_IsDeleted_IsActive_ClosingDate",
                table: "JobPostings",
                columns: new[] { "IsDeleted", "IsActive", "ClosingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_EmployeeUsername_OrderTimeUtc",
                table: "CanteenOrders",
                columns: new[] { "EmployeeUsername", "OrderTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForgotPasswordRequests");

            migrationBuilder.DropIndex(
                name: "IX_ZendeskPolicyArticles_SectionName_Title",
                table: "ZendeskPolicyArticles");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_IsDeleted_IsActive_MealSlot_DisplayOrder",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_JobPostings_IsDeleted_IsActive_ClosingDate",
                table: "JobPostings");

            migrationBuilder.DropIndex(
                name: "IX_CanteenOrders_EmployeeUsername_OrderTimeUtc",
                table: "CanteenOrders");
        }
    }
}
