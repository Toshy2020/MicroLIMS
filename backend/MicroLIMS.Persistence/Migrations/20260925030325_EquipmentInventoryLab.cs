using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentInventoryLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "EquipmentInventories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentInventories_SectionId",
                table: "EquipmentInventories",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentInventories_DocumentSections_SectionId",
                table: "EquipmentInventories",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Back-fill by Code match against the Equipment (lab equipment
            // master) list, which already carries SectionId. Column stays
            // nullable afterward - rows with no matching Code are legacy/
            // unassigned and are picked up by Task 13's UI ("Unassigned",
            // requires a lab on the next edit) rather than by this migration.
            migrationBuilder.Sql(@"
                UPDATE ""EquipmentInventories"" i
                SET ""SectionId"" = e.""SectionId""
                FROM ""Equipment"" e
                WHERE e.""Code"" = i.""Code"" AND i.""SectionId"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentInventories_DocumentSections_SectionId",
                table: "EquipmentInventories");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentInventories_SectionId",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "EquipmentInventories");
        }
    }
}
