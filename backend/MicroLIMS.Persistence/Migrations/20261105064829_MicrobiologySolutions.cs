using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MicrobiologySolutions : Migration
    {
        private const string Process = "Migration 20261105064829_MicrobiologySolutions";

        // Schema plus one permission, no data backfill: existing recipes keep both flags off and their
        // components become Kind = Material (column default 0). Solutions.Release is granted to the
        // built-in Section Head (Type 1) and Reviewer (Type 2) roles, never to one that manages users
        // or roles (RoleGrantSql). DbSeeder covers a fresh database.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "SolutionPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoclaveCycleNumber",
                table: "SolutionPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoclaveCycleTime",
                table: "SolutionPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoclaveEquipmentId",
                table: "SolutionPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoclaveProgram",
                table: "SolutionPreparations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AutoclaveTemperature",
                table: "SolutionPreparations",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoadType",
                table: "SolutionPreparations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseComment",
                table: "SolutionPreparations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseSignatureId",
                table: "SolutionPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "SolutionPreparations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleasedByUserId",
                table: "SolutionPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RecipeUnit",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "RecipeQuantity",
                table: "SolutionPreparationComponents",
                type: "numeric(12,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)");

            migrationBuilder.AlterColumn<int>(
                name: "MaterialMasterEntryId",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourcePreparationId",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceSolutionMasterId",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "SolutionPreparationComponents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReleaseRequired",
                table: "SolutionMasters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SterilizationRequired",
                table: "SolutionMasters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Unit",
                table: "SolutionComponents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "SolutionComponents",
                type: "numeric(12,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)");

            migrationBuilder.AlterColumn<int>(
                name: "MaterialMasterEntryId",
                table: "SolutionComponents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "SolutionComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceSolutionMasterId",
                table: "SolutionComponents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "SolutionComponents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_AutoclaveEquipmentId",
                table: "SolutionPreparations",
                column: "AutoclaveEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_ReleaseSignatureId",
                table: "SolutionPreparations",
                column: "ReleaseSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparationComponents_SourcePreparationId",
                table: "SolutionPreparationComponents",
                column: "SourcePreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionComponents_SourceSolutionMasterId",
                table: "SolutionComponents",
                column: "SourceSolutionMasterId");

            migrationBuilder.AddForeignKey(
                name: "FK_SolutionComponents_SolutionMasters_SourceSolutionMasterId",
                table: "SolutionComponents",
                column: "SourceSolutionMasterId",
                principalTable: "SolutionMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SolutionPreparationComponents_SolutionPreparations_SourcePr~",
                table: "SolutionPreparationComponents",
                column: "SourcePreparationId",
                principalTable: "SolutionPreparations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SolutionPreparations_ElectronicSignatures_ReleaseSignatureId",
                table: "SolutionPreparations",
                column: "ReleaseSignatureId",
                principalTable: "ElectronicSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SolutionPreparations_Equipment_AutoclaveEquipmentId",
                table: "SolutionPreparations",
                column: "AutoclaveEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Solutions.Release', 'Release a prepared solution for use.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Solutions.Release');
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Solutions.Release';""");

            migrationBuilder.Sql(RoleGrantSql.Grant(@"ro.""Type"" IN (1, 2) AND ro.""IsSystemRole""",
                new[] { "Solutions.Release" }, Process, "Section Heads and Reviewers release prepared solutions"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(new[] { "Solutions.Release" }, Process + " (Down)", "Solution release permission rolled back (migration Down)"));
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" = 'Solutions.Release';""");

            migrationBuilder.DropForeignKey(
                name: "FK_SolutionComponents_SolutionMasters_SourceSolutionMasterId",
                table: "SolutionComponents");

            migrationBuilder.DropForeignKey(
                name: "FK_SolutionPreparationComponents_SolutionPreparations_SourcePr~",
                table: "SolutionPreparationComponents");

            migrationBuilder.DropForeignKey(
                name: "FK_SolutionPreparations_ElectronicSignatures_ReleaseSignatureId",
                table: "SolutionPreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SolutionPreparations_Equipment_AutoclaveEquipmentId",
                table: "SolutionPreparations");

            migrationBuilder.DropIndex(
                name: "IX_SolutionPreparations_AutoclaveEquipmentId",
                table: "SolutionPreparations");

            migrationBuilder.DropIndex(
                name: "IX_SolutionPreparations_ReleaseSignatureId",
                table: "SolutionPreparations");

            migrationBuilder.DropIndex(
                name: "IX_SolutionPreparationComponents_SourcePreparationId",
                table: "SolutionPreparationComponents");

            migrationBuilder.DropIndex(
                name: "IX_SolutionComponents_SourceSolutionMasterId",
                table: "SolutionComponents");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "AutoclaveCycleNumber",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "AutoclaveCycleTime",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "AutoclaveEquipmentId",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "AutoclaveProgram",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "AutoclaveTemperature",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "LoadType",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "ReleaseComment",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "ReleaseSignatureId",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "ReleasedByUserId",
                table: "SolutionPreparations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SolutionPreparationComponents");

            migrationBuilder.DropColumn(
                name: "SourcePreparationId",
                table: "SolutionPreparationComponents");

            migrationBuilder.DropColumn(
                name: "SourceSolutionMasterId",
                table: "SolutionPreparationComponents");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "SolutionPreparationComponents");

            migrationBuilder.DropColumn(
                name: "ReleaseRequired",
                table: "SolutionMasters");

            migrationBuilder.DropColumn(
                name: "SterilizationRequired",
                table: "SolutionMasters");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "SolutionComponents");

            migrationBuilder.DropColumn(
                name: "SourceSolutionMasterId",
                table: "SolutionComponents");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "SolutionComponents");

            migrationBuilder.AlterColumn<int>(
                name: "RecipeUnit",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RecipeQuantity",
                table: "SolutionPreparationComponents",
                type: "numeric(12,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MaterialMasterEntryId",
                table: "SolutionPreparationComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Unit",
                table: "SolutionComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "SolutionComponents",
                type: "numeric(12,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MaterialMasterEntryId",
                table: "SolutionComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
