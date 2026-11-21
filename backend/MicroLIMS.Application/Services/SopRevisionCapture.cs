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

    // True when another writer already stored the capture: the order then carries those values unchanged (and the row
    // version they produced), so this step's own save neither overwrites them nor fails on the stale version.
    private async Task<bool> AdoptStoredCaptureAsync(TestOrder order, CancellationToken cancellationToken)
    {
        var stored = await db.TestOrders.AsNoTracking()
            .Where(o => o.Id == order.Id && o.SopCaptureStatus != null)
            .Select(o => new
            {
                o.SopCaptureStatus, o.SopDocumentMasterId, o.SopDocumentCode, o.SopDocumentTitle, o.SopRevisionId,
                o.SopRevisionNumber, o.SopEffectiveDateUtc, o.SopCapturedAtUtc,
                Version = EF.Property<uint>(o, "xmin")
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (stored is null)
            return false;

        var entry = db.Entry(order);
        void Adopt(string name, object? value)
        {
            entry.Property(name).CurrentValue = value;
            entry.Property(name).OriginalValue = value;
            entry.Property(name).IsModified = false;
        }
        Adopt(nameof(TestOrder.SopCaptureStatus), stored.SopCaptureStatus);
        Adopt(nameof(TestOrder.SopDocumentMasterId), stored.SopDocumentMasterId);
        Adopt(nameof(TestOrder.SopDocumentCode), stored.SopDocumentCode);
        Adopt(nameof(TestOrder.SopDocumentTitle), stored.SopDocumentTitle);
        Adopt(nameof(TestOrder.SopRevisionId), stored.SopRevisionId);
        Adopt(nameof(TestOrder.SopRevisionNumber), stored.SopRevisionNumber);
        Adopt(nameof(TestOrder.SopEffectiveDateUtc), stored.SopEffectiveDateUtc);
        Adopt(nameof(TestOrder.SopCapturedAtUtc), stored.SopCapturedAtUtc);
        entry.Property("xmin").OriginalValue = stored.Version;
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
