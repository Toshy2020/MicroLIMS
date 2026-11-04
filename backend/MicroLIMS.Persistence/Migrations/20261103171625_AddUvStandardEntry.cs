using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUvStandardEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UvStandardEntryId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_UvStandardEntryId",
                table: "TestDefinitions",
                column: "UvStandardEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_MaterialMasterEntries_UvStandardEntryId",
                table: "TestDefinitions",
                column: "UvStandardEntryId",
                principalTable: "MaterialMasterEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_MaterialMasterEntries_UvStandardEntryId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_UvStandardEntryId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "UvStandardEntryId",
                table: "TestDefinitions");
        }
    }
}
