using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigifyCXIntranet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivationAndUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the table if it already exists from a previous partial run
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.AccountActivationLogs', 'U') IS NOT NULL
                    DROP TABLE [dbo].[AccountActivationLogs];
            ");

            // Add columns only if they don't already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'CustomRole')
                    ALTER TABLE [AspNetUsers] ADD [CustomRole] nvarchar(max) NOT NULL DEFAULT '';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'DisplayName')
                    ALTER TABLE [AspNetUsers] ADD [DisplayName] nvarchar(max) NOT NULL DEFAULT '';
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'IsFirstTimeLogin')
                    ALTER TABLE [AspNetUsers] ADD [IsFirstTimeLogin] bit NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'PersonalEmail')
                    ALTER TABLE [AspNetUsers] ADD [PersonalEmail] nvarchar(max) NULL;
            ");

            // Create AccountActivationLogs fresh
            migrationBuilder.CreateTable(
                name: "AccountActivationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonalEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActivatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActivationLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountActivationLogs");

            migrationBuilder.DropColumn(
                name: "CustomRole",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsFirstTimeLogin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalEmail",
                table: "AspNetUsers");
        }
    }
}