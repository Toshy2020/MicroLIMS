using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropStageReplicateStandardCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardReplicates",
                table: "TestDefinitionStageReplicates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StandardReplicates",
                table: "TestDefinitionStageReplicates",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
