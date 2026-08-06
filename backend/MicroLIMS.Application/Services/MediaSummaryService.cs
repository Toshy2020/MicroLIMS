using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Read model + exportable document for one prepared Media lot. Mirrors
// SampleSummaryService's shape so the media report reads like the sample
// report rather than inventing its own vocabulary.
public class MediaSummaryService
{
    private readonly MicroLimsDbContext _db;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IWordGenerator _wordGenerator;
    private readonly ReviewGateService _reviewGate;

    public MediaSummaryService(MicroLimsDbContext db, IPdfGenerator pdfGenerator, IWordGenerator wordGenerator, ReviewGateService reviewGate)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
        _wordGenerator = wordGenerator;
        _reviewGate = reviewGate;
    }

    public async Task<MediaSummaryDto?> GetSummaryAsync(int mediaId)
    {
        var media = await _db.Media
            .Include(m => m.MediaType)
            .Include(m => m.Material)
            .Include(m => m.AutoclaveEquipment)
            .FirstOrDefaultAsync(m => m.Id == mediaId);
        if (media is null) return null;

        var evaluation = await _db.MediaEvaluations
            .Include(e => e.Challenges).ThenInclude(c => c.Organism)
            .Include(e => e.Challenges).ThenInclude(c => c.Cryovial)
            .Include(e => e.Challenges).ThenInclude(c => c.Incubation).ThenInclude(i => i!.IncubatorEquipment)
            .FirstOrDefaultAsync(e => e.MediaId == mediaId);

        var timeline = await _reviewGate.GetTimelineAsync(ReviewEntityTypes.Media, mediaId);

        var signatures = await _db.ElectronicSignatures
            .Where(s => s.EntityType == ReviewEntityTypes.Media && s.EntityId == mediaId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .ToListAsync();

        // One batched name lookup for every user referenced here.
        var userIds = new HashSet<int> { media.PreparedByUserId };
        if (media.ApprovedByUserId is not null) userIds.Add(media.ApprovedByUserId.Value);
        if (evaluation?.CompletedByUserId is not null) userIds.Add(evaluation.CompletedByUserId.Value);
        foreach (var c in evaluation?.Challenges ?? new())
            if (c.ReadByUserId is not null) userIds.Add(c.ReadByUserId.Value);

        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string NameOf(int id) => names.TryGetValue(id, out var n) ? n : "Unknown";

        return new MediaSummaryDto
        {
            MediaId = media.Id,
            LotNumber = media.LotNumber,
            MediaClass = media.MediaType?.Class.ToString() ?? string.Empty,
            MaterialName = media.Material?.MaterialName ?? string.Empty,
            ManufacturerName = media.ManufacturerName,
            ManufacturerLot = media.ManufacturerLot,
            TotalWeight = media.TotalWeight,
            TotalVolume = media.TotalVolume,
            AutoclaveName = media.AutoclaveEquipment?.Name,
            AutoclaveProgram = media.AutoclaveProgram,
            LoadType = media.LoadType,
            Temperature = media.Temperature,
            CycleTime = media.CycleTime,
            CycleNumber = media.CycleNumber,
            Ph = media.Ph,
            ExpiryDate = media.ExpiryDate,
            PreparedAt = media.PreparedAt,
            // Legacy lots predate PreparedByUserId and carry 0 - say so
            // rather than resolving it to a misleading "Unknown user".
            PreparedByName = media.PreparedByUserId == 0 ? "Not recorded" : NameOf(media.PreparedByUserId),
            Status = media.Status.ToString(),
            ApprovalStatus = media.ApprovalStatus.ToString(),
            IsReleasedForUse = media.IsReleasedForUse,
            ApprovedByName = media.ApprovedByUserId is not null ? NameOf(media.ApprovedByUserId.Value) : null,
            ApprovedAt = media.ApprovedAt,
            Timeline = timeline.Select(e => new SampleWorkflowEventDto
            {
                EventType = e.EventType.ToString(),
                PerformedByName = e.PerformedByNameSnapshot,
                Timestamp = e.Timestamp,
                Comment = e.Comment,
                Decision = e.Decision?.ToString()
            }).ToList(),
            Signatures = signatures,
            Evaluation = evaluation is null ? null : new MediaEvaluationSummaryDto
            {
                EvaluationType = evaluation.EvaluationType.ToString(),
                Status = evaluation.Status.ToString(),
                Outcome = evaluation.Outcome?.ToString(),
                AssignedAt = evaluation.AssignedAt,
                CompletedAt = evaluation.CompletedAt,
                CompletedByName = evaluation.CompletedByUserId is not null ? NameOf(evaluation.CompletedByUserId.Value) : null,
                Challenges = evaluation.Challenges.Select(c => new MediaChallengeSummaryDto
                {
                    OrganismName = c.Organism?.ScientificName ?? string.Empty,
                    ChallengeRole = c.ChallengeRole?.ToString(),
                    CryovialCode = c.Cryovial?.Code,
                    InitialInoculum = c.InitialInoculum,
                    IncubatorName = c.Incubation?.IncubatorEquipment?.Name,
                    Temperature = c.Incubation?.Temperature,
                    Duration = c.Incubation?.Duration,
                    IncubationStartedAt = c.Incubation?.StartedAt,
                    ExpectedReadingAt = c.Incubation?.ExpectedReadingAt,
                    OldMediaCount = c.OldMediaCount,
                    NewMediaCount = c.NewMediaCount,
                    RecoveryPercent = c.RecoveryPercent,
                    GrowthObserved = c.GrowthObserved,
                    ObservedDescription = c.ObservedDescription,
                    ExpectedDescription = c.ExpectedDescription,
                    IsTurbid = c.IsTurbid,
                    Outcome = c.Outcome?.ToString(),
                    ReadAt = c.ReadAt,
                    ReadByName = c.ReadByUserId is not null ? NameOf(c.ReadByUserId.Value) : null
                }).ToList()
            }
        };
    }

    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryPdfAsync(int mediaId)
    {
        var summary = await GetSummaryAsync(mediaId);
        if (summary is null) return null;
        return (FileStemFor(summary), await _pdfGenerator.GenerateReportAsync(ReportDocumentMapper.ForMedia(summary)));
    }

    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryWordAsync(int mediaId)
    {
        var summary = await GetSummaryAsync(mediaId);
        if (summary is null) return null;
        return (FileStemFor(summary), await _wordGenerator.GenerateFromLinesAsync(TitleFor(summary), BuildReportLines(summary)));
    }

    public async Task<Infrastructure.Pdf.ReportDocument?> BuildReportDocumentAsync(int mediaId)
    {
        var summary = await GetSummaryAsync(mediaId);
        return summary is null ? null : ReportDocumentMapper.ForMedia(summary);
    }

    private static string TitleFor(MediaSummaryDto s) => $"Media Lot Record - {s.LotNumber}";
    private static string FileStemFor(MediaSummaryDto s) => $"MediaLot_{s.LotNumber.Replace('/', '-')}";

    private static List<string> BuildReportLines(MediaSummaryDto s)
    {
        var lines = new List<string>
        {
            "LOT IDENTITY",
            $"Lot Number: {s.LotNumber}",
            $"Media Class: {s.MediaClass}",
            $"Source Material: {s.MaterialName}",
            $"Manufacturer: {s.ManufacturerName}   Manufacturer Lot: {s.ManufacturerLot}",
            $"Expiry Date: {s.ExpiryDate:dd-MMM-yyyy}",
            "",
            "PREPARATION",
            $"Total Weight: {s.TotalWeight}   Total Volume: {s.TotalVolume}",
            $"Autoclave: {s.AutoclaveName ?? "-"}   Program: {s.AutoclaveProgram}   Load: {s.LoadType}",
            $"Temperature: {s.Temperature}   Cycle Time: {s.CycleTime}   Cycle Number: {s.CycleNumber}",
            $"pH: {s.Ph}",
            $"Prepared By: {s.PreparedByName} - {s.PreparedAt:dd-MMM-yyyy HH:mm}",
            "",
            "RELEASE STATUS",
            $"Status: {s.Status}   Approval: {s.ApprovalStatus}   Released For Use: {(s.IsReleasedForUse ? "Yes" : "No")}",
        };
        if (s.ApprovedByName is not null)
            lines.Add($"Decided By: {s.ApprovedByName} - {s.ApprovedAt:dd-MMM-yyyy HH:mm}");
        lines.Add("");

        if (s.Evaluation is not null)
        {
            var e = s.Evaluation;
            lines.Add("MEDIA EVALUATION");
            lines.Add($"Type: {e.EvaluationType}   Status: {e.Status}   Outcome: {e.Outcome ?? "-"}");
            lines.Add($"Assigned: {e.AssignedAt:dd-MMM-yyyy HH:mm}   Completed: {(e.CompletedAt is null ? "-" : e.CompletedAt.Value.ToString("dd-MMM-yyyy HH:mm"))}");
            if (e.CompletedByName is not null) lines.Add($"Completed By: {e.CompletedByName}");
            lines.Add("");

            foreach (var c in e.Challenges)
            {
                lines.Add($"  Challenge: {c.OrganismName}{(c.ChallengeRole is null ? "" : $" ({c.ChallengeRole})")}");
                lines.Add($"    Cryovial: {c.CryovialCode ?? "-"}   Initial Inoculum: {c.InitialInoculum}");
                lines.Add($"    Incubator: {c.IncubatorName ?? "-"}   Temperature: {c.Temperature ?? "-"}   Duration: {c.Duration ?? "-"}");
                if (c.RecoveryPercent is not null)
                    lines.Add($"    Old Count: {c.OldMediaCount}   New Count: {c.NewMediaCount}   Recovery: {c.RecoveryPercent}%");
                if (c.GrowthObserved is not null)
                    lines.Add($"    Growth Observed: {(c.GrowthObserved.Value ? "Yes" : "No")}");
                if (c.ObservedDescription is not null)
                    lines.Add($"    Observed: {c.ObservedDescription}   Expected: {c.ExpectedDescription ?? "-"}");
                if (c.IsTurbid is not null)
                    lines.Add($"    Turbid: {(c.IsTurbid.Value ? "Yes" : "No")}");
                lines.Add($"    Outcome: {c.Outcome ?? "-"}   Read By: {c.ReadByName ?? "-"} - {(c.ReadAt is null ? "-" : c.ReadAt.Value.ToString("dd-MMM-yyyy HH:mm"))}");
                lines.Add("");
            }
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
