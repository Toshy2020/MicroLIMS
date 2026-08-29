using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceMediaToMediaEvaluationChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReferenceMediaId",
                table: "MediaEvaluationChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceMediaLabel",
                table: "MediaEvaluationChallenges",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_ReferenceMediaId",
                table: "MediaEvaluationChallenges",
                column: "ReferenceMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaEvaluationChallenges_Media_ReferenceMediaId",
                table: "MediaEvaluationChallenges",
                column: "ReferenceMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaEvaluationChallenges_Media_ReferenceMediaId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropIndex(
                name: "IX_MediaEvaluationChallenges_ReferenceMediaId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropColumn(
                name: "ReferenceMediaId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropColumn(
                name: "ReferenceMediaLabel",
                table: "MediaEvaluationChallenges");
        }
    }
}
