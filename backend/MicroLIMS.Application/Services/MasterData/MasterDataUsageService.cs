using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

public enum MasterRecordType
{
    TestDefinition,
    Specification,
    Item,
    Equipment,
    MaterialMaster
}

public sealed record UsageLine(string Key, string Label, int Count, bool IsOpenWork, string? ListRoute);

public sealed record UsageSummary(
    MasterRecordType Type,
    int Id,
    int OpenWorkCount,
    int HistoricalCount,
    IReadOnlyList<UsageLine> Lines);

/// <summary>
/// Read-only "where used" counts for master configuration records, using COUNT queries only
/// and scoped to the sections the caller may read.
/// </summary>
public sealed class MasterDataUsageService
{
    private const string OtherSectionsKey = "other-sections";

    private static readonly ApprovalStatus[] ClosedTestOrderStatuses =
    {
        ApprovalStatus.Approved,
        ApprovalStatus.Rejected,
        ApprovalStatus.Voided,
        ApprovalStatus.Cancelled
    };

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public MasterDataUsageService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<UsageSummary> GetAsync(
        MasterRecordType type,
        int id,
        int userId,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown master record type.");

        // Equipment usage is read through the equipment's own read rule, like the equipment page:
        // equipment in a section the caller cannot read is refused, not summarised.
        if (type == MasterRecordType.Equipment
            && await _db.Equipment.AsNoTracking().AnyAsync(e => e.Id == id, ct))
            await _scope.EnsureEquipmentAccessAsync(userId, id, ct, SectionAccess.Read);

        var readableSections = await _scope.GetReadableSectionIdsAsync(userId, ct);

        return type switch
        {
            MasterRecordType.TestDefinition => await GetForTestDefinitionAsync(id, readableSections, ct),
            MasterRecordType.Specification => await GetForSpecificationAsync(id, readableSections, ct),
            MasterRecordType.Item => await GetForItemAsync(id, readableSections, ct),
            MasterRecordType.Equipment => await GetForEquipmentAsync(id, readableSections, ct),
            MasterRecordType.MaterialMaster => await GetForMaterialMasterAsync(id, readableSections, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown master record type.")
        };
    }

    public async Task<int> GetUnscopedOpenWorkCountAsync(
        MasterRecordType type,
        int id,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown master record type.");

        var summary = type switch
        {
            MasterRecordType.TestDefinition => await GetForTestDefinitionAsync(id, readableSections: null, ct),
            MasterRecordType.Specification => await GetForSpecificationAsync(id, readableSections: null, ct),
            MasterRecordType.Item => await GetForItemAsync(id, readableSections: null, ct),
            MasterRecordType.Equipment => await GetForEquipmentAsync(id, readableSections: null, ct),
            MasterRecordType.MaterialMaster => await GetForMaterialMasterAsync(id, readableSections: null, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown master record type.")
        };

        return summary.OpenWorkCount;
    }

    private async Task<UsageSummary> GetForTestDefinitionAsync(
        int id,
        IReadOnlyList<int>? readableSections,
        CancellationToken ct)
    {
        var td = await _db.TestDefinitions.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.Id, t.Code, t.SectionId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Test definition {id} not found.");

        bool ownerReadable = readableSections is null || readableSections.Contains(td.SectionId);

        int specCount = await _db.Specifications.AsNoTracking()
            .CountAsync(s => s.TestCode == td.Code, ct);
        int sampleTestCount = await _db.SampleTests.AsNoTracking()
            .CountAsync(st => st.TestCode == td.Code, ct);
        int roomConfigCount = await _db.RoomTestConfigurations.AsNoTracking()
            .CountAsync(r => r.TestCode == td.Code, ct);
        int samplingConfigCount = await _db.SamplingConfigurations.AsNoTracking()
            .CountAsync(sc => sc.TestCode == td.Code, ct);
        int machinePartConfigCount = await _db.MachinePartConfigurations.AsNoTracking()
            .CountAsync(mp => mp.TestCode == td.Code, ct);
        int waterPointCount = await _db.WaterSamplingPoints.AsNoTracking()
            .CountAsync(p => p.AssignedTestCodes.Contains(td.Code), ct);

        int openOrderCount;
        int allOrderCount;
        int otherOrderCount = 0;

        if (readableSections is null)
        {
            openOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == td.Code
                    && !t.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(t.Status), ct);
            allOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == td.Code, ct);
        }
        else
        {
            openOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == td.Code
                    && readableSections.Contains(t.SectionId)
                    && !t.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(t.Status), ct);
            allOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == td.Code
                    && readableSections.Contains(t.SectionId), ct);
            otherOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == td.Code
                    && !readableSections.Contains(t.SectionId), ct);
        }

        int otherSectionsCount = otherOrderCount;
        if (!ownerReadable)
        {
            otherSectionsCount += specCount + sampleTestCount + roomConfigCount
                + samplingConfigCount + machinePartConfigCount + waterPointCount;
        }

        var lines = new List<UsageLine>
        {
            new("specifications", "Specifications", ownerReadable ? specCount : 0, false, "/laboratory-configuration/specifications"),
            new("sample-tests", "Auto-assigned item tests", ownerReadable ? sampleTestCount : 0, false, "/laboratory-configuration/items"),
            new("room-test-configs", "EM room configurations", ownerReadable ? roomConfigCount : 0, false, "/laboratory-configuration/environmental-monitoring"),
            new("sampling-configs", "Water sampling configurations", ownerReadable ? samplingConfigCount : 0, false, "/laboratory-configuration/water"),
            new("machine-part-configs", "After-cleaning part configurations", ownerReadable ? machinePartConfigCount : 0, false, "/laboratory-configuration/after-cleaning"),
            new("water-sampling-points", "Water sampling points", ownerReadable ? waterPointCount : 0, false, "/laboratory-configuration/water"),
            new("open-test-orders", "Open test orders", openOrderCount, true, "/receiving-testing"),
            new("all-test-orders", "Total test orders", allOrderCount, false, "/receiving-testing")
        };

        return BuildSummary(MasterRecordType.TestDefinition, id, lines, otherSectionsCount);
    }

    private async Task<UsageSummary> GetForSpecificationAsync(
        int id,
        IReadOnlyList<int>? readableSections,
        CancellationToken ct)
    {
        var spec = await _db.Specifications.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Id, s.ItemId, s.TestCode })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Specification {id} not found.");

        int? testSectionId = await _db.TestDefinitions.AsNoTracking()
            .Where(t => t.Code == spec.TestCode)
            .Select(t => (int?)t.SectionId)
            .FirstOrDefaultAsync(ct);

        bool specSectionReadable = readableSections is null
            || (testSectionId.HasValue && readableSections.Contains(testSectionId.Value));

        int openOrderCount;
        int parameterResultCount;
        int stageCount = await _db.SpecificationStages.AsNoTracking()
            .CountAsync(st => st.SpecificationId == spec.Id, ct);

        int otherSectionsCount = 0;

        if (readableSections is null)
        {
            openOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == spec.TestCode
                    && t.Sample != null
                    && t.Sample.ItemId == spec.ItemId
                    && !t.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(t.Status), ct);
            parameterResultCount = await _db.ParameterResults.AsNoTracking()
                .CountAsync(pr => pr.SpecificationId == spec.Id, ct);
        }
        else
        {
            openOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == spec.TestCode
                    && t.Sample != null
                    && t.Sample.ItemId == spec.ItemId
                    && readableSections.Contains(t.SectionId)
                    && !t.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(t.Status), ct);
            int otherOpenOrderCount = await _db.TestOrders.AsNoTracking()
                .CountAsync(t => t.TestCode == spec.TestCode
                    && t.Sample != null
                    && t.Sample.ItemId == spec.ItemId
                    && !readableSections.Contains(t.SectionId)
                    && !t.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(t.Status), ct);

            parameterResultCount = await _db.ParameterResults.AsNoTracking()
                .CountAsync(pr => pr.SpecificationId == spec.Id
                    && pr.TestOrder != null
                    && readableSections.Contains(pr.TestOrder.SectionId), ct);
            int otherParameterResultCount = await _db.ParameterResults.AsNoTracking()
                .CountAsync(pr => pr.SpecificationId == spec.Id
                    && (pr.TestOrder == null || !readableSections.Contains(pr.TestOrder.SectionId)), ct);

            otherSectionsCount = otherOpenOrderCount + otherParameterResultCount + (specSectionReadable ? 0 : stageCount);
        }

        var lines = new List<UsageLine>
        {
            new("open-test-orders", "Open test orders", openOrderCount, true, "/receiving-testing"),
            new("parameter-results", "Recorded parameter results", parameterResultCount, false, "/receiving-testing"),
            new("specification-stages", "Specification stages", specSectionReadable ? stageCount : 0, false, "/laboratory-configuration/specifications")
        };

        return BuildSummary(MasterRecordType.Specification, id, lines, otherSectionsCount);
    }

    private async Task<UsageSummary> GetForItemAsync(
        int id,
        IReadOnlyList<int>? readableSections,
        CancellationToken ct)
    {
        bool exists = await _db.Items.AsNoTracking().AnyAsync(i => i.Id == id, ct);
        if (!exists)
            throw new NotFoundException($"Item {id} not found.");

        int specCount;
        int sampleTestCount;
        int openSampleCount;
        int allSampleCount;
        int itemDocCount = await _db.ItemDocuments.AsNoTracking()
            .CountAsync(d => d.ItemId == id, ct);
        int prepConfigCount = await _db.ItemPreparationConfigurations.AsNoTracking()
            .CountAsync(p => p.ItemId == id, ct);
        int hplcMethodCount;
        int otherSectionsCount = 0;

        if (readableSections is null)
        {
            specCount = await _db.Specifications.AsNoTracking()
                .CountAsync(s => s.ItemId == id, ct);
            sampleTestCount = await _db.SampleTests.AsNoTracking()
                .CountAsync(st => st.ItemId == id, ct);
            openSampleCount = await _db.Samples.AsNoTracking()
                .CountAsync(s => s.ItemId == id && !SampleWorkflowQueues.ClosedSampleStatuses.Contains(s.Status), ct);
            allSampleCount = await _db.Samples.AsNoTracking()
                .CountAsync(s => s.ItemId == id, ct);
            hplcMethodCount = await _db.HplcMethods.AsNoTracking()
                .CountAsync(m => m.ProductItemId == id, ct);
        }
        else
        {
            specCount = await _db.Specifications.AsNoTracking()
                .CountAsync(s => s.ItemId == id
                    && _db.TestDefinitions.Any(td => td.Code == s.TestCode && readableSections.Contains(td.SectionId)), ct);
            int otherSpecCount = await _db.Specifications.AsNoTracking()
                .CountAsync(s => s.ItemId == id
                    && !_db.TestDefinitions.Any(td => td.Code == s.TestCode && readableSections.Contains(td.SectionId)), ct);

            sampleTestCount = await _db.SampleTests.AsNoTracking()
                .CountAsync(st => st.ItemId == id
                    && _db.TestDefinitions.Any(td => td.Code == st.TestCode && readableSections.Contains(td.SectionId)), ct);
            int otherSampleTestCount = await _db.SampleTests.AsNoTracking()
                .CountAsync(st => st.ItemId == id
                    && !_db.TestDefinitions.Any(td => td.Code == st.TestCode && readableSections.Contains(td.SectionId)), ct);

            openSampleCount = await _db.Samples.AsNoTracking()
                .CountAsync(s => s.ItemId == id
                    && !SampleWorkflowQueues.ClosedSampleStatuses.Contains(s.Status)
                    && (s.TestOrders.Any(t => !t.IsSuperseded && readableSections.Contains(t.SectionId))
                        || (s.LabSectionId != null && readableSections.Contains(s.LabSectionId.Value))), ct);
            allSampleCount = await _db.Samples.AsNoTracking()
                .CountAsync(s => s.ItemId == id
                    && (s.TestOrders.Any(t => !t.IsSuperseded && readableSections.Contains(t.SectionId))
                        || (s.LabSectionId != null && readableSections.Contains(s.LabSectionId.Value))), ct);
            int otherSampleCount = await _db.Samples.AsNoTracking()
                .CountAsync(s => s.ItemId == id
                    && !(s.TestOrders.Any(t => !t.IsSuperseded && readableSections.Contains(t.SectionId))
                        || (s.LabSectionId != null && readableSections.Contains(s.LabSectionId.Value))), ct);

            hplcMethodCount = await _db.HplcMethods.AsNoTracking()
                .CountAsync(m => m.ProductItemId == id && readableSections.Contains(m.SectionId), ct);
            int otherHplcMethodCount = await _db.HplcMethods.AsNoTracking()
                .CountAsync(m => m.ProductItemId == id && !readableSections.Contains(m.SectionId), ct);

            otherSectionsCount = otherSpecCount + otherSampleTestCount + otherSampleCount + otherHplcMethodCount;
        }

        var lines = new List<UsageLine>
        {
            new("specifications", "Specifications", specCount, false, "/laboratory-configuration/specifications"),
            new("sample-tests", "Auto-assigned tests", sampleTestCount, false, "/laboratory-configuration/items"),
            new("open-samples", "Open samples", openSampleCount, true, "/receiving-testing"),
            new("all-samples", "Total samples", allSampleCount, false, "/receiving-testing"),
            new("item-documents", "Item documents", itemDocCount, false, "/laboratory-configuration/items"),
            new("item-preparations", "Preparation configurations", prepConfigCount, false, "/laboratory-configuration/items"),
            new("hplc-methods", "HPLC methods", hplcMethodCount, false, "/laboratory-configuration/hplc-methods")
        };

        return BuildSummary(MasterRecordType.Item, id, lines, otherSectionsCount);
    }

    private async Task<UsageSummary> GetForEquipmentAsync(
        int id,
        IReadOnlyList<int>? readableSections,
        CancellationToken ct)
    {
        var equipment = await _db.Equipment.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.Id, e.SectionId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Equipment {id} not found.");

        bool equipmentSectionReadable = readableSections is null || readableSections.Contains(equipment.SectionId);

        int openIncubationCount;
        int allIncubationCount;
        int openAnalysisCount;
        int allAnalysisCount;
        int openHplcRunCount;
        int allHplcRunCount;
        int openIcpRunCount;
        int allIcpRunCount;
        int autoclaveProgramCount = await _db.AutoclavePrograms.AsNoTracking()
            .CountAsync(p => p.EquipmentId == id, ct);
        int instrumentAgentCount;
        int otherSectionsCount = 0;

        if (readableSections is null)
        {
            openIncubationCount = await _db.Incubations.AsNoTracking()
                .CountAsync(i => i.IncubatorEquipmentId == id
                    && i.CompletedAt == null
                    && (i.TestOrderId == null || (!i.TestOrder!.IsSuperseded && !ClosedTestOrderStatuses.Contains(i.TestOrder.Status))), ct);
            allIncubationCount = await _db.Incubations.AsNoTracking()
                .CountAsync(i => i.IncubatorEquipmentId == id, ct);

            openAnalysisCount = await _db.TestAnalyses.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id
                    && a.IsActive
                    && a.TestOrder != null
                    && !a.TestOrder.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(a.TestOrder.Status), ct);
            allAnalysisCount = await _db.TestAnalyses.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id, ct);

            openHplcRunCount = await _db.HplcRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && r.Status == HplcRunStatus.Open, ct);
            allHplcRunCount = await _db.HplcRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id, ct);

            openIcpRunCount = await _db.IcpRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && r.Status == IcpRunStatus.Open, ct);
            allIcpRunCount = await _db.IcpRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id, ct);

            instrumentAgentCount = await _db.InstrumentAgents.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id, ct);
        }
        else
        {
            openIncubationCount = await _db.Incubations.AsNoTracking()
                .CountAsync(i => i.IncubatorEquipmentId == id
                    && i.CompletedAt == null
                    && ((i.TestOrderId == null && equipmentSectionReadable)
                        || (i.TestOrder != null
                            && readableSections.Contains(i.TestOrder.SectionId)
                            && !i.TestOrder.IsSuperseded
                            && !ClosedTestOrderStatuses.Contains(i.TestOrder.Status))), ct);
            allIncubationCount = await _db.Incubations.AsNoTracking()
                .CountAsync(i => i.IncubatorEquipmentId == id
                    && ((i.TestOrderId == null && equipmentSectionReadable)
                        || (i.TestOrder != null && readableSections.Contains(i.TestOrder.SectionId))), ct);
            int otherIncubationCount = await _db.Incubations.AsNoTracking()
                .CountAsync(i => i.IncubatorEquipmentId == id
                    && !((i.TestOrderId == null && equipmentSectionReadable)
                        || (i.TestOrder != null && readableSections.Contains(i.TestOrder.SectionId))), ct);

            openAnalysisCount = await _db.TestAnalyses.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id
                    && a.IsActive
                    && a.TestOrder != null
                    && readableSections.Contains(a.TestOrder.SectionId)
                    && !a.TestOrder.IsSuperseded
                    && !ClosedTestOrderStatuses.Contains(a.TestOrder.Status), ct);
            allAnalysisCount = await _db.TestAnalyses.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id
                    && a.TestOrder != null
                    && readableSections.Contains(a.TestOrder.SectionId), ct);
            int otherAnalysisCount = await _db.TestAnalyses.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id
                    && (a.TestOrder == null || !readableSections.Contains(a.TestOrder.SectionId)), ct);

            openHplcRunCount = await _db.HplcRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && r.Status == HplcRunStatus.Open && readableSections.Contains(r.SectionId), ct);
            allHplcRunCount = await _db.HplcRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && readableSections.Contains(r.SectionId), ct);
            int otherHplcRunCount = await _db.HplcRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && !readableSections.Contains(r.SectionId), ct);

            openIcpRunCount = await _db.IcpRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && r.Status == IcpRunStatus.Open && readableSections.Contains(r.SectionId), ct);
            allIcpRunCount = await _db.IcpRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && readableSections.Contains(r.SectionId), ct);
            int otherIcpRunCount = await _db.IcpRuns.AsNoTracking()
                .CountAsync(r => r.EquipmentId == id && !readableSections.Contains(r.SectionId), ct);

            instrumentAgentCount = await _db.InstrumentAgents.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id && readableSections.Contains(a.SectionId), ct);
            int otherInstrumentAgentCount = await _db.InstrumentAgents.AsNoTracking()
                .CountAsync(a => a.EquipmentId == id && !readableSections.Contains(a.SectionId), ct);

            otherSectionsCount = otherIncubationCount
                + otherAnalysisCount
                + otherHplcRunCount
                + otherIcpRunCount
                + (equipmentSectionReadable ? 0 : autoclaveProgramCount)
                + otherInstrumentAgentCount;
        }

        var lines = new List<UsageLine>
        {
            new("open-incubations", "Open incubations", openIncubationCount, true, "/receiving-testing"),
            new("open-test-analyses", "Open test analyses", openAnalysisCount, true, "/receiving-testing"),
            new("open-hplc-runs", "HPLC runs in progress", openHplcRunCount, true, "/hplc-workspace"),
            new("open-icp-runs", "ICP runs in progress", openIcpRunCount, true, "/icp-workspace"),
            new("all-incubations", "Total incubations", allIncubationCount, false, "/receiving-testing"),
            new("all-test-analyses", "Total test analyses", allAnalysisCount, false, "/receiving-testing"),
            new("all-hplc-runs", "Total HPLC runs", allHplcRunCount, false, "/hplc-workspace"),
            new("all-icp-runs", "Total ICP runs", allIcpRunCount, false, "/icp-workspace"),
            new("autoclave-programs", "Autoclave programs", equipmentSectionReadable ? autoclaveProgramCount : 0, false, "/laboratory-configuration/equipment"),
            new("instrument-agents", "Instrument agents", instrumentAgentCount, false, "/laboratory-configuration/fp-instruments")
        };

        return BuildSummary(MasterRecordType.Equipment, id, lines, otherSectionsCount);
    }

    private async Task<UsageSummary> GetForMaterialMasterAsync(
        int id,
        IReadOnlyList<int>? readableSections,
        CancellationToken ct)
    {
        bool exists = await _db.MaterialMasterEntries.AsNoTracking().AnyAsync(m => m.Id == id, ct);
        if (!exists)
            throw new NotFoundException($"Material master entry {id} not found.");

        int materialLotCount;
        int solutionComponentCount;
        int prepConfigCount = await _db.ItemPreparationConfigurations.AsNoTracking()
            .CountAsync(p => p.DiluentMaterialMasterEntryId == id || p.NeutralizerMaterialMasterEntryId == id, ct);
        int otherSectionsCount = 0;

        if (readableSections is null)
        {
            materialLotCount = await _db.Materials.AsNoTracking()
                .CountAsync(m => m.MaterialMasterEntryId == id, ct);
            solutionComponentCount = await _db.SolutionComponents.AsNoTracking()
                .CountAsync(c => c.MaterialMasterEntryId == id, ct);
        }
        else
        {
            materialLotCount = await _db.Materials.AsNoTracking()
                .CountAsync(m => m.MaterialMasterEntryId == id && readableSections.Contains(m.SectionId), ct);
            int otherMaterialLotCount = await _db.Materials.AsNoTracking()
                .CountAsync(m => m.MaterialMasterEntryId == id && !readableSections.Contains(m.SectionId), ct);

            solutionComponentCount = await _db.SolutionComponents.AsNoTracking()
                .CountAsync(c => c.MaterialMasterEntryId == id
                    && c.SolutionMaster != null
                    && readableSections.Contains(c.SolutionMaster.SectionId), ct);
            int otherSolutionComponentCount = await _db.SolutionComponents.AsNoTracking()
                .CountAsync(c => c.MaterialMasterEntryId == id
                    && (c.SolutionMaster == null || !readableSections.Contains(c.SolutionMaster.SectionId)), ct);

            otherSectionsCount = otherMaterialLotCount + otherSolutionComponentCount;
        }

        // Note: MediaConfiguration in this codebase links to MediaProduct/MediaIncubationCondition
        // and has no MaterialMasterEntryId column, so it is omitted.
        var lines = new List<UsageLine>
        {
            new("material-lots", "Material stock lots", materialLotCount, false, "/inventory/materials"),
            new("solution-components", "Solution components", solutionComponentCount, false, "/laboratory-configuration/solution-master"),
            new("item-preparations", "Preparation configurations", prepConfigCount, false, "/laboratory-configuration/items")
        };

        return BuildSummary(MasterRecordType.MaterialMaster, id, lines, otherSectionsCount);
    }

    private static UsageSummary BuildSummary(
        MasterRecordType type,
        int id,
        List<UsageLine> lines,
        int otherSectionsCount)
    {
        int openWorkCount = lines.Where(l => l.IsOpenWork).Sum(l => l.Count);
        int historicalCount = lines.Where(l => !l.IsOpenWork).Sum(l => l.Count);

        if (otherSectionsCount > 0)
        {
            lines.Add(new UsageLine(OtherSectionsKey, "In other sections", otherSectionsCount, false, null));
        }

        return new UsageSummary(type, id, openWorkCount, historicalCount, lines);
    }
}
