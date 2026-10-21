using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonitoringPlanFrequencyAndLaf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FrequencyAnchorMonth",
                table: "WaterSamplingPoints",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyInterval",
                table: "WaterSamplingPoints",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyType",
                table: "WaterSamplingPoints",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SamplingWeekday",
                table: "WaterSamplingPoints",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyAnchorMonth",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyInterval",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyType",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentRoomId",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SamplingWeekday",
                table: "Rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyAnchorMonth",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyInterval",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyType",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SamplingWeekday",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabCalendarDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabCalendarDates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    LabWeekStartDay = table.Column<int>(type: "integer", nullable: false),
                    WorkingWeekdays = table.Column<int>(type: "integer", nullable: false),
                    WaterEarlyToleranceDays = table.Column<int>(type: "integer", nullable: false),
                    WaterLateToleranceDays = table.Column<int>(type: "integer", nullable: false),
                    EmEarlyToleranceDays = table.Column<int>(type: "integer", nullable: false),
                    EmLateToleranceDays = table.Column<int>(type: "integer", nullable: false),
                    AcEarlyToleranceDays = table.Column<int>(type: "integer", nullable: false),
                    AcLateToleranceDays = table.Column<int>(type: "integer", nullable: false),
                    WarningDayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
                    MonthlyMinGapDays = table.Column<int>(type: "integer", nullable: false),
                    MonthlyMaxGapDays = table.Column<int>(type: "integer", nullable: false),
                    WeeklyMinGapDays = table.Column<int>(type: "integer", nullable: false),
                    WeeklyMaxGapDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_ParentRoomId",
                table: "Rooms",
                column: "ParentRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_LabCalendarDates_Date",
                table: "LabCalendarDates",
                column: "Date",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Rooms_ParentRoomId",
                table: "Rooms",
                column: "ParentRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Rooms_ParentRoomId",
                table: "Rooms");

            migrationBuilder.DropTable(
                name: "LabCalendarDates");

            migrationBuilder.DropTable(
                name: "MonitoringSettings");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_ParentRoomId",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "FrequencyAnchorMonth",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "FrequencyInterval",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "FrequencyType",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "SamplingWeekday",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "FrequencyAnchorMonth",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "FrequencyInterval",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "FrequencyType",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ParentRoomId",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "SamplingWeekday",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "FrequencyAnchorMonth",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "FrequencyInterval",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "FrequencyType",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "SamplingWeekday",
                table: "Departments");
        }
    }
}
