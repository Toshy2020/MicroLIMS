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
    Task EnsureMaterialAccessAsync(int userId, int materialId, CancellationToken ct = default);
    // Prepared media lots and cryovials belong to the section of the
    // Material they were made from.
    Task EnsureMediaAccessAsync(int userId, int mediaId, CancellationToken ct = default);
    Task EnsureCryovialAccessAsync(int userId, int cryovialId, CancellationToken ct = default);
    Task EnsureMediaEvaluationAccessAsync(int userId, int evaluationId, CancellationToken ct = default);
    Task EnsureMediaEvaluationChallengeAccessAsync(int userId, int challengeId, CancellationToken ct = default);
    Task EnsureEquipmentAccessAsync(int userId, int equipmentId, CancellationToken ct = default);
    Task EnsureColumnAccessAsync(int userId, int columnId, CancellationToken ct = default);
    // An OOS group belongs to the section(s) of its retest samples (each
    // carries only the deciding section's tests), not every section of the
    // origin sample.
    Task EnsureOosGroupAccessAsync(int userId, string oosGroupCode, CancellationToken ct = default);
}
