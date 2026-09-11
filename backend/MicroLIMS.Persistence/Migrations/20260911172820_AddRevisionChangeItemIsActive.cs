using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRevisionChangeItemIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue is true, not the false EF scaffolded. The scaffolded
            // default would have backfilled every pre-existing change item to
            // IsActive = false, and since GetChangeItemsAsync now filters on
            // IsActive, every change item already recorded against an open draft
            // would have silently disappeared from the UI on deploy. Existing
            // rows predate the concept of removal, so they are all active.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RevisionChangeItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RevisionChangeItems");
        }
    }
}
