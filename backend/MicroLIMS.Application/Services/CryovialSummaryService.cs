using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Read model + exportable document for one Cryovial batch, mirroring
// SampleSummaryService/MediaSummaryService.
public class CryovialSummaryService
{
    private readonly MicroLimsDbContext _db;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IWordGenerator _wordGenerator;
    private readonly ReviewGateService _reviewGate;

    public CryovialSummaryService(MicroLimsDbContext db, IPdfGenerator pdfGenerator, IWordGenerator wordGenerator, ReviewGateService reviewGate)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
        _wordGenerator = wordGenerator;
        _reviewGate = reviewGate;
    }

    public async Task<CryovialSummaryDto?> GetSummaryAsync(int cryovialId)
    {
        var cryovial = await _db.Cryovials
            .Include(c => c.Material)
            .Include(c => c.Organism)
            .Include(c => c.IdentityConfirmations).ThenInclude(i => i.Media)
            .Include(c => c.IdentityConfirmations).ThenInclude(i => i.IncubatorEquipment)
            .Include(c => c.ThawHistory)
            .FirstOrDefaultAsync(c => c.Id == cryovialId);
        if (cryovial is null) return null;

        var timeline = await _reviewGate.GetTimelineAsync(ReviewEntityTypes.Cryovial, cryovialId);

        var signatures = await _db.ElectronicSignatures
            .Where(s => s.EntityType == ReviewEntityTypes.Cryovial && s.EntityId == cryovialId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .ToListAsync();

        var userIds = new HashSet<int> { cryovial.PreparedByUserId };
        if (cryovial.ApprovedByUserId is not null) userIds.Add(cryovial.ApprovedByUserId.Value);
        foreach (var t in cryovial.ThawHistory) userIds.Add(t.ThawedByUserId);

        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : "Unknown";

        return new CryovialSummaryDto
        {
            CryovialId = cryovial.Id,
            Code = cryovial.Code,
            OrganismName = cryovial.Organism?.ScientificName ?? cryovial.OrganismNameSnapshot,
            ManufacturerName = cryovial.ManufacturerName,
            MaterialName = cryovial.Material?.MaterialName ?? string.Empty,
            MaterialBatchNumber = cryovial.Material?.BatchNumber ?? string.Empty,
            ExpiryDate = cryovial.ExpiryDate,
            NumberOfVialsPrepared = cryovial.NumberOfVialsPrepared,
            VialsRemaining = cryovial.VialsRemaining,
            StorageCondition = cryovial.StorageCondition,
            PhysicalCheckText = cryovial.PhysicalCheckText,
            PreparedAt = cryovial.PreparedAt,
            // Legacy batches predate PreparedByUserId and carry 0.
            PreparedByName = cryovial.PreparedByUserId == 0 ? "Not recorded" : NameOf(cryovial.PreparedByUserId),
            ApprovalStatus = cryovial.ApprovalStatus.ToString(),
            ApprovedByName = cryovial.ApprovedByUserId is not null ? NameOf(cryovial.ApprovedByUserId.Value) : null,
            ApprovedAt = cryovial.ApprovedAt,
            IsDestroyed = cryovial.IsDestroyed,
            IdentityConfirmations = cryovial.IdentityConfirmations.Select(i => new IdentityConfirmationSummaryDto
            {
                MediaLotNumber = i.Media?.LotNumber,
                IncubatorName = i.IncubatorEquipment?.Name,
                IncubationStart = i.IncubationStart,
                IncubationEnd = i.IncubationEnd,
                ObservationText = i.ObservationText
            }).ToList(),
            ThawHistory = cryovial.ThawHistory.OrderBy(t => t.ThawedAt).Select(t => new ThawEventSummaryDto
            {
                ThawedAt = t.ThawedAt,
                ThawedByName = NameOf(t.ThawedByUserId),
                Notes = t.Notes
            }).ToList(),
            Timeline = timeline.Select(e => new SampleWorkflowEventDto
            {
                EventType = e.EventType.ToString(),
                PerformedByName = e.PerformedByNameSnapshot,
                Timestamp = e.Timestamp,
                Comment = e.Comment,
                Decision = e.Decision?.ToString()
            }).ToList(),
            Signatures = signatures
        };
    }

    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryPdfAsync(int cryovialId)
    {
        var summary = await GetSummaryAsync(cryovialId);
        if (summary is null) return null;
        return (FileStemFor(summary), await _pdfGenerator.GenerateReportAsync(ReportDocumentMapper.ForCryovial(summary)));
    }

    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryWordAsync(int cryovialId)
    {
        var summary = await GetSummaryAsync(cryovialId);
        if (summary is null) return null;
        return (FileStemFor(summary), await _wordGenerator.GenerateFromLinesAsync(TitleFor(summary), BuildReportLines(summary)));
    }

    public async Task<Infrastructure.Pdf.ReportDocument?> BuildReportDocumentAsync(int cryovialId)
    {
        var summary = await GetSummaryAsync(cryovialId);
        return summary is null ? null : ReportDocumentMapper.ForCryovial(summary);
    }

    private static string TitleFor(CryovialSummaryDto s) => $"Cryovial Batch Record - {s.Code}";
    private static string FileStemFor(CryovialSummaryDto s) =>
        $"CryovialBatch_{new string(s.Code.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray())}";

    private static List<string> BuildReportLines(CryovialSummaryDto s)
    {
        var lines = new List<string>
        {
            "BATCH IDENTITY",
            $"Code: {s.Code}",
            $"Organism: {s.OrganismName}",
            $"Manufacturer: {s.ManufacturerName}",
            $"Source Material: {s.MaterialName} (batch {s.MaterialBatchNumber})",
            $"Expiry Date: {s.ExpiryDate:dd-MMM-yyyy}",
            "",
            "PREPARATION",
            $"Vials Prepared: {s.NumberOfVialsPrepared}   Vials Remaining: {s.VialsRemaining}",
            $"Storage Condition: {s.StorageCondition}",
            $"Physical Check: {s.PhysicalCheckText}",
            $"Prepared By: {s.PreparedByName} - {s.PreparedAt:dd-MMM-yyyy HH:mm}",
            "",
            "APPROVAL STATUS",
            $"Status: {s.ApprovalStatus}{(s.IsDestroyed ? " (destroyed)" : "")}",
        };
        if (s.ApprovedByName is not null)
            lines.Add($"Decided By: {s.ApprovedByName} - {s.ApprovedAt:dd-MMM-yyyy HH:mm}");
        lines.Add("");

        lines.Add("IDENTITY CONFIRMATION PANEL");
        foreach (var i in s.IdentityConfirmations)
        {
            lines.Add($"  Media Lot: {i.MediaLotNumber ?? "-"}   Incubator: {i.IncubatorName ?? "-"}");
            lines.Add($"    Incubated: {i.IncubationStart:dd-MMM-yyyy} to {i.IncubationEnd:dd-MMM-yyyy}");
            lines.Add($"    Observation: {i.ObservationText}");
        }
        if (s.IdentityConfirmations.Count == 0) lines.Add("(no panel rows)");
        lines.Add("");

        if (s.ThawHistory.Count > 0)
        {
            lines.Add("THAW HISTORY");
            foreach (var t in s.ThawHistory)
            {
                var notes = string.IsNullOrWhiteSpace(t.Notes) ? "" : $" - {t.Notes}";
                lines.Add($"{t.ThawedAt:dd-MMM-yyyy HH:mm} - {t.ThawedByName}{notes}");
            }
            lines.Add("");
        }

        lines.Add("TIMELINE");
        foreach (var ev in s.Timeline)
        {
            var decision = ev.Decision is null ? "" : $" ({ev.Decision})";
            var comment = string.IsNullOrWhiteSpace(ev.Comment) ? "" : $" - \"{ev.Comment}\"";
            lines.Add($"{ev.EventType}{decision} - {ev.PerformedByName} - {ev.Timestamp:dd-MMM-yyyy HH:mm}{comment}");
        }
        if (s.Timeline.Count == 0) lines.Add("(no lifecycle events)");
        lines.Add("");

        if (s.Signatures.Count > 0)
        {
            lines.Add("SIGNATURES");
            foreach (var sig in s.Signatures)
            {
                var comment = string.IsNullOrWhiteSpace(sig.Comment) ? "" : $" - \"{sig.Comment}\"";
                lines.Add($"{sig.PrintedName} (@{sig.Username}, {sig.Role}) - {sig.Meaning} - {sig.SignedAt:dd-MMM-yyyy HH:mm}{comment}");
            }
        }

        return lines;
    }
}
