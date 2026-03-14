using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCiCdRunRetryOfRunId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RetryOfRunId",
                table: "cicd_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cicd_runs_RetryOfRunId",
                table: "cicd_runs",
                column: "RetryOfRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_cicd_runs_cicd_runs_RetryOfRunId",
                table: "cicd_runs",
                column: "RetryOfRunId",
                principalTable: "cicd_runs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cicd_runs_cicd_runs_RetryOfRunId",
                table: "cicd_runs");

            migrationBuilder.DropIndex(
                name: "IX_cicd_runs_RetryOfRunId",
                table: "cicd_runs");

            migrationBuilder.DropColumn(
                name: "RetryOfRunId",
                table: "cicd_runs");
        }
    }
}
