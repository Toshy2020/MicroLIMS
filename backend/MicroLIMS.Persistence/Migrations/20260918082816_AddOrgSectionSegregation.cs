using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgSectionSegregation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // a. Add DocumentSections.Code as nullable first
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "DocumentSections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // b. SQL find-or-create org data, idempotent
            migrationBuilder.Sql(@"
                -- QC and RD departments
                INSERT INTO ""DocumentDepartments"" (""Code"", ""Name"", ""IsActive"")
                SELECT 'QC', 'Quality Control', true
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""DocumentDepartments"" WHERE ""Code"" = 'QC'
                );

                INSERT INTO ""DocumentDepartments"" (""Code"", ""Name"", ""IsActive"")
                SELECT 'RD', 'Research & Development', true
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""DocumentDepartments"" WHERE ""Code"" = 'RD'
                );

                -- QC sections
                INSERT INTO ""DocumentSections"" (""DepartmentId"", ""Name"", ""Code"", ""IsActive"")
                SELECT d.""Id"", 'Microbiology Laboratory', 'MICRO', true
                FROM ""DocumentDepartments"" d
                WHERE d.""Code"" = 'QC'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""DocumentSections"" s
                      WHERE s.""DepartmentId"" = d.""Id"" AND s.""Name"" = 'Microbiology Laboratory'
                  );

                INSERT INTO ""DocumentSections"" (""DepartmentId"", ""Name"", ""Code"", ""IsActive"")
                SELECT d.""Id"", 'Finished Product Laboratory', 'FP', true
                FROM ""DocumentDepartments"" d
                WHERE d.""Code"" = 'QC'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""DocumentSections"" s
                      WHERE s.""DepartmentId"" = d.""Id"" AND s.""Name"" = 'Finished Product Laboratory'
                  );

                -- RD sections
                INSERT INTO ""DocumentSections"" (""DepartmentId"", ""Name"", ""Code"", ""IsActive"")
                SELECT d.""Id"", 'Stability', 'STAB', true
                FROM ""DocumentDepartments"" d
                WHERE d.""Code"" = 'RD'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""DocumentSections"" s
                      WHERE s.""DepartmentId"" = d.""Id"" AND s.""Name"" = 'Stability'
                  );

                INSERT INTO ""DocumentSections"" (""DepartmentId"", ""Name"", ""Code"", ""IsActive"")
                SELECT d.""Id"", 'Methodology', 'METH', true
                FROM ""DocumentDepartments"" d
                WHERE d.""Code"" = 'RD'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""DocumentSections"" s
                      WHERE s.""DepartmentId"" = d.""Id"" AND s.""Name"" = 'Methodology'
                  );

                INSERT INTO ""DocumentSections"" (""DepartmentId"", ""Name"", ""Code"", ""IsActive"")
                SELECT d.""Id"", 'Formulation', 'FORM', true
                FROM ""DocumentDepartments"" d
                WHERE d.""Code"" = 'RD'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""DocumentSections"" s
                      WHERE s.""DepartmentId"" = d.""Id"" AND s.""Name"" = 'Formulation'
                  );

                -- Update codes for existing sections with matching names under QC
                UPDATE ""DocumentSections"" s
                SET ""Code"" = 'MICRO'
                FROM ""DocumentDepartments"" d
                WHERE s.""DepartmentId"" = d.""Id""
                  AND d.""Code"" = 'QC'
                  AND s.""Name"" = 'Microbiology Laboratory'
                  AND (s.""Code"" IS NULL OR s.""Code"" = '');

                UPDATE ""DocumentSections"" s
                SET ""Code"" = 'FP'
                FROM ""DocumentDepartments"" d
                WHERE s.""DepartmentId"" = d.""Id""
                  AND d.""Code"" = 'QC'
                  AND s.""Name"" = 'Finished Product Laboratory'
                  AND (s.""Code"" IS NULL OR s.""Code"" = '');

                -- Update codes for existing sections with matching names under RD
                UPDATE ""DocumentSections"" s
                SET ""Code"" = 'STAB'
                FROM ""DocumentDepartments"" d
                WHERE s.""DepartmentId"" = d.""Id""
                  AND d.""Code"" = 'RD'
                  AND s.""Name"" = 'Stability'
                  AND (s.""Code"" IS NULL OR s.""Code"" = '');

                UPDATE ""DocumentSections"" s
                SET ""Code"" = 'METH'
                FROM ""DocumentDepartments"" d
                WHERE s.""DepartmentId"" = d.""Id""
                  AND d.""Code"" = 'RD'
                  AND s.""Name"" = 'Methodology'
                  AND (s.""Code"" IS NULL OR s.""Code"" = '');

                UPDATE ""DocumentSections"" s
                SET ""Code"" = 'FORM'
                FROM ""DocumentDepartments"" d
                WHERE s.""DepartmentId"" = d.""Id""
                  AND d.""Code"" = 'RD'
                  AND s.""Name"" = 'Formulation'
                  AND (s.""Code"" IS NULL OR s.""Code"" = '');

                -- Fallback for any other existing section with NULL Code
                UPDATE ""DocumentSections""
                SET ""Code"" = 'SEC' || ""Id""
                WHERE ""Code"" IS NULL OR ""Code"" = '';
            ");

            // c. Alter DocumentSections.Code to NOT NULL, then create its unique index
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "DocumentSections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_Code",
                table: "DocumentSections",
                column: "Code",
                unique: true);

            // d. Add SectionId to TestDefinitions, TestOrders, Materials as nullable;
            //    UPDATE every existing row to the Id of the section with Code 'MICRO';
            //    then alter to NOT NULL; then add FKs and indexes.
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Materials",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""TestDefinitions""
                SET ""SectionId"" = (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'MICRO' LIMIT 1)
                WHERE ""SectionId"" IS NULL;

                UPDATE ""TestOrders""
                SET ""SectionId"" = (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'MICRO' LIMIT 1)
                WHERE ""SectionId"" IS NULL;

                UPDATE ""Materials""
                SET ""SectionId"" = (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'MICRO' LIMIT 1)
                WHERE ""SectionId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "TestDefinitions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "TestOrders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "Materials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_SectionId",
                table: "TestDefinitions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrders_SectionId",
                table: "TestOrders",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_SectionId",
                table: "Materials",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_DocumentSections_SectionId",
                table: "TestDefinitions",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestOrders_DocumentSections_SectionId",
                table: "TestOrders",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_DocumentSections_SectionId",
                table: "Materials",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // e. Create UserOrgMemberships, then insert one row per existing user whose Role.Type <> 0 (0 = SystemAdministrator)
            migrationBuilder.CreateTable(
                name: "UserOrgMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOrgMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserOrgMemberships_DocumentDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DocumentDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserOrgMemberships_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserOrgMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserOrgMemberships_DepartmentId",
                table: "UserOrgMemberships",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrgMemberships_SectionId",
                table: "UserOrgMemberships",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrgMemberships_UserId_DepartmentId_SectionId",
                table: "UserOrgMemberships",
                columns: new[] { "UserId", "DepartmentId", "SectionId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.Sql(@"
                INSERT INTO ""UserOrgMemberships"" (""UserId"", ""DepartmentId"", ""SectionId"", ""CreatedAt"")
                SELECT 
                    u.""Id"",
                    (SELECT d.""Id"" FROM ""DocumentDepartments"" d WHERE d.""Code"" = 'QC' LIMIT 1),
                    (SELECT s.""Id"" FROM ""DocumentSections"" s WHERE s.""Code"" = 'MICRO' LIMIT 1),
                    now()
                FROM ""Users"" u
                JOIN ""Roles"" r ON u.""RoleId"" = r.""Id""
                WHERE r.""Type"" <> 0
                  AND NOT EXISTS (
                      SELECT 1 FROM ""UserOrgMemberships"" m
                      WHERE m.""UserId"" = u.""Id""
                        AND m.""DepartmentId"" = (SELECT d.""Id"" FROM ""DocumentDepartments"" d WHERE d.""Code"" = 'QC' LIMIT 1)
                        AND m.""SectionId"" = (SELECT s.""Id"" FROM ""DocumentSections"" s WHERE s.""Code"" = 'MICRO' LIMIT 1)
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_DocumentSections_SectionId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_DocumentSections_SectionId",
                table: "TestDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_TestOrders_DocumentSections_SectionId",
                table: "TestOrders");

            migrationBuilder.DropTable(
                name: "UserOrgMemberships");

            migrationBuilder.DropIndex(
                name: "IX_TestOrders_SectionId",
                table: "TestOrders");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_SectionId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Materials_SectionId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSections_Code",
                table: "DocumentSections");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "DocumentSections");
        }
    }
}
