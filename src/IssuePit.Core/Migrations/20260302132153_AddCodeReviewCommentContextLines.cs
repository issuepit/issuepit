using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeReviewCommentContextLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextAfter",
                table: "code_review_comments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextBefore",
                table: "code_review_comments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextAfter",
                table: "code_review_comments");

            migrationBuilder.DropColumn(
                name: "ContextBefore",
                table: "code_review_comments");
        }
    }
}
