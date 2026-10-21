using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkSampleLocationsToMonitoringOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonitoringOccurrenceId",
                table: "SampleLocations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_MonitoringOccurrenceId",
                table: "SampleLocations",
                column: "MonitoringOccurrenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_SampleLocations_MonitoringOccurrences_MonitoringOccurrenceId",
                table: "SampleLocations",
                column: "MonitoringOccurrenceId",
                principalTable: "MonitoringOccurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SampleLocations_MonitoringOccurrences_MonitoringOccurrenceId",
                table: "SampleLocations");

            migrationBuilder.DropIndex(
                name: "IX_SampleLocations_MonitoringOccurrenceId",
                table: "SampleLocations");

            migrationBuilder.DropColumn(
                name: "MonitoringOccurrenceId",
                table: "SampleLocations");
        }
    }
}
