using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiSettingsAndAnalystAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalystAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FractionAvailable = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalystAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_AnalystAvailabilities_Fraction", "\"FractionAvailable\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_AnalystAvailabilities_Range", "\"ToDate\" >= \"FromDate\"");
                    table.ForeignKey(
                        name: "FK_AnalystAvailabilities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KpiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    HoursPerAnalystDay = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    WorkingWeekdays = table.Column<int>(type: "integer", nullable: false),
                    LoadHorizonDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiSettings", x => x.Id);
                    table.CheckConstraint("CK_KpiSettings_Horizon", "\"LoadHorizonDays\" BETWEEN 1 AND 30");
                    table.CheckConstraint("CK_KpiSettings_Hours", "\"HoursPerAnalystDay\" > 0 AND \"HoursPerAnalystDay\" <= 24");
                    table.CheckConstraint("CK_KpiSettings_Weekdays", "\"WorkingWeekdays\" BETWEEN 1 AND 127");
                    table.ForeignKey(
                        name: "FK_KpiSettings_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalystAvailabilities_UserId_FromDate",
                table: "AnalystAvailabilities",
                columns: new[] { "UserId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_KpiSettings_SectionId",
                table: "KpiSettings",
                column: "SectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalystAvailabilities");

            migrationBuilder.DropTable(
                name: "KpiSettings");
        }
    }
}
