using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewGateFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing Media/Cryovial rows predate this column, so there is
            // no record of who prepared them - they keep 0 ("unknown"), which
            // matches no real user Id. Segregation of duties therefore never
            // falsely blocks anyone on a legacy lot, and never falsely
            // attributes one either.
            migrationBuilder.AddColumn<int>(
                name: "PreparedByUserId",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreparedByUserId",
                table: "Cryovials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReviewWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PerformedByNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewWorkflowEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewWorkflowEvents_EntityType_EntityId",
                table: "ReviewWorkflowEvents",
                columns: new[] { "EntityType", "EntityId" });

            // Carry the existing sample lifecycle events across before the
            // old table is dropped - EF scaffolded a plain drop/create,
            // which would have silently discarded them. Id is left to the
            // identity column rather than copied: nothing references these
            // rows by Id, and letting Postgres assign keeps the sequence
            // consistent. Every existing row is by definition a Sample event.
            migrationBuilder.Sql(@"
                INSERT INTO ""ReviewWorkflowEvents""
                    (""EntityType"", ""EntityId"", ""EventType"", ""PerformedByUserId"",
                     ""PerformedByNameSnapshot"", ""Timestamp"", ""Comment"", ""Decision"")
                SELECT 'Sample', ""SampleId"", ""EventType"", ""PerformedByUserId"",
                       ""PerformedByNameSnapshot"", ""Timestamp"", ""Comment"", ""Decision""
                FROM ""SampleWorkflowEvents"";");

            migrationBuilder.DropTable(
                name: "SampleWorkflowEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreparedByUserId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "PreparedByUserId",
                table: "Cryovials");

            migrationBuilder.CreateTable(
                name: "SampleWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    PerformedByNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleWorkflowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleWorkflowEvents_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SampleWorkflowEvents_SampleId",
                table: "SampleWorkflowEvents",
                column: "SampleId");

            // Mirror of Up()'s copy. Only Sample events can go back - Media
            // and Cryovial events have nowhere to live in the old schema, so
            // rolling back past this migration does discard them. That is
            // inherent to the rollback, not an oversight.
            migrationBuilder.Sql(@"
                INSERT INTO ""SampleWorkflowEvents""
                    (""SampleId"", ""EventType"", ""PerformedByUserId"",
                     ""PerformedByNameSnapshot"", ""Timestamp"", ""Comment"", ""Decision"")
                SELECT ""EntityId"", ""EventType"", ""PerformedByUserId"",
                       ""PerformedByNameSnapshot"", ""Timestamp"", ""Comment"", ""Decision""
                FROM ""ReviewWorkflowEvents""
                WHERE ""EntityType"" = 'Sample'
                  AND ""EntityId"" IN (SELECT ""Id"" FROM ""Samples"");");

            migrationBuilder.DropTable(
                name: "ReviewWorkflowEvents");
        }
    }
}
