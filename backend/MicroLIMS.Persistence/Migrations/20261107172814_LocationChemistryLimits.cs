using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocationChemistryLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedResultText",
                table: "SamplingConfigurations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LimitType",
                table: "SamplingConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LowerInclusive",
                table: "SamplingConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowerLimit",
                table: "SamplingConfigurations",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpperInclusive",
                table: "SamplingConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UpperLimit",
                table: "SamplingConfigurations",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedResultText",
                table: "MachinePartConfigurations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LimitType",
                table: "MachinePartConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LowerInclusive",
                table: "MachinePartConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowerLimit",
                table: "MachinePartConfigurations",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpperInclusive",
                table: "MachinePartConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UpperLimit",
                table: "MachinePartConfigurations",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedResultText",
                table: "SamplingConfigurations");

            migrationBuilder.DropColumn(
                name: "LimitType",
                table: "SamplingConfigurations");

            migrationBuilder.DropColumn(
                name: "LowerInclusive",
                table: "SamplingConfigurations");

            migrationBuilder.DropColumn(
                name: "LowerLimit",
                table: "SamplingConfigurations");

            migrationBuilder.DropColumn(
                name: "UpperInclusive",
                table: "SamplingConfigurations");

            migrationBuilder.DropColumn(
                name: "UpperLimit",
                table: "SamplingConfigurations");

            migrationBuilder.DropColumn(
                name: "ExpectedResultText",
                table: "MachinePartConfigurations");

            migrationBuilder.DropColumn(
                name: "LimitType",
                table: "MachinePartConfigurations");

            migrationBuilder.DropColumn(
                name: "LowerInclusive",
                table: "MachinePartConfigurations");

            migrationBuilder.DropColumn(
                name: "LowerLimit",
                table: "MachinePartConfigurations");

            migrationBuilder.DropColumn(
                name: "UpperInclusive",
                table: "MachinePartConfigurations");

            migrationBuilder.DropColumn(
                name: "UpperLimit",
                table: "MachinePartConfigurations");
        }
    }
}
