using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentConfigurationEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoclavePrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    ProgramCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProgramName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LoadType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CycleTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoclavePrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoclavePrograms_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncubatorSetPointHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    PreviousSetPoint = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    NewSetPoint = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncubatorSetPointHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncubatorSetPointHistories_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutoclaveProgramHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AutoclaveProgramId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProgramCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreviousProgramName = table.Column<string>(type: "text", nullable: false),
                    NewProgramName = table.Column<string>(type: "text", nullable: false),
                    PreviousLoadType = table.Column<string>(type: "text", nullable: false),
                    NewLoadType = table.Column<string>(type: "text", nullable: false),
                    PreviousTemperature = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    NewTemperature = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PreviousCycleTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    NewCycleTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    PreviousIsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NewIsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoclaveProgramHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoclaveProgramHistories_AutoclavePrograms_AutoclaveProgra~",
                        column: x => x.AutoclaveProgramId,
                        principalTable: "AutoclavePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoclaveProgramHistories_AutoclaveProgramId",
                table: "AutoclaveProgramHistories",
                column: "AutoclaveProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoclavePrograms_EquipmentId_ProgramCode",
                table: "AutoclavePrograms",
                columns: new[] { "EquipmentId", "ProgramCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncubatorSetPointHistories_EquipmentId",
                table: "IncubatorSetPointHistories",
                column: "EquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoclaveProgramHistories");

            migrationBuilder.DropTable(
                name: "IncubatorSetPointHistories");

            migrationBuilder.DropTable(
                name: "AutoclavePrograms");
        }
    }
}
