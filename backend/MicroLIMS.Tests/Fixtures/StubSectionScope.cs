using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Tests.Fixtures;

// Only GetAccessibleSectionIdsAsync matters to the KPI service; the rest of
// the interface is guard calls the KPI code never makes.
internal sealed class StubSectionScope : IUserSectionScopeService
{
    private readonly Func<int, IReadOnlyList<int>?> _ids;
    public StubSectionScope(Func<int, IReadOnlyList<int>?> ids) => _ids = ids;

    public Task<IReadOnlyList<int>?> GetAccessibleSectionIdsAsync(int userId, CancellationToken ct = default) => Task.FromResult(_ids(userId));
    public Task<IReadOnlyList<WorkspaceArea>> GetPhyschemAreasAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> ResolveSectionForCreateAsync(int userId, int? requestedSectionId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureTestOrderAccessAsync(int userId, int testOrderId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureTestOrdersAccessAsync(int userId, IEnumerable<int> testOrderIds, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureIncubationAccessAsync(int userId, int incubationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsurePathogenSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMaterialAccessAsync(int userId, int materialId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMediaAccessAsync(int userId, int mediaId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureCryovialAccessAsync(int userId, int cryovialId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMediaEvaluationAccessAsync(int userId, int evaluationId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureMediaEvaluationChallengeAccessAsync(int userId, int challengeId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureEquipmentAccessAsync(int userId, int equipmentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureEquipmentInventoryAccessAsync(int userId, int inventoryId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureColumnAccessAsync(int userId, int columnId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task EnsureOosGroupAccessAsync(int userId, string oosGroupCode, CancellationToken ct = default) => throw new NotSupportedException();
}
