using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentAgentSectionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InstrumentAgents_SectionId",
                table: "InstrumentAgents",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstrumentAgents_DocumentSections_SectionId",
                table: "InstrumentAgents",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstrumentAgents_DocumentSections_SectionId",
                table: "InstrumentAgents");

            migrationBuilder.DropIndex(
                name: "IX_InstrumentAgents_SectionId",
                table: "InstrumentAgents");
        }
    }
}
