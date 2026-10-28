using MicroLIMS.Application.Helpers;

namespace MicroLIMS.Application.Interfaces;

// What an access check is for. Write follows membership for everyone; Read also
// spans every section for a role holding Sections.ViewAll. The Ensure*Access
// guards default to Write, so a caller that forgets to say Read fails closed.
public enum SectionAccess
{
    Write = 0,
    Read = 1,
    // Master data / settings: every section for a role holding Sections.ConfigureAll, else membership. Never used for GxP records.
    Configure = 2
}

public interface IUserSectionScopeService
{
    // READ scope: null = every section (the user's role holds Sections.ViewAll), else the sections the user belongs to.
    // Use it only to decide what a user may see, never what they may change.
    Task<IReadOnlyList<int>?> GetReadableSectionIdsAsync(int userId, CancellationToken ct = default);
    // WRITE scope: the sections the user belongs to, for everyone including administrators. Never null.
    Task<IReadOnlyList<int>> GetMemberSectionIdsAsync(int userId, CancellationToken ct = default);
    // CONFIGURATION scope: null = every section (the user's role holds Sections.ConfigureAll), else the sections the user belongs to.
    // Only for master data and settings writes; GxP records always use GetMemberSectionIdsAsync.
    Task<IReadOnlyList<int>?> GetConfigurableSectionIdsAsync(int userId, CancellationToken ct = default);
    // Physicochemical areas the user may work in: a department membership covering FP = both; none when not in FP.
    // Read access also gives both to a role holding Sections.ViewAll.
    Task<IReadOnlyList<WorkspaceArea>> GetPhyschemAreasAsync(int userId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    // access = Configure is for master-data creation only: a role holding Sections.ConfigureAll may pick any active section.
    Task<int> ResolveSectionForCreateAsync(int userId, int? requestedSectionId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureTestOrderAccessAsync(int userId, int testOrderId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureTestOrdersAccessAsync(int userId, IEnumerable<int> testOrderIds, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    // Water/EM/After Cleaning Prepare: the sample has no test orders yet, so access follows the lab sections of the tests being assigned.
    Task EnsureSectionsAccessAsync(int userId, IEnumerable<int> sectionIds, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureIncubationAccessAsync(int userId, int incubationId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsurePathogenSampleAccessAsync(int userId, int sampleId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureMaterialAccessAsync(int userId, int materialId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    // Prepared media lots and cryovials belong to the section of the
    // Material they were made from.
    Task EnsureMediaAccessAsync(int userId, int mediaId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureCryovialAccessAsync(int userId, int cryovialId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureMediaEvaluationAccessAsync(int userId, int evaluationId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureMediaEvaluationChallengeAccessAsync(int userId, int challengeId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureEquipmentAccessAsync(int userId, int equipmentId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    // The asset register (EquipmentInventory) is a separate entity from the
    // Equipment master list above - keep the two Ensure*AccessAsync guards
    // separate even though their bodies look alike.
    Task EnsureEquipmentInventoryAccessAsync(int userId, int inventoryId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    Task EnsureColumnAccessAsync(int userId, int columnId, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
    // An OOS group belongs to the section(s) of its retest samples (each
    // carries only the deciding section's tests), not every section of the
    // origin sample.
    Task EnsureOosGroupAccessAsync(int userId, string oosGroupCode, CancellationToken ct = default, SectionAccess access = SectionAccess.Write);
}

public static class UserSectionScopeServiceExtensions
{
    // The scope for one kind of access: null = every section, which only Read can return.
    public static Task<IReadOnlyList<int>?> GetSectionIdsAsync(
        this IUserSectionScopeService scope, int userId, SectionAccess access, CancellationToken ct = default) =>
        access switch
        {
            SectionAccess.Read => scope.GetReadableSectionIdsAsync(userId, ct),
            SectionAccess.Configure => scope.GetConfigurableSectionIdsAsync(userId, ct),
            _ => MemberAsync(scope, userId, ct),
        };

    private static async Task<IReadOnlyList<int>?> MemberAsync(IUserSectionScopeService scope, int userId, CancellationToken ct) =>
        await scope.GetMemberSectionIdsAsync(userId, ct);
}
