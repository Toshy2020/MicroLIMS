using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MyWork.Sources;

// Controlled-document work assigned to the caller:
//   - open technical review tasks (DocumentReviewService.GetMyAssignedReviewTasksAsync);
//   - pending document approvals where the caller is the assigned approver (DocumentApprovalService.GetApprovalTasksAsync);
//   - open training / reading assignments (TrainingAssignmentService.GetUserAssignmentsAsync).
// One SQL statement for the page and one for the counts: the three kinds are projected to one shape and concatenated.
public sealed class DocumentsSource : IMyWorkSource
{
    private const string ReviewTask = "document-review";
    private const string ApprovalTask = "document-approval";
    private const string TrainingAssignment = "training-assignment";

    private readonly IMicroLimsDbContext _db;

    public DocumentsSource(IMicroLimsDbContext db) => _db = db;

    public string Key => "documents";

    public bool AppliesTo(MyWorkContext ctx) => true;

    private sealed class Row
    {
        public string Kind { get; init; } = string.Empty;
        public int Id { get; init; }
        public int DocumentId { get; init; }
        public string Identifier { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string RevisionNumber { get; init; } = string.Empty;
        public DateTime? Due { get; init; }
        public DateTime? Anchor { get; init; }
        public int SectionId { get; init; }
        public string SectionName { get; init; } = string.Empty;
    }

    private IQueryable<Row> Rows(MyWorkContext ctx)
    {
        var userId = ctx.UserId;

        var reviews = _db.DocumentReviewTasks.AsNoTracking()
            .Where(t => t.AssignedReviewerUserId == userId
                && (t.Status == ReviewTaskStatus.Pending || t.Status == ReviewTaskStatus.InProgress))
            .Select(t => new Row
            {
                Kind = ReviewTask,
                Id = t.Id,
                DocumentId = t.DocumentRevision.DocumentMasterId,
                Identifier = t.DocumentRevision.DocumentMaster.MicroLimsDocumentId,
                Title = t.DocumentRevision.DocumentMaster.Title,
                RevisionNumber = t.DocumentRevision.RevisionNumber,
                Due = t.DueDate,
                Anchor = t.AssignedAt,
                SectionId = t.DocumentRevision.DocumentMaster.SectionId,
                SectionName = t.DocumentRevision.DocumentMaster.Section.Name,
            });

        var approvals = _db.DocumentApprovalTasks.AsNoTracking()
            .Where(t => t.AssignedApproverUserId == userId && t.Status == DocumentApprovalTaskStatus.Pending)
            .Select(t => new Row
            {
                Kind = ApprovalTask,
                Id = t.Id,
                DocumentId = t.DocumentRevision.DocumentMasterId,
                Identifier = t.DocumentRevision.DocumentMaster.MicroLimsDocumentId,
                Title = t.DocumentRevision.DocumentMaster.Title,
                RevisionNumber = t.DocumentRevision.RevisionNumber,
                Due = t.DueDate,
                Anchor = t.AssignedAt,
                SectionId = t.DocumentRevision.DocumentMaster.SectionId,
                SectionName = t.DocumentRevision.DocumentMaster.Section.Name,
            });

        var trainings = _db.DocumentTrainingAssignments.AsNoTracking()
            .Where(a => a.AssignedUserId == userId
                && (a.Status == TrainingAssignmentStatus.Assigned
                    || a.Status == TrainingAssignmentStatus.Reading
                    || a.Status == TrainingAssignmentStatus.Overdue
                    || a.Status == TrainingAssignmentStatus.KafPending
                    || a.Status == TrainingAssignmentStatus.Failed))
            .Select(a => new Row
            {
                Kind = TrainingAssignment,
                Id = a.Id,
                DocumentId = a.DocumentMasterId,
                Identifier = a.DocumentMaster.MicroLimsDocumentId,
                Title = a.DocumentMaster.Title,
                RevisionNumber = a.DocumentRevision.RevisionNumber,
                Due = a.DueDateUtc,
                Anchor = a.AssignedDateUtc,
                SectionId = a.DocumentMaster.SectionId,
                SectionName = a.DocumentMaster.Section.Name,
            });

        return reviews.Concat(approvals).Concat(trainings);
    }

    public async Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct)
    {
        var counts = await CountAsync(ctx, ct);
        var total = counts.Values.Sum();
        var rows = total == 0
            ? new List<Row>()
            : await Rows(ctx)
                .OrderBy(r => r.Due == null).ThenBy(r => r.Due).ThenBy(r => r.Kind).ThenBy(r => r.Id)
                .Take(take).ToListAsync(ct);

        var items = rows.Select(r => ToItem(ctx, r)).ToList();
        return new MyWorkSourceResult(items, new Dictionary<string, int> { ["documents"] = total }, new Dictionary<string, int>());
    }

    // Urgency by due time, bucketed in SQL with the same boundaries as MyWorkUrgency.ForDueDate.
    public async Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct)
    {
        var now = ctx.NowUtc;
        var endOfDay = ctx.EndOfLabDayUtc;
        var buckets = await Rows(ctx)
            .GroupBy(r => r.Due == null ? MyWorkUrgencyLevel.None
                : r.Due < now ? MyWorkUrgencyLevel.Overdue
                : r.Due <= endOfDay ? MyWorkUrgencyLevel.Today
                : MyWorkUrgencyLevel.Later)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return buckets.ToDictionary(b => b.Level, b => b.Count);
    }

    private static MyWorkItem ToItem(MyWorkContext ctx, Row r)
    {
        var context = r.Kind switch
        {
            ReviewTask => $"Technical review · Rev {r.RevisionNumber}",
            ApprovalTask => $"Document approval · Rev {r.RevisionNumber}",
            _ => $"Training / reading · Rev {r.RevisionNumber}",
        };

        return new MyWorkItem(
            $"{r.Kind}:{r.Id}",
            "documents",
            r.Identifier,
            r.Title,
            context,
            r.Due,
            MyWorkUrgency.ForDueDate(r.Due, ctx.NowUtc, ctx.EndOfLabDayUtc),
            r.Due ?? r.Anchor ?? ctx.NowUtc,
            false,
            null,
            new MyWorkTarget(r.Kind, DocumentId: r.DocumentId, TaskId: r.Id),
            Array.Empty<string>(),
            r.SectionId,
            r.SectionName,
            null);
    }
}
