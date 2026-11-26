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
        var terminalAt = await core.GetTerminalAtUtcAsync(recordId, ct);
        var comment = new QualityComment
        {
            RecordId = recordId, AuthorUserId = userId, Text = QualityRecordCore.RequiredText(text, 2000),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.QualityComments.Add(comment);
        await db.SaveChangesAsync(ct);
        var authorName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        return new QualityCommentDto(comment.Id, comment.Text, comment.AuthorUserId, comment.CreatedAtUtc, authorName,
            terminalAt is DateTime t && comment.CreatedAtUtc > t);
    }, ct);

    public async Task<IReadOnlyList<QualityCommentDto>> ListAsync(int recordId, int userId, int offset, int pageSize,
        CancellationToken ct = default)
    {
        var records = await core.ReadableRecordsAsync(userId, ct);
        if (!await records.AnyAsync(r => r.Id == recordId, ct)) throw QualityRecordCore.Missing();
        var terminalAt = await core.GetTerminalAtUtcAsync(recordId, ct);
        return await db.QualityComments.AsNoTracking().Where(c => c.RecordId == recordId)
            .OrderBy(c => c.Id).Skip(Math.Max(0, offset))
            .Take(Paging.Normalize(1, pageSize, 1).PageSize)
            .Select(c => new QualityCommentDto(
                c.Id,
                c.Text,
                c.AuthorUserId,
                c.CreatedAtUtc,
                db.Users.Where(u => u.Id == c.AuthorUserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                terminalAt != null && c.CreatedAtUtc > terminalAt))
            .ToListAsync(ct);
    }
}
