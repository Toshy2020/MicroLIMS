using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeepInvalidatedResultsOnReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvalidatedAt",
                table: "Results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvalidatedByUserId",
                table: "Results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvalidationReason",
                table: "Results",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvalidatedAt",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "InvalidatedByUserId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "InvalidationReason",
                table: "Results");
        }
    }
}
