namespace MicroLIMS.Application.Interfaces;

// Per-user interface preferences. Always scoped to the user passed in; the
// controller takes that id from the caller's claims, never from the request.
public interface IUserUiStateService
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(int userId, CancellationToken ct = default);
    Task SetAsync(int userId, string key, string value, CancellationToken ct = default);
    Task RemoveAsync(int userId, string key, CancellationToken ct = default);
}
