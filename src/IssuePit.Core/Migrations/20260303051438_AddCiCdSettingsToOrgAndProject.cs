using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCiCdSettingsToOrgAndProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActRunnerImage",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActEnv",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActRunnerImage",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActSecrets",
                table: "organizations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActRunnerImage",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ActEnv",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ActRunnerImage",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ActSecrets",
                table: "organizations");
        }
    }
}
