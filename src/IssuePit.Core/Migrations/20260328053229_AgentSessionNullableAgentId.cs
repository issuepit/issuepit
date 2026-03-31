using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AgentSessionNullableAgentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_agents_AgentId",
                table: "agent_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "agent_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_agents_AgentId",
                table: "agent_sessions",
                column: "AgentId",
                principalTable: "agents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_agents_AgentId",
                table: "agent_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "agent_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_agents_AgentId",
                table: "agent_sessions",
                column: "AgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
