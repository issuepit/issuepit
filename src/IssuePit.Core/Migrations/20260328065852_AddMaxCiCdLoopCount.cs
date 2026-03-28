using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxCiCdLoopCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxCiCdLoopCount",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCiCdLoopCount",
                table: "organizations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxCiCdLoopCount",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "MaxCiCdLoopCount",
                table: "organizations");
        }
    }
}
