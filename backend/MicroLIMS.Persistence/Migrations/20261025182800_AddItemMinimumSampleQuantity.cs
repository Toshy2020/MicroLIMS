using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMinimumSampleQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinRetestSampleQuantity",
                table: "Items",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinSampleQuantity",
                table: "Items",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinSampleQuantityUnit",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinRetestSampleQuantity",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MinSampleQuantity",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MinSampleQuantityUnit",
                table: "Items");
        }
    }
}
