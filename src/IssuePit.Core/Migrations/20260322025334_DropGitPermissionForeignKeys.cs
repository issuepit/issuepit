using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class DropGitPermissionForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_git_server_permissions_api_keys_ApiKeyId",
                table: "git_server_permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_git_server_permissions_users_UserId",
                table: "git_server_permissions");

            migrationBuilder.DropIndex(
                name: "IX_git_server_permissions_ApiKeyId",
                table: "git_server_permissions");

            migrationBuilder.DropIndex(
                name: "IX_git_server_permissions_UserId",
                table: "git_server_permissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_git_server_permissions_ApiKeyId",
                table: "git_server_permissions",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_git_server_permissions_UserId",
                table: "git_server_permissions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_git_server_permissions_api_keys_ApiKeyId",
                table: "git_server_permissions",
                column: "ApiKeyId",
                principalTable: "api_keys",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_git_server_permissions_users_UserId",
                table: "git_server_permissions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");
        }
    }
}
