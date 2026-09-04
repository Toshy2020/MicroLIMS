using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRelease1cTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentRoleCurricula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRoleCurricula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRoleCurricula_DocumentDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DocumentDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRoleCurricula_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRoleCurricula_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTrainingConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: true),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: true),
                    RequiresReading = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresRetrainingOnRevision = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultGracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultAcknowledgementStatement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EscalationDaysBeforeDue = table.Column<int>(type: "integer", nullable: false),
                    EscalationDaysAfterDue = table.Column<int>(type: "integer", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTrainingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingConfigurations_DocumentMasters_DocumentMaste~",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingConfigurations_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingConfigurations_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRoleCurriculumItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentRoleCurriculumId = table.Column<int>(type: "integer", nullable: false),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    CustomGracePeriodDays = table.Column<int>(type: "integer", nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRoleCurriculumItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRoleCurriculumItems_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRoleCurriculumItems_DocumentRoleCurricula_DocumentRo~",
                        column: x => x.DocumentRoleCurriculumId,
                        principalTable: "DocumentRoleCurricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTrainingAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    AssignedUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatementText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AcknowledgedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceAssignmentId = table.Column<int>(type: "integer", nullable: true),
                    AssignmentReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTrainingAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_DocumentRevisions_DocumentRevisi~",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_DocumentTrainingAssignments_Sour~",
                        column: x => x.SourceAssignmentId,
                        principalTable: "DocumentTrainingAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_Users_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTrainingAssignments_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoleCurricula_CreatedByUserId",
                table: "DocumentRoleCurricula",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoleCurricula_DepartmentId",
                table: "DocumentRoleCurricula",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoleCurricula_IsActive",
                table: "DocumentRoleCurricula",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoleCurricula_RoleId",
                table: "DocumentRoleCurricula",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoleCurriculumItems_DocumentMasterId",
                table: "DocumentRoleCurriculumItems",
                column: "DocumentMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRoleCurriculumItems_DocumentRoleCurriculumId_Documen~",
                table: "DocumentRoleCurriculumItems",
                columns: new[] { "DocumentRoleCurriculumId", "DocumentMasterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_AcknowledgedByUserId",
                table: "DocumentTrainingAssignments",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_AssignedUserId",
                table: "DocumentTrainingAssignments",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_AssignedUserId_DocumentRevisionI~",
                table: "DocumentTrainingAssignments",
                columns: new[] { "AssignedUserId", "DocumentRevisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_CreatedByUserId",
                table: "DocumentTrainingAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_DocumentMasterId",
                table: "DocumentTrainingAssignments",
                column: "DocumentMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_DocumentRevisionId",
                table: "DocumentTrainingAssignments",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_DocumentRevisionId_AssignedUserI~",
                table: "DocumentTrainingAssignments",
                columns: new[] { "DocumentRevisionId", "AssignedUserId", "AssignmentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_DueDateUtc",
                table: "DocumentTrainingAssignments",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_ModifiedByUserId",
                table: "DocumentTrainingAssignments",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_SourceAssignmentId",
                table: "DocumentTrainingAssignments",
                column: "SourceAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingAssignments_Status",
                table: "DocumentTrainingAssignments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingConfigurations_DocumentMasterId",
                table: "DocumentTrainingConfigurations",
                column: "DocumentMasterId",
                unique: true,
                filter: "\"DocumentMasterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingConfigurations_DocumentTypeId",
                table: "DocumentTrainingConfigurations",
                column: "DocumentTypeId",
                unique: true,
                filter: "\"DocumentTypeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTrainingConfigurations_ModifiedByUserId",
                table: "DocumentTrainingConfigurations",
                column: "ModifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentRoleCurriculumItems");

            migrationBuilder.DropTable(
                name: "DocumentTrainingAssignments");

            migrationBuilder.DropTable(
                name: "DocumentTrainingConfigurations");

            migrationBuilder.DropTable(
                name: "DocumentRoleCurricula");
        }
    }
}
