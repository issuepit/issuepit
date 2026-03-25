using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AgentSessionNullableIssueAndProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add ProjectId as nullable (will be filled before making it required)
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "agent_sessions",
                type: "uuid",
                nullable: true);

            // Step 2: Populate ProjectId from the linked issue's ProjectId for all existing rows
            migrationBuilder.Sql(@"
                UPDATE agent_sessions
                SET ""ProjectId"" = i.""ProjectId""
                FROM issues i
                WHERE agent_sessions.""IssueId"" = i.""Id""
            ");

            // Step 3: For any orphaned rows without a matched issue, use Guid.Empty as fallback
            migrationBuilder.Sql(@"
                UPDATE agent_sessions SET ""ProjectId"" = '00000000-0000-0000-0000-000000000000'
                WHERE ""ProjectId"" IS NULL
            ");

            // Step 4: Make ProjectId non-nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "agent_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Step 5: Make IssueId nullable (drop the old non-nullable FK, re-add as nullable)
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_issues_IssueId",
                table: "agent_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "IssueId",
                table: "agent_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_issues_IssueId",
                table: "agent_sessions",
                column: "IssueId",
                principalTable: "issues",
                principalColumn: "Id");

            // Step 6: Add FK and index for ProjectId
            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_projects_ProjectId",
                table: "agent_sessions",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_ProjectId",
                table: "agent_sessions",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_projects_ProjectId",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "IX_agent_sessions_ProjectId",
                table: "agent_sessions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_issues_IssueId",
                table: "agent_sessions");

            // Restore IssueId as non-nullable — requires data to be valid
            migrationBuilder.AlterColumn<Guid>(
                name: "IssueId",
                table: "agent_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_issues_IssueId",
                table: "agent_sessions",
                column: "IssueId",
                principalTable: "issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
