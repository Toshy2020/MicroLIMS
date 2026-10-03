using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public record TitrantInfoDto(int SolutionMasterId, string Name, decimal NominalStrength, TitrantStrengthUnit StrengthUnit);

public record TitrantPreparationOption(
    int PreparationId, string Code, DateTime? ExpiresAt, decimal? Factor, string FactorState,
    DateTime? StandardizedAt, DateTime? ValidUntil, int? StandardizationId, decimal? StandardizationTemperatureC,
    bool Usable, string? Warning, string? BlockReason);

public record StandardLotOption(
    int MaterialId, string LotLabel, string Kind, decimal PurityPercent, decimal? MoisturePercent,
    DateTime? ExpiryDate, decimal QuantityRemaining, MaterialUnit Unit);

public record TitrationSpecificationDto(
    int SpecificationId, string ParameterName, ResultBasis? ResultBasis, string Unit, string SpecLimit,
    decimal? LabelClaim, string? LabelClaimUnit);

public record TitrationContextDto(
    int TestOrderId, string TestCode, string DisplayName,
    TitrationType TitrationType, bool NonAqueous, TitrationMode Mode, TitrationCalculation Calculation,
    TitrationEndpoint Endpoint, string? Indicator,
    decimal? EquivalencyFactor, bool BlankRequired, int ReplicateCount,
    decimal? MaxRsdPercent, bool TempCorrection, decimal? ExpansionCoefficient,
    decimal? ExcessVolumeMl,
    TitrantInfoDto Titrant, TitrantInfoDto? ExcessTitrant,
    List<TitrantPreparationOption> TitrantPreparations,
    List<TitrantPreparationOption> ExcessPreparations,
    List<StandardLotOption> StandardLots,
    List<TitrationSpecificationDto> Specifications);

// Which titrant preparations / standard lots an analyst may pick for a titration
// test order. TitrationRecorder re-applies the same rules server-side.
public class TitrationContextService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly ILabClock _clock;

    public TitrationContextService(IMicroLimsDbContext db, IUserSectionScopeService scope, ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _clock = clock ?? LabClock.Default;
    }

    public async Task<TitrationContextDto> GetContextAsync(int testOrderId, int userId, CancellationToken ct = default)
    {
        await _scope.EnsureTestOrderAccessAsync(userId, testOrderId);

        var order = await _db.TestOrders.AsNoTracking().Include(t => t.Sample)
            .FirstOrDefaultAsync(t => t.Id == testOrderId, ct)
            ?? throw new NotFoundException($"Test order {testOrderId} not found.");
        var t = await _db.TestDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Code == order.TestCode, ct)
            ?? throw new InvalidOperationException($"Test definition \"{order.TestCode}\" not found.");
        if (t.WorkflowType != WorkflowType.Titration)
            throw new InvalidOperationException($"Test order {testOrderId} is not a Titration workflow.");

        var cfg = TitrationConfig.From(t);
        var nowUtc = _clock.UtcNow.UtcDateTime;
        var today = _clock.LabToday;

        var titrantMaster = await LoadMasterAsync(cfg.TitrantMasterId, ct);
        var excessMaster = cfg.ExcessMasterId.HasValue ? await LoadMasterAsync(cfg.ExcessMasterId.Value, ct) : null;

        var titrantPreps = await OptionsAsync(titrantMaster.Id, t.SectionId, cfg, cfg.TempCorrection, nowUtc, ct);
        var excessPreps = excessMaster == null
            ? new List<TitrantPreparationOption>()
            : await OptionsAsync(excessMaster.Id, t.SectionId, cfg, false, nowUtc, ct);

        var lots = new List<StandardLotOption>();
        if (cfg.Calculation == TitrationCalculation.Relative)
        {
            var candidates = await _db.Materials.AsNoTracking()
                .Where(m => m.SectionId == t.SectionId && m.MaterialMasterEntryId == cfg.StandardEntryId
                    && (m.MaterialType == MaterialType.ReferenceStandard || m.MaterialType == MaterialType.WorkingStandard))
                .OrderBy(m => m.ExpiryDate).ThenBy(m => m.Id)
                .ToListAsync(ct);
            foreach (var m in candidates)
            {
                if (!m.Purity.HasValue || !Helpers.LotUsability.Check(m, cfg.StandardEntryId!.Value, null, today).Usable) continue;
                lots.Add(new StandardLotOption(m.Id, m.LotLabel, m.MaterialType.ToString(), m.Purity.Value, m.MoisturePercent,
                    m.ExpiryDate, m.QuantityRemaining, m.Unit));
            }
        }

        var specs = new List<TitrationSpecificationDto>();
        if (order.Sample?.ItemId is int itemId)
        {
            foreach (var s in await SpecificationLookup.ForSampleAsync(_db, order.SampleId, itemId, order.TestCode, ct))
                specs.Add(new TitrationSpecificationDto(s.Id, s.ParameterName, s.ResultBasis, s.Unit,
                    !string.IsNullOrWhiteSpace(s.SpecLimit) ? s.SpecLimit : SpecificationService.BuildCanonicalSpecLimit(s),
                    s.LabelClaim, s.LabelClaimUnit));
        }

        TitrantInfoDto Info(SolutionMaster m) => new(m.Id, m.Name, m.NominalStrength ?? 0m, m.StrengthUnit ?? TitrantStrengthUnit.Normal);

        return new TitrationContextDto(
            order.Id, t.Code, t.DisplayName,
            cfg.Type, cfg.NonAqueous, cfg.Mode, cfg.Calculation, cfg.Endpoint, t.TitrationIndicator,
            t.TitrationEquivalencyFactor, cfg.BlankRequired, cfg.ReplicateCount,
            t.TitrationMaxRsdPercent, cfg.TempCorrection, t.TitrationExpansionCoefficient,
            t.TitrationExcessVolumeMl,
            Info(titrantMaster), excessMaster == null ? null : Info(excessMaster),
            titrantPreps, excessPreps, lots, specs);
    }

    private async Task<SolutionMaster> LoadMasterAsync(int id, CancellationToken ct) =>
        await _db.SolutionMasters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new InvalidOperationException("The titrant solution master configured on this test no longer exists.");

    private async Task<List<TitrantPreparationOption>> OptionsAsync(
        int masterId, int sectionId, TitrationConfig cfg, bool requireTemperature, DateTime nowUtc, CancellationToken ct)
    {
        var preps = await _db.SolutionPreparations.AsNoTracking()
            .Where(p => p.SolutionMasterId == masterId && p.SectionId == sectionId
                && p.Status == SolutionPreparationStatus.Prepared && (!p.ExpiresAt.HasValue || p.ExpiresAt.Value > nowUtc))
            .OrderByDescending(p => p.PreparedAt).ThenByDescending(p => p.Id)
            .ToListAsync(ct);
        var ids = preps.Select(p => p.Id).ToList();
        var records = await _db.TitrantStandardizations.AsNoTracking()
            .Where(x => ids.Contains(x.SolutionPreparationId)).ToListAsync(ct);
        return preps.Select(p => TitrationTitrants.Evaluate(
            p, records.Where(r => r.SolutionPreparationId == p.Id).ToList(),
            cfg.Calculation, requireTemperature, nowUtc, _clock.ToLabLocal, _clock.LabToday)).ToList();
    }
}

// The titration columns of a TestDefinition, checked non-null once.
public record TitrationConfig(
    TitrationType Type, bool NonAqueous, TitrationMode Mode, TitrationCalculation Calculation, TitrationEndpoint Endpoint,
    int TitrantMasterId, int? ExcessMasterId, bool BlankRequired, bool TempCorrection, int ReplicateCount, int? StandardEntryId)
{
    public static TitrationConfig From(TestDefinition t)
    {
        if (!t.TitrationType.HasValue || !t.TitrationMode.HasValue || !t.TitrationCalculation.HasValue
            || !t.TitrationEndpoint.HasValue || !t.TitrantSolutionMasterId.HasValue || !t.ReplicateCount.HasValue)
            throw new InvalidOperationException($"Test \"{t.Code}\" is not fully configured as a titration test in Test Master.");
        return new TitrationConfig(
            t.TitrationType.Value, t.TitrationNonAqueous ?? false, t.TitrationMode.Value, t.TitrationCalculation.Value,
            t.TitrationEndpoint.Value, t.TitrantSolutionMasterId.Value, t.TitrationExcessSolutionMasterId,
            t.TitrationBlankRequired ?? false, t.TitrationTempCorrection ?? false, t.ReplicateCount.Value, t.TitrationStandardEntryId);
    }
}

public static class TitrationTitrants
{
    // Factor state of one preparation and whether a titration of this method may use it.
    public static TitrantPreparationOption Evaluate(
        SolutionPreparation prep, IReadOnlyList<TitrantStandardization> records, TitrationCalculation calc,
        bool requireTemperature, DateTime nowUtc, Func<DateTime, DateTime> toLabLocal, DateOnly today)
    {
        var cur = TitrantStandardizationService.ComputeCurrentFactor(records, nowUtc);
        bool usable = true;
        string? warning = null, block = null;

        if (cur.State == "NotStandardized")
        {
            usable = false;
            block = "This titrant preparation has not been standardized.";
        }
        else if (calc == TitrationCalculation.UspFactor)
        {
            bool sameDay = cur.StandardizedAt.HasValue && DateOnly.FromDateTime(toLabLocal(cur.StandardizedAt.Value)) == today;
            if (cur.State == "Due")
            {
                usable = false;
                block = "Titrant standardization is due - restandardize before use.";
            }
            else if (cur.State == "BeforeEachUse" && !sameDay)
            {
                usable = false;
                block = "This titrant must be restandardized before each use - it was not standardized today.";
            }
        }
        else if (cur.State == "Due")
        {
            warning = "Titrant standardization is due - factor not used in the relative calculation.";
        }
        else if (cur.State == "BeforeEachUse"
                 && !(cur.StandardizedAt.HasValue && DateOnly.FromDateTime(toLabLocal(cur.StandardizedAt.Value)) == today))
        {
            warning = "This titrant is restandardized before each use and was not standardized today - factor not used in the relative calculation.";
        }

        if (usable && requireTemperature && !cur.TemperatureC.HasValue)
        {
            usable = false;
            block = "Restandardize this titrant and record the temperature - temperature correction needs it";
        }

        return new TitrantPreparationOption(
            prep.Id, prep.Code ?? $"#{prep.Id}", prep.ExpiresAt, cur.Factor, cur.State,
            cur.StandardizedAt, cur.ValidUntil, cur.StandardizationId, cur.TemperatureC, usable, warning, block);
    }
}
