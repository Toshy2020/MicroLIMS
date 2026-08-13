using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncubationReceivedAtAndAnalystDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnalystDecision",
                table: "WorkflowStepResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnalystDecisionAtUtc",
                table: "WorkflowStepResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnalystDecisionByUserId",
                table: "WorkflowStepResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WindowReceivedAtUtc",
                table: "Incubations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalystDecision",
                table: "WorkflowStepResults");

            migrationBuilder.DropColumn(
                name: "AnalystDecisionAtUtc",
                table: "WorkflowStepResults");

            migrationBuilder.DropColumn(
                name: "AnalystDecisionByUserId",
                table: "WorkflowStepResults");

            migrationBuilder.DropColumn(
                name: "WindowReceivedAtUtc",
                table: "Incubations");
        }
    }
}
