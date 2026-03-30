using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Notes.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "note_workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StorageEngine = table.Column<int>(type: "integer", nullable: false),
                    LinkedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    GitRepositoryUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GitBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notes_note_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "note_workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "note_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkType = table.Column<int>(type: "integer", nullable: false),
                    TargetNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawLinkText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_note_links_notes_SourceNoteId",
                        column: x => x.SourceNoteId,
                        principalTable: "notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_note_links_notes_TargetNoteId",
                        column: x => x.TargetNoteId,
                        principalTable: "notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_note_links_SourceNoteId",
                table: "note_links",
                column: "SourceNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_note_links_TargetNoteId",
                table: "note_links",
                column: "TargetNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_note_workspaces_TenantId",
                table: "note_workspaces",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notes_TenantId_WorkspaceId",
                table: "notes",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_notes_WorkspaceId",
                table: "notes",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "note_links");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "note_workspaces");
        }
    }
}
