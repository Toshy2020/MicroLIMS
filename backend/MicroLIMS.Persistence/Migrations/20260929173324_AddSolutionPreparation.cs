using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolutionPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolutionPreparations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SolutionMasterId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    RecipeSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    HplcMethodId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalVolumeMl = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    MeasuredPh = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    SignatureId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionPreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolutionPreparations_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionPreparations_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionPreparations_HplcMethods_HplcMethodId",
                        column: x => x.HplcMethodId,
                        principalTable: "HplcMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionPreparations_SolutionMasters_SolutionMasterId",
                        column: x => x.SolutionMasterId,
                        principalTable: "SolutionMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolutionPreparationComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolutionPreparationId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MaterialMasterEntryId = table.Column<int>(type: "integer", nullable: false),
                    EntryCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipeQuantity = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    RecipeUnit = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: true),
                    QuantityUsed = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionPreparationComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolutionPreparationComponents_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionPreparationComponents_SolutionPreparations_Solution~",
                        column: x => x.SolutionPreparationId,
                        principalTable: "SolutionPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolutionPreparationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolutionPreparationId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionPreparationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolutionPreparationStatusHistories_SolutionPreparations_Sol~",
                        column: x => x.SolutionPreparationId,
                        principalTable: "SolutionPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparationComponents_MaterialId",
                table: "SolutionPreparationComponents",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparationComponents_SolutionPreparationId",
                table: "SolutionPreparationComponents",
                column: "SolutionPreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_Code",
                table: "SolutionPreparations",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_HplcMethodId",
                table: "SolutionPreparations",
                column: "HplcMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_SectionId",
                table: "SolutionPreparations",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_SignatureId",
                table: "SolutionPreparations",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_SolutionMasterId",
                table: "SolutionPreparations",
                column: "SolutionMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparations_Status_ExpiresAt",
                table: "SolutionPreparations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SolutionPreparationStatusHistories_SolutionPreparationId",
                table: "SolutionPreparationStatusHistories",
                column: "SolutionPreparationId");

            // DbSeeder.SeedPermissionsAndGrants seeds Solutions.Prepare on a fresh
            // database. This block carries the same INSERTs for databases that
            // already exist, following 20260924192621_LabSeparationPrivileges.
            // Both statements are idempotent and no-op where the seeder already
            // did the work. RoleType: 0 = SystemAdministrator, 1 = SectionHead,
            // 3 = Analyst (see RoleType.cs).
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'Solutions.Prepare', 'Start, edit, complete, cancel or discard a solution preparation (HPLC chain).', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'Solutions.Prepare');");
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" IN (0, 1, 3) -- SystemAdministrator, SectionHead, Analyst
                  AND p.""Code"" = 'Solutions.Prepare'
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
                (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'Solutions.Prepare');");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'Solutions.Prepare';");

            migrationBuilder.DropTable(
                name: "SolutionPreparationComponents");

            migrationBuilder.DropTable(
                name: "SolutionPreparationStatusHistories");

            migrationBuilder.DropTable(
                name: "SolutionPreparations");
        }
    }
}
