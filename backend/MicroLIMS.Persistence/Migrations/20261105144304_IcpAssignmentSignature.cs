using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IcpAssignmentSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignmentSignatureId",
                table: "IcpRunSamples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcpRunSamples_AssignmentSignatureId",
                table: "IcpRunSamples",
                column: "AssignmentSignatureId");

            migrationBuilder.AddForeignKey(
                name: "FK_IcpRunSamples_ElectronicSignatures_AssignmentSignatureId",
                table: "IcpRunSamples",
                column: "AssignmentSignatureId",
                principalTable: "ElectronicSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IcpRunSamples_ElectronicSignatures_AssignmentSignatureId",
                table: "IcpRunSamples");

            migrationBuilder.DropIndex(
                name: "IX_IcpRunSamples_AssignmentSignatureId",
                table: "IcpRunSamples");

            migrationBuilder.DropColumn(
                name: "AssignmentSignatureId",
                table: "IcpRunSamples");
        }
    }
}
