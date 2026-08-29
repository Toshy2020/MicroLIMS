using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimumDurationOverrideToIncubation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MinimumDurationOverriddenAt",
                table: "Incubations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumDurationOverriddenByUserId",
                table: "Incubations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumDurationOverriddenAt",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "MinimumDurationOverriddenByUserId",
                table: "Incubations");
        }
    }
}
