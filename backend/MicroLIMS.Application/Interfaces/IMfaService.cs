using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

public interface IMfaService
{
    Task<string> CreateChallengeAsync(int userId, MfaChallengePurpose purpose, CancellationToken ct = default); // returns raw token
    Task<bool> IsTrustedDeviceAsync(int userId, string? deviceToken, CancellationToken ct = default);
    Task<LoginOutcome> VerifyAsync(string mfaToken, string? code, string? recoveryCode, bool trustDevice, string? ip, string? userAgent, CancellationToken ct = default);
    Task<int> RevokeTrustedDevicesAsync(int userId, int? revokedByUserId, CancellationToken ct = default); // no SaveChanges; caller's unit of work
}
