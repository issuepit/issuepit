using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramPairingAndChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RateLimitCount",
                table: "telegram_bots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitWindowMinutes",
                table: "telegram_bots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SilentMode",
                table: "telegram_bots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "telegram_chats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramChatId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TelegramUsername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EncryptedBotToken = table.Column<string>(type: "text", nullable: false),
                    Events = table.Column<int>(type: "integer", nullable: false),
                    IsSilent = table.Column<bool>(type: "boolean", nullable: false),
                    DigestInterval = table.Column<int>(type: "integer", nullable: false),
                    RateLimitCount = table.Column<int>(type: "integer", nullable: false),
                    RateLimitWindowMinutes = table.Column<int>(type: "integer", nullable: false),
                    SilentMode = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telegram_chats_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_telegram_chats_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_telegram_chats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "telegram_pairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TelegramChatId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TelegramUsername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsRedeemed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_pairings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_chats_OrgId",
                table: "telegram_chats",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_chats_ProjectId",
                table: "telegram_chats",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_chats_UserId",
                table: "telegram_chats",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_chats");

            migrationBuilder.DropTable(
                name: "telegram_pairings");

            migrationBuilder.DropColumn(
                name: "RateLimitCount",
                table: "telegram_bots");

            migrationBuilder.DropColumn(
                name: "RateLimitWindowMinutes",
                table: "telegram_bots");

            migrationBuilder.DropColumn(
                name: "SilentMode",
                table: "telegram_bots");
        }
    }
}
