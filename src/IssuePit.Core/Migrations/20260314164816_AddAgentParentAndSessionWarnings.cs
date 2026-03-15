using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentParentAndSessionWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentAgentId",
                table: "agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Warnings",
                table: "agent_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_ParentAgentId",
                table: "agents",
                column: "ParentAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_agents_agents_ParentAgentId",
                table: "agents",
                column: "ParentAgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agents_agents_ParentAgentId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_ParentAgentId",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "ParentAgentId",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "Warnings",
                table: "agent_sessions");
        }
    }
}
