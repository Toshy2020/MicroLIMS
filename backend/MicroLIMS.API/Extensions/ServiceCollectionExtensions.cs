using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Validators;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Infrastructure.Authentication;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Persistence.Repositories;

namespace MicroLIMS.API.Extensions;

// Central place all DI registrations live - Program.cs stays thin and
// just calls builder.Services.AddApplicationServices(config).
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        // Workflows (the frozen laboratory logic - now real state machines)
        services.AddScoped<IProductWorkflowEngine, ProductWorkflowEngine>();
        services.AddScoped<IWaterWorkflowEngine, WaterWorkflowEngine>();
        services.AddScoped<IEMWorkflowEngine, EMWorkflowEngine>();
        services.AddScoped<IAfterCleaningWorkflowEngine, AfterCleaningWorkflowEngine>();
        services.AddScoped<IMediaEvaluationEngine, MediaEvaluationEngine>();
        services.AddScoped<ITestWorkflowEngine, TestWorkflowEngine>();

        // Application services
        services.AddScoped<IReceivingService, ReceivingService>();
        services.AddScoped<ITestWorkspaceService, TestingWorkspaceService>();
        services.AddScoped<WorkflowStateResolver>();
        services.AddScoped<IResultService, ResultService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IElectronicSignatureService, ElectronicSignatureService>();
        services.AddScoped<SegregationOfDutiesGuard>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<ReviewGateService>();
        services.AddScoped<RecordArchiveService>();
        services.AddScoped<MediaReleaseService>();
        services.AddScoped<MediaSummaryService>();
        services.AddScoped<CryovialSummaryService>();
        services.AddScoped<SampleReviewService>();
        services.AddScoped<SampleApprovalService>();
        services.AddScoped<SampleSummaryService>();
        services.AddScoped<OosTrackingService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<DashboardNotificationService>();
        services.AddScoped<RecentActivityService>();
        services.AddScoped<MyTasksService>();
        services.AddScoped<MediaExpiryService>();
        services.AddScoped<KpiService>();
        services.AddScoped<CryovialService>();
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
        services.AddScoped<SpecificationService>();
        services.AddScoped<UserService>();
        services.AddScoped<UserDeletionService>();
        services.AddScoped<AdminPasswordRecoveryService>();
        services.AddScoped<AuditService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<RoleService>();
        services.AddScoped<ReferenceNumberGenerator>();
        services.AddScoped<AuditSearchService>();
        services.AddScoped<AuditTraceabilityService>();
        services.AddScoped<MaterialService>();
        services.AddScoped<EquipmentInventoryService>();
        services.AddScoped<EquipmentConfigurationService>();
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
        services.AddScoped<IncubatorEligibilityService>();
        services.AddScoped<MediaAppearanceSnapshotService>();
        services.AddScoped<ReportingQueryService>();
        services.AddScoped<MediaGptReportService>();
        services.AddScoped<ReferenceStrainReportService>();
        services.AddScoped<DataExportAuditService>();

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
        services.AddScoped<IEmailSender>(_ => new EmailSender(
            config["Smtp:Host"] ?? "",
            int.TryParse(config["Smtp:Port"], out var p) ? p : 587,
            config["Smtp:Username"] ?? "",
            config["Smtp:Password"] ?? "",
            config["Smtp:FromAddress"] ?? "no-reply@microlims.local",
            bool.TryParse(config["Smtp:EnableSsl"], out var ssl) && ssl));
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddScoped<IFileStorageService>(_ => new LocalFileStorageService(config["Storage:BasePath"] ?? "storage"));

        services.AddSingleton<IJwtTokenService>(_ => new JwtTokenService(
            config["Jwt:Key"]!, config["Jwt:Issuer"]!, config["Jwt:Audience"]!));

        // AuthenticationService needs a token-issuing delegate - wire it
        // from IJwtTokenService so Application does not reference Infrastructure directly.
        services.AddScoped<Func<string, string, IEnumerable<string>, string>>(sp =>
        {
            var jwt = sp.GetRequiredService<IJwtTokenService>();
            return (userId, role, permissionCodes) => jwt.IssueToken(userId, role, permissionCodes);
        });

        return services;
    }
}
