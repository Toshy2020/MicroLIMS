using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabSeparationCloseTesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelledAtStage",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledAtStep",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseReason",
                table: "SampleSectionSignoffs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CloseSignatureId",
                table: "SampleSectionSignoffs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "SampleSectionSignoffs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedByUserId",
                table: "SampleSectionSignoffs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_ClosedByUserId",
                table: "SampleSectionSignoffs",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_CloseSignatureId",
                table: "SampleSectionSignoffs",
                column: "CloseSignatureId");

            migrationBuilder.AddForeignKey(
                name: "FK_SampleSectionSignoffs_ElectronicSignatures_CloseSignatureId",
                table: "SampleSectionSignoffs",
                column: "CloseSignatureId",
                principalTable: "ElectronicSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SampleSectionSignoffs_Users_ClosedByUserId",
                table: "SampleSectionSignoffs",
                column: "ClosedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SampleSectionSignoffs_ElectronicSignatures_CloseSignatureId",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropForeignKey(
                name: "FK_SampleSectionSignoffs_Users_ClosedByUserId",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropIndex(
                name: "IX_SampleSectionSignoffs_ClosedByUserId",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropIndex(
                name: "IX_SampleSectionSignoffs_CloseSignatureId",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropColumn(
                name: "CancelledAtStage",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "CancelledAtStep",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "CloseReason",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropColumn(
                name: "CloseSignatureId",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "SampleSectionSignoffs");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "SampleSectionSignoffs");
        }
    }
}
