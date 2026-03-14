using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubSyncMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add SyncMode column with default Import (0).
            migrationBuilder.AddColumn<int>(
                name: "SyncMode",
                table: "github_sync_configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Migrate existing AutoCreateOnGitHub = true rows to SyncMode = 2 (CreateOnGitHub).
            migrationBuilder.Sql(
                "UPDATE github_sync_configs SET \"SyncMode\" = 2 WHERE \"AutoCreateOnGitHub\" = true;");

            // Drop the now-superseded boolean column.
            migrationBuilder.DropColumn(
                name: "AutoCreateOnGitHub",
                table: "github_sync_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoCreateOnGitHub",
                table: "github_sync_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Restore AutoCreateOnGitHub for rows that had CreateOnGitHub mode.
            migrationBuilder.Sql(
                "UPDATE github_sync_configs SET \"AutoCreateOnGitHub\" = true WHERE \"SyncMode\" = 2;");

            migrationBuilder.DropColumn(
                name: "SyncMode",
                table: "github_sync_configs");
        }
    }
}
