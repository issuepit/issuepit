using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubSyncContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SyncContent",
                table: "github_sync_configs",
                type: "integer",
                nullable: false,
                defaultValue: 1); // 1 = GitHubSyncContent.Issues — preserves existing behaviour for old rows
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncContent",
                table: "github_sync_configs");
        }
    }
}
