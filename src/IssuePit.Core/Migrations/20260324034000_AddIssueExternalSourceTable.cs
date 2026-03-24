using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueExternalSourceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSource",
                table: "issues");

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalSourceId",
                table: "issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "issue_external_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_external_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_issue_external_sources_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issues_ExternalSourceId",
                table: "issues",
                column: "ExternalSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_issue_external_sources_ProjectId",
                table: "issue_external_sources",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_issues_issue_external_sources_ExternalSourceId",
                table: "issues",
                column: "ExternalSourceId",
                principalTable: "issue_external_sources",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issues_issue_external_sources_ExternalSourceId",
                table: "issues");

            migrationBuilder.DropTable(
                name: "issue_external_sources");

            migrationBuilder.DropIndex(
                name: "IX_issues_ExternalSourceId",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "ExternalSourceId",
                table: "issues");

            migrationBuilder.AddColumn<string>(
                name: "ExternalSource",
                table: "issues",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
