using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniversalSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId",
                table: "Specifications");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Specifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedResultText",
                table: "Specifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedState",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LimitType",
                table: "Specifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LowerInclusive",
                table: "Specifications",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowerLimit",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParameterName",
                table: "Specifications",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceStandard",
                table: "Specifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SampleQuantity",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleQuantityUnit",
                table: "Specifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Target",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Tolerance",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToleranceMode",
                table: "Specifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpperInclusive",
                table: "Specifications",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UpperLimit",
                table: "Specifications",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecificationStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpecificationId = table.Column<int>(type: "integer", nullable: false),
                    StageNumber = table.Column<int>(type: "integer", nullable: false),
                    StageLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcceptanceCriteriaText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecificationStages_Specifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "Specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
UPDATE ""Specifications"" s
SET ""ParameterName"" = COALESCE(NULLIF(td.""DisplayName"", ''), s.""TestCode"")
FROM ""TestDefinitions"" td
WHERE td.""Code"" = s.""TestCode"";

UPDATE ""Specifications""
SET ""ParameterName"" = ""TestCode""
WHERE ""ParameterName"" IS NULL OR ""ParameterName"" = '';

UPDATE ""Specifications""
SET ""LimitType"" = 6,
    ""ExpectedState"" = 1,
    ""SpecLimit"" = 'Absent'
WHERE TRIM(""SpecLimit"") ILIKE 'absent' OR TRIM(""SpecLimit"") ILIKE 'abent';

UPDATE ""Specifications""
SET ""LimitType"" = 4
WHERE ""LimitType"" <> 6;
");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName",
                table: "Specifications",
                columns: new[] { "ItemId", "TestCode", "ParameterName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationStages_SpecificationId",
                table: "SpecificationStages",
                column: "SpecificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecificationStages");

            migrationBuilder.DropIndex(
                name: "IX_Specifications_ItemId_TestCode_ParameterName",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ExpectedResultText",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ExpectedState",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "LimitType",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "LowerInclusive",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "LowerLimit",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ParameterName",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ReferenceStandard",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "SampleQuantity",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "SampleQuantityUnit",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "Tolerance",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ToleranceMode",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "UpperInclusive",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "UpperLimit",
                table: "Specifications");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId",
                table: "Specifications",
                column: "ItemId");
        }
    }
}
