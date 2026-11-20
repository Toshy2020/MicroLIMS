using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOosInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OosCauseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsNoCauseFound = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosCauseCategories", x => x.Id);
                    table.CheckConstraint("CK_OosCauseCategories_NoCauseFound", "NOT \"IsNoCauseFound\" OR (\"IsActive\" AND \"Name\" = 'No cause found')");
                });

            migrationBuilder.CreateTable(
                name: "OosInvestigationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PhaseOneDueWorkingDays = table.Column<int>(type: "integer", nullable: true),
                    TotalDueWorkingDays = table.Column<int>(type: "integer", nullable: true),
                    AlertOpensExcursionReview = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosInvestigationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosInvestigationTemplates_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosInvestigations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "integer", nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    PhaseOneDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PhaseOneAssessment = table.Column<int>(type: "integer", nullable: true),
                    PhaseOneCauseCategoryId = table.Column<int>(type: "integer", nullable: true),
                    PhaseOneCauseDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PhaseOneSignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhaseOneSignedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PhaseOneSignatureId = table.Column<int>(type: "integer", nullable: true),
                    Conclusion = table.Column<int>(type: "integer", nullable: true),
                    RootCauseCategoryId = table.Column<int>(type: "integer", nullable: true),
                    RootCauseDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConclusionSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConcludedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcludedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ConclusionSignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosInvestigations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_ElectronicSignatures_ConclusionSignatureId",
                        column: x => x.ConclusionSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_ElectronicSignatures_PhaseOneSignatureId",
                        column: x => x.PhaseOneSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_OosCauseCategories_PhaseOneCauseCategoryId",
                        column: x => x.PhaseOneCauseCategoryId,
                        principalTable: "OosCauseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_OosCauseCategories_RootCauseCategoryId",
                        column: x => x.RootCauseCategoryId,
                        principalTable: "OosCauseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_Users_ConcludedByUserId",
                        column: x => x.ConcludedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigations_Users_PhaseOneSignedByUserId",
                        column: x => x.PhaseOneSignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosChecklistItems", x => x.Id);
                    table.CheckConstraint("CK_OosChecklistItems_Phase", "\"Phase\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_OosChecklistItems_OosInvestigationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "OosInvestigationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosDeviationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigationId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AddressedComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AddressedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AddressedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosDeviationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosDeviationRequests_OosInvestigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "OosInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosDeviationRequests_Users_AddressedByUserId",
                        column: x => x.AddressedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosDeviationRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosInvestigationEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigationId = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ChecklistItemKey = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    Answer = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ParentEntryId = table.Column<int>(type: "integer", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosInvestigationEntries", x => x.Id);
                    table.CheckConstraint("CK_OosInvestigationEntries_ChecklistComment", "\"Kind\" <> 1 OR char_length(\"Text\") <= 1000");
                    table.CheckConstraint("CK_OosInvestigationEntries_Phase", "\"Phase\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_OosInvestigationEntries_OosInvestigationEntries_ParentEntry~",
                        column: x => x.ParentEntryId,
                        principalTable: "OosInvestigationEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationEntries_OosInvestigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "OosInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationEntries_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosInvestigationReopenings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigationId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PreviousConclusion = table.Column<int>(type: "integer", nullable: false),
                    PreviousConclusionSignatureId = table.Column<int>(type: "integer", nullable: false),
                    ReopenedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReopenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosInvestigationReopenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosInvestigationReopenings_ElectronicSignatures_PreviousCon~",
                        column: x => x.PreviousConclusionSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationReopenings_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationReopenings_OosInvestigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "OosInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationReopenings_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosInvestigationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigationId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    OutcomeAtOpening = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResultSummaryAtOpening = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsInvalidated = table.Column<bool>(type: "boolean", nullable: false),
                    InvalidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosInvestigationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosInvestigationResults_OosInvestigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "OosInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationResults_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosRetestPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigationId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RetestCount = table.Column<int>(type: "integer", nullable: false),
                    TestOrderIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AnalystUserIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    JudgingRule = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AmendmentReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByUserId = table.Column<int>(type: "integer", nullable: true),
                    SignatureId = table.Column<int>(type: "integer", nullable: true),
                    SamplesCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosRetestPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosRetestPlans_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosRetestPlans_OosInvestigations_InvestigationId",
                        column: x => x.InvestigationId,
                        principalTable: "OosInvestigations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosRetestPlans_Users_SignedByUserId",
                        column: x => x.SignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OosRetestPlanSamples",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosRetestPlanSamples", x => new { x.PlanId, x.SampleId });
                    table.ForeignKey(
                        name: "FK_OosRetestPlanSamples_OosRetestPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "OosRetestPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosRetestPlanSamples_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "OosCauseCategories",
                columns: new[] { "Id", "IsActive", "IsNoCauseFound", "Name" },
                values: new object[,]
                {
                    { 1, true, false, "Analyst error" },
                    { 2, true, false, "Equipment" },
                    { 3, true, false, "Method" },
                    { 4, true, false, "Sample" },
                    { 5, true, false, "Media or reagent" },
                    { 6, true, false, "Environment" },
                    { 7, true, true, "No cause found" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OosCauseCategories_IsNoCauseFound",
                table: "OosCauseCategories",
                column: "IsNoCauseFound",
                unique: true,
                filter: "\"IsNoCauseFound\"");

            migrationBuilder.CreateIndex(
                name: "IX_OosCauseCategories_Name",
                table: "OosCauseCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosChecklistItems_TemplateId_Key",
                table: "OosChecklistItems",
                columns: new[] { "TemplateId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosDeviationRequests_AddressedByUserId",
                table: "OosDeviationRequests",
                column: "AddressedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosDeviationRequests_InvestigationId",
                table: "OosDeviationRequests",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_OosDeviationRequests_RequestedByUserId",
                table: "OosDeviationRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationEntries_InvestigationId",
                table: "OosInvestigationEntries",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationEntries_ParentEntryId",
                table: "OosInvestigationEntries",
                column: "ParentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationEntries_RecordedByUserId",
                table: "OosInvestigationEntries",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationReopenings_InvestigationId",
                table: "OosInvestigationReopenings",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationReopenings_PreviousConclusionSignatureId",
                table: "OosInvestigationReopenings",
                column: "PreviousConclusionSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationReopenings_ReopenedByUserId",
                table: "OosInvestigationReopenings",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationReopenings_SignatureId",
                table: "OosInvestigationReopenings",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationResults_InvestigationId_TestOrderId",
                table: "OosInvestigationResults",
                columns: new[] { "InvestigationId", "TestOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationResults_TestOrderId",
                table: "OosInvestigationResults",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_ConcludedByUserId",
                table: "OosInvestigations",
                column: "ConcludedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_ConclusionSignatureId",
                table: "OosInvestigations",
                column: "ConclusionSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_Number",
                table: "OosInvestigations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_OpenedByUserId",
                table: "OosInvestigations",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_PhaseOneCauseCategoryId",
                table: "OosInvestigations",
                column: "PhaseOneCauseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_PhaseOneSignatureId",
                table: "OosInvestigations",
                column: "PhaseOneSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_PhaseOneSignedByUserId",
                table: "OosInvestigations",
                column: "PhaseOneSignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_RootCauseCategoryId",
                table: "OosInvestigations",
                column: "RootCauseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_SampleId_SectionId_Kind",
                table: "OosInvestigations",
                columns: new[] { "SampleId", "SectionId", "Kind" },
                unique: true,
                filter: "\"Status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigations_SectionId",
                table: "OosInvestigations",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationTemplates_SectionId_Kind",
                table: "OosInvestigationTemplates",
                columns: new[] { "SectionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlans_InvestigationId_Revision",
                table: "OosRetestPlans",
                columns: new[] { "InvestigationId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlans_SignatureId",
                table: "OosRetestPlans",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlans_SignedByUserId",
                table: "OosRetestPlans",
                column: "SignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosRetestPlanSamples_SampleId",
                table: "OosRetestPlanSamples",
                column: "SampleId");

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_oos_investigation_result_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF NOT OLD."IsInvalidated" AND NEW."IsInvalidated"
                           AND (to_jsonb(NEW) - 'IsInvalidated' - 'InvalidatedAtUtc')
                               = (to_jsonb(OLD) - 'IsInvalidated' - 'InvalidatedAtUtc') THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'OOS investigation result % is immutable except for one-way invalidation.', OLD."Id";
                END;
                $$;

                CREATE FUNCTION prevent_oos_retest_plan_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'OOS retest plan % cannot be deleted.', OLD."Id";
                    END IF;
                    IF OLD."SignedAtUtc" IS NOT NULL
                       AND (to_jsonb(NEW) - 'SamplesCreatedAtUtc')
                           IS DISTINCT FROM (to_jsonb(OLD) - 'SamplesCreatedAtUtc') THEN
                        RAISE EXCEPTION 'OOS retest plan % is signed and immutable.', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_oosinvestigations_immutable
                BEFORE DELETE ON "OosInvestigations"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_oosinvestigations_no_truncate
                BEFORE TRUNCATE ON "OosInvestigations"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_oosinvestigationentries_immutable
                BEFORE UPDATE OR DELETE ON "OosInvestigationEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_oosinvestigationentries_no_truncate
                BEFORE TRUNCATE ON "OosInvestigationEntries"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_oosinvestigationresults_immutable
                BEFORE UPDATE OR DELETE ON "OosInvestigationResults"
                FOR EACH ROW EXECUTE FUNCTION prevent_oos_investigation_result_modification();

                CREATE TRIGGER trg_oosinvestigationresults_no_truncate
                BEFORE TRUNCATE ON "OosInvestigationResults"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_oosinvestigationreopenings_immutable
                BEFORE UPDATE OR DELETE ON "OosInvestigationReopenings"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_oosinvestigationreopenings_no_truncate
                BEFORE TRUNCATE ON "OosInvestigationReopenings"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_oosretestplans_immutable
                BEFORE UPDATE OR DELETE ON "OosRetestPlans"
                FOR EACH ROW EXECUTE FUNCTION prevent_oos_retest_plan_modification();

                CREATE TRIGGER trg_oosretestplans_no_truncate
                BEFORE TRUNCATE ON "OosRetestPlans"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_oosretestplans_no_truncate ON "OosRetestPlans";
                DROP TRIGGER IF EXISTS trg_oosretestplans_immutable ON "OosRetestPlans";
                DROP TRIGGER IF EXISTS trg_oosinvestigationreopenings_no_truncate ON "OosInvestigationReopenings";
                DROP TRIGGER IF EXISTS trg_oosinvestigationreopenings_immutable ON "OosInvestigationReopenings";
                DROP TRIGGER IF EXISTS trg_oosinvestigationresults_no_truncate ON "OosInvestigationResults";
                DROP TRIGGER IF EXISTS trg_oosinvestigationresults_immutable ON "OosInvestigationResults";
                DROP TRIGGER IF EXISTS trg_oosinvestigationentries_no_truncate ON "OosInvestigationEntries";
                DROP TRIGGER IF EXISTS trg_oosinvestigationentries_immutable ON "OosInvestigationEntries";
                DROP TRIGGER IF EXISTS trg_oosinvestigations_no_truncate ON "OosInvestigations";
                DROP TRIGGER IF EXISTS trg_oosinvestigations_immutable ON "OosInvestigations";
                DROP FUNCTION IF EXISTS prevent_oos_retest_plan_modification();
                DROP FUNCTION IF EXISTS prevent_oos_investigation_result_modification();
                """);

            migrationBuilder.DropTable(
                name: "OosChecklistItems");

            migrationBuilder.DropTable(
                name: "OosDeviationRequests");

            migrationBuilder.DropTable(
                name: "OosInvestigationEntries");

            migrationBuilder.DropTable(
                name: "OosInvestigationReopenings");

            migrationBuilder.DropTable(
                name: "OosInvestigationResults");

            migrationBuilder.DropTable(
                name: "OosRetestPlanSamples");

            migrationBuilder.DropTable(
                name: "OosInvestigationTemplates");

            migrationBuilder.DropTable(
                name: "OosRetestPlans");

            migrationBuilder.DropTable(
                name: "OosInvestigations");

            migrationBuilder.DropTable(
                name: "OosCauseCategories");
        }
    }
}
