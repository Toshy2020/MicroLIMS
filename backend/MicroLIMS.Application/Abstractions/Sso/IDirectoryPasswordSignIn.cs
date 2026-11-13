using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Abstractions.Sso;

/// <summary>
/// Seam for Active Directory password verification during form sign-in.
/// Returns <c>null</c> to decline or fall back; otherwise returns the sign-in outcome.
/// </summary>
public interface IDirectoryPasswordSignIn
{
    Task<Interfaces.LoginOutcome> SignInAsync(
        User user,
        string password,
        string? ipAddress,
        string? userAgent,
        string? deviceToken,
        CancellationToken ct = default);
}
