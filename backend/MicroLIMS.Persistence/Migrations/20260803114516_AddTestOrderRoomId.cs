using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestOrderRoomId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "TestOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestOrders_RoomId",
                table: "TestOrders",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestOrders_Rooms_RoomId",
                table: "TestOrders",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestOrders_Rooms_RoomId",
                table: "TestOrders");

            migrationBuilder.DropIndex(
                name: "IX_TestOrders_RoomId",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "TestOrders");
        }
    }
}
