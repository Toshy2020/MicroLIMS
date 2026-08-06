using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaMaterialLinkAndSimplifiedMediaType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "MediaTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "MediaTypes");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "ExpectedIndicationResults",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MediaTypes_Class",
                table: "MediaTypes",
                column: "Class",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_MaterialId",
                table: "Media",
                column: "MaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Materials_MaterialId",
                table: "Media",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Media_Materials_MaterialId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_MediaTypes_Class",
                table: "MediaTypes");

            migrationBuilder.DropIndex(
                name: "IX_Media_MaterialId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "ExpectedIndicationResults");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "MediaTypes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "MediaTypes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
