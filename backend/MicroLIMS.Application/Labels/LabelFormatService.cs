using System.Globalization;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Labels;

public sealed record LabelFormatDto(
    int Id,
    uint Version,
    LabelEntityType EntityType,
    decimal WidthMm,
    decimal HeightMm,
    LabelLayout Layout,
    int DefaultCopies);

public sealed record UpdateLabelFormatRequest(
    decimal WidthMm,
    decimal HeightMm,
    LabelLayout Layout,
    int DefaultCopies);

public interface ILabelFormatService
{
    Task<IReadOnlyList<LabelFormatDto>> GetAllAsync(CancellationToken ct);
    Task UpdateAsync(int id, UpdateLabelFormatRequest request, uint expectedVersion, CancellationToken ct);
}

/// <summary>
/// Reads and updates per-entity-type barcode label formats with optimistic concurrency.
/// </summary>
public sealed class LabelFormatService(IMicroLimsDbContext db) : ILabelFormatService
{
    private const decimal MinDimensionMm = 10m;
    private const decimal MaxDimensionMm = 150m;
    private const int MinCopies = 1;
    private const int MaxCopies = 50;

    private readonly IMicroLimsDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<LabelFormatDto>> GetAllAsync(CancellationToken ct) =>
        await _db.LabelFormats
            .AsNoTracking()
            .OrderBy(f => f.EntityType)
            .Select(f => new LabelFormatDto(
                f.Id,
                f.Version,
                f.EntityType,
                f.WidthMm,
                f.HeightMm,
                f.Layout,
                f.DefaultCopies))
            .ToListAsync(ct);

    public async Task UpdateAsync(int id, UpdateLabelFormatRequest request, uint expectedVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.WidthMm < MinDimensionMm || request.WidthMm > MaxDimensionMm)
        {
            throw new BusinessRuleException(
                MessageCodes.LabelWidthOutOfRange,
                $"Label width must be between {MinDimensionMm} and {MaxDimensionMm} mm.",
                new Dictionary<string, string>
                {
                    ["maxMm"] = MaxDimensionMm.ToString(CultureInfo.InvariantCulture),
                    ["minMm"] = MinDimensionMm.ToString(CultureInfo.InvariantCulture),
                });
        }

        if (request.HeightMm < MinDimensionMm || request.HeightMm > MaxDimensionMm)
        {
            throw new BusinessRuleException(
                MessageCodes.LabelHeightOutOfRange,
                $"Label height must be between {MinDimensionMm} and {MaxDimensionMm} mm.",
                new Dictionary<string, string>
                {
                    ["maxMm"] = MaxDimensionMm.ToString(CultureInfo.InvariantCulture),
                    ["minMm"] = MinDimensionMm.ToString(CultureInfo.InvariantCulture),
                });
        }

        if (request.DefaultCopies < MinCopies || request.DefaultCopies > MaxCopies)
        {
            throw new BusinessRuleException(
                MessageCodes.LabelDefaultCopiesOutOfRange,
                $"Default copies must be between {MinCopies} and {MaxCopies}.",
                new Dictionary<string, string>
                {
                    ["max"] = MaxCopies.ToString(CultureInfo.InvariantCulture),
                    ["min"] = MinCopies.ToString(CultureInfo.InvariantCulture),
                });
        }

        if (!Enum.IsDefined(request.Layout))
        {
            throw new BusinessRuleException(MessageCodes.LabelLayoutInvalid, "Label layout is invalid.");
        }

        var format = await _db.LabelFormats.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException(
                MessageCodes.LabelFormatNotFound,
                $"Label format {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        _db.ExpectedVersion = expectedVersion;
        RecordVersion.EnsureCurrent(_db, format);

        format.WidthMm = request.WidthMm;
        format.HeightMm = request.HeightMm;
        format.Layout = request.Layout;
        format.DefaultCopies = request.DefaultCopies;

        await _db.SaveChangesAsync(ct);
    }
}
