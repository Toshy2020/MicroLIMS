using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

public class SampleBulkAssignmentService
{
    public const int MaxSamplesPerRequest = 200;

    private readonly IMicroLimsDbContext _db;
    private readonly SampleAssignmentService _assignmentService;
    private readonly IUserSectionScopeService _scope;

    public SampleBulkAssignmentService(
        IMicroLimsDbContext db,
        SampleAssignmentService assignmentService,
        IUserSectionScopeService scope)
    {
        _db = db;
        _assignmentService = assignmentService;
        _scope = scope;
    }

    public async Task<BulkAssignResult> AssignAsync(
        BulkAssignRequest request,
        int actingUserId,
        CancellationToken ct = default)
    {
        if (request is null || request.SampleIds is null || request.SampleIds.Count == 0)
            throw new BusinessRuleException("Choose at least one sample to assign.");

        if (request.SampleIds.Count > MaxSamplesPerRequest)
            throw new BusinessRuleException($"Bulk assignment is limited to {MaxSamplesPerRequest} samples per request.");

        if (request.SampleIds.Distinct().Count() != request.SampleIds.Count)
            throw new BusinessRuleException("Each sample can be listed only once.");

        var analystExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == request.AnalystUserId, ct);
        if (!analystExists)
            throw new BusinessRuleException($"Analyst user {request.AnalystUserId} was not found.");

        var samplesById = await _db.Samples
            .Include(s => s.TestOrders)
            .Where(s => request.SampleIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var actingScope = await _scope.GetMemberSectionIdsAsync(actingUserId, ct);
        var assigneeScope = await _scope.GetMemberSectionIdsAsync(request.AnalystUserId, ct);

        var rows = new List<BulkAssignRowResult>(request.SampleIds.Count);
        var passing = new List<(Sample Sample, List<(TestOrder Order, int? AnalystId)> Changes)>();

        foreach (var sampleId in request.SampleIds)
        {
            if (!samplesById.TryGetValue(sampleId, out var sample))
            {
                rows.Add(new BulkAssignRowResult(sampleId, $"#{sampleId}", false, $"Sample {sampleId} not found."));
                continue;
            }

            try
            {
                var changes = await _assignmentService.PrepareSampleAssignmentAsync(
                    sample,
                    request.AnalystUserId,
                    actingUserId,
                    actingScope,
                    assigneeScope);

                passing.Add((sample, changes));
                rows.Add(new BulkAssignRowResult(sample.Id, sample.ReferenceNumber, true, null));
            }
            catch (Exception ex) when (ex is BusinessRuleException or UnauthorizedAccessException or NotFoundException)
            {
                rows.Add(new BulkAssignRowResult(sample.Id, sample.ReferenceNumber, false, ex.Message));
            }
        }

        if (passing.Count > 0)
        {
            await UnitOfWork.RunAsync(_db, async () =>
            {
                foreach (var (sample, changes) in passing)
                {
                    await _assignmentService.ApplyAsync(sample, changes, actingUserId, request.Reason, ct);
                }
            });
        }

        return new BulkAssignResult(passing.Count, rows.Count - passing.Count, rows);
    }
}
