using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CleaningResidueHplcAndIcp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CleaningLimitPpm",
                table: "TestDefinitionIcpElements",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SpecificationId",
                table: "ParameterResults",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ProductItemId",
                table: "HplcMethods",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SwabAreaCm2",
                table: "HplcMethods",
                type: "numeric(10,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacoLimit",
                table: "HplcMethodAnalytes",
                type: "numeric(12,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MacoUnit",
                table: "HplcMethodAnalytes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HplcMethods_ProductItemId",
                table: "HplcMethods",
                column: "ProductItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_HplcMethods_Items_ProductItemId",
                table: "HplcMethods",
                column: "ProductItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HplcMethods_Items_ProductItemId",
                table: "HplcMethods");

            migrationBuilder.DropIndex(
                name: "IX_HplcMethods_ProductItemId",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "CleaningLimitPpm",
                table: "TestDefinitionIcpElements");

            migrationBuilder.DropColumn(
                name: "ProductItemId",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "SwabAreaCm2",
                table: "HplcMethods");

            migrationBuilder.DropColumn(
                name: "MacoLimit",
                table: "HplcMethodAnalytes");

            migrationBuilder.DropColumn(
                name: "MacoUnit",
                table: "HplcMethodAnalytes");

            migrationBuilder.AlterColumn<int>(
                name: "SpecificationId",
                table: "ParameterResults",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
