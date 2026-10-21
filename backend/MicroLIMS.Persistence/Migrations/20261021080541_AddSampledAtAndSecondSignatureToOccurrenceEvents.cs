using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSampledAtAndSecondSignatureToOccurrenceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SampledAt",
                table: "Samples",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SecondElectronicSignatureId",
                table: "MonitoringOccurrenceEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrenceEvents_SecondElectronicSignatureId",
                table: "MonitoringOccurrenceEvents",
                column: "SecondElectronicSignatureId");

            migrationBuilder.AddForeignKey(
                name: "FK_MonitoringOccurrenceEvents_ElectronicSignatures_SecondElect~",
                table: "MonitoringOccurrenceEvents",
                column: "SecondElectronicSignatureId",
                principalTable: "ElectronicSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonitoringOccurrenceEvents_ElectronicSignatures_SecondElect~",
                table: "MonitoringOccurrenceEvents");

            migrationBuilder.DropIndex(
                name: "IX_MonitoringOccurrenceEvents_SecondElectronicSignatureId",
                table: "MonitoringOccurrenceEvents");

            migrationBuilder.DropColumn(
                name: "SampledAt",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "SecondElectronicSignatureId",
                table: "MonitoringOccurrenceEvents");
        }
    }
}
