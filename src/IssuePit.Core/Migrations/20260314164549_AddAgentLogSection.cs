using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentLogSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Section",
                table: "agent_session_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionIndex",
                table: "agent_session_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Section",
                table: "agent_session_logs");

            migrationBuilder.DropColumn(
                name: "SectionIndex",
                table: "agent_session_logs");
        }
    }
}
