using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    GitRepoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GitSubDir = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GitAuthUsername = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GitAuthToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SyncStatus = table.Column<int>(type: "integer", nullable: false),
                    SyncMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_skills_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_skills_OrgId",
                table: "skills",
                column: "OrgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
