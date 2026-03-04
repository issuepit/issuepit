using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddActionCacheAndLocalRepositories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionCachePath",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ActionOfflineMode",
                table: "projects",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalRepositories",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseNewActionCache",
                table: "projects",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionCachePath",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ActionOfflineMode",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocalRepositories",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseNewActionCache",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionCachePath",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ActionOfflineMode",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "LocalRepositories",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UseNewActionCache",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ActionCachePath",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ActionOfflineMode",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "LocalRepositories",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "UseNewActionCache",
                table: "organizations");
        }
    }
}
