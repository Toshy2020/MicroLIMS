using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SampleLabSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabSectionId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_LabSectionId",
                table: "Samples",
                column: "LabSectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_DocumentSections_LabSectionId",
                table: "Samples",
                column: "LabSectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""LabSectionId"" = (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'MICRO') WHERE ""Category"" IN (3, 5);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_DocumentSections_LabSectionId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_LabSectionId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "LabSectionId",
                table: "Samples");
        }
    }
}
