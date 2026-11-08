using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AfterCleaningPreviousProductAndTechniques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CleaningTechniques",
                table: "Samples",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousProductItemId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_PreviousProductItemId",
                table: "Samples",
                column: "PreviousProductItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_Items_PreviousProductItemId",
                table: "Samples",
                column: "PreviousProductItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_Items_PreviousProductItemId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_PreviousProductItemId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "CleaningTechniques",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "PreviousProductItemId",
                table: "Samples");
        }
    }
}
