using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddOrchestrationGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrchestrationAttempts",
                table: "issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAgentId",
                table: "kanban_columns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_kanban_columns_DefaultAgentId",
                table: "kanban_columns",
                column: "DefaultAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_kanban_columns_agents_DefaultAgentId",
                table: "kanban_columns",
                column: "DefaultAgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_kanban_columns_agents_DefaultAgentId",
                table: "kanban_columns");

            migrationBuilder.DropIndex(
                name: "IX_kanban_columns_DefaultAgentId",
                table: "kanban_columns");

            migrationBuilder.DropColumn(
                name: "DefaultAgentId",
                table: "kanban_columns");

            migrationBuilder.DropColumn(
                name: "OrchestrationAttempts",
                table: "issues");
        }
    }
}
