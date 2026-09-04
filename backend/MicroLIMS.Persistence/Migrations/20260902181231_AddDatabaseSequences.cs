using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    public partial class AddDatabaseSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE SEQUENCE IF NOT EXISTS document_number_seq START WITH 1 INCREMENT BY 1 NO CYCLE;
CREATE SEQUENCE IF NOT EXISTS audit_event_seq START WITH 1 INCREMENT BY 1 NO CYCLE;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP SEQUENCE IF EXISTS audit_event_seq;
DROP SEQUENCE IF EXISTS document_number_seq;
");
        }
    }
}
