using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginSampleIdToSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginSampleId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OriginSampleId",
                table: "Samples",
                column: "OriginSampleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_Samples_OriginSampleId",
                table: "Samples",
                column: "OriginSampleId",
                principalTable: "Samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_Samples_OriginSampleId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_OriginSampleId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "OriginSampleId",
                table: "Samples");
        }
    }
}
