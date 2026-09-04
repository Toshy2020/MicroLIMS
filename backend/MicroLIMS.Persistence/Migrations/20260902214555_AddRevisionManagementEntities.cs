using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRevisionManagementEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                table: "DocumentRevisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginatingPeriodicReviewTaskId",
                table: "DocumentRevisions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RevisionChangeItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    SectionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectionTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DescriptionOfChange = table.Column<string>(type: "text", nullable: false),
                    ChangeRationale = table.Column<string>(type: "text", nullable: false),
                    ChangeCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    OriginatingReviewFindingId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionChangeItems_DocumentReviewFindings_OriginatingRevie~",
                        column: x => x.OriginatingReviewFindingId,
                        principalTable: "DocumentReviewFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionChangeItems_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionChangeItems_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevisionImpactAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    ProcedureOrMethodImpact = table.Column<bool>(type: "boolean", nullable: false),
                    ProcedureOrMethodDetails = table.Column<string>(type: "text", nullable: true),
                    TrainingImpact = table.Column<bool>(type: "boolean", nullable: false),
                    TrainingDetails = table.Column<string>(type: "text", nullable: true),
                    FormsOrTemplatesImpact = table.Column<bool>(type: "boolean", nullable: false),
                    FormsOrTemplatesDetails = table.Column<string>(type: "text", nullable: true),
                    SpecificationsImpact = table.Column<bool>(type: "boolean", nullable: false),
                    SpecificationsDetails = table.Column<string>(type: "text", nullable: true),
                    EquipmentImpact = table.Column<bool>(type: "boolean", nullable: false),
                    EquipmentDetails = table.Column<string>(type: "text", nullable: true),
                    MaterialsOrMediaImpact = table.Column<bool>(type: "boolean", nullable: false),
                    MaterialsOrMediaDetails = table.Column<string>(type: "text", nullable: true),
                    ValidationImpact = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationDetails = table.Column<string>(type: "text", nullable: true),
                    RegulatoryCommitmentImpact = table.Column<bool>(type: "boolean", nullable: false),
                    RegulatoryCommitmentDetails = table.Column<string>(type: "text", nullable: true),
                    RelatedDocumentsImpact = table.Column<bool>(type: "boolean", nullable: false),
                    RelatedDocumentsDetails = table.Column<string>(type: "text", nullable: true),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionImpactAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionImpactAssessments_DocumentRevisions_DocumentRevisio~",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionImpactAssessments_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionChangeItems_CreatedByUserId",
                table: "RevisionChangeItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionChangeItems_DocumentRevisionId",
                table: "RevisionChangeItems",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionChangeItems_OriginatingReviewFindingId",
                table: "RevisionChangeItems",
                column: "OriginatingReviewFindingId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionImpactAssessments_CompletedByUserId",
                table: "RevisionImpactAssessments",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionImpactAssessments_DocumentRevisionId",
                table: "RevisionImpactAssessments",
                column: "DocumentRevisionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevisionChangeItems");

            migrationBuilder.DropTable(
                name: "RevisionImpactAssessments");

            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                table: "DocumentRevisions");

            migrationBuilder.DropColumn(
                name: "OriginatingPeriodicReviewTaskId",
                table: "DocumentRevisions");
        }
    }
}
