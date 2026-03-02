using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeReviewComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "code_review_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false),
                    Sha = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Snippet = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_review_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_review_comments_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_code_review_comments_IssueId",
                table: "code_review_comments",
                column: "IssueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_review_comments");
        }
    }
}
