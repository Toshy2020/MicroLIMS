using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <summary>
    /// Seeds the Document Control permission codes and their role grants.
    ///
    /// DbSeeder inserts missing permission codes one at a time, so a database
    /// seeded before these codes existed does pick them up - but its grant block
    /// is guarded by <c>if (!db.RolePermissions.Any())</c>, which is false on any
    /// established database. Without this migration such a database would end up
    /// with the codes present and granted to nobody, and Documents.Register is now
    /// enforced, so registering a document would fail for every user.
    ///
    /// Every statement is written to be idempotent and safe to re-run.
    /// </summary>
    public partial class SeedDocumentControlPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Documents.Register is checked by DocumentAuthorizationService, so it
            // is marked enforced; the rest are declared for the Roles screen and
            // will flip as each action migrates off its role check.
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT v.code, v.descr, v.enforced
                FROM (VALUES
                    ('Documents.Register', 'Register a new controlled Document Master.', TRUE),
                    ('Documents.DraftEdit', 'Edit draft metadata and controlled files, and cancel a draft revision.', FALSE),
                    ('Documents.RevisionCreate', 'Create a new revision from the current effective revision.', FALSE),
                    ('Documents.Review', 'Perform technical review of a document revision (still requires being the assigned reviewer).', FALSE),
                    ('Documents.Approve', 'Execute an approval decision on a document revision (still requires being the designated approver).', FALSE),
                    ('Documents.PeriodicReview', 'Perform a periodic review of an effective document.', FALSE),
                    ('Documents.TrainingAssign', 'Assign document reading and training curricula to personnel.', FALSE),
                    ('Documents.TrainingViewMatrix', 'View and export the document training matrix.', FALSE),
                    ('Documents.ConfigManage', 'Manage Document Control configuration (types, departments, sections, numbering, settings).', FALSE)
                ) AS v(code, descr, enforced)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Permissions"" p WHERE p.""Code"" = v.code
                );");

            // Roles.Type is the RoleType enum stored as int:
            // 0 SystemAdministrator, 1 SectionHead (Document Controller),
            // 2 Reviewer, 3 Analyst. Grants mirror DbSeeder and the FRS-1A
            // permission matrix. Voiding a master and overriding a revision number
            // are deliberately absent - they stay fixed Document Controller rules
            // (FS-1a-111, FRS-1B §3.2:184) rather than grantable permissions.
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM (VALUES
                    (0, 'Documents.Register'),
                    (0, 'Documents.DraftEdit'),
                    (0, 'Documents.RevisionCreate'),
                    (0, 'Documents.Review'),
                    (0, 'Documents.Approve'),
                    (0, 'Documents.PeriodicReview'),
                    (0, 'Documents.TrainingAssign'),
                    (0, 'Documents.TrainingViewMatrix'),
                    (0, 'Documents.ConfigManage'),
                    (1, 'Documents.Register'),
                    (1, 'Documents.DraftEdit'),
                    (1, 'Documents.RevisionCreate'),
                    (1, 'Documents.PeriodicReview'),
                    (1, 'Documents.Approve'),
                    (1, 'Documents.TrainingAssign'),
                    (1, 'Documents.TrainingViewMatrix'),
                    (2, 'Documents.Review'),
                    (2, 'Documents.PeriodicReview'),
                    (2, 'Documents.Approve'),
                    (2, 'Documents.TrainingViewMatrix'),
                    (3, 'Documents.Register'),
                    (3, 'Documents.DraftEdit'),
                    (3, 'Documents.RevisionCreate')
                ) AS g(roletype, code)
                JOIN ""Roles"" r ON r.""Type"" = g.roletype
                JOIN ""Permissions"" p ON p.""Code"" = g.code
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""RolePermissions"" rp
                    WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id""
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Grants first - RolePermissions references Permissions.
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions""
                WHERE ""PermissionId"" IN (
                    SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" LIKE 'Documents.%'
                );");

            migrationBuilder.Sql(@"
                DELETE FROM ""Permissions"" WHERE ""Code"" LIKE 'Documents.%';");
        }
    }
}
