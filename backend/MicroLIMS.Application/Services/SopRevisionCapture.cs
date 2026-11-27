using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Services;

public interface ISopRevisionCapture
{
    Task CaptureIfNeededAsync(TestOrder order, CancellationToken cancellationToken = default);
}

public sealed class SopRevisionCapture(
    IMicroLimsDbContext db,
    ItemSopResolver resolver,
    TimeProvider clock) : ISopRevisionCapture
{
    public async Task CaptureIfNeededAsync(TestOrder order, CancellationToken cancellationToken = default)
    {
        if (order.SopCaptureStatus is not null)
            return;

        var itemId = await db.Samples.AsNoTracking()
            .Where(s => s.Id == order.SampleId)
            .Select(s => s.ItemId)
            .SingleOrDefaultAsync(cancellationToken);

        if (itemId is null)
            return;

        // A database trigger lets the eight columns go from empty to a value once, so two first steps recorded at the
        // same time must not both write them. Inside a transaction the order row is locked first, which makes the second
        // writer wait for the first to commit; it then reads what the first stored and keeps it.
        if (db.Database.CurrentTransaction is not null && db.Database.ProviderName == NpgsqlProvider)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"TestOrders\" WHERE \"Id\" = {order.Id} FOR UPDATE", cancellationToken);
        if (db.Database.IsRelational() && await AdoptStoredCaptureAsync(order, cancellationToken))
            return;

        var resolution = await resolver.ResolveAsync(itemId.Value, cancellationToken);
        var captured = resolution.Status == SopResolutionStatus.Effective;
        order.SopCaptureStatus = captured ? SopCaptureStatus.Captured : Map(resolution.Status);
        if (captured)
        {
            order.SopDocumentMasterId = resolution.DocumentMasterId;
            order.SopDocumentCode = resolution.DocumentCode;
            order.SopDocumentTitle = resolution.Title;
            order.SopRevisionId = resolution.RevisionId;
            order.SopRevisionNumber = resolution.RevisionNumber;
            order.SopEffectiveDateUtc = resolution.EffectiveDate;
        }
        order.SopCapturedAtUtc = clock.GetUtcNow().UtcDateTime;
    }

    // True when another writer already stored the capture. Adopt those values without making them dirty.
    private async Task<bool> AdoptStoredCaptureAsync(TestOrder order, CancellationToken cancellationToken)
    {
        var entry = db.Entry(order);
        var stored = await entry.GetDatabaseValuesAsync(cancellationToken);
        if (stored?[nameof(TestOrder.SopCaptureStatus)] is null)
            return false;

        // Rebase only a capture-only change. Check both snapshots so a business edit made after
        // this method returns still cannot overwrite a change already made by the first writer.
        var canRefreshVersion = true;
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            switch (name)
            {
                case nameof(TestOrder.SopCaptureStatus):
                case nameof(TestOrder.SopDocumentMasterId):
                case nameof(TestOrder.SopDocumentCode):
                case nameof(TestOrder.SopDocumentTitle):
                case nameof(TestOrder.SopRevisionId):
                case nameof(TestOrder.SopRevisionNumber):
                case nameof(TestOrder.SopEffectiveDateUtc):
                case nameof(TestOrder.SopCapturedAtUtc):
                    property.CurrentValue = stored[name];
                    property.OriginalValue = stored[name];
                    property.IsModified = false;
                    break;
                case "xmin":
                    break;
                default:
                    canRefreshVersion &= !property.IsModified
                        && property.Metadata.GetValueComparer().Equals(property.OriginalValue, property.CurrentValue)
                        && property.Metadata.GetValueComparer().Equals(property.OriginalValue, stored[name]);
                    break;
            }
        }

        if (canRefreshVersion)
            entry.Property("xmin").OriginalValue = stored["xmin"];
        return true;
    }

    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static SopCaptureStatus Map(SopResolutionStatus status) => status switch
    {
        SopResolutionStatus.NoSopNumber => SopCaptureStatus.NoSopNumber,
        SopResolutionStatus.NotFound => SopCaptureStatus.NotFound,
        SopResolutionStatus.NoEffectiveRevision => SopCaptureStatus.NoEffectiveRevision,
        SopResolutionStatus.Ambiguous => SopCaptureStatus.Ambiguous,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
