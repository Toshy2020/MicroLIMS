using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutsourcedTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OutsourcedAt",
                table: "TestOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutsourcedByUserId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutsourcedToLab",
                table: "TestOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestOrderId",
                table: "SampleDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleDocuments_TestOrderId",
                table: "SampleDocuments",
                column: "TestOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_SampleDocuments_TestOrders_TestOrderId",
                table: "SampleDocuments",
                column: "TestOrderId",
                principalTable: "TestOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("UPDATE \"Permissions\" SET \"IsEnforced\" = TRUE WHERE \"Code\" = 'tests.outsource';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SampleDocuments_TestOrders_TestOrderId",
                table: "SampleDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SampleDocuments_TestOrderId",
                table: "SampleDocuments");

            migrationBuilder.DropColumn(
                name: "OutsourcedAt",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "OutsourcedByUserId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "OutsourcedToLab",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "TestOrderId",
                table: "SampleDocuments");

            migrationBuilder.Sql("UPDATE \"Permissions\" SET \"IsEnforced\" = FALSE WHERE \"Code\" = 'tests.outsource';");
        }
    }
}
