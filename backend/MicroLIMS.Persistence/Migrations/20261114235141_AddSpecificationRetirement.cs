using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecificationRetirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                table: "Specifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "Specifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetiredByUserId",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole", "Grade", "RelatedSubstanceRow", "HplcMethodImpurityId" },
                unique: true,
                filter: "\"IsRetired\" = false")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "RetiredByUserId",
                table: "Specifications");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName_ProductionStag~",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName", "ProductionStageRole", "Grade", "RelatedSubstanceRow", "HplcMethodImpurityId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
