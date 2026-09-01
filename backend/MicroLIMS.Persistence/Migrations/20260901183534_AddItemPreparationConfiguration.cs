using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemPreparationConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceConfigurationId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasConfirmedFromConfig",
                table: "SamplePreparations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ItemPreparationConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Technique = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FiltrationVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    WashingVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    DiluentTypeId = table.Column<int>(type: "integer", nullable: false),
                    DiluentMediaId = table.Column<int>(type: "integer", nullable: true),
                    NeutralizerId = table.Column<int>(type: "integer", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPreparationConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemPreparationConfigurations_DiluentTypes_DiluentTypeId",
                        column: x => x.DiluentTypeId,
                        principalTable: "DiluentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemPreparationConfigurations_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemPreparationConfigurations_Media_DiluentMediaId",
                        column: x => x.DiluentMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemPreparationConfigurations_Neutralizers_NeutralizerId",
                        column: x => x.NeutralizerId,
                        principalTable: "Neutralizers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_SourceConfigurationId",
                table: "SamplePreparations",
                column: "SourceConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMediaId",
                table: "ItemPreparationConfigurations",
                column: "DiluentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentTypeId",
                table: "ItemPreparationConfigurations",
                column: "DiluentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_ItemId",
                table: "ItemPreparationConfigurations",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerId",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerId");

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_ItemPreparationConfigurations_SourceConf~",
                table: "SamplePreparations",
                column: "SourceConfigurationId",
                principalTable: "ItemPreparationConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_ItemPreparationConfigurations_SourceConf~",
                table: "SamplePreparations");

            migrationBuilder.DropTable(
                name: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_SourceConfigurationId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "SourceConfigurationId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "WasConfirmedFromConfig",
                table: "SamplePreparations");
        }
    }
}
