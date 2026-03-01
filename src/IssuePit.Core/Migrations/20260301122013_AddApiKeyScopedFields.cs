using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyScopedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "api_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "api_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "api_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_ProjectId",
                table: "api_keys",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_TeamId",
                table: "api_keys",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId",
                table: "api_keys",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_api_keys_projects_ProjectId",
                table: "api_keys",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_api_keys_teams_TeamId",
                table: "api_keys",
                column: "TeamId",
                principalTable: "teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_api_keys_users_UserId",
                table: "api_keys",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_api_keys_projects_ProjectId",
                table: "api_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_api_keys_teams_TeamId",
                table: "api_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_api_keys_users_UserId",
                table: "api_keys");

            migrationBuilder.DropIndex(
                name: "IX_api_keys_ProjectId",
                table: "api_keys");

            migrationBuilder.DropIndex(
                name: "IX_api_keys_TeamId",
                table: "api_keys");

            migrationBuilder.DropIndex(
                name: "IX_api_keys_UserId",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "api_keys");
        }
    }
}
