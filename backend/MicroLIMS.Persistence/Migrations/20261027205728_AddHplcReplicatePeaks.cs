using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHplcReplicatePeaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HplcReplicatePeaks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcSampleReplicateId = table.Column<int>(type: "integer", nullable: false),
                    HplcMethodImpurityId = table.Column<int>(type: "integer", nullable: true),
                    RelativeRetention = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    Area = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcReplicatePeaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcReplicatePeaks_HplcMethodImpurities_HplcMethodImpurityId",
                        column: x => x.HplcMethodImpurityId,
                        principalTable: "HplcMethodImpurities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcReplicatePeaks_HplcSampleReplicates_HplcSampleReplicate~",
                        column: x => x.HplcSampleReplicateId,
                        principalTable: "HplcSampleReplicates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HplcReplicatePeaks_HplcMethodImpurityId",
                table: "HplcReplicatePeaks",
                column: "HplcMethodImpurityId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcReplicatePeaks_HplcSampleReplicateId",
                table: "HplcReplicatePeaks",
                column: "HplcSampleReplicateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HplcReplicatePeaks");
        }
    }
}
