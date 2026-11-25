using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities.Quality;

namespace MicroLIMS.Application.Services.Quality;

public class QualityCommentService(IMicroLimsDbContext db, QualityRecordCore core, TimeProvider clock)
{
    public Task<QualityCommentDto> AddAsync(int recordId, uint version, string text, int userId,
        CancellationToken ct = default) => core.MutateAsync(recordId, version, userId, false, async _ =>
    {
        var comment = new QualityComment
        {
            RecordId = recordId, AuthorUserId = userId, Text = QualityRecordCore.RequiredText(text, 2000),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.QualityComments.Add(comment);
        await db.SaveChangesAsync(ct);
        return new QualityCommentDto(comment.Id, comment.Text, comment.AuthorUserId, comment.CreatedAtUtc);
    }, ct);

    public async Task<IReadOnlyList<QualityCommentDto>> ListAsync(int recordId, int userId, int offset, int pageSize,
        CancellationToken ct = default)
    {
        var records = await core.ReadableRecordsAsync(userId, ct);
        if (!await records.AnyAsync(r => r.Id == recordId, ct)) throw QualityRecordCore.Missing();
        return await db.QualityComments.AsNoTracking().Where(c => c.RecordId == recordId)
            .OrderBy(c => c.Id).Skip(Math.Max(0, offset))
            .Take(Paging.Normalize(1, pageSize, 1).PageSize)
            .Select(c => new QualityCommentDto(c.Id, c.Text, c.AuthorUserId, c.CreatedAtUtc)).ToListAsync(ct);
    }
}
