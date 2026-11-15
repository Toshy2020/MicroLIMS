using System.Security.Claims;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.API.Hubs;

/// <summary>
/// SignalR hub at <c>/hubs/live</c> delivering data-free invalidation signals
/// to authenticated users, readable laboratory sections, and authorized records.
/// </summary>
[Authorize]
public sealed class LiveHub : Hub
{
    private readonly LiveHubConnectionTracker _tracker;
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scopeService;
    private readonly IDocumentAuthorizationService _docAuth;

    public LiveHub(
        LiveHubConnectionTracker tracker,
        IMicroLimsDbContext db,
        IUserSectionScopeService scopeService,
        IDocumentAuthorizationService docAuth)
    {
        _tracker = tracker;
        _db = db;
        _scopeService = scopeService;
        _docAuth = docAuth;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null || Context.User is null)
        {
            Context.Abort();
            return;
        }

        await _tracker.RegisterAsync(
            Context.ConnectionId,
            userId.Value,
            Context.User,
            Context.Abort,
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.Unregister(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task WatchRecord(string type, int id)
    {
        var userId = GetUserId();
        if (userId is null || Context.User is null || id <= 0 || string.IsNullOrWhiteSpace(type))
            throw new HubException("Invalid watch request.");

        var stillValid = await _tracker.ReevaluateAsync(Context.ConnectionId, Context.ConnectionAborted);
        if (!stillValid)
            throw new HubException("Session is no longer active.");

        if (!await CanReadRecordAsync(userId.Value, Context.User, type, id, Context.ConnectionAborted))
            throw new HubException("You do not have read access to this record.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"record:{type}:{id}", Context.ConnectionAborted);
    }

    public Task UnwatchRecord(string type, int id)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(type))
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"record:{type}:{id}", Context.ConnectionAborted);
    }

    private int? GetUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> CanReadRecordAsync(
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
                return user.HasClaim("permission", PermissionConstants.KpiView)
                    && await _db.WorkflowProfileVersions.AsNoTracking().AnyAsync(e => e.Id == id, ct);
            default:
                return await VersionedConfigExistsAsync(type, id, ct);
        }
    }

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

    private Task<bool> VersionedConfigExistsAsync(string type, int id, CancellationToken ct) =>
        type switch
        {
            "specification" => _db.Specifications.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "item" => _db.Items.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "solutionMaster" => _db.SolutionMasters.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "autoclaveProgram" => _db.AutoclavePrograms.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "chromatographyColumn" => _db.ChromatographyColumns.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "hplcMethod" => _db.HplcMethods.AsNoTracking().AnyAsync(e => e.Id == id, ct),
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
            "role" => _db.Roles.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "kpiSetting" => _db.KpiSettings.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "monitoringSettings" => _db.MonitoringSettings.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "monitoringPlanRevision" => _db.MonitoringPlanRevisions.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "equipmentPlanRevision" => _db.EquipmentPlanRevisions.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "sectionSelfAssignmentSetting" => _db.SectionSelfAssignmentSettings.AsNoTracking().AnyAsync(e => e.SectionId == id, ct),
            "sampler" => _db.Samplers.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "mediaIncubationCondition" => _db.MediaIncubationConditions.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            "testDefinitionStageReplicate" => _db.TestDefinitionStageReplicates.AsNoTracking().AnyAsync(e => e.Id == id, ct),
            _ => Task.FromResult(false)
        };
}
