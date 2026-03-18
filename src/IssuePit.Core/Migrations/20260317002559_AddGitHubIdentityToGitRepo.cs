using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubIdentityToGitRepo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GitHubIdentityId",
                table: "git_repositories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_git_repositories_GitHubIdentityId",
                table: "git_repositories",
                column: "GitHubIdentityId");

            migrationBuilder.AddForeignKey(
                name: "FK_git_repositories_github_identities_GitHubIdentityId",
                table: "git_repositories",
                column: "GitHubIdentityId",
                principalTable: "github_identities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_git_repositories_github_identities_GitHubIdentityId",
                table: "git_repositories");

            migrationBuilder.DropIndex(
                name: "IX_git_repositories_GitHubIdentityId",
                table: "git_repositories");

            migrationBuilder.DropColumn(
                name: "GitHubIdentityId",
                table: "git_repositories");
        }
    }
}
