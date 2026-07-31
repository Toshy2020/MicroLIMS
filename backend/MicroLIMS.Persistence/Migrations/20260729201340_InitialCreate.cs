using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityName = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: true),
                    ControlNumber = table.Column<string>(type: "text", nullable: true),
                    SampleReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    MediaLotNumber = table.Column<string>(type: "text", nullable: true),
                    ReferenceStrainCode = table.Column<string>(type: "text", nullable: true),
                    CryovialCode = table.Column<string>(type: "text", nullable: true),
                    SampleId = table.Column<int>(type: "integer", nullable: true),
                    TestOrderId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CausesOfTesting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CausesOfTesting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<string>(type: "text", nullable: false),
                    TestingFrequency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    SetPointTemperature = table.Column<decimal>(type: "numeric", nullable: true),
                    CalibrationDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    SopNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<int>(type: "integer", nullable: false),
                    IncubationMinHours = table.Column<int>(type: "integer", nullable: false),
                    IncubationMaxHours = table.Column<int>(type: "integer", nullable: false),
                    RequiredTemperatureMin = table.Column<decimal>(type: "numeric", nullable: false),
                    RequiredTemperatureMax = table.Column<decimal>(type: "numeric", nullable: false),
                    ApprovedTestCodes = table.Column<List<string>>(type: "text[]", nullable: false),
                    RecoveryPercentMin = table.Column<decimal>(type: "numeric", nullable: true),
                    RecoveryPercentMax = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Neutralizers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Neutralizers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceStrains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    AtccNumber = table.Column<string>(type: "text", nullable: false),
                    PassageNumber = table.Column<int>(type: "integer", nullable: false),
                    NumberOfDiscs = table.Column<int>(type: "integer", nullable: false),
                    ManufacturerName = table.Column<string>(type: "text", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StorageCondition = table.Column<string>(type: "text", nullable: false),
                    PhysicalCheckText = table.Column<string>(type: "text", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ReceivedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceStrains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PdfPath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterSamplingPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    AssignedTestCodes = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterSamplingPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    GradeClassification = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SampleTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleTests_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Specifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    AlertLimit = table.Column<string>(type: "text", nullable: false),
                    ActionLimit = table.Column<string>(type: "text", nullable: false),
                    SpecLimit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Specifications_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MachineParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MachineId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineParts_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiluentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RequiresBatchTracking = table.Column<bool>(type: "boolean", nullable: false),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiluentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiluentTypes_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExpectedIndicationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpectedIndicationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpectedIndicationResults_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufacturerLot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManufacturerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalVolume = table.Column<string>(type: "text", nullable: false),
                    AutoclaveEquipmentId = table.Column<int>(type: "integer", nullable: true),
                    AutoclaveProgram = table.Column<string>(type: "text", nullable: false),
                    LoadType = table.Column<string>(type: "text", nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric", nullable: false),
                    CycleTime = table.Column<int>(type: "integer", nullable: false),
                    CycleNumber = table.Column<int>(type: "integer", nullable: false),
                    Ph = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GptStage = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Media_Equipment_AutoclaveEquipmentId",
                        column: x => x.AutoclaveEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Media_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cryovials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ReferenceStrainId = table.Column<int>(type: "integer", nullable: false),
                    PassageNumber = table.Column<int>(type: "integer", nullable: false),
                    ManufacturerName = table.Column<string>(type: "text", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NumberOfVialsPrepared = table.Column<int>(type: "integer", nullable: false),
                    StorageCondition = table.Column<string>(type: "text", nullable: false),
                    PhysicalCheckText = table.Column<string>(type: "text", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ThawedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDestroyed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cryovials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cryovials_ReferenceStrains_ReferenceStrainId",
                        column: x => x.ReferenceStrainId,
                        principalTable: "ReferenceStrains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    WaterSamplingPointId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    MachineId = table.Column<int>(type: "integer", nullable: true),
                    ProductionStage = table.Column<string>(type: "text", nullable: true),
                    CauseOfTestingId = table.Column<int>(type: "integer", nullable: false),
                    SampleQuantity = table.Column<string>(type: "text", nullable: true),
                    SampledBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BatchNumber = table.Column<string>(type: "text", nullable: true),
                    ControlNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MfgDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreparationStatus = table.Column<int>(type: "integer", nullable: false),
                    StorageCondition = table.Column<string>(type: "text", nullable: true),
                    StorageTimeHours = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Samples_CausesOfTesting_CauseOfTestingId",
                        column: x => x.CauseOfTestingId,
                        principalTable: "CausesOfTesting",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_WaterSamplingPoints_WaterSamplingPointId",
                        column: x => x.WaterSamplingPointId,
                        principalTable: "WaterSamplingPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SamplingConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WaterSamplingPointId = table.Column<int>(type: "integer", nullable: false),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    AlertLimit = table.Column<string>(type: "text", nullable: false),
                    ActionLimit = table.Column<string>(type: "text", nullable: false),
                    SpecLimit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SamplingConfigurations_WaterSamplingPoints_WaterSamplingPoi~",
                        column: x => x.WaterSamplingPointId,
                        principalTable: "WaterSamplingPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EMRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    SamplingPositions = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EMRooms_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomTestConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    TestType = table.Column<string>(type: "text", nullable: false),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    AlertLimit = table.Column<string>(type: "text", nullable: false),
                    ActionLimit = table.Column<string>(type: "text", nullable: false),
                    SpecLimit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTestConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomTestConfigurations_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MachinePartConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MachinePartId = table.Column<int>(type: "integer", nullable: false),
                    TestType = table.Column<string>(type: "text", nullable: false),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    AlertLimit = table.Column<string>(type: "text", nullable: false),
                    ActionLimit = table.Column<string>(type: "text", nullable: false),
                    SpecLimit = table.Column<string>(type: "text", nullable: false),
                    IsPathogenTest = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachinePartConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachinePartConfigurations_MachineParts_MachinePartId",
                        column: x => x.MachinePartId,
                        principalTable: "MachineParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GptChallengeResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    Panel = table.Column<string>(type: "text", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    CryovialId = table.Column<int>(type: "integer", nullable: true),
                    Atcc = table.Column<string>(type: "text", nullable: true),
                    InitialInoculum = table.Column<string>(type: "text", nullable: true),
                    OldMediaResult = table.Column<int>(type: "integer", nullable: true),
                    NewMediaResult = table.Column<int>(type: "integer", nullable: true),
                    RecoveryPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    NegativeControlGrowth = table.Column<bool>(type: "boolean", nullable: false),
                    TurbidResult = table.Column<string>(type: "text", nullable: true),
                    ObservationText = table.Column<string>(type: "text", nullable: true),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GptChallengeResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GptChallengeResults_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GptChallengeResults_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityConfirmationEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceStrainId = table.Column<int>(type: "integer", nullable: true),
                    CryovialId = table.Column<int>(type: "integer", nullable: true),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    IncubatorEquipmentId = table.Column<int>(type: "integer", nullable: false),
                    IncubationStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IncubationEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservationText = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityConfirmationEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdentityConfirmationEntry_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IdentityConfirmationEntry_Equipment_IncubatorEquipmentId",
                        column: x => x.IncubatorEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IdentityConfirmationEntry_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IdentityConfirmationEntry_ReferenceStrains_ReferenceStrainId",
                        column: x => x.ReferenceStrainId,
                        principalTable: "ReferenceStrains",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PassageEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CryovialId = table.Column<int>(type: "integer", nullable: false),
                    PassageNumber = table.Column<int>(type: "integer", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassageEvents_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SamplePreparations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Technique = table.Column<string>(type: "text", nullable: false),
                    FiltrationVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    WashingVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    DiluentTypeId = table.Column<int>(type: "integer", nullable: false),
                    DiluentMediaId = table.Column<int>(type: "integer", nullable: true),
                    NeutralizerId = table.Column<int>(type: "integer", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplePreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SamplePreparations_DiluentTypes_DiluentTypeId",
                        column: x => x.DiluentTypeId,
                        principalTable: "DiluentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SamplePreparations_Media_DiluentMediaId",
                        column: x => x.DiluentMediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SamplePreparations_Neutralizers_NeutralizerId",
                        column: x => x.NeutralizerId,
                        principalTable: "Neutralizers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SamplePreparations_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    TestCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    AssignedAnalystId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestOrders_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incubations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incubations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incubations_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaUsages_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaUsages_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PathogenObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    GrowthObserved = table.Column<bool>(type: "boolean", nullable: false),
                    ObservedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PathogenObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PathogenObservations_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    RawValue = table.Column<string>(type: "text", nullable: false),
                    InterpretedValue = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Results_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomMonitorings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    SamplingPosition = table.Column<string>(type: "text", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    Step1Count = table.Column<int>(type: "integer", nullable: false),
                    Step2Count = table.Column<int>(type: "integer", nullable: false),
                    IsOutOfTrend = table.Column<bool>(type: "boolean", nullable: false),
                    SampledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMonitorings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomMonitorings_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMonitorings_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    FromStep = table.Column<int>(type: "integer", nullable: false),
                    ToStep = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowHistories_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cryovials_ReferenceStrainId",
                table: "Cryovials",
                column: "ReferenceStrainId");

            migrationBuilder.CreateIndex(
                name: "IX_DiluentTypes_MediaTypeId",
                table: "DiluentTypes",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EMRooms_RoomId",
                table: "EMRooms",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpectedIndicationResults_MediaTypeId",
                table: "ExpectedIndicationResults",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GptChallengeResults_CryovialId",
                table: "GptChallengeResults",
                column: "CryovialId");

            migrationBuilder.CreateIndex(
                name: "IX_GptChallengeResults_MediaId",
                table: "GptChallengeResults",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityConfirmationEntry_CryovialId",
                table: "IdentityConfirmationEntry",
                column: "CryovialId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityConfirmationEntry_IncubatorEquipmentId",
                table: "IdentityConfirmationEntry",
                column: "IncubatorEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityConfirmationEntry_MediaId",
                table: "IdentityConfirmationEntry",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityConfirmationEntry_ReferenceStrainId",
                table: "IdentityConfirmationEntry",
                column: "ReferenceStrainId");

            migrationBuilder.CreateIndex(
                name: "IX_Incubations_TestOrderId",
                table: "Incubations",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Code",
                table: "Items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachinePartConfigurations_MachinePartId",
                table: "MachinePartConfigurations",
                column: "MachinePartId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineParts_MachineId",
                table: "MachineParts",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_AutoclaveEquipmentId",
                table: "Media",
                column: "AutoclaveEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_LotNumber",
                table: "Media",
                column: "LotNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_MediaTypeId",
                table: "Media",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaUsages_MediaId",
                table: "MediaUsages",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaUsages_TestOrderId",
                table: "MediaUsages",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PassageEvents_CryovialId",
                table: "PassageEvents",
                column: "CryovialId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PathogenObservations_TestOrderId",
                table: "PathogenObservations",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_RoleId",
                table: "Permissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_ReportId",
                table: "ReportSnapshots",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Results_TestOrderId",
                table: "Results",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMonitorings_RoomId",
                table: "RoomMonitorings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomMonitorings_TestOrderId",
                table: "RoomMonitorings",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_DepartmentId",
                table: "Rooms",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTestConfigurations_RoomId",
                table: "RoomTestConfigurations",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentMediaId",
                table: "SamplePreparations",
                column: "DiluentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentTypeId",
                table: "SamplePreparations",
                column: "DiluentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_NeutralizerId",
                table: "SamplePreparations",
                column: "NeutralizerId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_SampleId",
                table: "SamplePreparations",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_CauseOfTestingId",
                table: "Samples",
                column: "CauseOfTestingId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_DepartmentId",
                table: "Samples",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ItemId",
                table: "Samples",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_MachineId",
                table: "Samples",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ReferenceNumber",
                table: "Samples",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_WaterSamplingPointId",
                table: "Samples",
                column: "WaterSamplingPointId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleTests_ItemId",
                table: "SampleTests",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplingConfigurations_WaterSamplingPointId",
                table: "SamplingConfigurations",
                column: "WaterSamplingPointId");

            migrationBuilder.CreateIndex(
                name: "IX_Specifications_ItemId",
                table: "Specifications",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrders_SampleId",
                table: "TestOrders",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowHistories_TestOrderId",
                table: "WorkflowHistories",
                column: "TestOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EMRooms");

            migrationBuilder.DropTable(
                name: "ExpectedIndicationResults");

            migrationBuilder.DropTable(
                name: "GptChallengeResults");

            migrationBuilder.DropTable(
                name: "IdentityConfirmationEntry");

            migrationBuilder.DropTable(
                name: "Incubations");

            migrationBuilder.DropTable(
                name: "LoginHistories");

            migrationBuilder.DropTable(
                name: "MachinePartConfigurations");

            migrationBuilder.DropTable(
                name: "MediaUsages");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "PassageEvents");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "PathogenObservations");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ReportSnapshots");

            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "RoomMonitorings");

            migrationBuilder.DropTable(
                name: "RoomTestConfigurations");

            migrationBuilder.DropTable(
                name: "SamplePreparations");

            migrationBuilder.DropTable(
                name: "SampleTests");

            migrationBuilder.DropTable(
                name: "SamplingConfigurations");

            migrationBuilder.DropTable(
                name: "Specifications");

            migrationBuilder.DropTable(
                name: "WorkflowHistories");

            migrationBuilder.DropTable(
                name: "MachineParts");

            migrationBuilder.DropTable(
                name: "Cryovials");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "DiluentTypes");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "Neutralizers");

            migrationBuilder.DropTable(
                name: "TestOrders");

            migrationBuilder.DropTable(
                name: "ReferenceStrains");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "MediaTypes");

            migrationBuilder.DropTable(
                name: "Samples");

            migrationBuilder.DropTable(
                name: "CausesOfTesting");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "WaterSamplingPoints");
        }
    }
}
