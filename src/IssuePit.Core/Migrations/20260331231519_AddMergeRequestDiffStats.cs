using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeRequestDiffStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinesAdded",
                table: "merge_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinesRemoved",
                table: "merge_requests",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinesAdded",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "LinesRemoved",
                table: "merge_requests");
        }
    }
}
