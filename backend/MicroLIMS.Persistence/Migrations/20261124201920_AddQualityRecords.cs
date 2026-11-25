using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QualityChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecklistTemplates_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityClassifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityClassifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QualitySectionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    ChangeControlRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualitySectionSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualitySectionSettings_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecklistItems_QualityChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "QualityChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityDueRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ClassificationId = table.Column<int>(type: "integer", nullable: true),
                    WorkingDays = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityDueRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityDueRules_QualityClassifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "QualityClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    RaisedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RaisedBySystemProcess = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RaisedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    ClassificationId = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityRecords_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityRecords_QualityClassifications_ClassificationId",
                        column: x => x.ClassificationId,
                        principalTable: "QualityClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityRecords_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityRecords_Users_RaisedByUserId",
                        column: x => x.RaisedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CapaDetails",
                columns: table => new
                {
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceRecordId = table.Column<int>(type: "integer", nullable: true),
                    EffectivenessCriteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EffectivenessMethod = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EffectivenessPlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectivenessResult = table.Column<int>(type: "integer", nullable: true),
                    EffectivenessFindings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SuccessorCapaId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapaDetails", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_CapaDetails_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapaDetails_QualityRecords_SourceRecordId",
                        column: x => x.SourceRecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapaDetails_QualityRecords_SuccessorCapaId",
                        column: x => x.SuccessorCapaId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeControlDetails",
                columns: table => new
                {
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ImpactSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PostChangeReview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UnchangedJustification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeControlDetails", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_ChangeControlDetails_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintDetails",
                columns: table => new
                {
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    LotText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ComplaintText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WhatToTest = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Findings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    ConclusionSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintDetails", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_ComplaintDetails_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplaintDetails_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviationDetails",
                columns: table => new
                {
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImmediateAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ImpactAssessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RootCauseCategoryId = table.Column<int>(type: "integer", nullable: true),
                    RootCauseDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProposedActions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CapaRequired = table.Column<bool>(type: "boolean", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationDetails", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_DeviationDetails_OosCauseCategories_RootCauseCategoryId",
                        column: x => x.RootCauseCategoryId,
                        principalTable: "OosCauseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviationDetails_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityAttachments_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityAttachments_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecklistAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    ItemKey = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    ItemText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Answer = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnsweredByUserId = table.Column<int>(type: "integer", nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecklistAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecklistAnswers_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityChecklistAnswers_Users_AnsweredByUserId",
                        column: x => x.AnsweredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityComments_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityRecordLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: true),
                    NewRecordCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsAffectedByChange = table.Column<bool>(type: "boolean", nullable: false),
                    AddedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangeAuditLogId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityRecordLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityRecordLinks_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityRecordLinks_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityRecordSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityRecordSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityRecordSteps_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityRecordSteps_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityRecordSteps_Users_SignedByUserId",
                        column: x => x.SignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ResponsibleUserId = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DoneAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DoneByUserId = table.Column<int>(type: "integer", nullable: true),
                    Evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EvidenceLinkId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityActions_QualityRecordLinks_EvidenceLinkId",
                        column: x => x.EvidenceLinkId,
                        principalTable: "QualityRecordLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityActions_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityActions_Users_DoneByUserId",
                        column: x => x.DoneByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityActions_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityExtensionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    ActionId = table.Column<int>(type: "integer", nullable: true),
                    RequestedDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityExtensionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityExtensionRequests_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityExtensionRequests_QualityActions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "QualityActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityExtensionRequests_QualityRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "QualityRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityExtensionRequests_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityExtensionRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "QualityClassifications",
                columns: new[] { "Id", "IsActive", "Name", "Order" },
                values: new object[,]
                {
                    { 1, true, "Minor", 1 },
                    { 2, true, "Major", 2 },
                    { 3, true, "Critical", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapaDetails_SourceRecordId",
                table: "CapaDetails",
                column: "SourceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CapaDetails_SuccessorCapaId",
                table: "CapaDetails",
                column: "SuccessorCapaId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintDetails_ItemId",
                table: "ComplaintDetails",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviationDetails_RootCauseCategoryId",
                table: "DeviationDetails",
                column: "RootCauseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityActions_DoneByUserId",
                table: "QualityActions",
                column: "DoneByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityActions_EvidenceLinkId",
                table: "QualityActions",
                column: "EvidenceLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityActions_RecordId_DueDate",
                table: "QualityActions",
                columns: new[] { "RecordId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityActions_ResponsibleUserId",
                table: "QualityActions",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityAttachments_RecordId",
                table: "QualityAttachments",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityAttachments_UploadedByUserId",
                table: "QualityAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityAttachments_VoidedByUserId",
                table: "QualityAttachments",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistAnswers_AnsweredByUserId",
                table: "QualityChecklistAnswers",
                column: "AnsweredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistAnswers_RecordId_ItemKey_AnsweredAtUtc",
                table: "QualityChecklistAnswers",
                columns: new[] { "RecordId", "ItemKey", "AnsweredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistItems_TemplateId_Key",
                table: "QualityChecklistItems",
                columns: new[] { "TemplateId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistTemplates_SectionId",
                table: "QualityChecklistTemplates",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecklistTemplates_Type_SectionId",
                table: "QualityChecklistTemplates",
                columns: new[] { "Type", "SectionId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_QualityClassifications_Name",
                table: "QualityClassifications",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityComments_AuthorUserId",
                table: "QualityComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityComments_RecordId",
                table: "QualityComments",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityDueRules_ClassificationId",
                table: "QualityDueRules",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityDueRules_Type_ClassificationId",
                table: "QualityDueRules",
                columns: new[] { "Type", "ClassificationId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_QualityExtensionRequests_ActionId",
                table: "QualityExtensionRequests",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityExtensionRequests_DecidedByUserId",
                table: "QualityExtensionRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityExtensionRequests_RecordId",
                table: "QualityExtensionRequests",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityExtensionRequests_RequestedByUserId",
                table: "QualityExtensionRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityExtensionRequests_SignatureId",
                table: "QualityExtensionRequests",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecordLinks_AddedByUserId",
                table: "QualityRecordLinks",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecordLinks_RecordId_TargetType_TargetId_NewRecordCo~",
                table: "QualityRecordLinks",
                columns: new[] { "RecordId", "TargetType", "TargetId", "NewRecordCode" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecordLinks_TargetType_TargetId",
                table: "QualityRecordLinks",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecords_ClassificationId",
                table: "QualityRecords",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecords_Number",
                table: "QualityRecords",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecords_OwnerUserId",
                table: "QualityRecords",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecords_RaisedByUserId",
                table: "QualityRecords",
                column: "RaisedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecords_SectionId",
                table: "QualityRecords",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecords_Type_RaisedAtUtc",
                table: "QualityRecords",
                columns: new[] { "Type", "RaisedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecordSteps_RecordId_SignedAtUtc",
                table: "QualityRecordSteps",
                columns: new[] { "RecordId", "SignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecordSteps_SignatureId",
                table: "QualityRecordSteps",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityRecordSteps_SignedByUserId",
                table: "QualityRecordSteps",
                column: "SignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QualitySectionSettings_SectionId",
                table: "QualitySectionSettings",
                column: "SectionId",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_quality_record_link_delete()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "QualityRecordSteps" WHERE "RecordId" = OLD."RecordId") THEN
                        RAISE EXCEPTION 'Quality record link % cannot be deleted after a step has been signed.', OLD."Id";
                    END IF;
                    RETURN OLD;
                END;
                $$;

                CREATE TRIGGER trg_qualityrecordsteps_immutable
                BEFORE UPDATE OR DELETE ON "QualityRecordSteps"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_qualityrecordsteps_no_truncate
                BEFORE TRUNCATE ON "QualityRecordSteps"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_qualitychecklistanswers_immutable
                BEFORE UPDATE OR DELETE ON "QualityChecklistAnswers"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_qualitychecklistanswers_no_truncate
                BEFORE TRUNCATE ON "QualityChecklistAnswers"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_qualityrecords_immutable
                BEFORE DELETE ON "QualityRecords"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_qualityrecords_no_truncate
                BEFORE TRUNCATE ON "QualityRecords"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_qualityactions_immutable
                BEFORE DELETE ON "QualityActions"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_qualityactions_no_truncate
                BEFORE TRUNCATE ON "QualityActions"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_qualityrecordlinks_immutable
                BEFORE DELETE ON "QualityRecordLinks"
                FOR EACH ROW EXECUTE FUNCTION prevent_quality_record_link_delete();

                CREATE TRIGGER trg_qualityrecordlinks_no_truncate
                BEFORE TRUNCATE ON "QualityRecordLinks"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_qualityattachments_immutable
                BEFORE DELETE ON "QualityAttachments"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

                CREATE TRIGGER trg_qualityattachments_no_truncate
                BEFORE TRUNCATE ON "QualityAttachments"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_qualityattachments_no_truncate ON "QualityAttachments";
                DROP TRIGGER IF EXISTS trg_qualityattachments_immutable ON "QualityAttachments";
                DROP TRIGGER IF EXISTS trg_qualityrecordlinks_no_truncate ON "QualityRecordLinks";
                DROP TRIGGER IF EXISTS trg_qualityrecordlinks_immutable ON "QualityRecordLinks";
                DROP TRIGGER IF EXISTS trg_qualityactions_no_truncate ON "QualityActions";
                DROP TRIGGER IF EXISTS trg_qualityactions_immutable ON "QualityActions";
                DROP TRIGGER IF EXISTS trg_qualityrecords_no_truncate ON "QualityRecords";
                DROP TRIGGER IF EXISTS trg_qualityrecords_immutable ON "QualityRecords";
                DROP TRIGGER IF EXISTS trg_qualitychecklistanswers_no_truncate ON "QualityChecklistAnswers";
                DROP TRIGGER IF EXISTS trg_qualitychecklistanswers_immutable ON "QualityChecklistAnswers";
                DROP TRIGGER IF EXISTS trg_qualityrecordsteps_no_truncate ON "QualityRecordSteps";
                DROP TRIGGER IF EXISTS trg_qualityrecordsteps_immutable ON "QualityRecordSteps";
                DROP FUNCTION IF EXISTS prevent_quality_record_link_delete();
                """);

            migrationBuilder.DropTable(
                name: "CapaDetails");

            migrationBuilder.DropTable(
                name: "ChangeControlDetails");

            migrationBuilder.DropTable(
                name: "ComplaintDetails");

            migrationBuilder.DropTable(
                name: "DeviationDetails");

            migrationBuilder.DropTable(
                name: "QualityAttachments");

            migrationBuilder.DropTable(
                name: "QualityChecklistAnswers");

            migrationBuilder.DropTable(
                name: "QualityChecklistItems");

            migrationBuilder.DropTable(
                name: "QualityComments");

            migrationBuilder.DropTable(
                name: "QualityDueRules");

            migrationBuilder.DropTable(
                name: "QualityExtensionRequests");

            migrationBuilder.DropTable(
                name: "QualityRecordSteps");

            migrationBuilder.DropTable(
                name: "QualitySectionSettings");

            migrationBuilder.DropTable(
                name: "QualityChecklistTemplates");

            migrationBuilder.DropTable(
                name: "QualityActions");

            migrationBuilder.DropTable(
                name: "QualityRecordLinks");

            migrationBuilder.DropTable(
                name: "QualityRecords");

            migrationBuilder.DropTable(
                name: "QualityClassifications");
        }
    }
}
