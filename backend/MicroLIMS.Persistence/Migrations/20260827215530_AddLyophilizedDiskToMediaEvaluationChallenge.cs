using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLyophilizedDiskToMediaEvaluationChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LyophilizedDiskId",
                table: "MediaEvaluationChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_LyophilizedDiskId",
                table: "MediaEvaluationChallenges",
                column: "LyophilizedDiskId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaEvaluationChallenges_Materials_LyophilizedDiskId",
                table: "MediaEvaluationChallenges",
                column: "LyophilizedDiskId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaEvaluationChallenges_Materials_LyophilizedDiskId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropIndex(
                name: "IX_MediaEvaluationChallenges_LyophilizedDiskId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropColumn(
                name: "LyophilizedDiskId",
                table: "MediaEvaluationChallenges");
        }
    }
}
