using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jira_sync_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    JiraBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    JiraProjectKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JiraEmail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerMode = table.Column<int>(type: "integer", nullable: false),
                    OnlyImportWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    ImportComments = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jira_sync_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jira_sync_configs_api_keys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "api_keys",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_jira_sync_configs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jira_sync_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jira_sync_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jira_sync_runs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jira_sync_run_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jira_sync_run_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_jira_sync_run_logs_jira_sync_runs_SyncRunId",
                        column: x => x.SyncRunId,
                        principalTable: "jira_sync_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_jira_sync_configs_ApiKeyId",
                table: "jira_sync_configs",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_jira_sync_configs_ProjectId",
                table: "jira_sync_configs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_jira_sync_run_logs_SyncRunId",
                table: "jira_sync_run_logs",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_jira_sync_runs_ProjectId",
                table: "jira_sync_runs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jira_sync_configs");

            migrationBuilder.DropTable(
                name: "jira_sync_run_logs");

            migrationBuilder.DropTable(
                name: "jira_sync_runs");
        }
    }
}
