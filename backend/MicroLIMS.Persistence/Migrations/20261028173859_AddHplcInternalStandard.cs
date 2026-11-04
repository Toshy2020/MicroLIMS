using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHplcInternalStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InternalStandardResponse",
                table: "HplcSstInjections",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalStandardResponse",
                table: "HplcSampleReplicates",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalStandardName",
                table: "HplcMethods",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalStandardResponse",
                table: "HplcSstInjections");

            migrationBuilder.DropColumn(
                name: "InternalStandardResponse",
                table: "HplcSampleReplicates");

            migrationBuilder.DropColumn(
                name: "InternalStandardName",
                table: "HplcMethods");
        }
    }
}
