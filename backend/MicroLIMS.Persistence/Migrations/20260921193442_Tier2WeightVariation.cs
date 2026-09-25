using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tier2WeightVariation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WvCapsuleInnerPercent",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WvCapsuleOuterPercent",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WvCapsuleS1MaxForRetest",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WvCapsuleS1MaxOutside",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WvCapsuleS2ExtraUnits",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WvCapsuleS2MaxOutside",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WvTabletBand1MaxMg",
                table: "TestDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WvTabletBand1Percent",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WvTabletBand2MaxMg",
                table: "TestDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WvTabletBand2Percent",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WvTabletBand3Percent",
                table: "TestDefinitions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WvTabletMaxOutside",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WvUnitCount",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DosageForm",
                table: "Specifications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WvCapsuleInnerPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvCapsuleOuterPercent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvCapsuleS1MaxForRetest",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvCapsuleS1MaxOutside",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvCapsuleS2ExtraUnits",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvCapsuleS2MaxOutside",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvTabletBand1MaxMg",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvTabletBand1Percent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvTabletBand2MaxMg",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvTabletBand2Percent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvTabletBand3Percent",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvTabletMaxOutside",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "WvUnitCount",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "DosageForm",
                table: "Specifications");
        }
    }
}
