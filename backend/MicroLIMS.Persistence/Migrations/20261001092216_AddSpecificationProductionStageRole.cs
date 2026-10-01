using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecificationProductionStageRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName",
                table: "Specifications");

            migrationBuilder.AddColumn<int>(
                name: "ProductionStageRole",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ProductionStageRole",
                table: "Specifications");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName" },
                unique: true);
        }
    }
}
