namespace MicroLIMS.Application.Interfaces;

public interface IUserSectionScopeService
{
    Task<IReadOnlyList<int>?> GetAccessibleSectionIdsAsync(int userId, CancellationToken ct = default);
    Task<int> ResolveSectionForCreateAsync(int userId, int? requestedSectionId, CancellationToken ct = default);
}
