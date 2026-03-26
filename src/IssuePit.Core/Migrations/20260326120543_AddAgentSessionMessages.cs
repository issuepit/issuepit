using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSessionMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_session_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModelOverride = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgentIdOverride = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_session_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_session_messages_agent_sessions_AgentSessionId",
                        column: x => x.AgentSessionId,
                        principalTable: "agent_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_session_messages_agents_AgentIdOverride",
                        column: x => x.AgentIdOverride,
                        principalTable: "agents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_session_messages_AgentIdOverride",
                table: "agent_session_messages",
                column: "AgentIdOverride");

            migrationBuilder.CreateIndex(
                name: "IX_agent_session_messages_AgentSessionId",
                table: "agent_session_messages",
                column: "AgentSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_session_messages");
        }
    }
}
