using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueLastActivityAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "issues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Back-fill: set LastActivityAt to the latest of UpdatedAt and the latest comment on the issue.
            migrationBuilder.Sql(@"
                UPDATE issues
                SET ""LastActivityAt"" = GREATEST(
                    ""UpdatedAt"",
                    COALESCE((SELECT MAX(c.""CreatedAt"") FROM issue_comments c WHERE c.""IssueId"" = issues.""Id""), ""UpdatedAt"")
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "issues");
        }
    }
}
