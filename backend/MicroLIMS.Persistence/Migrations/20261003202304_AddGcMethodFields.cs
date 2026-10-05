using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGcMethodFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ParticleSizeUm",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ColumnTemperatureC",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)");

            migrationBuilder.AddColumn<int>(
                name: "CarrierGas",
                table: "HplcMethods",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DetectorTemperatureC",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FilmThicknessUm",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HeadspaceEnabled",
                table: "HplcMethods",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HeadspaceEquilibrationMin",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeadspaceEquilibrationTemperatureC",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeadspaceTransferLineTemperatureC",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InletTemperatureC",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultMode",
                table: "HplcMethods",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SampleSolutionVolumeMl",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SplitRatio",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Technique",
                table: "HplcMethods",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "WavelengthNm",
                table: "HplcMethodAnalytes",
                type: "numeric(10,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)");

            migrationBuilder.AddColumn<decimal>(
                name: "StandardConcentrationUgPerMl",
                table: "HplcMethodAnalytes",
                type: "numeric(12,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HplcMethodOvenSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: false),
                    StepNo = table.Column<int>(type: "integer", nullable: false),
                    RateCPerMin = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    TemperatureC = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    HoldMin = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcMethodOvenSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcMethodOvenSteps_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethodOvenSteps_HplcMethodId_StepNo",
                table: "HplcMethodOvenSteps",
                columns: new[] { "HplcMethodId", "StepNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HplcMethodOvenSteps");

            migrationBuilder.DropColumn(
                name: "CarrierGas",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "DetectorTemperatureC",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "FilmThicknessUm",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "HeadspaceEnabled",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "HeadspaceEquilibrationMin",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "HeadspaceEquilibrationTemperatureC",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "HeadspaceTransferLineTemperatureC",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "InletTemperatureC",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "ResultMode",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "SampleSolutionVolumeMl",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "SplitRatio",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "Technique",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "StandardConcentrationUgPerMl",
                table: "HplcMethodAnalytes");

            migrationBuilder.AlterColumn<decimal>(
                name: "ParticleSizeUm",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ColumnTemperatureC",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "WavelengthNm",
                table: "HplcMethodAnalytes",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldNullable: true);
        }
    }
}
