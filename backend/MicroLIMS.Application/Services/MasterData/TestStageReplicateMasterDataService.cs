using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: test stage replicate. Behind TestStageReplicateMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class TestStageReplicateMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public TestStageReplicateMasterDataService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<object> GetTestDefinitionStageReplicatesAsync(int id)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var replicates = await _db.TestDefinitionStageReplicates
            .Where(r => r.TestDefinitionId == id)
            .OrderBy(r => r.Role)
            .ToListAsync();

        return replicates.Select(TestDefinitionStageReplicateDto.From);
    }

    public async Task<object> CreateTestDefinitionStageReplicateAsync(int currentUserId, int id, CreateTestDefinitionStageReplicateRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        if (request.SampleReplicates < 1)
            throw new InvalidOperationException("Sample replicates must be at least 1.");

        if (await _db.TestDefinitionStageReplicates.AnyAsync(r => r.TestDefinitionId == id && r.Role == request.Role))
            throw new InvalidOperationException($"Stage replicate configuration for role {request.Role} already exists for this test definition.");

        var entity = new TestDefinitionStageReplicate
        {
            TestDefinitionId = id,
            Role = request.Role,
            SampleReplicates = request.SampleReplicates
        };

        _db.TestDefinitionStageReplicates.Add(entity);
        await _db.SaveChangesAsync();
        return TestDefinitionStageReplicateDto.From(entity);
    }

    public async Task<object> UpdateTestDefinitionStageReplicateAsync(int currentUserId, int id, int replicateId, UpdateTestDefinitionStageReplicateRequest request)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var replicate = await _db.TestDefinitionStageReplicates.FirstOrDefaultAsync(r => r.Id == replicateId && r.TestDefinitionId == id)
            ?? throw new NotFoundException($"Stage replicate {replicateId} not found for test {id}.");
        RecordVersion.EnsureCurrent(_db, replicate);

        if (request.SampleReplicates.HasValue)
        {
            if (request.SampleReplicates.Value < 1)
                throw new InvalidOperationException("Sample replicates must be at least 1.");
            replicate.SampleReplicates = request.SampleReplicates.Value;
        }

        await _db.SaveChangesAsync();
        return TestDefinitionStageReplicateDto.From(replicate);
    }

    public async Task<object> DeleteTestDefinitionStageReplicateAsync(int currentUserId, int id, int replicateId)
    {
        var test = await _db.TestDefinitions.FindAsync(id)
            ?? throw new NotFoundException($"Test {id} not found.");

        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(test.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");

        var replicate = await _db.TestDefinitionStageReplicates.FirstOrDefaultAsync(r => r.Id == replicateId && r.TestDefinitionId == id)
            ?? throw new NotFoundException($"Stage replicate {replicateId} not found for test {id}.");

        _db.TestDefinitionStageReplicates.Remove(replicate);
        await _db.SaveChangesAsync();
        return new
        {
            message = "Stage replicate configuration deleted.",
            deleted = true
        };
    }
}
