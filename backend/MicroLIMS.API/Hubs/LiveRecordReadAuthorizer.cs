using System.Security.Claims;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.API.Hubs;

/// <summary>
/// The one read rule for <see cref="LiveHub"/> record groups. It is used when a connection asks to watch a
/// record and again when the connection is re-evaluated, so a group is dropped once the user can no longer
/// read the record. Each type applies the rule its GET endpoint applies.
/// </summary>
public sealed class LiveRecordReadAuthorizer
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scopeService;
    private readonly IDocumentAuthorizationService _docAuth;

    public LiveRecordReadAuthorizer(
        IMicroLimsDbContext db,
        IUserSectionScopeService scopeService,
        IDocumentAuthorizationService docAuth)
    {
        _db = db;
        _scopeService = scopeService;
        _docAuth = docAuth;
    }

    public async Task<bool> CanReadAsync(
        int userId,
        ClaimsPrincipal user,
        string type,
        int id,
        CancellationToken ct)
    {
        switch (type)
        {
            case "sample":
            {
                if (!await _db.Samples.AsNoTracking().AnyAsync(s => s.Id == id, ct))
                    return false;
                try
                {
                    await _scopeService.EnsureSampleAccessAsync(userId, id, ct, SectionAccess.Read);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
            case "testOrder":
            {
                if (!await _db.TestOrders.AsNoTracking().AnyAsync(t => t.Id == id, ct))
                    return false;
                try
                {
                    await _scopeService.EnsureTestOrderAccessAsync(userId, id, ct, SectionAccess.Read);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
            // Equipment and materials are section-scoped on read: same check as their GET endpoints.
            case "equipment":
                return await ScopedReadAsync(() => _scopeService.EnsureEquipmentAccessAsync(userId, id, ct, SectionAccess.Read),
                    () => _db.Equipment.AsNoTracking().AnyAsync(e => e.Id == id, ct));
            case "material":
                return await ScopedReadAsync(() => _scopeService.EnsureMaterialAccessAsync(userId, id, ct, SectionAccess.Read),
                    () => _db.Materials.AsNoTracking().AnyAsync(e => e.Id == id, ct));
            case "document":
                return await _docAuth.CanViewDocumentAsync(id, userId);
            case "workflowProfileVersion":
                return HasPermission(user, PermissionConstants.KpiView)
                    && await _db.WorkflowProfileVersions.AsNoTracking().AnyAsync(e => e.Id == id, ct);
            default:
                return await VersionedConfigReadableAsync(userId, user, type, id, ct);
        }
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission) =>
        user.HasClaim("permission", permission);

    private static async Task<bool> ScopedReadAsync(Func<Task> ensure, Func<Task<bool>> exists)
    {
        if (!await exists())
            return false;
        try
        {
            await ensure();
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or MicroLIMS.Shared.Exceptions.NotFoundException)
        {
            return false;
        }
    }

    // Same rule as SolutionMasterService / HplcMethodService / ChromatographyColumnService / KpiSettingsService
    // GET: the record's section must be readable (null scope = Sections.ViewAll).
    private async Task<bool> InReadableSectionAsync(int userId, IQueryable<int?> sectionIdQuery, CancellationToken ct)
    {
        var sectionId = await sectionIdQuery.FirstOrDefaultAsync(ct);
        if (sectionId is null)
            return false;
        var readable = await _scopeService.GetReadableSectionIdsAsync(userId, ct);
        return readable is null || readable.Contains(sectionId.Value);
    }

    // Same rule as SectionSelfAssignmentService.GetForUserAsync: only the user's own member sections.
    private async Task<bool> IsMemberSectionWithSettingAsync(int userId, int sectionId, CancellationToken ct)
    {
        var members = await _scopeService.GetMemberSectionIdsAsync(userId, ct);
        return members.Contains(sectionId)
            && await _db.SectionSelfAssignmentSettings.AsNoTracking().AnyAsync(e => e.SectionId == sectionId, ct);
    }

    private Task<bool> VersionedConfigReadableAsync(int userId, ClaimsPrincipal user, string type, int id, CancellationToken ct) =>
        type switch
        {
            "specification" => _db.Specifications.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "item" => _db.Items.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "solutionMaster" => InReadableSectionAsync(userId,
                _db.SolutionMasters.AsNoTracking().Where(e => e.Id == id).Select(e => (int?)e.SectionId), ct),
            "autoclaveProgram" => _db.AutoclavePrograms.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "chromatographyColumn" => InReadableSectionAsync(userId,
                _db.ChromatographyColumns.AsNoTracking().Where(e => e.Id == id).Select(e => (int?)e.SectionId), ct),
            "hplcMethod" => InReadableSectionAsync(userId,
                _db.HplcMethods.AsNoTracking().Where(e => e.Id == id).Select(e => (int?)e.SectionId), ct),
            "materialCategory" => _db.MaterialCategories.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "materialMasterEntry" => _db.MaterialMasterEntries.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "department" => _db.Departments.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "room" => _db.Rooms.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "roomTestConfiguration" => _db.RoomTestConfigurations.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "waterDepartment" => _db.WaterDepartments.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "waterSamplingPoint" => _db.WaterSamplingPoints.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "waterPointLabFrequency" => _db.WaterPointLabFrequencies.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "samplingConfiguration" => _db.SamplingConfigurations.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "machine" => _db.Machines.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "machinePart" => _db.MachineParts.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "machinePartConfiguration" => _db.MachinePartConfigurations.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "testDefinition" => _db.TestDefinitions.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "testWorkflowStep" => _db.TestWorkflowSteps.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "mediaConfiguration" => _db.MediaConfigurations.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "mediaProduct" => _db.MediaProducts.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "organism" => _db.Organisms.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "causeOfTesting" => _db.CausesOfTesting.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "productionStage" => _db.ProductionStages.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "labelFormat" => _db.LabelFormats.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "documentType" => _db.DocumentTypes.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "documentDepartment" => _db.DocumentDepartments.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "documentSection" => _db.DocumentSections.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            // Roles.Manage is the controller policy for every role endpoint.
            "role" => HasPermission(user, PermissionConstants.RolesManage)
                ? _db.Roles.AsNoTracking().AnyAsync(e => e.Id == id, ct)
                : Task.FromResult(false),
            // KpiWorkloadController: Kpi.View, then the section's read scope (KpiSettingsService.GetAsync).
            "kpiSetting" => HasPermission(user, PermissionConstants.KpiView)
                ? InReadableSectionAsync(userId,
                    _db.KpiSettings.AsNoTracking().Where(e => e.Id == id).Select(e => (int?)e.SectionId), ct)
                : Task.FromResult(false),
            // MonitoringSettingsController / MonitoringPlanController / EquipmentPlanRevisionsController GETs.
            "monitoringSettings" => HasPermission(user, PermissionConstants.MonitoringPlanView)
                ? _db.MonitoringSettings.AsNoTracking().AnyAsync(e => e.Id == id, ct)
                : Task.FromResult(false),
            "monitoringPlanRevision" => HasPermission(user, PermissionConstants.MonitoringPlanView)
                ? _db.MonitoringPlanRevisions.AsNoTracking().AnyAsync(e => e.Id == id, ct)
                : Task.FromResult(false),
            "equipmentPlanRevision" => HasPermission(user, PermissionConstants.MonitoringPlanView)
                ? _db.EquipmentPlanRevisions.AsNoTracking().AnyAsync(e => e.Id == id, ct)
                : Task.FromResult(false),
            // SectionSelfAssignmentController: Samples.AssignAnalyst, the user's own sections only.
            "sectionSelfAssignmentSetting" => HasPermission(user, PermissionConstants.SamplesAssignAnalyst)
                ? IsMemberSectionWithSettingAsync(userId, id, ct)
                : Task.FromResult(false),
            "sampler" => _db.Samplers.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "mediaIncubationCondition" => _db.MediaIncubationConditions.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "testDefinitionStageReplicate" => _db.TestDefinitionStageReplicates.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            _ => Task.FromResult(false)
        };
}
