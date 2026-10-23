using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringDeviations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviationDueDays",
                table: "MonitoringSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpWindowDays",
                table: "MonitoringSettings",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AlterColumn<int>(
                name: "PerformedByUserId",
                table: "MonitoringOccurrenceEvents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "MonitoringDeviations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MonitoringOccurrenceId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FollowUpOccurrenceId = table.Column<int>(type: "integer", nullable: true),
                    Closure = table.Column<int>(type: "integer", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ClosureSignatureId = table.Column<int>(type: "integer", nullable: true),
                    ClosureSecondSignatureId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDeviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringDeviations_ElectronicSignatures_ClosureSecondSign~",
                        column: x => x.ClosureSecondSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringDeviations_ElectronicSignatures_ClosureSignatureId",
                        column: x => x.ClosureSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringDeviations_MonitoringOccurrences_FollowUpOccurren~",
                        column: x => x.FollowUpOccurrenceId,
                        principalTable: "MonitoringOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringDeviations_MonitoringOccurrences_MonitoringOccurr~",
                        column: x => x.MonitoringOccurrenceId,
                        principalTable: "MonitoringOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviations_ClosureSecondSignatureId",
                table: "MonitoringDeviations",
                column: "ClosureSecondSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviations_ClosureSignatureId",
                table: "MonitoringDeviations",
                column: "ClosureSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviations_FollowUpOccurrenceId",
                table: "MonitoringDeviations",
                column: "FollowUpOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviations_MonitoringOccurrenceId_Kind",
                table: "MonitoringDeviations",
                columns: new[] { "MonitoringOccurrenceId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviations_Status",
                table: "MonitoringDeviations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringDeviations");

            migrationBuilder.DropColumn(
                name: "DeviationDueDays",
                table: "MonitoringSettings");

            migrationBuilder.DropColumn(
                name: "FollowUpWindowDays",
                table: "MonitoringSettings");

            migrationBuilder.AlterColumn<int>(
                name: "PerformedByUserId",
                table: "MonitoringOccurrenceEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
