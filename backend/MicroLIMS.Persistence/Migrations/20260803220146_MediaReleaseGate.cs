using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaReleaseGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Media",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "Media",
                type: "integer",
                nullable: true);

            // Lots released under the old auto-release rule are already in
            // service. Left at the column default (0 = PendingReview) they
            // would show up in the Section Head's new approval queue asking
            // for permission to do something they have been doing for
            // weeks. Grandfather them as Approved instead - ApprovedByUserId
            // stays null, which is honest: no one ever signed for these.
            migrationBuilder.Sql(@"
                UPDATE ""Media"" SET ""ApprovalStatus"" = 1
                WHERE ""IsReleasedForUse"" = true;");

            // Lots whose evaluation already failed would otherwise sit at
            // PendingReview forever. The engine now quarantines these at
            // the point of failure; apply the same outcome retroactively.
            // Only touches lots still at Prepared(0) so Expired/Destroyed
            // states are not overwritten.
            migrationBuilder.Sql(@"
                UPDATE ""Media"" SET ""ApprovalStatus"" = 2, ""Status"" = 3
                WHERE ""Status"" = 0
                  AND ""IsReleasedForUse"" = false
                  AND ""Id"" IN (
                      SELECT ""MediaId"" FROM ""MediaEvaluations""
                      WHERE ""Status"" = 2 AND ""Outcome"" = 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Media");
        }
    }
}
