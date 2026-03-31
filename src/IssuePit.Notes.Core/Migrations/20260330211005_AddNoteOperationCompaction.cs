using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Notes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteOperationCompaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompacted",
                table: "note_operations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCompacted",
                table: "note_operations");
        }
    }
}
