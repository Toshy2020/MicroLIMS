using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TestingFrequency",
                table: "WaterSamplingPoints",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WaterDepartmentId",
                table: "WaterSamplingPoints",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WaterDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterDepartments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaterSamplingPoints_WaterDepartmentId",
                table: "WaterSamplingPoints",
                column: "WaterDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_WaterSamplingPoints_WaterDepartments_WaterDepartmentId",
                table: "WaterSamplingPoints",
                column: "WaterDepartmentId",
                principalTable: "WaterDepartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Seed one default department and attach every pre-existing
            // sampling point to it, so no location is orphaned in the new
            // Department -> Location hierarchy on the Water config page.
            migrationBuilder.Sql(@"
INSERT INTO ""WaterDepartments"" (""Name"") VALUES ('Water');
UPDATE ""WaterSamplingPoints""
SET ""WaterDepartmentId"" = (SELECT ""Id"" FROM ""WaterDepartments"" WHERE ""Name"" = 'Water' LIMIT 1)
WHERE ""WaterDepartmentId"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WaterSamplingPoints_WaterDepartments_WaterDepartmentId",
                table: "WaterSamplingPoints");

            migrationBuilder.DropTable(
                name: "WaterDepartments");

            migrationBuilder.DropIndex(
                name: "IX_WaterSamplingPoints_WaterDepartmentId",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "TestingFrequency",
                table: "WaterSamplingPoints");

            migrationBuilder.DropColumn(
                name: "WaterDepartmentId",
                table: "WaterSamplingPoints");
        }
    }
}
