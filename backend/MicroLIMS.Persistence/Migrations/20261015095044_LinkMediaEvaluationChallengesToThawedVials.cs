using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkMediaEvaluationChallengesToThawedVials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThawEventId",
                table: "MediaEvaluationChallenges",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_ThawEventId",
                table: "MediaEvaluationChallenges",
                column: "ThawEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaEvaluationChallenges_ThawEvents_ThawEventId",
                table: "MediaEvaluationChallenges",
                column: "ThawEventId",
                principalTable: "ThawEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaEvaluationChallenges_ThawEvents_ThawEventId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropIndex(
                name: "IX_MediaEvaluationChallenges_ThawEventId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropColumn(
                name: "ThawEventId",
                table: "MediaEvaluationChallenges");
        }
    }
}
