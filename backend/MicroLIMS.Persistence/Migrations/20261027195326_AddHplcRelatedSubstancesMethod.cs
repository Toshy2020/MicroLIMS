using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHplcRelatedSubstancesMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DisregardLimitPercent",
                table: "HplcMethods",
                type: "numeric(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferenceSolutionPercent",
                table: "HplcMethods",
                type: "numeric(10,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HplcMethodImpurities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RelativeRetention = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    CorrectionFactor = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcMethodImpurities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcMethodImpurities_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodImpurities_HplcMethodId_Name",
                table: "HplcMethodImpurities",
                columns: new[] { "HplcMethodId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HplcMethodImpurities");

            migrationBuilder.DropColumn(
                name: "DisregardLimitPercent",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "ReferenceSolutionPercent",
                table: "HplcMethods");
        }
    }
}
