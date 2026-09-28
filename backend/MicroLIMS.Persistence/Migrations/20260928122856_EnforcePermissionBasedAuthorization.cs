using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    // No model changes. Every controller now authorizes by permission
    // ([Authorize(Policy = <code>)]) instead of role name. This migration
    // makes that switch change nobody's access on an existing database:
    //
    // 1. Adds the ten codes that had no catalogue entry (DbSeeder adds them
    //    on a fresh database).
    // 2. Grants every role - system and custom - the permissions its role
    //    type has by default, for each code that now takes effect. Until
    //    now those endpoints admitted a user by role type alone, whatever
    //    the Roles screen said, so this keeps each user's effective access
    //    as it was. From here on, editing a role's permissions really
    //    changes what its users can do.
    // 3. Marks every code an endpoint now checks as enforced, so the Roles
    //    screen stops calling it legacy-only.
    //
    // All statements are idempotent.
    public partial class EnforcePermissionBasedAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, description) in new[]
            {
                ("Dashboards.LabOverview", "View the laboratory overview dashboard (section workload, approvals, overdue work)."),
                ("Dashboards.Review", "View the reviewer dashboard."),
                ("Kpi.View", "View laboratory KPIs and manage workload weights."),
                ("Media.Prepare", "Prepare media lots and mark them out of stock."),
                ("Media.Release", "Release a prepared media lot for use."),
                ("Oos.Manage", "Track out-of-specification groups and manage their investigation documents."),
                ("TestWorkflow.Supervise", "Supervisor interventions in a running test: override an incubation's minimum duration, reset a pathogen session."),
                ("Samples.ReceiveOwnLab", "Receive samples for the laboratories the user belongs to."),
                ("Samples.Correct", "Correct or void a sample record."),
                ("Samples.AssignAnalyst", "Assign an analyst to a sample."),
            })
            {
                migrationBuilder.Sql($@"
                    INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                    SELECT '{code}', '{description.Replace("'", "''")}', TRUE
                    WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = '{code}');");
            }

            // (role type, code): the default grants from DbSeeder.SeedPermissionsAndGrants.
            // 0 SystemAdministrator, 1 SectionHead, 2 Reviewer, 3 Analyst.
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM (VALUES
                    (0, 'Audit.View'),
                    (1, 'Audit.View'),
                    (0, 'Cryovials.Approve'),
                    (1, 'Cryovials.Approve'),
                    (0, 'Cryovials.Manage'),
                    (1, 'Cryovials.Manage'),
                    (2, 'Cryovials.Manage'),
                    (3, 'Cryovials.Manage'),
                    (0, 'Documents.ConfigManage'),
                    (0, 'Documents.TrainingAssign'),
                    (1, 'Documents.TrainingAssign'),
                    (0, 'Equipment.DocumentControl'),
                    (1, 'Equipment.DocumentControl'),
                    (0, 'Items.DocumentUpload'),
                    (1, 'Items.DocumentUpload'),
                    (0, 'MasterData.Manage'),
                    (1, 'MasterData.Manage'),
                    (0, 'Materials.DocumentControl'),
                    (1, 'Materials.DocumentControl'),
                    (0, 'Materials.Manage'),
                    (1, 'Materials.Manage'),
                    (3, 'Materials.Manage'),
                    (0, 'Reporting.Admin'),
                    (0, 'Roles.Manage'),
                    (0, 'Samples.Review'),
                    (1, 'Samples.Review'),
                    (2, 'Samples.Review'),
                    (0, 'Signatures.Manage'),
                    (1, 'Signatures.Manage'),
                    (0, 'TestWorkflow.BiochemicalDecision'),
                    (1, 'TestWorkflow.BiochemicalDecision'),
                    (2, 'TestWorkflow.BiochemicalDecision'),
                    (0, 'Users.Manage'),
                    (0, 'Discussions.EditAny'),
                    (1, 'Discussions.EditAny'),
                    (0, 'Dashboards.LabOverview'),
                    (1, 'Dashboards.LabOverview'),
                    (0, 'Dashboards.Review'),
                    (1, 'Dashboards.Review'),
                    (2, 'Dashboards.Review'),
                    (0, 'Kpi.View'),
                    (1, 'Kpi.View'),
                    (0, 'Media.Prepare'),
                    (1, 'Media.Prepare'),
                    (2, 'Media.Prepare'),
                    (3, 'Media.Prepare'),
                    (0, 'Media.Release'),
                    (1, 'Media.Release'),
                    (0, 'Oos.Manage'),
                    (1, 'Oos.Manage'),
                    (0, 'TestWorkflow.Supervise'),
                    (1, 'TestWorkflow.Supervise'),
                    (0, 'Samples.ReceiveOwnLab'),
                    (1, 'Samples.ReceiveOwnLab'),
                    (2, 'Samples.ReceiveOwnLab'),
                    (3, 'Samples.ReceiveOwnLab'),
                    (0, 'Samples.Correct'),
                    (1, 'Samples.Correct'),
                    (2, 'Samples.Correct'),
                    (0, 'Samples.AssignAnalyst'),
                    (1, 'Samples.AssignAnalyst')
                ) AS d(""Type"", ""Code"")
                JOIN ""Roles"" r ON r.""Type"" = d.""Type""
                JOIN ""Permissions"" p ON p.""Code"" = d.""Code""
                WHERE NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");

            migrationBuilder.Sql(@"
                UPDATE ""Permissions"" SET ""IsEnforced"" = TRUE
                WHERE ""Code"" IN ('Audit.View', 'Cryovials.Approve', 'Cryovials.Manage', 'Dashboards.LabOverview', 'Dashboards.Review', 'Discussions.EditAny', 'Documents.ConfigManage', 'Documents.Register', 'Documents.TrainingAssign', 'Equipment.DocumentControl', 'Equipment.Manage', 'Items.DocumentUpload', 'Items.Manage', 'Kpi.View', 'MasterData.Manage', 'Materials.DocumentControl', 'Materials.Manage', 'Media.Prepare', 'Media.Release', 'Oos.Manage', 'Reporting.Admin', 'Roles.Manage', 'Samples.Approve', 'Samples.AssignAnalyst', 'Samples.Correct', 'Samples.Receive', 'Samples.ReceiveOwnLab', 'Samples.Review', 'Samples.TrackAll', 'Signatures.Manage', 'System.ViewErrorLog', 'TestWorkflow.BiochemicalDecision', 'TestWorkflow.Execute', 'TestWorkflow.Supervise', 'Users.Manage');");
        }

        /// <inheritdoc />
        // The grants added in Up are indistinguishable from ones an
        // administrator made, so they stay; with the role attributes back
        // they are simply unenforced again.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Permissions"" SET ""IsEnforced"" = FALSE
                WHERE ""Code"" IN ('Audit.View', 'Cryovials.Approve', 'Cryovials.Manage', 'Documents.ConfigManage', 'Documents.TrainingAssign', 'Equipment.DocumentControl', 'Items.DocumentUpload', 'MasterData.Manage', 'Materials.DocumentControl', 'Materials.Manage', 'Reporting.Admin', 'Roles.Manage', 'Samples.Review', 'Signatures.Manage', 'TestWorkflow.BiochemicalDecision', 'Users.Manage');");
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
                (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" IN ('Dashboards.LabOverview', 'Dashboards.Review', 'Kpi.View', 'Media.Prepare', 'Media.Release', 'Oos.Manage', 'TestWorkflow.Supervise', 'Samples.ReceiveOwnLab', 'Samples.Correct', 'Samples.AssignAnalyst'));");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" IN ('Dashboards.LabOverview', 'Dashboards.Review', 'Kpi.View', 'Media.Prepare', 'Media.Release', 'Oos.Manage', 'TestWorkflow.Supervise', 'Samples.ReceiveOwnLab', 'Samples.Correct', 'Samples.AssignAnalyst');");
        }
    }
}
