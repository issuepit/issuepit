using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledTaskRunLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_detection_run_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_detection_run_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_detection_run_logs_branch_detection_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "branch_detection_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "config_repo_sync_run_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_repo_sync_run_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_config_repo_sync_run_logs_config_repo_sync_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "config_repo_sync_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_detection_run_logs_RunId",
                table: "branch_detection_run_logs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_config_repo_sync_run_logs_RunId",
                table: "config_repo_sync_run_logs",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_detection_run_logs");

            migrationBuilder.DropTable(
                name: "config_repo_sync_run_logs");
        }
    }
}
