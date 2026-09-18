namespace MicroLIMS.Application.Interfaces;

public interface IUserSectionScopeService
{
    Task<IReadOnlyList<int>?> GetAccessibleSectionIdsAsync(int userId, CancellationToken ct = default);
    Task<int> ResolveSectionForCreateAsync(int userId, int? requestedSectionId, CancellationToken ct = default);
    Task EnsureTestOrderAccessAsync(int userId, int testOrderId, CancellationToken ct = default);
    Task EnsureTestOrdersAccessAsync(int userId, IEnumerable<int> testOrderIds, CancellationToken ct = default);
    Task EnsureSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default);
    Task EnsureIncubationAccessAsync(int userId, int incubationId, CancellationToken ct = default);
    Task EnsurePathogenSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default);
}
