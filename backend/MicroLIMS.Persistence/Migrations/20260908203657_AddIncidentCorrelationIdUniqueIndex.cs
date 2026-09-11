using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentCorrelationIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Collapse any duplicates the pre-constraint lookup-then-insert
            // race may already have created, keeping the earliest Incident per
            // correlation id. Children are re-pointed first: deleting a
            // duplicate outright would cascade its ErrorLogs away, losing the
            // very records this feature exists to keep.
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT ""Id"",
                           row_number() OVER (PARTITION BY ""CorrelationId"" ORDER BY ""FirstSeenUtc"", ""Id"") AS rn,
                           first_value(""Id"") OVER (PARTITION BY ""CorrelationId"" ORDER BY ""FirstSeenUtc"", ""Id"") AS keep_id
                    FROM ""Incidents""
                )
                UPDATE ""ErrorLogs"" e
                SET ""IncidentId"" = r.keep_id
                FROM ranked r
                WHERE e.""IncidentId"" = r.""Id"" AND r.rn > 1;");

            migrationBuilder.Sql(@"
                DELETE FROM ""Incidents"" i
                USING (
                    SELECT ""Id"",
                           row_number() OVER (PARTITION BY ""CorrelationId"" ORDER BY ""FirstSeenUtc"", ""Id"") AS rn
                    FROM ""Incidents""
                ) d
                WHERE i.""Id"" = d.""Id"" AND d.rn > 1;");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_CorrelationId",
                table: "Incidents");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CorrelationId",
                table: "Incidents",
                column: "CorrelationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_CorrelationId",
                table: "Incidents");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CorrelationId",
                table: "Incidents",
                column: "CorrelationId");
        }
    }
}
