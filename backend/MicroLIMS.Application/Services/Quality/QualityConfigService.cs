using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services.Quality;

public sealed partial class QualityConfigService(IMicroLimsDbContext db, QualityRecordCore core,
    PermissionService permissions, IUserSectionScopeService scope, TimeProvider clock)
{
    internal static readonly System.Linq.Expressions.Expression<Func<QualityRecord, bool>> IsOpenExpression =
        r => !r.IsCancelled
            && (r.DeviationDetail == null || (r.DeviationDetail.Status != QualityDeviationStatus.Rejected
                && r.DeviationDetail.Status != QualityDeviationStatus.Closed && r.DeviationDetail.Status != QualityDeviationStatus.Cancelled))
            && (r.CapaDetail == null || (r.CapaDetail.Status != CapaStatus.Closed && r.CapaDetail.Status != CapaStatus.Cancelled))
            && (r.ChangeControlDetail == null || (r.ChangeControlDetail.Status != ChangeControlStatus.Closed
                && r.ChangeControlDetail.Status != ChangeControlStatus.Rejected && r.ChangeControlDetail.Status != ChangeControlStatus.Cancelled))
            && (r.ComplaintDetail == null || (r.ComplaintDetail.Status != ComplaintStatus.Closed
                && r.ComplaintDetail.Status != ComplaintStatus.Cancelled));

    private async Task EnsureOpenAsync(int recordId, CancellationToken ct)
    {
        if (!await db.QualityRecords.AsNoTracking().Where(r => r.Id == recordId).AnyAsync(IsOpenExpression, ct))
            throw new BusinessRuleException(MessageCodes.QualityRecordTerminal,
                "This quality record is closed, rejected, or cancelled.");
    }

    // These existing configuration tables have no mapped version. Use their existing
    // PostgreSQL row versions and serialize writes, including creation of absent keys.
    private sealed record ConfigVersion(int Id, long Version);

    private IQueryable<ConfigVersion> TemplateVersions => db.Database.SqlQuery<ConfigVersion>(
        $"""SELECT "Id", xmin::text::bigint AS "Version" FROM "QualityChecklistTemplates" """);
    private IQueryable<ConfigVersion> ClassificationVersions => db.Database.SqlQuery<ConfigVersion>(
        $"""SELECT "Id", xmin::text::bigint AS "Version" FROM "QualityClassifications" """);
    private IQueryable<ConfigVersion> DueRuleVersions => db.Database.SqlQuery<ConfigVersion>(
        $"""SELECT "Id", xmin::text::bigint AS "Version" FROM "QualityDueRules" """);

    private IQueryable<QualityChecklistTemplate> EffectiveTemplates(QualityRecordType type, int? sectionId) =>
        db.QualityChecklistTemplates.AsNoTracking()
            .Where(t => t.Type == type && (t.SectionId == sectionId || t.SectionId == null))
            .OrderByDescending(t => t.SectionId == sectionId).Take(1);

    private Task<QualityChecklistTemplateDto?> ProjectTemplateAsync(IQueryable<QualityChecklistTemplate> templates,
        CancellationToken ct) => templates.Select(t => new QualityChecklistTemplateDto(t.Id, t.Type, t.SectionId,
            (uint)TemplateVersions.Where(v => v.Id == t.Id).Select(v => v.Version).Single(),
            t.Items.OrderBy(i => i.Order).ThenBy(i => i.Id)
                .Select(i => new QualityChecklistItemDto(i.Key, i.Order, i.Text, i.IsActive)).ToList()))
        .SingleOrDefaultAsync(ct);

    public async Task<QualityChecklistTemplateDto?> GetTemplateAsync(QualityRecordType type, int? sectionId, int userId,
        CancellationToken ct = default)
    {
        var access = await core.GetReadAccessAsync(userId, ct);
        if (!access.Holds(PermissionConstants.QualityView) || !access.ReadsSection(sectionId))
            throw QualityRecordCore.Missing();
        EnsureType(type);
        return await ProjectTemplateAsync(EffectiveTemplates(type, sectionId), ct);
    }

    public Task<QualityChecklistTemplateDto> SaveTemplateAsync(QualityRecordType type, int? sectionId,
        SaveQualityTemplateRequest request, uint expectedVersion, int userId, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(db, async () =>
        {
            EnsureType(type);
            if (sectionId is int section)
            {
                await RequireAsync(userId, PermissionConstants.QualityInvestigate, ct);
                var sections = await scope.GetConfigurableSectionIdsAsync(userId, ct);
                if (sections is not null && !sections.Contains(section)) throw QualityRecordCore.Denied();
                if (!await db.DocumentSections.AsNoTracking().AnyAsync(s => s.Id == section, ct))
                    throw QualityRecordCore.Missing();
            }
            else await RequireAsync(userId, PermissionConstants.QualityApprove, ct);

            var items = request.Items.Select(i => i with
            {
                Key = QualityRecordCore.RequiredText(i.Key, 36),
                Text = QualityRecordCore.RequiredText(i.Text, 4000)
            }).ToList();
            if (items.Select(i => i.Key).Distinct(StringComparer.Ordinal).Count() != items.Count)
                throw new BusinessRuleException(MessageCodes.QualityDuplicateItem, "Checklist item keys must be unique.");

            await db.Database.ExecuteSqlRawAsync("""LOCK TABLE "QualityChecklistTemplates" IN SHARE ROW EXCLUSIVE MODE""", ct);
            var template = await db.QualityChecklistTemplates.Include(t => t.Items)
                .SingleOrDefaultAsync(t => t.Type == type && t.SectionId == sectionId, ct);
            await EnsureVersionAsync(TemplateVersions, template?.Id, expectedVersion, ct);
            if (template is null)
            {
                template = new QualityChecklistTemplate { Type = type, SectionId = sectionId };
                db.QualityChecklistTemplates.Add(template);
            }
            else
            {
                db.Entry(template).Property(nameof(QualityChecklistTemplate.Type)).IsModified = true;
            }

            var previous = template.Items.ToDictionary(i => i.Key, StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (!previous.Remove(item.Key, out var entity))
                {
                    entity = new QualityChecklistItem { Key = item.Key };
                    template.Items.Add(entity);
                }
                entity.Order = item.Order;
                entity.Text = item.Text;
                entity.IsActive = item.IsActive;
            }
            db.QualityChecklistItems.RemoveRange(previous.Values);
            await db.SaveChangesAsync(ct);
            return await ProjectTemplateAsync(db.QualityChecklistTemplates.AsNoTracking().Where(t => t.Id == template.Id), ct)
                ?? throw QualityRecordCore.Missing();
        }, ct);

    public async Task<IReadOnlyList<QualityClassificationDto>> ListClassificationsAsync(int page, int pageSize, int userId,
        CancellationToken ct = default)
    {
        await core.EnsureViewAsync(userId, ct);
        var (skip, take) = PageWindow(page, pageSize);
        return await db.QualityClassifications.AsNoTracking()
            .Join(ClassificationVersions, c => c.Id, v => v.Id, (c, v) => new { c, v.Version })
            .OrderBy(x => x.c.Order).ThenBy(x => x.c.Id).Skip(skip).Take(take)
            .Select(x => new QualityClassificationDto(x.c.Id, x.c.Name, x.c.Order, x.c.IsActive, (uint)x.Version))
            .ToListAsync(ct);
    }

    public Task<QualityClassificationDto> SaveClassificationAsync(int id, SaveQualityClassificationRequest request,
        uint expectedVersion, int userId, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequireAsync(userId, PermissionConstants.QualityApprove, ct);
        var name = QualityRecordCore.RequiredText(request.Name, 100);
        await db.Database.ExecuteSqlRawAsync("""LOCK TABLE "QualityClassifications" IN SHARE ROW EXCLUSIVE MODE""", ct);
        var classification = id == 0 ? null : await db.QualityClassifications.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw QualityRecordCore.Missing();
        await EnsureVersionAsync(ClassificationVersions, classification?.Id, expectedVersion, ct);
        if (await db.QualityClassifications.AsNoTracking().AnyAsync(c => c.Id != id && c.Name == name, ct))
            throw new BusinessRuleException(MessageCodes.QualityDuplicateClassification, "A quality classification with this name already exists.");

        if (classification is null)
        {
            classification = new QualityClassification();
            db.QualityClassifications.Add(classification);
        }
        classification.Name = name;
        classification.Order = request.Order;
        classification.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new QualityClassificationDto(classification.Id, name, request.Order, request.IsActive,
            await ReadVersionAsync(ClassificationVersions, classification.Id, ct));
    }, ct);

    public async Task<IReadOnlyList<QualityDueRuleDto>> ListDueRulesAsync(int page, int pageSize, int userId,
        CancellationToken ct = default)
    {
        await core.EnsureViewAsync(userId, ct);
        var (skip, take) = PageWindow(page, pageSize);
        return await db.QualityDueRules.AsNoTracking().Join(DueRuleVersions, r => r.Id, v => v.Id, (r, v) => new { r, v.Version })
            .OrderBy(x => x.r.Type).ThenBy(x => x.r.Id).Skip(skip).Take(take)
            .Select(x => new QualityDueRuleDto(x.r.Id, x.r.Type, x.r.ClassificationId, x.r.WorkingDays, (uint)x.Version))
            .ToListAsync(ct);
    }

    public Task<QualityDueRuleDto> SaveDueRuleAsync(QualityRecordType type, int? classificationId, int? workingDays,
        uint expectedVersion, int userId, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        await RequireAsync(userId, PermissionConstants.QualityApprove, ct);
        EnsureType(type);
        if (workingDays is < 0)
            throw new BusinessRuleException(MessageCodes.QualityInvalidWorkingDays, "Working days must be zero or greater.");
        if (classificationId is int classification &&
            !await db.QualityClassifications.AsNoTracking().AnyAsync(c => c.Id == classification, ct))
            throw QualityRecordCore.Missing();

        await db.Database.ExecuteSqlRawAsync("""LOCK TABLE "QualityDueRules" IN SHARE ROW EXCLUSIVE MODE""", ct);
        var rule = await db.QualityDueRules.SingleOrDefaultAsync(r => r.Type == type && r.ClassificationId == classificationId, ct);
        await EnsureVersionAsync(DueRuleVersions, rule?.Id, expectedVersion, ct);
        if (rule is null)
        {
            rule = new QualityDueRule { Type = type, ClassificationId = classificationId };
            db.QualityDueRules.Add(rule);
        }
        rule.WorkingDays = workingDays;
        await db.SaveChangesAsync(ct);
        return new QualityDueRuleDto(rule.Id, type, classificationId, workingDays, await ReadVersionAsync(DueRuleVersions, rule.Id, ct));
    }, ct);

    public Task<QualityAnswerDto> AnswerAsync(int recordId, string itemKey, QualityChecklistAnswerValue answer, string? comment,
        uint expectedVersion, int userId, CancellationToken ct = default) => UnitOfWork.RunAsync(db, async () =>
    {
        var record = await core.LockForWriteAsync(recordId, expectedVersion, userId, ct);
        if (record.OwnerUserId != userId && !await permissions.UserHasPermissionAsync(userId, PermissionConstants.QualityInvestigate, ct))
            throw QualityRecordCore.Denied();
        await EnsureMemberAsync(record, userId, ct);
        await EnsureOpenAsync(recordId, ct);

        var key = QualityRecordCore.RequiredText(itemKey, 36);
        if (!Enum.IsDefined(answer))
            throw new BusinessRuleException(MessageCodes.QualityInvalidAnswer, "Choose a valid checklist answer.");
        var text = await EffectiveTemplates(record.Type, record.SectionId).SelectMany(t => t.Items)
            .Where(i => i.Key == key && i.IsActive).Select(i => i.Text).SingleOrDefaultAsync(ct)
            ?? throw QualityRecordCore.Missing();
        var entry = new QualityChecklistAnswer
        {
            RecordId = recordId, ItemKey = key, ItemText = text, Answer = answer,
            Comment = comment is null ? null : QualityRecordCore.RequiredText(comment, 2000),
            AnsweredByUserId = userId, AnsweredAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.QualityChecklistAnswers.Add(entry);
        core.AdvanceVersion(record);
        await db.SaveChangesAsync(ct);
        return new QualityAnswerDto(entry.Id, key, text, answer, entry.Comment, userId, entry.AnsweredAtUtc, record.Version);
    }, ct);

    public async Task<IReadOnlyList<QualityAnswerDto>> ListAnswersAsync(int recordId, int page, int pageSize, int userId,
        CancellationToken ct = default)
    {
        var records = (await core.ReadableRecordsAsync(userId, ct)).Where(r => r.Id == recordId);
        if (!await records.AnyAsync(ct)) throw QualityRecordCore.Missing();
        var (skip, take) = PageWindow(page, pageSize);
        return await records.SelectMany(r => r.ChecklistAnswers.Where(a =>
                !r.ChecklistAnswers.Any(later => later.ItemKey == a.ItemKey && later.Id > a.Id)),
                (r, a) => new { r.Version, a })
            .OrderBy(x => x.a.ItemKey).Skip(skip).Take(take)
            .Select(x => new QualityAnswerDto(x.a.Id, x.a.ItemKey, x.a.ItemText, x.a.Answer, x.a.Comment,
                x.a.AnsweredByUserId, x.a.AnsweredAtUtc, x.Version))
            .ToListAsync(ct);
    }

    private async Task RequireAsync(int userId, string code, CancellationToken ct)
    {
        if (!await permissions.UserHasPermissionAsync(userId, code, ct)) throw QualityRecordCore.Denied();
    }

    private async Task EnsureMemberAsync(QualityRecord record, int userId, CancellationToken ct)
    {
        if (record.SectionId is int section && !(await scope.GetMemberSectionIdsAsync(userId, ct)).Contains(section))
            throw QualityRecordCore.Denied();
    }

    private static void EnsureType(QualityRecordType type)
    {
        if (!Enum.IsDefined(type))
            throw new BusinessRuleException(MessageCodes.QualityInvalidType, "Unknown quality record type.");
    }

    private static async Task EnsureVersionAsync(IQueryable<ConfigVersion> versions, int? id, uint expectedVersion, CancellationToken ct)
    {
        var current = id is int key ? await ReadVersionAsync(versions, key, ct) : 0;
        if (current != expectedVersion) throw QualityRecordCore.Stale();
    }

    private static async Task<uint> ReadVersionAsync(IQueryable<ConfigVersion> versions, int id, CancellationToken ct) =>
        (uint)await versions.Where(v => v.Id == id).Select(v => v.Version).SingleAsync(ct);

    private static (int Skip, int Take) PageWindow(int page, int pageSize)
    {
        var (number, size) = Paging.Normalize(page, pageSize, 1);
        return (int.CreateSaturating((long)(number - 1) * size), size);
    }
}
