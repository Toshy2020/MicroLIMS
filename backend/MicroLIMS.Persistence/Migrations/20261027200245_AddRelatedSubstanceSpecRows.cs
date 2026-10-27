using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedSubstanceSpecRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HplcMethodImpurityId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelatedSubstanceRow",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_HplcMethodImpurityId",
                table: "Specifications",
                column: "HplcMethodImpurityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Specifications_HplcMethodImpurities_HplcMethodImpurityId",
                table: "Specifications",
                column: "HplcMethodImpurityId",
                principalTable: "HplcMethodImpurities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Specifications_HplcMethodImpurities_HplcMethodImpurityId",
                table: "Specifications");

            migrationBuilder.DropIndex(
                name: "IX_Specifications_HplcMethodImpurityId",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "HplcMethodImpurityId",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "RelatedSubstanceRow",
                table: "Specifications");
        }
    }
}
