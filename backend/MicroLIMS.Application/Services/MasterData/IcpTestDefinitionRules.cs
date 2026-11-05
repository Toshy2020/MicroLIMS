using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MasterData;

// Id is null for a new element; an existing one is kept by Id so item specifications stay linked.
public record IcpTestElementInput(int? Id, string Symbol, decimal? TargetPpm);

// What an ICP test defines in the Test Master: reporting mode, the sample basis for the assay formula and
// the elements (with target ppm in assay mode). Validates the effective values and applies them to the entity.
public static class IcpTestDefinitionRules
{
    public static async Task ApplyAsync(
        IMicroLimsDbContext db, TestDefinition entity, bool isIcpTest, IcpTestMode? mode, IcpSampleForm? form,
        decimal? theoreticalAmount, decimal? specificGravity, List<IcpTestElementInput>? elements)
    {
        if (!isIcpTest)
        {
            if (mode.HasValue || form.HasValue || theoreticalAmount.HasValue || specificGravity.HasValue || elements is { Count: > 0 })
                throw new InvalidOperationException("ICP settings are only allowed for ICP method assay tests.");
            entity.IcpMode = null; entity.IcpSampleForm = null; entity.IcpTheoreticalAmount = null; entity.IcpSpecificGravity = null;
            return;
        }

        var effectiveMode = mode ?? entity.IcpMode
            ?? throw new InvalidOperationException("The ICP test type (assay or impurity) is required.");

        if (entity.Id != 0 && entity.IcpMode.HasValue && effectiveMode != entity.IcpMode
            && await db.Specifications.AnyAsync(s => s.TestCode == entity.Code && s.IcpTestElementId != null))
            throw new InvalidOperationException("This test has specifications; remove them before changing between assay and impurity.");

        IcpSampleForm? effectiveForm = null;
        decimal? effectiveAmount = null, effectiveGravity = null;
        if (effectiveMode == IcpTestMode.Assay)
        {
            effectiveForm = form ?? entity.IcpSampleForm
                ?? throw new InvalidOperationException("Choose whether the sample is a solid or a liquid.");
            effectiveAmount = theoreticalAmount ?? entity.IcpTheoreticalAmount;
            if (effectiveAmount is not > 0m)
                throw new InvalidOperationException(effectiveForm == IcpSampleForm.Solid
                    ? "The theoretical weight (g) must be greater than zero."
                    : "The theoretical volume (mL) must be greater than zero.");
            if (effectiveForm == IcpSampleForm.Liquid)
            {
                effectiveGravity = specificGravity ?? entity.IcpSpecificGravity;
                if (effectiveGravity is not > 0m)
                    throw new InvalidOperationException("The specific gravity must be greater than zero for a liquid sample.");
            }
        }

        entity.IcpMode = effectiveMode;
        entity.IcpSampleForm = effectiveForm;
        entity.IcpTheoreticalAmount = effectiveAmount;
        entity.IcpSpecificGravity = effectiveGravity;

        var inputs = elements ?? entity.IcpElements.OrderBy(e => e.DisplayOrder)
            .Select(e => new IcpTestElementInput(e.Id, e.Symbol, e.TargetPpm)).ToList();
        if (inputs.Count == 0)
            throw new InvalidOperationException("At least one element is required.");

        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<int>();
        var normalized = new List<(IcpTestElementInput Input, string Symbol)>();
        foreach (var input in inputs)
        {
            var symbol = NormalizeSymbol(input.Symbol);
            if (!symbols.Add(symbol))
                throw new InvalidOperationException($"Element \"{symbol}\" is listed more than once.");
            if (effectiveMode == IcpTestMode.Assay)
            {
                if (input.TargetPpm is not > 0m)
                    throw new InvalidOperationException($"{symbol}: the target concentration (ppm) must be greater than zero.");
            }
            else if (input.TargetPpm.HasValue)
                throw new InvalidOperationException($"{symbol}: impurity tests are judged against the item specification; no target is used.");

            if (input.Id.HasValue)
            {
                if (entity.IcpElements.All(e => e.Id != input.Id.Value))
                    throw new InvalidOperationException($"Element {input.Id.Value} does not belong to this test.");
                if (!seenIds.Add(input.Id.Value))
                    throw new InvalidOperationException($"Element {input.Id.Value} is listed more than once.");
            }
            normalized.Add((input, symbol));
        }

        foreach (var dropped in entity.IcpElements.Where(e => !seenIds.Contains(e.Id)).ToList())
        {
            if (await db.Specifications.AnyAsync(s => s.IcpTestElementId == dropped.Id))
                throw new InvalidOperationException($"Element \"{dropped.Symbol}\" is used by specifications; it can't be removed.");
            entity.IcpElements.Remove(dropped);
            if (entity.Id != 0) db.TestDefinitionIcpElements.Remove(dropped);
        }

        for (var i = 0; i < normalized.Count; i++)
        {
            var (input, symbol) = normalized[i];
            if (input.Id.HasValue)
            {
                var kept = entity.IcpElements.First(e => e.Id == input.Id.Value);
                if (!string.Equals(kept.Symbol, symbol, StringComparison.Ordinal)
                    && await db.Specifications.AnyAsync(s => s.IcpTestElementId == kept.Id))
                    throw new InvalidOperationException($"Element \"{kept.Symbol}\" is used by specifications; its symbol can't be changed.");
                kept.Symbol = symbol; kept.TargetPpm = input.TargetPpm; kept.DisplayOrder = i;
            }
            else
                entity.IcpElements.Add(new TestDefinitionIcpElement { Symbol = symbol, TargetPpm = input.TargetPpm, DisplayOrder = i });
        }
    }

    // "zn" / " ZN " -> "Zn"
    public static string NormalizeSymbol(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length is < 1 or > 3 || !s.All(char.IsLetter))
            throw new InvalidOperationException("Element symbol must be 1-3 letters.");
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }
}
