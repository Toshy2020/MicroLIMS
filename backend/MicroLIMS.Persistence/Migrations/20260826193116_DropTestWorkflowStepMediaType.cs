using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTestWorkflowStepMediaType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiluentTypes_MediaTypes_MediaTypeId",
                table: "DiluentTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_MediaTypes_MediaTypeId",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_TestWorkflowSteps_MediaTypes_MediaTypeId",
                table: "TestWorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowSteps_MediaTypeId",
                table: "TestWorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_Media_MediaTypeId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "MediaTypeId",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "MediaTypeId",
                table: "Media");

            migrationBuilder.RenameColumn(
                name: "MediaTypeId",
                table: "DiluentTypes",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_DiluentTypes_MediaTypeId",
                table: "DiluentTypes",
                newName: "IX_DiluentTypes_MaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiluentTypes_Materials_MaterialId",
                table: "DiluentTypes",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiluentTypes_Materials_MaterialId",
                table: "DiluentTypes");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "DiluentTypes",
                newName: "MediaTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_DiluentTypes_MaterialId",
                table: "DiluentTypes",
                newName: "IX_DiluentTypes_MediaTypeId");

            migrationBuilder.AddColumn<int>(
                name: "MediaTypeId",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaTypeId",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowSteps_MediaTypeId",
                table: "TestWorkflowSteps",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_MediaTypeId",
                table: "Media",
                column: "MediaTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiluentTypes_MediaTypes_MediaTypeId",
                table: "DiluentTypes",
                column: "MediaTypeId",
                principalTable: "MediaTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Media_MediaTypes_MediaTypeId",
                table: "Media",
                column: "MediaTypeId",
                principalTable: "MediaTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestWorkflowSteps_MediaTypes_MediaTypeId",
                table: "TestWorkflowSteps",
                column: "MediaTypeId",
                principalTable: "MediaTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
