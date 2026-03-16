using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubPrFieldsToMergeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GitHubPrNumber",
                table: "merge_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubPrUrl",
                table: "merge_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubPrNumber",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "GitHubPrUrl",
                table: "merge_requests");
        }
    }
}
