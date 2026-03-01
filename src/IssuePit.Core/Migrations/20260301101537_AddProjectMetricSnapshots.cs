using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMetricSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_metric_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenIssues = table.Column<int>(type: "integer", nullable: false),
                    InProgressIssues = table.Column<int>(type: "integer", nullable: false),
                    DoneIssues = table.Column<int>(type: "integer", nullable: false),
                    TotalAgentRuns = table.Column<int>(type: "integer", nullable: false),
                    TotalCiCdRuns = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_metric_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_metric_snapshots_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_metric_snapshots_ProjectId",
                table: "project_metric_snapshots",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_metric_snapshots");
        }
    }
}
