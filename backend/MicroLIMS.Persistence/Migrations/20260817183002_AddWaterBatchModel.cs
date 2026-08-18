using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterBatchModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WaterDepartmentId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SamplingConfigurationId",
                table: "SampleLocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaterSamplingPointId",
                table: "SampleLocations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_WaterDepartmentId",
                table: "Samples",
                column: "WaterDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_SamplingConfigurationId",
                table: "SampleLocations",
                column: "SamplingConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_TestOrderId_WaterSamplingPointId",
                table: "SampleLocations",
                columns: new[] { "TestOrderId", "WaterSamplingPointId" },
                unique: true,
                filter: "\"WaterSamplingPointId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_WaterSamplingPointId",
                table: "SampleLocations",
                column: "WaterSamplingPointId");

            migrationBuilder.AddForeignKey(
                name: "FK_SampleLocations_SamplingConfigurations_SamplingConfiguratio~",
                table: "SampleLocations",
                column: "SamplingConfigurationId",
                principalTable: "SamplingConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SampleLocations_WaterSamplingPoints_WaterSamplingPointId",
                table: "SampleLocations",
                column: "WaterSamplingPointId",
                principalTable: "WaterSamplingPoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_WaterDepartments_WaterDepartmentId",
                table: "Samples",
                column: "WaterDepartmentId",
                principalTable: "WaterDepartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SampleLocations_SamplingConfigurations_SamplingConfiguratio~",
                table: "SampleLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_SampleLocations_WaterSamplingPoints_WaterSamplingPointId",
                table: "SampleLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Samples_WaterDepartments_WaterDepartmentId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_WaterDepartmentId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_SampleLocations_SamplingConfigurationId",
                table: "SampleLocations");

            migrationBuilder.DropIndex(
                name: "IX_SampleLocations_TestOrderId_WaterSamplingPointId",
                table: "SampleLocations");

            migrationBuilder.DropIndex(
                name: "IX_SampleLocations_WaterSamplingPointId",
                table: "SampleLocations");

            migrationBuilder.DropColumn(
                name: "WaterDepartmentId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "SamplingConfigurationId",
                table: "SampleLocations");

            migrationBuilder.DropColumn(
                name: "WaterSamplingPointId",
                table: "SampleLocations");
        }
    }
}
