using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentMasterSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentMasterSections",
                columns: table => new
                {
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentMasterSections", x => new { x.DocumentMasterId, x.SectionId });
                    table.ForeignKey(
                        name: "FK_DocumentMasterSections_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasterSections_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasterSections_SectionId",
                table: "DocumentMasterSections",
                column: "SectionId");

            // Every existing document belongs to (at least) its primary section.
            migrationBuilder.Sql(
                "INSERT INTO \"DocumentMasterSections\" (\"DocumentMasterId\",\"SectionId\") SELECT \"Id\",\"SectionId\" FROM \"DocumentMasters\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentMasterSections");
        }
    }
}
