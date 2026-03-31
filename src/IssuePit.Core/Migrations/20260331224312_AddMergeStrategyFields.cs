using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeStrategyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeleteSourceBranchOnMerge",
                table: "merge_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MergeStrategy",
                table: "merge_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireCiToPass",
                table: "merge_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleteSourceBranchOnMerge",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "MergeStrategy",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "RequireCiToPass",
                table: "merge_requests");
        }
    }
}
