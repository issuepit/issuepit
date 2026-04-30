using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddActionRemoteTokenAndReplacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionRemoteToken",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionReplacements",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseGitHubTokenForActions",
                table: "projects",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionRemoteToken",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionReplacements",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseGitHubTokenForActions",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionRemoteToken",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ActionReplacements",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UseGitHubTokenForActions",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ActionRemoteToken",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ActionReplacements",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "UseGitHubTokenForActions",
                table: "organizations");
        }
    }
}
