using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: PersonalEmail column already exists on AspNetUsers from a prior migration
            // (20260612014505_AddCustomUserFields). Do NOT add it again.

            // Create AccountActivationLogs table
            migrationBuilder.CreateTable(
                name: "AccountActivationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PersonalEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActivatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActivationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountActivationLogs_ActivatedUtc",
                table: "AccountActivationLogs",
                column: "ActivatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccountActivationLogs_UserId",
                table: "AccountActivationLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AccountActivationLogs");
            // PersonalEmail is NOT dropped here since it was added by a prior migration
        }
    }
}
