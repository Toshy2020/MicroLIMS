using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using System.Globalization;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: test stage replicate. Behind TestStageReplicateMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class TestStageReplicateMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly ChangeControlGate _gate;

    public TestStageReplicateMasterDataService(
        IMicroLimsDbContext db,
        IUserSectionScopeService scope,
        ChangeControlGate? changeControlGate = null)
    {
        _db = db;
        _scope = scope;
        _gate = changeControlGate ?? new ChangeControlGate(db, NullChangeControlContext.Instance, TimeProvider.System);
    }

    public async Task<object> GetTestDefinitionStageReplicatesAsync(int id)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException(
                MessageCodes.TestDefinitionRecordNotFound,
                $"Test {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var replicates = await _db.TestDefinitionStageReplicates
            .Where(r => r.TestDefinitionId == id)
            .OrderBy(r => r.Role)
            .ToListAsync();

        return replicates.Select(TestDefinitionStageReplicateDto.From);
    }

    public Task<object> CreateTestDefinitionStageReplicateAsync(int currentUserId, int id, CreateTestDefinitionStageReplicateRequest request, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync<object>(_db, async () =>
    {
        var test = await _db.TestDefinitions.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.TestDefinitionRecordNotFound,
                $"Test {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, cancellationToken);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var grant = await _gate.EnsureAllowedAsync(test.SectionId, QualityLinkTargetType.TestDefinition, test.Id, test.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        if (request.SampleReplicates < 1)
            throw new BusinessRuleException(
                MessageCodes.TestDefinitionSampleReplicatesAtLeastOne,
                "Sample replicates must be at least 1.");

        if (await _db.TestDefinitionStageReplicates.AnyAsync(r => r.TestDefinitionId == id && r.Role == request.Role, cancellationToken))
            throw new BusinessRuleException(
                MessageCodes.TestDefinitionStageReplicateRoleExists,
                $"Stage replicate configuration for role {request.Role} already exists for this test definition.",
                new Dictionary<string, string> { ["role"] = request.Role.ToString() });

        var entity = new TestDefinitionStageReplicate
        {
            TestDefinitionId = id,
            Role = request.Role,
            SampleReplicates = request.SampleReplicates
        };

        _db.TestDefinitionStageReplicates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestDefinitionStageReplicate), entity.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return TestDefinitionStageReplicateDto.From(entity);
    }, cancellationToken);

    public Task<object> UpdateTestDefinitionStageReplicateAsync(int currentUserId, int id, int replicateId, UpdateTestDefinitionStageReplicateRequest request, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync<object>(_db, async () =>
    {
        var test = await _db.TestDefinitions.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.TestDefinitionRecordNotFound,
                $"Test {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, cancellationToken);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var replicate = await _db.TestDefinitionStageReplicates.FirstOrDefaultAsync(r => r.Id == replicateId && r.TestDefinitionId == id, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.TestDefinitionStageReplicateNotFound,
                $"Stage replicate {replicateId} not found for test {id}.",
                new Dictionary<string, string> { ["replicateId"] = replicateId.ToString(CultureInfo.InvariantCulture), ["id"] = id.ToString(CultureInfo.InvariantCulture) });
        RecordVersion.EnsureCurrent(_db, replicate);

        var grant = await _gate.EnsureAllowedAsync(test.SectionId, QualityLinkTargetType.TestDefinition, test.Id, test.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        if (request.SampleReplicates.HasValue)
        {
            if (request.SampleReplicates.Value < 1)
                throw new BusinessRuleException(
                    MessageCodes.TestDefinitionSampleReplicatesAtLeastOne,
                    "Sample replicates must be at least 1.");
            replicate.SampleReplicates = request.SampleReplicates.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestDefinitionStageReplicate), replicate.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return TestDefinitionStageReplicateDto.From(replicate);
    }, cancellationToken);

    public Task<object> DeleteTestDefinitionStageReplicateAsync(int currentUserId, int id, int replicateId, CancellationToken cancellationToken = default) =>
        UnitOfWork.RunAsync<object>(_db, async () =>
    {
        var test = await _db.TestDefinitions.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.TestDefinitionRecordNotFound,
                $"Test {id} not found.",
                new Dictionary<string, string> { ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var scope = await _scope.GetConfigurableSectionIdsAsync(currentUserId, cancellationToken);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var replicate = await _db.TestDefinitionStageReplicates.FirstOrDefaultAsync(r => r.Id == replicateId && r.TestDefinitionId == id, cancellationToken)
            ?? throw new NotFoundException(
                MessageCodes.TestDefinitionStageReplicateNotFound,
                $"Stage replicate {replicateId} not found for test {id}.",
                new Dictionary<string, string> { ["replicateId"] = replicateId.ToString(CultureInfo.InvariantCulture), ["id"] = id.ToString(CultureInfo.InvariantCulture) });

        var grant = await _gate.EnsureAllowedAsync(test.SectionId, QualityLinkTargetType.TestDefinition, test.Id, test.Code, cancellationToken);
        using var _ = AuditReasonScope.Append(_db, grant is null ? null : $"Change control {grant.Number}");

        _db.TestDefinitionStageReplicates.Remove(replicate);
        await _db.SaveChangesAsync(cancellationToken);
        await _gate.RecordAppliedAsync(grant, nameof(TestDefinitionStageReplicate), replicate.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return new
        {
            message = "Stage replicate configuration deleted.",
            deleted = true
        };
    }, cancellationToken);
}
