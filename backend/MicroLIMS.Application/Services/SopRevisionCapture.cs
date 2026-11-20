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

    private static SopCaptureStatus Map(SopResolutionStatus status) => status switch
    {
        SopResolutionStatus.NoSopNumber => SopCaptureStatus.NoSopNumber,
        SopResolutionStatus.NotFound => SopCaptureStatus.NotFound,
        SopResolutionStatus.NoEffectiveRevision => SopCaptureStatus.NoEffectiveRevision,
        SopResolutionStatus.Ambiguous => SopCaptureStatus.Ambiguous,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
