using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedByUserIdForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Samples_ReceivedByUserId",
                table: "Samples",
                column: "ReceivedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_Users_ReceivedByUserId",
                table: "Samples",
                column: "ReceivedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_Users_ReceivedByUserId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_ReceivedByUserId",
                table: "Samples");
        }
    }
}
