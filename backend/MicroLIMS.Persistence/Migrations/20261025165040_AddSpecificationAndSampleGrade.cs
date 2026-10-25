using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecificationAndSampleGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Specifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Samples",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole", "Grade" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Samples");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
