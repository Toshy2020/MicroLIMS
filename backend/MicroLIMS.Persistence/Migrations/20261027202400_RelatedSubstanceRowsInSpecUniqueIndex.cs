using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelatedSubstanceRowsInSpecUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole", "Grade", "RelatedSubstanceRow", "HplcMethodImpurityId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole", "Grade" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
