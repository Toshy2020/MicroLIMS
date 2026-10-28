using MicroLIMS.Application.DTOs.DocumentControl;

namespace MicroLIMS.Application.Interfaces.DocumentControl;

// Succession for deactivated users' document responsibilities. Deactivating a user never moves their work;
// it shows up here, and a person with authority hands it over by an explicit, audited act.
public interface IDocumentReassignmentService
{
    // The open responsibilities held by deactivated users in the departments the caller may act on
    // (every department for Documents.ConfigManage; otherwise the departments a Documents.Reassign holder belongs to).
    Task<ReassignmentQueueDto> GetQueueAsync(int? departmentId, int userId);

    Task<ReassignmentCountDto> GetCountAsync(int userId);

    // The same count, or null when the user has no authority to reassign (so a badge can simply stay hidden).
    Task<ReassignmentCountDto?> TryGetCountAsync(int userId);

    // Active members of the item's department whose role holds the permission the role needs (and who pass the
    // segregation-of-duties rules), i.e. exactly who ReassignAsync would accept.
    Task<IReadOnlyList<ReassignmentCandidateDto>> GetCandidatesAsync(string itemType, int itemId, int userId);

    Task<ReassignmentResultDto> ReassignAsync(ReassignRequest request, int userId);

    // Notifies each department's Section Heads about items that have waited longer than
    // DocumentControl.ReassignmentEscalationDays. Never reassigns. Returns the notifications created.
    Task<int> NotifyOverdueAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default);
}
