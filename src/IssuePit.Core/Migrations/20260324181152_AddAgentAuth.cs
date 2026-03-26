using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_auths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuthJsonContent = table.Column<string>(type: "text", nullable: false),
                    RestoreOnAgentRuns = table.Column<bool>(type: "boolean", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_auths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_auths_agent_sessions_AgentSessionId",
                        column: x => x.AgentSessionId,
                        principalTable: "agent_sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_agent_auths_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_auths_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_auths_AgentSessionId",
                table: "agent_auths",
                column: "AgentSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_auths_OrgId",
                table: "agent_auths",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_auths_TenantId",
                table: "agent_auths",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_auths");
        }
    }
}
