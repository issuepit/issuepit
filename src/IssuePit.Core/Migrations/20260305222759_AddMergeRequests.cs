using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "merge_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SourceBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AutoMergeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastKnownSourceSha = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastCiCdRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MergeCommitSha = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merge_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_merge_requests_cicd_runs_LastCiCdRunId",
                        column: x => x.LastCiCdRunId,
                        principalTable: "cicd_runs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_merge_requests_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_merge_requests_LastCiCdRunId",
                table: "merge_requests",
                column: "LastCiCdRunId");

            migrationBuilder.CreateIndex(
                name: "IX_merge_requests_ProjectId",
                table: "merge_requests",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "merge_requests");
        }
    }
}
