using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplersAndProductionStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionStages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Samplers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samplers", x => x.Id);
                });

            // Carry over the values that were previously hardcoded in
            // frontend/src/services/masterDataOptions.ts (SAMPLED_BY_SUGGESTIONS/
            // PRODUCTION_STAGES) so this ships with no visible change to the
            // receiving form's suggestion lists - only where they're managed from.
            migrationBuilder.Sql(@"
                INSERT INTO ""Samplers"" (""Name"", ""IsActive"") VALUES
                    ('Walid', true), ('Mohamed', true), ('Adel', true),
                    ('Ahmed Reda', true), ('Shawky', true), ('IPQA', true), ('R&D', true);

                INSERT INTO ""ProductionStages"" (""Name"", ""IsActive"") VALUES
                    ('B', true), ('IP', true), ('F.P', true),
                    ('S.F', true), ('Coating', true), ('Compressed Tab', true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionStages");

            migrationBuilder.DropTable(
                name: "Samplers");
        }
    }
}
