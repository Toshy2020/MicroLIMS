using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    // Diluent/Neutralizer become free text (previously FK to DiluentType/
    // Neutralizer master lists; DiluentType additionally gated a GPT-released
    // Media lot selection via RequiresBatchTracking/DiluentMediaId, dropped
    // as unused - no DiluentType ever required it). Unit is removed from
    // both preparation tables - the reporting unit lives on Specification
    // per test (TestWorkflowEngine.RecordCountTestAsync now requires it
    // configured there instead of falling back to this value).
    //
    // Ordered so existing DiluentTypeId/NeutralizerId values are backfilled
    // into the new free-text columns before the old FK columns are dropped -
    // see CR "Simplify Preparation Configuration" (2026-09) open question on
    // backfill vs. blank; decided: auto-copy existing names.
    public partial class SimplifyPreparationConfigurationDiluentNeutralizer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Diluent",
                table: "SamplePreparations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Neutralizer",
                table: "SamplePreparations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Diluent",
                table: "ItemPreparationConfigurations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Neutralizer",
                table: "ItemPreparationConfigurations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Backfill from the FK master lists' Name before those FK columns
            // are dropped below.
            migrationBuilder.Sql(@"
                UPDATE ""ItemPreparationConfigurations"" c
                SET ""Diluent"" = dt.""Name""
                FROM ""DiluentTypes"" dt
                WHERE c.""DiluentTypeId"" = dt.""Id"";

                UPDATE ""ItemPreparationConfigurations"" c
                SET ""Neutralizer"" = n.""Name""
                FROM ""Neutralizers"" n
                WHERE c.""NeutralizerId"" = n.""Id"";

                UPDATE ""SamplePreparations"" p
                SET ""Diluent"" = dt.""Name""
                FROM ""DiluentTypes"" dt
                WHERE p.""DiluentTypeId"" = dt.""Id"";

                UPDATE ""SamplePreparations"" p
                SET ""Neutralizer"" = n.""Name""
                FROM ""Neutralizers"" n
                WHERE p.""NeutralizerId"" = n.""Id"";
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_DiluentTypes_DiluentTypeId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_Media_DiluentMediaId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_Neutralizers_NeutralizerId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_DiluentTypes_DiluentTypeId",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_Media_DiluentMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_Neutralizers_NeutralizerId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_DiluentMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_DiluentTypeId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_NeutralizerId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMediaId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_DiluentTypeId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "DiluentMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentTypeId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "NeutralizerId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentMediaId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "DiluentTypeId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "NeutralizerId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "ItemPreparationConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "Technique",
                table: "SamplePreparations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diluent",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "Neutralizer",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "Diluent",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "Neutralizer",
                table: "ItemPreparationConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "Technique",
                table: "SamplePreparations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "DiluentMediaId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentTypeId",
                table: "SamplePreparations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerId",
                table: "SamplePreparations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "SamplePreparations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DiluentMediaId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentTypeId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "ItemPreparationConfigurations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentMediaId",
                table: "SamplePreparations",
                column: "DiluentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentTypeId",
                table: "SamplePreparations",
                column: "DiluentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_NeutralizerId",
                table: "SamplePreparations",
                column: "NeutralizerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMediaId",
                table: "ItemPreparationConfigurations",
                column: "DiluentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentTypeId",
                table: "ItemPreparationConfigurations",
                column: "DiluentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerId",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_DiluentTypes_DiluentTypeId",
                table: "ItemPreparationConfigurations",
                column: "DiluentTypeId",
                principalTable: "DiluentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_Media_DiluentMediaId",
                table: "ItemPreparationConfigurations",
                column: "DiluentMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_Neutralizers_NeutralizerId",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerId",
                principalTable: "Neutralizers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_DiluentTypes_DiluentTypeId",
                table: "SamplePreparations",
                column: "DiluentTypeId",
                principalTable: "DiluentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_Media_DiluentMediaId",
                table: "SamplePreparations",
                column: "DiluentMediaId",
                principalTable: "Media",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_Neutralizers_NeutralizerId",
                table: "SamplePreparations",
                column: "NeutralizerId",
                principalTable: "Neutralizers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
