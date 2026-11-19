using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableViews : Migration
    {
        private const string Process = "Migration 20261114083303_AddTableViews";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TableViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    TableKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableViews", x => x.Id);
                    table.CheckConstraint("CK_TableViews_ScopeSection", "(\"Scope\" = 1) = (\"SectionId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TableViews_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableViews_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserTableDefaults",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TableKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TableViewId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTableDefaults", x => new { x.UserId, x.TableKey });
                    table.ForeignKey(
                        name: "FK_UserTableDefaults_TableViews_TableViewId",
                        column: x => x.TableViewId,
                        principalTable: "TableViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTableDefaults_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TableViews_Personal_Name",
                table: "TableViews",
                columns: new[] { "OwnerUserId", "TableKey", "Name" },
                unique: true,
                filter: "\"Scope\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TableViews_Section_Name",
                table: "TableViews",
                columns: new[] { "SectionId", "TableKey", "Name" },
                unique: true,
                filter: "\"Scope\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TableViews_TableKey_OwnerUserId",
                table: "TableViews",
                columns: new[] { "TableKey", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTableDefaults_TableViewId",
                table: "UserTableDefaults",
                column: "TableViewId");

            // TableViews.Share is checked by TableViewService, so it is enforced; Data.Export is checked by the
            // export endpoint added later in the same plan and stays unenforced until then.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT v.code, v.descr, v.enforced
                FROM (VALUES
                    ('TableViews.Share', 'Share a saved table view with a laboratory section the user belongs to.', TRUE),
                    ('Data.Export', 'Export a table to a CSV file (every export is logged).', FALSE)
                ) AS v(code, descr, enforced)
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" p WHERE p."Code" = v.code);
                """);

            // RoleType: 1 = SectionHead, 2 = Reviewer, 4 = QcManager.
            migrationBuilder.Sql(RoleGrantSql.Grant(
                @"ro.""Type"" IN (1, 4) AND ro.""IsSystemRole""",
                new[] { "TableViews.Share" },
                Process,
                "Section Heads and QC Managers share saved table views with their section"));

            migrationBuilder.Sql(RoleGrantSql.Grant(
                @"ro.""Type"" IN (1, 2, 4) AND ro.""IsSystemRole""",
                new[] { "Data.Export" },
                Process,
                "Section Heads, Reviewers and QC Managers export tables to CSV"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(
                new[] { "TableViews.Share", "Data.Export" },
                Process + " (Down)",
                "Saved table view and export permissions rolled back (migration Down)"));

            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" IN ('TableViews.Share', 'Data.Export');
                """);

            migrationBuilder.DropTable(
                name: "UserTableDefaults");

            migrationBuilder.DropTable(
                name: "TableViews");
        }
    }
}
