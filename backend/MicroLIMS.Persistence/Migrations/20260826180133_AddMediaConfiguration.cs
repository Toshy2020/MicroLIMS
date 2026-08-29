using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UsageLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Class = table.Column<int>(type: "integer", nullable: false),
                    EvaluationType = table.Column<int>(type: "integer", nullable: false),
                    IncubationMinHours = table.Column<int>(type: "integer", nullable: false),
                    IncubationMaxHours = table.Column<int>(type: "integer", nullable: false),
                    TemperatureMin = table.Column<decimal>(type: "numeric", nullable: false),
                    TemperatureMax = table.Column<decimal>(type: "numeric", nullable: false),
                    RecoveryPercentMin = table.Column<decimal>(type: "numeric", nullable: true),
                    RecoveryPercentMax = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaConfigurationChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    OrganismId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeRole = table.Column<int>(type: "integer", nullable: true),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaConfigurationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaConfigurationChallenges_MediaConfigurations_MediaConfi~",
                        column: x => x.MediaConfigurationId,
                        principalTable: "MediaConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaConfigurationChallenges_Organisms_OrganismId",
                        column: x => x.OrganismId,
                        principalTable: "Organisms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurationChallenges_MediaConfigurationId_OrganismI~",
                table: "MediaConfigurationChallenges",
                columns: new[] { "MediaConfigurationId", "OrganismId", "ChallengeRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurationChallenges_OrganismId",
                table: "MediaConfigurationChallenges",
                column: "OrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_Name_UsageLabel",
                table: "MediaConfigurations",
                columns: new[] { "Name", "UsageLabel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaConfigurationChallenges");

            migrationBuilder.DropTable(
                name: "MediaConfigurations");
        }
    }
}
