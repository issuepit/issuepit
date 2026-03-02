using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentProjectOrgAndMcpProjectAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_orgs",
                columns: table => new
                {
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_orgs", x => new { x.AgentId, x.OrgId });
                    table.ForeignKey(
                        name: "FK_agent_orgs_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_orgs_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_projects",
                columns: table => new
                {
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_projects", x => new { x.AgentId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_agent_projects_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_projects_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mcp_server_project_agents",
                columns: table => new
                {
                    McpServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_server_project_agents", x => new { x.McpServerId, x.ProjectId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_mcp_server_project_agents_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mcp_server_project_agents_mcp_servers_McpServerId",
                        column: x => x.McpServerId,
                        principalTable: "mcp_servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mcp_server_project_agents_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_orgs_OrgId",
                table: "agent_orgs",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_projects_ProjectId",
                table: "agent_projects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_server_project_agents_AgentId",
                table: "mcp_server_project_agents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_server_project_agents_ProjectId",
                table: "mcp_server_project_agents",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_orgs");

            migrationBuilder.DropTable(
                name: "agent_projects");

            migrationBuilder.DropTable(
                name: "mcp_server_project_agents");
        }
    }
}
