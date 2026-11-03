using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotApplicableTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotApplicableTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TestCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    Criticality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImmediateAction = table.Column<string>(type: "text", nullable: false),
                    LongTermAction = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HeadSignatureId = table.Column<int>(type: "integer", nullable: true),
                    FinalSignatureId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotApplicableTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotApplicableTests_ElectronicSignatures_FinalSignatureId",
                        column: x => x.FinalSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotApplicableTests_ElectronicSignatures_HeadSignatureId",
                        column: x => x.HeadSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotApplicableTests_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotApplicableTests_FinalSignatureId",
                table: "NotApplicableTests",
                column: "FinalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_NotApplicableTests_HeadSignatureId",
                table: "NotApplicableTests",
                column: "HeadSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_NotApplicableTests_ItemId_Grade_TestCode",
                table: "NotApplicableTests",
                columns: new[] { "ItemId", "Grade", "TestCode" },
                unique: true,
                filter: "\"Status\" != 3")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.Sql(@"
                UPDATE ""Permissions""
                SET ""IsEnforced"" = TRUE
                WHERE ""Code"" IN ('tests.not-applicable.manage', 'tests.not-applicable.final-approve');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Permissions""
                SET ""IsEnforced"" = FALSE
                WHERE ""Code"" IN ('tests.not-applicable.manage', 'tests.not-applicable.final-approve');");

            migrationBuilder.DropTable(
                name: "NotApplicableTests");
        }
    }
}
