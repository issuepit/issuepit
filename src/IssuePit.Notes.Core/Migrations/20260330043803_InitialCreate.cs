using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Notes.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notebooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    StorageProvider = table.Column<int>(type: "integer", nullable: false),
                    GitRepoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GitBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notebooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "note_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_note_tags_notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotebookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Slug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notes_notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "note_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "note_tag_mappings",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_tag_mappings", x => new { x.NoteId, x.TagId });
                    table.ForeignKey(
                        name: "FK_note_tag_mappings_note_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "note_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_note_tag_mappings_notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_note_tag_mappings_TagId",
                table: "note_tag_mappings",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_note_tags_NotebookId",
                table: "note_tags",
                column: "NotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_notes_NotebookId_Slug",
                table: "notes",
                columns: new[] { "NotebookId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "note_links");

            migrationBuilder.DropTable(
                name: "note_tag_mappings");

            migrationBuilder.DropTable(
                name: "note_tags");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "notebooks");
        }
    }
}
