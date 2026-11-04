using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodicRetestSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AwaitingTestSelection",
                table: "Samples",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PendingTargetSectionIds",
                table: "Samples",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestSelectionSignatureId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTestSelection",
                table: "CausesOfTesting",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_TestSelectionSignatureId",
                table: "Samples",
                column: "TestSelectionSignatureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_ElectronicSignatures_TestSelectionSignatureId",
                table: "Samples",
                column: "TestSelectionSignatureId",
                principalTable: "ElectronicSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Idempotent: prod migrates at startup, and a cause already named
            // "Periodic retest" (added by hand) must not be duplicated - it only
            // gets the flag.
            migrationBuilder.Sql(InsertPeriodicRetestCauseSql);
        }

        public const string InsertPeriodicRetestCauseSql = @"
INSERT INTO ""CausesOfTesting"" (""Name"", ""IsActive"", ""RequiresTestSelection"")
SELECT 'Periodic retest', TRUE, TRUE
WHERE NOT EXISTS (SELECT 1 FROM ""CausesOfTesting"" WHERE ""Name"" = 'Periodic retest');
UPDATE ""CausesOfTesting"" SET ""RequiresTestSelection"" = TRUE WHERE ""Name"" = 'Periodic retest';";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only when no sample was received for it (Samples.CauseOfTestingId restricts the delete).
            migrationBuilder.Sql(@"
DELETE FROM ""CausesOfTesting"" c
WHERE c.""Name"" = 'Periodic retest'
  AND NOT EXISTS (SELECT 1 FROM ""Samples"" s WHERE s.""CauseOfTestingId"" = c.""Id"");");

            migrationBuilder.DropForeignKey(
                name: "FK_Samples_ElectronicSignatures_TestSelectionSignatureId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_TestSelectionSignatureId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "AwaitingTestSelection",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "PendingTargetSectionIds",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "TestSelectionSignatureId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "RequiresTestSelection",
                table: "CausesOfTesting");
        }
    }
}
