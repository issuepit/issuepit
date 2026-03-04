using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCiCdTestResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cicd_test_suites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CiCdRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TotalTests = table.Column<int>(type: "integer", nullable: false),
                    PassedTests = table.Column<int>(type: "integer", nullable: false),
                    FailedTests = table.Column<int>(type: "integer", nullable: false),
                    SkippedTests = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cicd_test_suites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cicd_test_suites_cicd_runs_CiCdRunId",
                        column: x => x.CiCdRunId,
                        principalTable: "cicd_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cicd_test_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CiCdTestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ClassName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MethodName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StackTrace = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cicd_test_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cicd_test_cases_cicd_test_suites_CiCdTestSuiteId",
                        column: x => x.CiCdTestSuiteId,
                        principalTable: "cicd_test_suites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cicd_test_cases_CiCdTestSuiteId",
                table: "cicd_test_cases",
                column: "CiCdTestSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_cicd_test_suites_CiCdRunId",
                table: "cicd_test_suites",
                column: "CiCdRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cicd_test_cases");

            migrationBuilder.DropTable(
                name: "cicd_test_suites");
        }
    }
}
