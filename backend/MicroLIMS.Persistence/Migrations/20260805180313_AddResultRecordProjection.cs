using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResultRecordProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResultRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    SourceTable = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    SubjectName = table.Column<string>(type: "text", nullable: false),
                    SubjectDetail = table.Column<string>(type: "text", nullable: true),
                    BatchNumber = table.Column<string>(type: "text", nullable: true),
                    ControlNumber = table.Column<string>(type: "text", nullable: true),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    TestDisplayName = table.Column<string>(type: "text", nullable: false),
                    ResultKind = table.Column<int>(type: "integer", nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric", nullable: true),
                    ReportedValue = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    IsBelowDetectionLimit = table.Column<bool>(type: "boolean", nullable: false),
                    DetectionLimit = table.Column<decimal>(type: "numeric", nullable: true),
                    AlertLimit = table.Column<string>(type: "text", nullable: true),
                    ActionLimit = table.Column<string>(type: "text", nullable: true),
                    SpecLimit = table.Column<string>(type: "text", nullable: true),
                    ResultLevel = table.Column<int>(type: "integer", nullable: false),
                    ResultEnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultEnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    ResultEnteredByName = table.Column<string>(type: "text", nullable: false),
                    SampleStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedByName = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultRecords_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultRecords_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_Category_ResultEnteredAt",
                table: "ResultRecords",
                columns: new[] { "Category", "ResultEnteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_ResultLevel",
                table: "ResultRecords",
                column: "ResultLevel");

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_SampleId",
                table: "ResultRecords",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_SampleStatus",
                table: "ResultRecords",
                column: "SampleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_SourceTable_SourceId_Round",
                table: "ResultRecords",
                columns: new[] { "SourceTable", "SourceId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_SubjectName",
                table: "ResultRecords",
                column: "SubjectName");

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_TestCode_ResultEnteredAt",
                table: "ResultRecords",
                columns: new[] { "TestCode", "ResultEnteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultRecords_TestOrderId",
                table: "ResultRecords",
                column: "TestOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultRecords");
        }
    }
}
