using System.Globalization;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Labels;

public enum ScanOutcome
{
    Found = 1,
    NotFound = 2,
    Ambiguous = 3
}

public sealed record ScanMatchDto(
    LabelEntityType EntityType,
    int Id,
    string Code,
    string DisplayName,
    string Status,
    bool Usable,
    string? BlockedReason,
    string NavigationTarget,
    int? LinkedEquipmentId);

public sealed record ScanResolveResponse(
    ScanOutcome Outcome,
    bool LayoutCorrected,
    string NormalizedValue,
    ScanMatchDto? Match,
    IReadOnlyList<ScanMatchDto> Candidates,
    string? Hint);

public interface IScanResolveService
{
    Task<ScanResolveResponse> ResolveAsync(string? value, int userId, CancellationToken ct);
}

/// <summary>
/// Normalizes barcode/manual scan input and resolves exact section-scoped record matches.
/// </summary>
public sealed class ScanResolveService(ILabelDataSource dataSource) : IScanResolveService
{
    public const int MaxValueLength = 300;
    private const string ArabicLayoutNotFoundHint =
        "The keyboard was in Arabic. Switch to English and scan again.";

    private static readonly LabelEntityType[] AllEntityTypes = Enum.GetValues<LabelEntityType>();

    private readonly ILabelDataSource _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

    public async Task<ScanResolveResponse> ResolveAsync(string? value, int userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(MessageCodes.LabelScanValueRequired, "Scan value is required.");
        }

        if (value.Length > MaxValueLength)
        {
            throw new BusinessRuleException(
                MessageCodes.LabelScanValueTooLong,
                $"Scan value must not exceed {MaxValueLength} characters.",
                new Dictionary<string, string> { ["maxLength"] = MaxValueLength.ToString(CultureInfo.InvariantCulture) });
        }

        var normalized = ScanInputNormalizer.Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized.Value))
        {
            throw new BusinessRuleException(MessageCodes.LabelScanValueRequired, "Scan value is required.");
        }

        if (normalized.Value.Length > MaxValueLength)
        {
            throw new BusinessRuleException(
                MessageCodes.LabelScanValueTooLong,
                $"Scan value must not exceed {MaxValueLength} characters.",
                new Dictionary<string, string> { ["maxLength"] = MaxValueLength.ToString(CultureInfo.InvariantCulture) });
        }

        if (LabelPayload.TryParse(normalized.Value, out var prefixedType, out var prefixedCode))
        {
            var record = await _dataSource.FindByCodeAsync(prefixedType, prefixedCode, userId, ct);
            return record is null
                ? NotFoundResponse(normalized)
                : FoundResponse(normalized, record);
        }

        var matches = new List<LabelRecord>();
        foreach (var entityType in AllEntityTypes)
        {
            var match = await _dataSource.FindByCodeAsync(entityType, normalized.Value, userId, ct);
            if (match is not null)
            {
                matches.Add(match);
            }
        }

        if (matches.Count == 0)
        {
            return NotFoundResponse(normalized);
        }

        if (matches.Count == 1)
        {
            return FoundResponse(normalized, matches[0]);
        }

        return new ScanResolveResponse(
            ScanOutcome.Ambiguous,
            normalized.LayoutCorrected,
            normalized.Value,
            Match: null,
            Candidates: matches.Select(ToMatchDto).ToList(),
            Hint: null);
    }

    private static ScanResolveResponse NotFoundResponse(NormalizedScan normalized) =>
        new(
            ScanOutcome.NotFound,
            normalized.LayoutCorrected,
            normalized.Value,
            Match: null,
            Candidates: Array.Empty<ScanMatchDto>(),
            Hint: normalized.LayoutCorrected ? ArabicLayoutNotFoundHint : null);

    private static ScanResolveResponse FoundResponse(NormalizedScan normalized, LabelRecord record) =>
        new(
            ScanOutcome.Found,
            normalized.LayoutCorrected,
            normalized.Value,
            Match: ToMatchDto(record),
            Candidates: Array.Empty<ScanMatchDto>(),
            Hint: null);

    private static ScanMatchDto ToMatchDto(LabelRecord record) =>
        new(
            record.Type,
            record.Id,
            record.Code ?? string.Empty,
            record.DisplayName,
            record.Status,
            record.Usable,
            record.BlockedReason,
            RecordNavigationTargets.ForLabelEntity(record.Type, record.Id),
            record.LinkedEquipmentId);
}
