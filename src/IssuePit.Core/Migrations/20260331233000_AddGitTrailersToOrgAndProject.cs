using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGitTrailersToOrgAndProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AddGitTrailers",
                table: "projects",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AddGitTrailers",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddGitTrailers",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "AddGitTrailers",
                table: "organizations");
        }
    }
}
