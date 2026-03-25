using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddHetznerServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hetzner_servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HetznerServerId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ipv4Address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Ipv6Address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ServerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActiveRunCount = table.Column<int>(type: "integer", nullable: false),
                    TotalRunCount = table.Column<int>(type: "integer", nullable: false),
                    CpuLoadPercent = table.Column<double>(type: "double precision", nullable: true),
                    RamUsedBytes = table.Column<long>(type: "bigint", nullable: true),
                    RamTotalBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastIdleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetricsLastCollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SetupTimeSeconds = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hetzner_servers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hetzner_servers_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_hetzner_servers_OrgId",
                table: "hetzner_servers",
                column: "OrgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hetzner_servers");
        }
    }
}
