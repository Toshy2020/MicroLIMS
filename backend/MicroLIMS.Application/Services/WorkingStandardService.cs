using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Working standard qualification (spec 2026-10-03): create/edit a draft,
// attach documents, and the lists. Assignment to a run lives in
// HplcRunService; the sign-off flow in WorkingStandardService.SignOff.cs.
public partial class WorkingStandardService
{
    private const long MaxDocumentBytes = 26_214_400; // 25 MB, same as HPLC evidence
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg"
    };
    private static readonly EquationType[] AssayEquations =
    {
        EquationType.HplcAssay, EquationType.HplcMultiAnalyte, EquationType.StandardComparison, EquationType.HplcMethodAssay
    };

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IElectronicSignatureService _signatures;
    private readonly IFileStorageService _storage;
    private readonly ILabClock _clock;

    public WorkingStandardService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        IElectronicSignatureService signatures,
        IFileStorageService storage,
        ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _signatures = signatures;
        _storage = storage;
        _clock = clock ?? LabClock.Default;
    }

    private static bool IsOpen(WorkingStandardQualificationStatus s) =>
        s is WorkingStandardQualificationStatus.Draft or WorkingStandardQualificationStatus.Assayed or WorkingStandardQualificationStatus.Reviewed;

    // ---- Lists ----

    public async Task<List<WorkingStandardLotDto>> GetLotsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.Materials.AsNoTracking().Where(m => m.MaterialType == MaterialType.WorkingStandard);
        if (scope != null) query = query.Where(m => scope.Contains(m.SectionId));
        var lots = await query.OrderBy(m => m.Code).ToListAsync(ct);

        var ids = lots.Select(l => l.Id).ToList();
        var entryIds = lots.Where(l => l.MaterialMasterEntryId.HasValue).Select(l => l.MaterialMasterEntryId!.Value).Distinct().ToList();
        var entryCodes = await _db.MaterialMasterEntries.Where(e => entryIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Code, ct);
        var openRows = await _db.WorkingStandardQualifications.AsNoTracking()
            .Where(q => q.WorkingStandardMaterialId != null && ids.Contains(q.WorkingStandardMaterialId.Value))
            .Select(q => new { LotId = q.WorkingStandardMaterialId!.Value, q.Id, q.Status })
            .ToListAsync(ct);
        var open = openRows.Where(x => IsOpen(x.Status))
            .GroupBy(x => x.LotId).ToDictionary(g => g.Key, g => g.Min(x => x.Id));

        var today = _clock.LabToday;
        return lots.Select(l =>
        {
            string status;
            if (l.QuantityRemaining <= 0) status = "Depleted";
            else if (l.ExpiryDate.HasValue && DateOnly.FromDateTime(l.ExpiryDate.Value) < today) status = "Expired";
            else if (l.ExpiryDate.HasValue && DateOnly.FromDateTime(l.ExpiryDate.Value) <= today.AddDays(WorkingStandardRules.DueSoonDays)) status = "DueSoon";
            else status = "Valid";
            return new WorkingStandardLotDto(
                l.Id, l.Code ?? string.Empty, l.MaterialName, l.BatchNumber,
                l.MaterialMasterEntryId.HasValue ? entryCodes.GetValueOrDefault(l.MaterialMasterEntryId.Value, string.Empty) : string.Empty,
                l.Purity, l.MoisturePercent, l.QuantityRemaining, l.ExpiryDate, status,
                open.TryGetValue(l.Id, out var oid) ? oid : null);
        }).ToList();
    }

    public async Task<List<WorkingStandardQualificationDto>> GetQualificationsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var query = _db.WorkingStandardQualifications.AsNoTracking().AsQueryable();
        if (scope != null) query = query.Where(q => scope.Contains(q.SectionId));
        var ids = await query.OrderByDescending(q => q.CreatedAt).ThenByDescending(q => q.Id).Select(q => q.Id).ToListAsync(ct);

        var result = new List<WorkingStandardQualificationDto>();
        foreach (var id in ids)
            result.Add(await GetAsync(id, userId, ct));
        return result;
    }

    public async Task<WorkingStandardQualificationDto> GetAsync(int id, int userId, CancellationToken ct = default) =>
        await BuildDtoAsync(await LoadAsync(id, userId, ct), ct);

    public async Task<List<EligibleSourceSampleDto>> GetEligibleSourceSamplesAsync(string? search, int userId, CancellationToken ct = default)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var orders = _db.TestOrders.AsNoTracking()
            .Where(o => o.CurrentStep == WorkflowStep.Approved && !o.IsSuperseded
                && o.Sample!.Category == SampleCategory.RawMaterial
                && o.Sample.Status != SampleStatus.Voided && o.Sample.Status != SampleStatus.Cancelled
                && o.Sample.Status != SampleStatus.Rejected && o.Sample.Status != SampleStatus.RetestRequested);
        if (scope != null) orders = orders.Where(o => scope.Contains(o.SectionId));

        var rows = await (from o in orders
                          join d in _db.TestDefinitions.AsNoTracking() on o.TestCode equals d.Code
                          where AssayEquations.Contains(d.EquationType)
                          orderby o.Id
                          select new
                          {
                              o.SampleId,
                              o.Sample!.ReferenceNumber,
                              Name = o.Sample.Item != null ? o.Sample.Item.Name : string.Empty,
                              o.Sample.BatchNumber,
                              o.TestCode
                          }).ToListAsync(ct);

        var term = search?.Trim();
        return rows
            .GroupBy(r => r.SampleId).Select(g => g.First())
            .Where(r => string.IsNullOrEmpty(term)
                || r.ReferenceNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (r.BatchNumber ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(r => new EligibleSourceSampleDto(r.SampleId, r.ReferenceNumber, r.Name, r.BatchNumber, r.TestCode))
            .ToList();
    }

    // ---- Create / edit ----

    public async Task<WorkingStandardQualificationDto> CreateAsync(CreateQualificationRequest r, int userId, CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow.UtcDateTime;
        var q = new WorkingStandardQualification
        {
            Kind = r.Kind,
            Status = WorkingStandardQualificationStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = nowUtc,
        };

        if (r.Kind == WorkingStandardQualificationKind.Requalification)
        {
            if (!r.WorkingStandardMaterialId.HasValue)
                throw new InvalidOperationException("Choose the working standard lot to requalify.");
            var lot = await _db.Materials.FirstOrDefaultAsync(m => m.Id == r.WorkingStandardMaterialId.Value && m.MaterialType == MaterialType.WorkingStandard, ct)
                ?? throw new NotFoundException("Working standard lot not found.");
            await EnsureSectionAsync(lot.SectionId, userId, ct, "Working standard lot not found.");

            var openCodes = await _db.WorkingStandardQualifications
                .Where(x => x.WorkingStandardMaterialId == lot.Id)
                .Select(x => new { x.Code, x.Status }).ToListAsync(ct);
            var openCode = openCodes.FirstOrDefault(x => IsOpen(x.Status))?.Code;
            if (openCode != null)
                throw new InvalidOperationException($"Lot {lot.Code} already has an open qualification ({openCode}).");
            if (!lot.MaterialMasterEntryId.HasValue)
                throw new InvalidOperationException("The lot has no master entry.");
            if (r.QuantityGrams.HasValue || !string.IsNullOrWhiteSpace(r.Location))
                throw new InvalidOperationException("Quantity and location belong to the lot for a requalification.");

            q.SectionId = lot.SectionId;
            q.WorkingStandardMaterialId = lot.Id;
            q.MaterialMasterEntryId = lot.MaterialMasterEntryId.Value;
            q.SourceMaterialName = lot.MaterialName;
            q.SourceBatchNumber = lot.BatchNumber;
        }
        else
        {
            if (!r.MaterialMasterEntryId.HasValue)
                throw new InvalidOperationException("Choose the reference standard master entry.");
            var entry = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == r.MaterialMasterEntryId.Value, ct)
                ?? throw new NotFoundException("Master entry not found.");
            await EnsureSectionAsync(entry.SectionId, userId, ct, "Material master entry not found.");
            if (entry.Category != MaterialMasterCategory.ReferenceStandard)
                throw new InvalidOperationException($"Master entry \"{entry.Name}\" is not a reference standard.");
            if (!entry.IsActive)
                throw new InvalidOperationException($"Master entry \"{entry.Name}\" is inactive.");

            var hasName = !string.IsNullOrWhiteSpace(r.SourceMaterialName) || !string.IsNullOrWhiteSpace(r.SourceBatchNumber);
            if (r.SourceSampleId.HasValue && hasName)
                throw new InvalidOperationException("Choose a received raw material sample or enter the material name and batch, not both.");
            if (r.SourceSampleId.HasValue)
            {
                var eligible = (await GetEligibleSourceSamplesAsync(null, userId, ct)).FirstOrDefault(s => s.SampleId == r.SourceSampleId.Value)
                    ?? throw new InvalidOperationException("That sample has no approved assay result.");
                if (string.IsNullOrWhiteSpace(eligible.MaterialName))
                    throw new InvalidOperationException("That sample has no item name.");
                q.SourceSampleId = eligible.SampleId;
                q.SourceMaterialName = eligible.MaterialName;
                q.SourceBatchNumber = eligible.BatchNumber ?? string.Empty;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(r.SourceMaterialName) || string.IsNullOrWhiteSpace(r.SourceBatchNumber))
                    throw new InvalidOperationException("Choose either a received raw material sample or enter the material name and batch.");
                q.SourceMaterialName = r.SourceMaterialName.Trim();
                q.SourceBatchNumber = r.SourceBatchNumber.Trim();
            }

            if (r.QuantityGrams is not > 0)
                throw new InvalidOperationException("Enter the quantity in grams.");
            if (string.IsNullOrWhiteSpace(r.Location))
                throw new InvalidOperationException("Enter the storage location.");

            q.SectionId = entry.SectionId;
            q.MaterialMasterEntryId = entry.Id;
            q.QuantityGrams = r.QuantityGrams;
            q.Location = r.Location.Trim();
        }

        q.MoisturePercent = ValidateMoisture(r.MoisturePercent);

        _db.CurrentUserId = userId;
        _db.WorkingStandardQualifications.Add(q);
        var saved = false;
        for (var attempt = 0; attempt < 2 && !saved; attempt++)
        {
            q.Code = await SolutionPreparationCode.NextAsync(
                _db.WorkingStandardQualifications.Select(x => x.Code), "WSQ-", _clock.ToLabLocal(nowUtc), ct);
            saved = await _db.TrySaveChangesAsync(UniqueIndexNames.WorkingStandardQualificationCode);
        }
        if (!saved)
            throw new InvalidOperationException("Could not allocate a qualification code - try again.");

        return await GetAsync(q.Id, userId, ct);
    }

    public async Task<WorkingStandardQualificationDto> UpdateDraftAsync(int id, UpdateQualificationRequest r, int userId, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        if (q.Status != WorkingStandardQualificationStatus.Draft)
            throw new InvalidOperationException("Only a draft qualification can be edited.");

        if (q.Kind == WorkingStandardQualificationKind.Requalification)
        {
            if (r.QuantityGrams.HasValue || !string.IsNullOrWhiteSpace(r.Location))
                throw new InvalidOperationException("Quantity and location belong to the lot for a requalification.");
        }
        else
        {
            if (r.QuantityGrams is not > 0)
                throw new InvalidOperationException("Enter the quantity in grams.");
            if (string.IsNullOrWhiteSpace(r.Location))
                throw new InvalidOperationException("Enter the storage location.");
            q.QuantityGrams = r.QuantityGrams;
            q.Location = r.Location.Trim();
        }
        q.MoisturePercent = ValidateMoisture(r.MoisturePercent);

        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, userId, ct);
    }

    // ---- Documents ----

    public async Task<WorkingStandardDocumentDto> UploadDocumentAsync(
        int id, WorkingStandardDocumentKind kind, string fileName, string contentType, byte[] content,
        int userId, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        if (q.Status != WorkingStandardQualificationStatus.Draft)
            throw new InvalidOperationException("Documents can only be attached to a draft qualification.");
        ValidateFile(fileName, contentType, content);

        var doc = new WorkingStandardDocument
        {
            WorkingStandardQualificationId = q.Id,
            Kind = kind,
            FileName = Path.GetFileName(fileName).Trim(),
            ContentType = NormalizeContentType(contentType),
            FilePath = "pending",
            UploadedByUserId = userId,
            UploadedAt = _clock.UtcNow.UtcDateTime,
        };
        _db.CurrentUserId = userId;
        _db.WorkingStandardDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        try
        {
            doc.FilePath = await _storage.SaveAsync($"working-standards/{q.Id}/{doc.Id}{Path.GetExtension(doc.FileName)}", content);
        }
        catch
        {
            // No half-saved current document: drop the row, previous stays current.
            _db.WorkingStandardDocuments.Remove(doc);
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        foreach (var old in q.Documents.Where(d => d.Kind == kind && d.Id != doc.Id && d.SupersededByDocumentId == null))
            old.SupersededByDocumentId = doc.Id;
        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return ToDocDto(doc, name);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadDocumentAsync(int documentId, int userId, CancellationToken ct = default)
    {
        var doc = await _db.WorkingStandardDocuments.Include(d => d.WorkingStandardQualification).FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException($"Document {documentId} not found.");
        await EnsureSectionAsync(doc.WorkingStandardQualification!.SectionId, userId, ct);
        return (await _storage.ReadAsync(doc.FilePath), doc.ContentType, doc.FileName);
    }

    // Used by HplcRunService when assigning (Task 5). Requires Documents loaded.
    public static string? AssignProblem(WorkingStandardQualification q)
    {
        if (q.Status != WorkingStandardQualificationStatus.Draft)
            return "Only a draft qualification can be assigned.";
        if (q.MoisturePercent == null)
            return "Enter the moisture content first.";
        if (q.Kind == WorkingStandardQualificationKind.Initial && q.SourceSampleId == null
            && !q.Documents.Any(d => d.Kind == WorkingStandardDocumentKind.SourceReport && d.SupersededByDocumentId == null))
            return "Attach the raw material's first test report first.";
        return null;
    }

    // ---- Helpers ----

    private static decimal? ValidateMoisture(decimal? moisture)
    {
        if (moisture is < 0 or >= 100)
            throw new InvalidOperationException("Moisture must be at least 0 and below 100 %.");
        return moisture;
    }

    private static void ValidateFile(string fileName, string contentType, byte[] content)
    {
        if (content == null || content.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");
        if (content.Length > MaxDocumentBytes)
            throw new InvalidOperationException($"File exceeds the maximum allowed size of {MaxDocumentBytes / 1024 / 1024} MB.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("A file name is required.");
        var normalized = NormalizeContentType(contentType);
        if (!AllowedContentTypes.Contains(normalized))
            throw new InvalidOperationException($"File type \"{normalized}\" is not supported. Allowed types: PDF, PNG, JPEG.");
    }

    private static string NormalizeContentType(string contentType) => contentType.Split(';')[0].Trim().ToLowerInvariant();

    private async Task EnsureSectionAsync(int sectionId, int userId, CancellationToken ct, string notFound = "Working standard qualification not found.")
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(sectionId))
            throw new NotFoundException(notFound);
    }

    private async Task<WorkingStandardQualification> LoadAsync(int id, int userId, CancellationToken ct)
    {
        var q = await _db.WorkingStandardQualifications
            .Include(x => x.Documents)
            .Include(x => x.MaterialMasterEntry)
            .Include(x => x.WorkingStandardMaterial)
            .Include(x => x.SourceSample)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Working standard qualification {id} not found.");
        await EnsureSectionAsync(q.SectionId, userId, ct);
        return q;
    }

    private static WorkingStandardDocumentDto ToDocDto(WorkingStandardDocument d, string? uploader) =>
        new(d.Id, d.Kind, d.FileName, d.ContentType, d.UploadedAt, uploader, d.SupersededByDocumentId == null);

    private async Task<WorkingStandardQualificationDto> BuildDtoAsync(WorkingStandardQualification q, CancellationToken ct)
    {
        var userIds = new[] { q.CreatedByUserId, q.PreparedByUserId, q.ReviewedByUserId, q.ApprovedByUserId, q.RejectedByUserId }
            .Select(x => x)
            .Concat(q.Documents.Select(d => (int?)d.UploadedByUserId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(int? uid) => uid.HasValue ? names.GetValueOrDefault(uid.Value) : null;

        var runSample = await _db.HplcRunSamples.AsNoTracking().Include(s => s.HplcRun)
            .Where(s => s.WorkingStandardQualificationId == q.Id && s.Status == HplcRunSampleStatus.Assigned
                && (q.Status != WorkingStandardQualificationStatus.Draft || s.HplcRun!.Status != HplcRunStatus.Abandoned))
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);

        WorkingStandardRunLinkDto? run = null;
        var evidenceIds = new List<int>();
        if (runSample != null)
        {
            run = new WorkingStandardRunLinkDto(runSample.HplcRunId, runSample.HplcRun!.EquipmentId, runSample.HplcRun.Code, runSample.Id, runSample.Status);
            evidenceIds = await _db.HplcEvidences.AsNoTracking()
                .Where(e => e.SupersededByEvidenceId == null && e.HplcRunId == runSample.HplcRunId
                    && (e.HplcRunSampleId == runSample.Id
                        || (e.Context == HplcEvidenceContext.Sst && e.Kind == HplcEvidenceKind.StandardReport)))
                .OrderBy(e => e.Id).Select(e => e.Id).ToListAsync(ct);
        }

        List<decimal>? replicates = string.IsNullOrWhiteSpace(q.ReplicateAssaysJson)
            ? null : JsonSerializer.Deserialize<List<decimal>>(q.ReplicateAssaysJson);

        return new WorkingStandardQualificationDto(
            q.Id, q.Version, q.Code, q.Kind, q.Status, q.SectionId,
            q.WorkingStandardMaterialId, q.WorkingStandardMaterial?.Code,
            q.MaterialMasterEntryId, q.MaterialMasterEntry?.Code ?? string.Empty, q.MaterialMasterEntry?.Name ?? string.Empty,
            q.SourceSampleId, q.SourceSample?.ReferenceNumber, q.SourceMaterialName, q.SourceBatchNumber,
            q.QuantityGrams, q.Location, q.MoisturePercent,
            replicates, q.MeanAssayPercent, q.RsdPercent, q.PotencyPercent,
            q.Passed, q.FailureReasons,
            NameOf(q.CreatedByUserId), q.CreatedAt,
            NameOf(q.PreparedByUserId), q.PreparedAt, NameOf(q.ReviewedByUserId), q.ReviewedAt,
            NameOf(q.ApprovedByUserId), q.ApprovedAt, NameOf(q.RejectedByUserId), q.RejectedAt,
            q.RejectReason, q.ReturnReason,
            run,
            q.Documents.OrderBy(d => d.Id).Select(d => ToDocDto(d, NameOf(d.UploadedByUserId))).ToList(),
            evidenceIds);
    }
}
