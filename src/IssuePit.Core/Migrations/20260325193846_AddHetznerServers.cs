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
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ipv6Address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ipv4Address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastJobEndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActiveJobCount = table.Column<int>(type: "integer", nullable: false),
                    TotalJobCount = table.Column<int>(type: "integer", nullable: false),
                    CpuLoadPercent = table.Column<double>(type: "double precision", nullable: true),
                    RamUsedMb = table.Column<int>(type: "integer", nullable: true),
                    RamTotalMb = table.Column<int>(type: "integer", nullable: true),
                    SetupDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    SshPrivateKey = table.Column<string>(type: "text", nullable: true),
                    HetznerSshKeyId = table.Column<long>(type: "bigint", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hetzner_servers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hetzner_servers_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
