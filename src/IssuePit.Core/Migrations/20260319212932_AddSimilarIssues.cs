using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSimilarIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "similar_issue_pairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimilarIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<float>(type: "real", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_similar_issue_pairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_similar_issue_pairs_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_similar_issue_pairs_issues_SimilarIssueId",
                        column: x => x.SimilarIssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "similar_issue_runs",
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
                    table.PrimaryKey("PK_similar_issue_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_similar_issue_runs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "similar_issue_run_logs",
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
                    table.PrimaryKey("PK_similar_issue_run_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_similar_issue_run_logs_similar_issue_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "similar_issue_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_similar_issue_pairs_IssueId",
                table: "similar_issue_pairs",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_similar_issue_pairs_SimilarIssueId",
                table: "similar_issue_pairs",
                column: "SimilarIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_similar_issue_run_logs_RunId",
                table: "similar_issue_run_logs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_similar_issue_runs_ProjectId",
                table: "similar_issue_runs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "similar_issue_pairs");

            migrationBuilder.DropTable(
                name: "similar_issue_run_logs");

            migrationBuilder.DropTable(
                name: "similar_issue_runs");
        }
    }
}
