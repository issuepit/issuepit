using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddHetznerServerRuntimeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hetzner_server_runtime_histories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HetznerServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProvisionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalRuntimeSeconds = table.Column<int>(type: "integer", nullable: true),
                    BillableSeconds = table.Column<int>(type: "integer", nullable: true),
                    TotalJobCount = table.Column<int>(type: "integer", nullable: false),
                    SetupDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCostEuroCents = table.Column<int>(type: "integer", nullable: true),
                    PeakCpuLoadPercent = table.Column<double>(type: "double precision", nullable: true),
                    PeakRamUsedMb = table.Column<int>(type: "integer", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hetzner_server_runtime_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hetzner_server_runtime_histories_hetzner_servers_HetznerSer~",
                        column: x => x.HetznerServerId,
                        principalTable: "hetzner_servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hetzner_server_runtime_histories_organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hetzner_server_runtime_histories_HetznerServerId",
                table: "hetzner_server_runtime_histories",
                column: "HetznerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_hetzner_server_runtime_histories_OrgId",
                table: "hetzner_server_runtime_histories",
                column: "OrgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hetzner_server_runtime_histories");
        }
    }
}
