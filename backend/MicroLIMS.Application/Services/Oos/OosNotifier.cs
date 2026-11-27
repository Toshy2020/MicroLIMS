using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.Oos;

public interface IOosNotifier
{
    Task OpenedAsync(int investigationId, CancellationToken ct = default);
    Task PhaseTwoStartedAsync(int investigationId, CancellationToken ct = default);
    Task ConcludedAsync(int investigationId, CancellationToken ct = default);
    Task DeviationRequestedAsync(int investigationId, CancellationToken ct = default);
}

public sealed class OosNotifier(IServiceScopeFactory scopes) : IOosNotifier
{
    public Task OpenedAsync(int investigationId, CancellationToken ct = default) =>
        PublishAsync(investigationId, "opened", "OosOpened", "was opened", managersOnly: false, ct);

    public Task PhaseTwoStartedAsync(int investigationId, CancellationToken ct = default) =>
        PublishAsync(investigationId, "phase-two-started", "OosPhaseTwoStarted", "entered Phase II", managersOnly: false, ct);

    public Task ConcludedAsync(int investigationId, CancellationToken ct = default) =>
        PublishAsync(investigationId, "concluded", "OosConcluded", "was concluded", managersOnly: false, ct);

    public Task DeviationRequestedAsync(int investigationId, CancellationToken ct = default) =>
        PublishAsync(investigationId, "deviation-requested", "OosDeviationRequested", "has a deviation request", managersOnly: true, ct);

    private async Task PublishAsync(int id, string eventName, string type, string description, bool managersOnly, CancellationToken ct)
    {
        // The publisher stages rows on its DbContext. Give it a fresh scope so only notification work is saved here.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();
        var recipients = scope.ServiceProvider.GetRequiredService<OosRecipientResolver>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var investigation = await db.OosInvestigations.AsNoTracking().Where(i => i.Id == id)
            .Select(i => new { i.Kind, i.Number, i.SampleId, i.SectionId }).SingleAsync(ct);

        var userIds = (await recipients.ResolveAsync(investigation.SectionId, PermissionConstants.OosManage, ct)).ToHashSet();
        if (!managersOnly)
        {
            userIds.UnionWith(await recipients.ResolveAsync(investigation.SectionId, PermissionConstants.OosRequestDeviation, ct));
            var orders = await OosInvestigationService.InvestigationOrderIds(db, id).ToArrayAsync(ct);
            var duties = scope.ServiceProvider.GetRequiredService<SegregationOfDutiesGuard>();
            userIds.UnionWith(await recipients.ResolvePerformingAnalystsAsync(investigation.SectionId, orders, duties, ct));
        }

        var isExcursion = investigation.Kind == Domain.Enums.OosInvestigationKind.Excursion;
        var notificationType = isExcursion
            ? eventName switch
            {
                "opened" => "ExcursionOpened",
                "concluded" => "ExcursionConcluded",
                _ => type
            }
            : type;
        var message = isExcursion
            ? eventName switch
            {
                "opened" => $"Excursion review {investigation.Number} was opened.",
                "concluded" => $"Excursion review {investigation.Number} was closed.",
                _ => $"Excursion review {investigation.Number} {description}."
            }
            : $"OOS investigation {investigation.Number} {description}.";

        await publisher.PublishAsync(new(notificationType, userIds, message,
            "oos-investigation", id, investigation.SectionId, $"oos:{id}:{eventName}", investigation.SampleId), ct);
        await db.SaveChangesAsync(ct);
    }
}
