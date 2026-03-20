using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCiCdCoverageReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cicd_coverage_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CiCdRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LineRate = table.Column<double>(type: "double precision", nullable: false),
                    BranchRate = table.Column<double>(type: "double precision", nullable: false),
                    LinesCovered = table.Column<int>(type: "integer", nullable: false),
                    LinesValid = table.Column<int>(type: "integer", nullable: false),
                    BranchesCovered = table.Column<int>(type: "integer", nullable: false),
                    BranchesValid = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cicd_coverage_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cicd_coverage_reports_cicd_runs_CiCdRunId",
                        column: x => x.CiCdRunId,
                        principalTable: "cicd_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cicd_coverage_reports_CiCdRunId",
                table: "cicd_coverage_reports",
                column: "CiCdRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cicd_coverage_reports");
        }
    }
}
