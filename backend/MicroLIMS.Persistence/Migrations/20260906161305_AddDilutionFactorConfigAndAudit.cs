using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    // NOTE: `dotnet ef migrations add` also generated CreateTable calls for
    // DocumentAcknowledgementRecords/DocumentEscalationRecords here - those
    // tables already exist via the previously-applied 20260904160000/
    // 20260904180000 migrations, but the checked-in model snapshot this
    // migration was diffed against had drifted and didn't reflect them yet.
    // That scaffolding was removed so this migration only adds the Dilution
    // Factor columns; the regenerated snapshot now correctly reflects those
    // tables' existence, which is all that was actually missing.
    public partial class AddDilutionFactorConfigAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DilutionFactor",
                table: "Specifications",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConfiguredDilutionFactor",
                table: "CountTestReadings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DilutionFactorOverridden",
                table: "CountTestReadings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DilutionFactorOverrideNote",
                table: "CountTestReadings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DilutionFactor",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ConfiguredDilutionFactor",
                table: "CountTestReadings");

            migrationBuilder.DropColumn(
                name: "DilutionFactorOverridden",
                table: "CountTestReadings");

            migrationBuilder.DropColumn(
                name: "DilutionFactorOverrideNote",
                table: "CountTestReadings");
        }
    }
}
