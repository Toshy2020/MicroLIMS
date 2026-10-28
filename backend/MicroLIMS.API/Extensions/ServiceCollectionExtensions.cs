using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Amazon.Runtime;
using Amazon.S3;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Application.Validators;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Application.Services.InstrumentImport;
using MicroLIMS.Infrastructure.InstrumentImport;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Application.Abstractions.Notifications;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Application.Abstractions.Word;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Persistence.Diagnostics;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Persistence.Repositories;

namespace MicroLIMS.API.Extensions;

// Central place all DI registrations live - Program.cs stays thin and
// just calls builder.Services.AddApplicationServices(config).
public static class ServiceCollectionExtensions
{
    // The DbContext, and the interface the Application layer asks for. Both
    // resolve to the same request-scoped instance, which AuditMiddleware
    // stamps with the current user.
    public static IServiceCollection AddMicroLimsDbContext(this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        services.AddDbContext<MicroLimsDbContext>(configure);
        services.AddScoped<IMicroLimsDbContext>(sp => sp.GetRequiredService<MicroLimsDbContext>());
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        // Workflows (the frozen laboratory logic - now real state machines)
        services.AddScoped<IProductWorkflowEngine, ProductWorkflowEngine>();
        services.AddScoped<IWaterWorkflowEngine, WaterWorkflowEngine>();
        services.AddScoped<IEMWorkflowEngine, EMWorkflowEngine>();
        services.AddScoped<IAfterCleaningWorkflowEngine, AfterCleaningWorkflowEngine>();
        services.AddScoped<IMediaEvaluationEngine, MediaEvaluationEngine>();
        services.AddScoped<ITestWorkflowEngine, TestWorkflowEngine>();
        services.AddScoped<TitrationContextService>();

        // Application services
        services.AddScoped<IReceivingService, ReceivingService>();
        services.AddScoped<ITestWorkspaceService, TestingWorkspaceService>();
        services.AddScoped<WorkflowStateResolver>();
        services.AddScoped<IResultService, ResultService>();
        // Security Audit Trail. Scoped so an event is appended to the same
        // DbContext (and therefore the same transaction) as the state
        // change that caused it. The HTTP-backed request context supplies
        // the technical correlation id, IP and user agent.
        services.AddHttpContextAccessor();
        services.AddScoped<ISecurityRequestContext, HttpSecurityRequestContext>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ElectronicSignatureService>();
        services.AddScoped<IElectronicSignatureService>(sp => sp.GetRequiredService<ElectronicSignatureService>());
        services.AddScoped<SegregationOfDutiesGuard>();
        services.AddScoped<ReviewGateService>();
        services.AddScoped<RecordArchiveService>();
        services.AddScoped<MediaReleaseService>();
        services.AddScoped<MediaSummaryService>();
        services.AddScoped<MediaStorageService>();
        services.AddScoped<CryovialSummaryService>();
        services.AddScoped<SampleReviewService>();
        services.AddScoped<SampleApprovalService>();
        services.AddScoped<SectionClosureService>();
        services.AddScoped<AddLaboratoryService>();
        services.AddScoped<SampleSummaryService>();
        services.AddScoped<SampleTrackingService>();
        services.AddScoped<OosTrackingService>();
        services.AddScoped<DashboardService>();
        services.AddSingleton(new NotificationRecomputeThrottle(TimeSpan.FromMinutes(5)));
        services.AddScoped<DashboardNotificationService>();
        services.AddScoped<RecentActivityService>();
        services.AddScoped<MyTasksService>();
        services.AddScoped<DashboardLabScopeService>();
        services.AddScoped<SectionCountersService>();
        services.AddScoped<GroupedTestActionService>();
        services.AddScoped<CurrentStepViewService>();
        services.AddScoped<TestWorkflowQueryService>();
        services.AddScoped<ReceiptLabService>();
        services.AddScoped<MediaExpiryService>();
        services.AddScoped<KpiService>();
        services.AddScoped<WorkflowProfileService>();
        services.AddScoped<IKpi, SampleTatKpi>();
        services.AddScoped<IKpi, OnTimeAllStagesKpi>();
        services.AddScoped<IKpi, RightFirstTimeKpi>();
        services.AddScoped<IKpi, RetestRateKpi>();
        services.AddScoped<IKpi, OosRateKpi>();
        services.AddScoped<IKpi, WipBacklogKpi>();
        services.AddScoped<IKpi, IncubationReadsOnTimeKpi>();
        services.AddScoped<IKpi, EmExcursionRateKpi>();
        services.AddScoped<IKpi, StageTatKpi>();
        services.AddScoped<IKpi, BacklogAgingKpi>();
        services.AddScoped<IKpi, ReturnsByTestKpi>();
        services.AddScoped<KpiRegistry>();
        services.AddScoped<KpiDashboardService>();
        services.AddScoped<KpiSettingsService>();
        services.AddScoped<AnalystAvailabilityService>();
        services.AddScoped<AnalystWorkloadService>();
        services.AddScoped<CryovialService>();
        services.AddScoped<BiologicalWasteDestructionService>();
        services.AddScoped<SamplePreparationService>();
        services.AddScoped<PreparationParameterValidator>();
        services.AddScoped<ItemPreparationConfigurationService>();
        services.AddScoped<SampleAssignmentService>();
        services.AddScoped<SampleCorrectionService>();
        services.AddScoped<MediaPreparationService>();
        services.AddScoped<WaterService>();
        services.AddScoped<EMService>();
        services.AddScoped<AfterCleaningService>();
        services.AddScoped<MediaEvaluationService>();
        services.AddScoped<ItemService>();
        services.AddScoped<ItemStmService>();
        services.AddScoped<SpecificationService>();
        services.AddScoped<AdministrationContinuityGuard>();
        services.AddScoped<UserService>();
        services.AddScoped<AdminPasswordRecoveryService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<RoleService>();
        services.AddScoped<ReferenceNumberGenerator>();
        services.AddScoped<AuditSearchService>();
        services.AddScoped<AuditExportService>();
        services.AddScoped<AuditTraceabilityService>();
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();
        services.AddScoped<IDatabaseSequenceHelper, DatabaseSequenceHelper>();
        services.AddScoped<IAuditEventService, AuditEventService>();
        services.AddScoped<IDocumentAuthorizationService, DocumentAuthorizationService>();
        services.AddScoped<IDocumentMasterService, DocumentMasterService>();
        services.AddScoped<IDocumentFileService, DocumentFileService>();
        services.AddScoped<IDocumentConfigurationService, DocumentConfigurationService>();
        services.AddScoped<IDocumentAuditService, DocumentAuditService>();
        services.AddScoped<IDocumentReviewService, DocumentReviewService>();
        services.AddScoped<IDocumentRevisionService, DocumentRevisionService>();
        services.AddScoped<IDocumentApprovalService, DocumentApprovalService>();
        services.AddScoped<IDocumentEffectiveDateService, DocumentEffectiveDateService>();
        services.AddScoped<IPeriodicReviewService, PeriodicReviewService>();
        services.AddScoped<ITrainingAssignmentService, TrainingAssignmentService>();
        services.AddScoped<ITrainingMatrixService, TrainingMatrixService>();
        services.AddScoped<IDocumentAcknowledgementService, DocumentAcknowledgementService>();
        services.AddScoped<IDocumentEscalationService, DocumentEscalationService>();
        services.AddScoped<IDocumentReassignmentService, DocumentReassignmentService>();
        services.AddSingleton<MicroLIMS.API.BackgroundServices.DocumentEffectiveDateWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.DocumentEffectiveDateWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.DatabaseHealthMonitorWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.DatabaseHealthMonitorWorker>());
        services.AddSingleton<MicroLIMS.Application.Services.BackgroundJobs.IBackgroundJobWork>(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.DatabaseHealthMonitorWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.ErrorLogRetentionWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.ErrorLogRetentionWorker>());
        services.AddSingleton<MicroLIMS.Application.Services.BackgroundJobs.IBackgroundJobWork>(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.ErrorLogRetentionWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.SessionTokenCleanupWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.SessionTokenCleanupWorker>());
        services.AddSingleton<MicroLIMS.Application.Services.BackgroundJobs.IBackgroundJobWork>(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.SessionTokenCleanupWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.CriticalAlertWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.CriticalAlertWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.SolutionPreparationExpiryWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.SolutionPreparationExpiryWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.MonitoringDeviationWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.MonitoringDeviationWorker>());
        services.AddSingleton<MicroLIMS.API.BackgroundServices.EquipmentOverdueWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroLIMS.API.BackgroundServices.EquipmentOverdueWorker>());
        // Background job run log and the admin Jobs screen. Singleton recorder: it owns the per-job run gate.
        services.AddSingleton<MicroLIMS.Application.Services.BackgroundJobs.IBackgroundJobRunRecorder, MicroLIMS.Application.Services.BackgroundJobs.BackgroundJobRunRecorder>();
        services.AddScoped<MicroLIMS.Application.Services.BackgroundJobs.IBackgroundJobAdminService, MicroLIMS.Application.Services.BackgroundJobs.BackgroundJobAdminService>();
        services.AddScoped<IUserSectionScopeService, UserSectionScopeService>();
        services.AddScoped<LaboratoryOrganizationService>();
        services.AddScoped<MaterialService>();
        services.AddScoped<MaterialMasterService>();
        services.AddScoped<ReferenceOrganismService>();
        services.AddScoped<MaterialCategoryService>();
        services.AddScoped<TestingMaterialCatalogService>();
        services.AddScoped<MaterialReleaseService>();
        services.AddScoped<SolutionMasterService>();
        services.AddScoped<HplcMethodService>();
        services.AddScoped<IcpMethodService>();
        services.AddScoped<SolutionPreparationService>();
        services.AddScoped<TitrantStandardizationService>();
        services.AddScoped<HplcRunService>();
        services.AddScoped<IcpRunService>();
        services.AddScoped<WorkingStandardService>();
        services.AddScoped<MediaProductService>();
        services.AddScoped<MediaIncubationConditionService>();
        services.AddScoped<EquipmentInventoryService>();
        services.AddScoped<EquipmentConfigurationService>();
        services.AddScoped<ChromatographyColumnService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILabClock>(sp => new LabClock(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()));

        services.AddScoped<PathogenSessionService>();
        services.AddScoped<LocationPathogenObservationService>();
        services.AddScoped<ConfirmationAgreementEvaluator>();
        // Material & Equipment document subsystems
        var maxDocBytes = config.GetValue<long>("MaterialDocuments:MaxFileSizeBytes", 26_214_400L); // 25 MB default
        services.AddSingleton(_ => new MaterialDocumentFileValidator(maxDocBytes));
        services.AddScoped<MaterialDocumentService>();
        services.AddScoped<EquipmentDocumentService>();
        services.AddScoped<ItemDocumentService>();
        services.AddScoped<OosInvestigationDocumentService>();
        services.AddScoped<ResultProjectionService>();
        services.AddScoped<DiscussionService>();
        services.AddScoped<MessageService>();
        services.AddScoped<IncubatorEligibilityService>();
        services.AddScoped<MediaAppearanceSnapshotService>();
        services.AddScoped<ReportingQueryService>();
        services.AddScoped<MicroLIMS.Application.Services.Spc.ControlChartService>();
        services.AddScoped<MicroLIMS.Application.Services.Spc.ControlChartReportService>();
        services.AddScoped<MediaGptReportService>();
        services.AddScoped<ReferenceStrainReportService>();
        services.AddScoped<DataExportAuditService>();

        // Master data (api/masterdata/...)
        services.AddScoped<WaterMasterDataService>();
        services.AddScoped<EnvironmentalMonitoringMasterDataService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringSettingsService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringPlanService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.OccurrenceStatusService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringLinkService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringOccurrenceService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringReportService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringDeviationService>();
        services.AddScoped<MicroLIMS.Application.Services.Monitoring.MonitoringReminderService>();
        services.AddScoped<MicroLIMS.Application.Services.EquipmentPlan.EquipmentWorkService>();
        services.AddScoped<MicroLIMS.Application.Services.EquipmentPlan.EquipmentBaselineService>();
        services.AddScoped<MicroLIMS.Application.Services.EquipmentPlan.EquipmentPlanService>();
        services.AddScoped<MicroLIMS.Application.Services.EquipmentPlan.CalendarEventService>();
        services.AddScoped<MicroLIMS.Application.Services.EquipmentPlan.EquipmentCalendarService>();
        services.AddScoped<AfterCleaningMasterDataService>();
        services.AddScoped<SpecificationMasterDataService>();
        services.AddScoped<ReferenceListMasterDataService>();
        services.AddScoped<EquipmentMasterDataService>();
        services.AddScoped<InstrumentAgentService>();
        services.AddScoped<InstrumentIngestService>();
        services.AddScoped<MediaMasterDataService>();
        services.AddScoped<OrganismMasterDataService>();
        services.AddScoped<TestDefinitionMasterDataService>();
        services.AddScoped<TestWorkflowStepMasterDataService>();
        services.AddScoped<TestStageReplicateMasterDataService>();
        services.AddScoped<MicroLIMS.Application.Abstractions.Workbook.IWorkbookFormat, MicroLIMS.Infrastructure.Workbook.ClosedXmlWorkbookFormat>();
        services.AddScoped<MicroLIMS.Application.Services.MasterDataWorkbook.MasterDataWorkbookService>();

        // Validators
        services.AddScoped<ReceiveSampleValidator>();
        services.AddScoped<WaterValidator>();
        services.AddScoped<EMValidator>();
        services.AddScoped<ProductValidator>();

        // Repositories
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<ISampleRepository, SampleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();

        // Infrastructure
        services.AddScoped<IPdfGenerator, PdfGenerator>();
        services.AddScoped<IWordGenerator, WordGenerator>();
        var smtpOptions = SmtpConfiguration.Resolve(config);
        services.AddSingleton(smtpOptions);
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.AddSingleton<MicroLIMS.Application.Abstractions.Email.ISmtpStatusProvider>(sp => new SmtpStatusProvider(sp.GetRequiredService<SmtpOptions>()));
        services.AddSingleton<MicroLIMS.Application.Services.Integrations.EmailDeliveryTracker>();
        services.AddScoped<IEmailSender>(sp => new MicroLIMS.Application.Services.Integrations.TrackingEmailSender(
            new EmailSender(sp.GetRequiredService<SmtpOptions>()),
            sp.GetRequiredService<MicroLIMS.Application.Services.Integrations.EmailDeliveryTracker>(),
            sp.GetRequiredService<MicroLIMS.Application.Abstractions.Email.ISmtpStatusProvider>()));
        services.AddSingleton<MicroLIMS.Application.Services.Integrations.AdminActionThrottle>();
        services.AddScoped<MicroLIMS.Application.Services.Integrations.IIntegrationsService, MicroLIMS.Application.Services.Integrations.IntegrationsService>();
        // Error capture is a singleton that opens its own DbContext
        // scope per write - the request-scoped context is typically
        // mid-exception when an error is captured.
        services.AddSingleton<IErrorCaptureService, ErrorCaptureService>();

        // Read/triage side of the same data - scoped, ordinary querying.
        services.AddScoped<IErrorMonitoringService, ErrorMonitoringService>();

        // Admin Settings registry: consumers read through ISystemSettingsReader (cached, falls back to
        // legacy config then code default); ISystemSettingsService changes values with reason/signature.
        services.AddSingleton<MicroLIMS.Application.Services.SystemSettings.SystemSettingsCache>();
        services.AddScoped<ISystemSettingsReader, MicroLIMS.Application.Services.SystemSettings.SystemSettingsReader>();
        services.AddScoped<ISystemSettingsService>(sp => new MicroLIMS.Application.Services.SystemSettings.SystemSettingsService(
            sp.GetRequiredService<MicroLIMS.Application.Abstractions.Persistence.IMicroLimsDbContext>(),
            sp.GetRequiredService<MicroLIMS.Application.Services.SystemSettings.SystemSettingsCache>(),
            sp.GetRequiredService<IElectronicSignatureService>(),
            sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<JwtSettings>().AccessTokenLifetime,
            sp.GetRequiredService<ISecurityAuditService>()));
        services.AddScoped<IAccessReviewService, MicroLIMS.Application.Services.AccessReviewService>();
        services.AddScoped<IDatabaseStatsQuery, DatabaseStatsQuery>();
        services.AddScoped<IDatabaseOverviewService, DatabaseOverviewService>();

        // Critical incident alerting. Disabled unless explicitly enabled,
        // so no environment can mail anyone by accident. Driven by
        // CriticalAlertWorker rather than the request thread.
        services.AddSingleton(new CriticalAlertOptions(
            Enabled: config.GetValue("ErrorMonitoring:CriticalAlerts:Enabled", false),
            Recipient: config["ErrorMonitoring:CriticalAlerts:Recipient"],
            EnvironmentName: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"));
        services.AddScoped<ICriticalAlertService, CriticalAlertService>();

        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        // Where uploaded files physically live. "Local" writes under
        // Storage:BasePath and is for development only - on Render that
        // directory is wiped on every restart and redeploy. "S3" writes to an
        // S3-compatible bucket (Backblaze B2 in production).
        var storageProvider = config["Storage:Provider"] ?? "Local";
        var storageBasePath = config["Storage:BasePath"] ?? "storage";
        if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            string RequiredS3Setting(string name) =>
                config[$"Storage:S3:{name}"] is { Length: > 0 } value
                    ? value
                    : throw new InvalidOperationException(
                        $"Storage:Provider is S3 but Storage:S3:{name} is not set (environment variable Storage__S3__{name}).");

            var bucketName = RequiredS3Setting("BucketName");
            var s3Client = new AmazonS3Client(
                new BasicAWSCredentials(RequiredS3Setting("AccessKeyId"), RequiredS3Setting("SecretAccessKey")),
                new AmazonS3Config
                {
                    ServiceURL = RequiredS3Setting("ServiceUrl"),
                    AuthenticationRegion = RequiredS3Setting("Region"),
                    ForcePathStyle = true,
                    // Send and check only the checksums an operation requires.
                    // The SDK's newer defaults add checksum headers that
                    // S3-compatible providers, B2 included, have rejected.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                });

            services.AddSingleton<IAmazonS3>(s3Client);
            // Legacy base path: rows written to local disk before the move
            // resolve to the key a re-upload of the original would use.
            services.AddSingleton<IFileStorageService>(sp =>
                new S3FileStorageService(sp.GetRequiredService<IAmazonS3>(), bucketName, storageBasePath));
        }
        else if (string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFileStorageService>(_ => new LocalFileStorageService(storageBasePath));
        }
        else
        {
            throw new InvalidOperationException($"Unknown Storage:Provider '{storageProvider}'. Use 'Local' or 'S3'.");
        }

        // Instrument report OCR: "tesseract" on PATH in the Docker image;
        // set Ocr:TesseractPath to the .exe on Windows.
        services.AddSingleton<IReportTextReader>(_ => new ReportTextReader(new TesseractOcr(
            config["Ocr:TesseractPath"] ?? "tesseract",
            config["Ocr:WorkDirectory"] ?? Path.GetTempPath())));

        // Takes the validated JwtSettings registered in Program.cs rather
        // than reading Jwt:Key again - a second read is what allowed the
        // signing key and the validation key to differ when the setting
        // was absent (signing got null, validation got the fallback).
        services.AddSingleton<IJwtTokenService>(sp =>
        {
            var jwt = sp.GetRequiredService<JwtSettings>();
            return new JwtTokenService(jwt.Key, jwt.Issuer, jwt.Audience, jwt.AccessTokenLifetime);
        });

        // AuthenticationService needs a token-issuing delegate - wire it
        // from IJwtTokenService so Application does not reference Infrastructure directly.
        services.AddScoped<AccessTokenIssuer>(sp =>
        {
            var jwt = sp.GetRequiredService<IJwtTokenService>();
            return subject => jwt.IssueToken(subject.UserId, subject.Role, subject.PermissionCodes, subject.RoleId, subject.PermissionVersion);
        });

        return services;
    }
}
