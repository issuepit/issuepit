using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_issue_links_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_issue_links_issues_TargetIssueId",
                        column: x => x.TargetIssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_IssueId",
                table: "issue_links",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_TargetIssueId",
                table: "issue_links",
                column: "TargetIssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_links");
        }
    }
}
