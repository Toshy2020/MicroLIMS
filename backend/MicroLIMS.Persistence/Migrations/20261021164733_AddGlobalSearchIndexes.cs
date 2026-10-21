using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Samples_BatchNumber",
                table: "Samples",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ControlNumber",
                table: "Samples",
                column: "ControlNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_BatchNumber",
                table: "Materials",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MaterialName",
                table: "Materials",
                column: "MaterialName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Samples_BatchNumber",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_ControlNumber",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Materials_BatchNumber",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_MaterialName",
                table: "Materials");
        }
    }
}
