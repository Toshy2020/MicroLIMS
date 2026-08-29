using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaConfigurationIdToTestWorkflowStepMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaConfigurationId",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_MediaConfigurationId",
                table: "TestWorkflowStepMedias",
                column: "MediaConfigurationId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaConfigurations_MediaConfigurati~",
                table: "TestWorkflowStepMedias",
                column: "MediaConfigurationId",
                principalTable: "MediaConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaConfigurations_MediaConfigurati~",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowStepMedias_MediaConfigurationId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropColumn(
                name: "MediaConfigurationId",
                table: "TestWorkflowStepMedias");
        }
    }
}
