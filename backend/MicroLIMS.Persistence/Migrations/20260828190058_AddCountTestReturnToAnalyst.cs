using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCountTestReturnToAnalyst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CountTestReadings_TestOrderId",
                table: "CountTestReadings");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CountTestReadings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "TestReturnEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    ReviewerUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAnalystId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReturnEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestReturnEvents_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestReturnEvents_Users_AssignedAnalystId",
                        column: x => x.AssignedAnalystId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestReturnEvents_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CountTestReadings_TestOrderId_StepName_IsActive",
                table: "CountTestReadings",
                columns: new[] { "TestOrderId", "StepName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TestReturnEvents_AssignedAnalystId_ReturnedAt",
                table: "TestReturnEvents",
                columns: new[] { "AssignedAnalystId", "ReturnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestReturnEvents_ReviewerUserId",
                table: "TestReturnEvents",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TestReturnEvents_TestOrderId",
                table: "TestReturnEvents",
                column: "TestOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestReturnEvents");

            migrationBuilder.DropIndex(
                name: "IX_CountTestReadings_TestOrderId_StepName_IsActive",
                table: "CountTestReadings");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CountTestReadings");

            migrationBuilder.CreateIndex(
                name: "IX_CountTestReadings_TestOrderId",
                table: "CountTestReadings",
                column: "TestOrderId");
        }
    }
}
