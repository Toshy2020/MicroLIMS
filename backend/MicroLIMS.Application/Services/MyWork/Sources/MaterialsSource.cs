using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Media, solutions and strains I am responsible for:
//   - media evaluations not completed, on lots I prepared (due = earliest open challenge incubation reading);
//   - media lots and solutions I prepared that expire within the look-ahead (or already have);
//   - vials I thawed that were not destroyed once the destruction reminder is due
//     (CryovialThaw.DestructionReminderAfter, the same rule MyTasksService used).
// One SQL statement for the page and one for the counts: the four kinds are projected to one shape and concatenated.
public sealed class MaterialsSource : IMyWorkSource
{
    private const string Evaluation = "media-evaluation";
    private const string MediaLot = "media";
    private const string Solution = "solution";
    private const string Vial = "cryovial";

    private readonly IMicroLimsDbContext _db;

    public MaterialsSource(IMicroLimsDbContext db) => _db = db;

    public string Key => "materials";

    public bool AppliesTo(MyWorkContext ctx) => true;

    private sealed class Row
    {
        public string Kind { get; init; } = string.Empty;
        public int Id { get; init; }
        public int RefId { get; init; }
        public string Identifier { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int? Detail { get; init; }
        public DateTime? Due { get; init; }
        public DateTime? Anchor { get; init; }
        public int SectionId { get; init; }
        public string SectionName { get; init; } = string.Empty;
        public string Lab { get; init; } = string.Empty;
    }

    private IQueryable<Row> Rows(MyWorkContext ctx)
    {
        var userId = ctx.UserId;
        var horizon = ctx.NowUtc.AddDays(ctx.LookAheadDays);
        var reminderCutoff = ctx.NowUtc - CryovialThaw.DestructionReminderAfter;

        var evaluations = _db.MediaEvaluations.AsNoTracking()
            .Where(e => e.Status != MediaEvaluationStatus.Completed && e.Media!.PreparedByUserId == userId)
            .InSections(ctx, e => e.Media!.Material!.SectionId)
            .Select(e => new Row
            {
                Kind = Evaluation,
                Id = e.Id,
                RefId = e.MediaId,
                Identifier = e.Media!.LotNumber,
                Name = e.Media.Material!.MaterialName,
                Detail = (int)e.EvaluationType,
                Due = e.Challenges
                    .Where(c => c.Incubation != null && c.Incubation.CompletedAt == null)
                    .Min(c => c.Incubation!.ExpectedReadingAt),
                Anchor = e.AssignedAt,
                SectionId = e.Media.Material.SectionId,
                SectionName = e.Media.Material.Section!.Name,
                Lab = e.Media.Material.Section.Code,
            });

        var lots = _db.Media.AsNoTracking()
            .Where(m => m.PreparedByUserId == userId && (m.Status == MediaStatus.Prepared || m.Status == MediaStatus.Active)
                && m.ExpiryDate <= horizon)
            .InSections(ctx, m => m.Material!.SectionId)
            .Select(m => new Row
            {
                Kind = MediaLot,
                Id = m.Id,
                RefId = m.Id,
                Identifier = m.LotNumber,
                Name = m.Material!.MaterialName,
                Detail = null,
                Due = m.ExpiryDate,
                Anchor = m.PreparedAt,
                SectionId = m.Material.SectionId,
                SectionName = m.Material.Section!.Name,
                Lab = m.Material.Section.Code,
            });

        var solutions = _db.SolutionPreparations.AsNoTracking()
            .Where(p => p.Status == SolutionPreparationStatus.Prepared && p.PreparedByUserId == userId
                && p.ExpiresAt != null && p.ExpiresAt <= horizon)
            .InSections(ctx, p => p.SectionId)
            .Select(p => new Row
            {
                Kind = Solution,
                Id = p.Id,
                RefId = p.Id,
                Identifier = p.Code ?? p.SolutionMaster!.Name,
                Name = p.SolutionMaster!.Name,
                Detail = null,
                Due = p.ExpiresAt,
                Anchor = p.PreparedAt,
                SectionId = p.SectionId,
                SectionName = p.Section!.Name,
                Lab = p.Section.Code,
            });

        var vials = _db.ThawEvents.AsNoTracking()
            .Where(t => t.ThawedByUserId == userId && t.DestructionItem == null && t.ThawedAt <= reminderCutoff)
            .InSections(ctx, t => t.Cryovial!.Material!.SectionId)
            .Select(t => new Row
            {
                Kind = Vial,
                Id = t.Id,
                RefId = t.CryovialId,
                Identifier = t.Cryovial!.Code,
                Name = t.Cryovial.OrganismNameSnapshot,
                Detail = null,
                Due = t.ThawedAt + CryovialThaw.DestructionReminderAfter,
                Anchor = t.ThawedAt,
                SectionId = t.Cryovial.Material!.SectionId,
                SectionName = t.Cryovial.Material.Section!.Name,
                Lab = t.Cryovial.Material.Section.Code,
            });

        return evaluations.Concat(lots).Concat(solutions).Concat(vials);
    }

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var counts = await CountAsync(ctx, ct);
        var total = counts.Values.Sum();
        var rows = total == 0
            ? new List<Row>()
            : await Rows(ctx)
                .OrderBy(r => r.Due == null).ThenBy(r => r.Due).ThenBy(r => r.Kind).ThenBy(r => r.Id)
                .Take(take).ToListAsync(ct);

        var items = rows.Select(r => ToItem(ctx, r)).ToList();
        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["materials"] = total }, new Dictionary<string, int>());
    }

    // Urgency by due time, bucketed in SQL with the same boundaries as MyWorkUrgency.ForDueDate.
    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var now = ctx.NowUtc;
        var endOfDay = ctx.EndOfLabDayUtc;
        var buckets = await Rows(ctx)
            .GroupBy(r => r.Due == null ? MyWorkUrgencyLevel.None
                : r.Due < now ? MyWorkUrgencyLevel.Overdue
                : r.Due <= endOfDay ? MyWorkUrgencyLevel.Today
                : MyWorkUrgencyLevel.Later)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return buckets.ToDictionary(b => b.Level, b => b.Count);
    }

    private static MyWorkItem ToItem(MyWorkContext ctx, Row r)
    {
        var (title, context, target) = r.Kind switch
        {
            Evaluation => ($"{EvaluationName((EvaluationType)r.Detail!.Value)}: media lot {r.Identifier}", r.Name,
                new MyWorkTarget("media-evaluation", MediaId: r.RefId, TaskId: r.Id)),
            MediaLot => ($"Media lot {r.Identifier} expires", r.Name, new MyWorkTarget("media", MediaId: r.Id)),
            Solution => ($"Solution {r.Identifier} expires", r.Name, new MyWorkTarget("solution", TaskId: r.Id)),
            _ => ($"Destroy thawed vial {r.Identifier}", $"{r.Name} · thawed {r.Anchor?.ToString("dd-MMM-yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC",
                new MyWorkTarget("cryovial", TaskId: r.RefId)),
        };

        return new MyWorkItem(
            $"{r.Kind}:{r.Id}", "materials", r.Identifier, title, context, r.Due,
            MyWorkUrgency.ForDueDate(r.Due, ctx.NowUtc, ctx.EndOfLabDayUtc), r.Due ?? r.Anchor ?? ctx.NowUtc, false, null,
            target, Array.Empty<string>(), r.SectionId, r.SectionName, r.Lab);
    }

    private static string EvaluationName(EvaluationType type) => type switch
    {
        EvaluationType.GrowthPromotion => "Growth promotion test",
        EvaluationType.IndicationInhibition => "Indication/inhibition test",
        EvaluationType.EnrichmentCharacteristics => "Enrichment characteristics test",
        _ => type.ToString(),
    };
}
