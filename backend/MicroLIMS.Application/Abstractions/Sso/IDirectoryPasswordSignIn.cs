namespace MicroLIMS.Application.Abstractions.Sso;

/// <summary>
/// Seam for Active Directory password verification during form sign-in. Called only in
/// Active Directory mode for a name that is not an emergency account. <paramref name="username"/>
/// is the company username typed on the shared login form.
/// </summary>
public interface IDirectoryPasswordSignIn
{
    Task<Interfaces.LoginOutcome> SignInAsync(
        string username,
        string password,
        string? ipAddress,
        string? userAgent,
        string? deviceToken,
        CancellationToken ct = default);
}
