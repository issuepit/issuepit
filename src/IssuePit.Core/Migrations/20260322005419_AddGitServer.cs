using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGitServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "git_server_repos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiskPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemporary = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAccessLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_server_repos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_git_server_repos_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_git_server_repos_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "git_server_branch_protections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisallowForcePush = table.Column<bool>(type: "boolean", nullable: false),
                    RequirePullRequest = table.Column<bool>(type: "boolean", nullable: false),
                    AllowAdminBypass = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_server_branch_protections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_git_server_branch_protections_git_server_repos_RepoId",
                        column: x => x.RepoId,
                        principalTable: "git_server_repos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_server_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepoId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_server_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_git_server_permissions_api_keys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "api_keys",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_git_server_permissions_git_server_repos_RepoId",
                        column: x => x.RepoId,
                        principalTable: "git_server_repos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_git_server_permissions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_git_server_branch_protections_RepoId",
                table: "git_server_branch_protections",
                column: "RepoId");

            migrationBuilder.CreateIndex(
                name: "IX_git_server_permissions_ApiKeyId",
                table: "git_server_permissions",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_git_server_permissions_RepoId",
                table: "git_server_permissions",
                column: "RepoId");

            migrationBuilder.CreateIndex(
                name: "IX_git_server_permissions_UserId",
                table: "git_server_permissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_git_server_repos_OrgId",
                table: "git_server_repos",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_git_server_repos_ProjectId",
                table: "git_server_repos",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_server_branch_protections");

            migrationBuilder.DropTable(
                name: "git_server_permissions");

            migrationBuilder.DropTable(
                name: "git_server_repos");
        }
    }
}
