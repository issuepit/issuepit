using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentOrchestrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireCodeReview",
                table: "kanban_transitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireGreenCiCd",
                table: "kanban_transitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePlanComment",
                table: "kanban_transitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireSubIssuesDone",
                table: "kanban_transitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireTasksDone",
                table: "kanban_transitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideFromAgents",
                table: "issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreventAgentMove",
                table: "issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireCodeReview",
                table: "kanban_transitions");

            migrationBuilder.DropColumn(
                name: "RequireGreenCiCd",
                table: "kanban_transitions");

            migrationBuilder.DropColumn(
                name: "RequirePlanComment",
                table: "kanban_transitions");

            migrationBuilder.DropColumn(
                name: "RequireSubIssuesDone",
                table: "kanban_transitions");

            migrationBuilder.DropColumn(
                name: "RequireTasksDone",
                table: "kanban_transitions");

            migrationBuilder.DropColumn(
                name: "HideFromAgents",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "PreventAgentMove",
                table: "issues");
        }
    }
}
