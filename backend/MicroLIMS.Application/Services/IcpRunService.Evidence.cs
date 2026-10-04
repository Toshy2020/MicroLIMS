using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public partial class IcpRunService
{
    // Matches the MaterialDocuments:MaxFileSizeBytes default (25 MB).
    private const long MaxEvidenceBytes = 26_214_400;
    private static readonly HashSet<string> AllowedEvidenceContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg"
    };

    public async Task<IcpEvidenceDto> UploadEvidenceAsync(
        int runId, int? runSampleId, IcpEvidenceContext context, IcpEvidenceKind kind,
        string fileName, string contentType, Stream content, int userId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        await EnsureAccessAsync(run, userId, ct);

        if (run.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");

        var allowed = context switch
        {
            IcpEvidenceContext.Calibration => kind == IcpEvidenceKind.CalibrationReport,
            IcpEvidenceContext.Sample => kind is IcpEvidenceKind.SampleReport or IcpEvidenceKind.Other,
            _ => kind == IcpEvidenceKind.Other,
        };
        if (!allowed)
            throw new InvalidOperationException($"{kind} evidence can't be attached to the {context} context.");

        var bytes = await ReadBoundedAsync(content, ct);
        ValidateEvidenceFile(fileName, contentType, bytes);

        IcpRunSample? runSample = null;
        if (context == IcpEvidenceContext.Sample)
        {
            if (!runSampleId.HasValue)
                throw new InvalidOperationException("Choose the sample this evidence belongs to.");
            runSample = run.Samples.FirstOrDefault(s => s.Id == runSampleId.Value)
                ?? throw new NotFoundException($"Sample {runSampleId} not found on this run.");
            if (runSample.Status == IcpRunSampleStatus.Removed)
                throw new InvalidOperationException("Evidence can't be added to a removed sample.");
        }
        else if (runSampleId.HasValue)
            throw new InvalidOperationException("Only sample evidence can be linked to a sample.");

        var evidence = new IcpEvidence
        {
            IcpRunId = run.Id,
            IcpRunSampleId = runSample?.Id,
            Context = context,
            Kind = kind,
            FilePath = "pending",
            FileName = Path.GetFileName(fileName).Trim(),
            ContentType = NormalizeContentType(contentType),
            UploadedByUserId = userId,
            UploadedAt = _clock.UtcNow.UtcDateTime,
        };

        _db.IcpEvidences.Add(evidence);
        await _db.SaveChangesAsync(ct);

        var key = $"icp-evidence/{run.Id}/{evidence.Id}{Path.GetExtension(evidence.FileName)}";
        evidence.FilePath = await _storage.SaveAsync(key, bytes);
        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return ToEvidenceDto(evidence, name);
    }

    public async Task<IcpEvidenceDto> SupersedeEvidenceAsync(
        int evidenceId, string reason, string fileName, string contentType, Stream content, int userId, CancellationToken ct = default)
    {
        var old = await _db.IcpEvidences.Include(e => e.IcpRun).FirstOrDefaultAsync(e => e.Id == evidenceId, ct)
            ?? throw new NotFoundException($"Evidence {evidenceId} not found.");

        await EnsureAccessAsync(old.IcpRun!, userId, ct);

        if (old.SupersededByEvidenceId.HasValue)
            throw new InvalidOperationException("This evidence has already been superseded.");
        if (old.IcpRun!.Status != IcpRunStatus.Open)
            throw new InvalidOperationException("The run is closed.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required.");

        if (old.IcpRunSampleId is int oldSampleId
            && await _db.IcpRunSamples.AnyAsync(s => s.Id == oldSampleId && s.Status == IcpRunSampleStatus.Removed, ct))
            throw new InvalidOperationException("Evidence can't be added to a removed sample.");

        var bytes = await ReadBoundedAsync(content, ct);
        ValidateEvidenceFile(fileName, contentType, bytes);

        var newEvidence = new IcpEvidence
        {
            IcpRunId = old.IcpRunId,
            IcpRunSampleId = old.IcpRunSampleId,
            Context = old.Context,
            Kind = old.Kind,
            FilePath = "pending",
            FileName = Path.GetFileName(fileName).Trim(),
            ContentType = NormalizeContentType(contentType),
            UploadedByUserId = userId,
            UploadedAt = _clock.UtcNow.UtcDateTime,
        };
        _db.IcpEvidences.Add(newEvidence);
        old.SupersedeReason = reason.Trim();

        await _db.SaveChangesAsync(ct);

        old.SupersededByEvidenceId = newEvidence.Id;
        var key = $"icp-evidence/{newEvidence.IcpRunId}/{newEvidence.Id}{Path.GetExtension(newEvidence.FileName)}";
        newEvidence.FilePath = await _storage.SaveAsync(key, bytes);

        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return ToEvidenceDto(newEvidence, name);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> DownloadEvidenceAsync(int evidenceId, int userId, CancellationToken ct = default)
    {
        var evidence = await _db.IcpEvidences.Include(e => e.IcpRun).FirstOrDefaultAsync(e => e.Id == evidenceId, ct)
            ?? throw new NotFoundException($"Evidence {evidenceId} not found.");

        await EnsureAccessAsync(evidence.IcpRun!, userId, ct);

        var bytes = await _storage.ReadAsync(evidence.FilePath);
        return (bytes, evidence.ContentType, evidence.FileName);
    }

    // Reads at most the limit plus one byte so an oversized upload is rejected without buffering all of it.
    private static async Task<byte[]> ReadBoundedAsync(Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = await content.ReadAsync(buffer, ct)) > 0)
        {
            ms.Write(buffer, 0, n);
            if (ms.Length > MaxEvidenceBytes) break;
        }
        return ms.ToArray();
    }

    private static void ValidateEvidenceFile(string fileName, string contentType, byte[] content)
    {
        if (content == null || content.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");
        if (content.Length > MaxEvidenceBytes)
            throw new InvalidOperationException($"File exceeds the maximum allowed size of {MaxEvidenceBytes / 1024 / 1024} MB.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("A file name is required.");

        var normalized = NormalizeContentType(contentType);
        if (!AllowedEvidenceContentTypes.Contains(normalized))
            throw new InvalidOperationException($"File type \"{normalized}\" is not supported. Allowed types: PDF, PNG, JPEG.");
    }

    private static string NormalizeContentType(string contentType) => contentType.Split(';')[0].Trim().ToLowerInvariant();

    private static IcpEvidenceDto ToEvidenceDto(IcpEvidence e, string? uploaderName) => new(
        e.Id, e.IcpRunId, e.IcpRunSampleId, e.Context, e.Kind, e.FileName, e.ContentType,
        e.UploadedByUserId, uploaderName, e.UploadedAt, e.SupersededByEvidenceId == null, e.SupersedeReason);
}
