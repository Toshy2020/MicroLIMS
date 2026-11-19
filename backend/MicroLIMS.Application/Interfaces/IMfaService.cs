using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

public sealed record EnrolmentStart(string ManualKey, string OtpAuthUri, string QrSvg);
public sealed record EnrolmentResult(IReadOnlyList<string> RecoveryCodes, LoginOutcome? Login);
public sealed record TrustedDeviceInfo(int Id, DateTime CreatedAt, DateTime ExpiresAt, string? CreatedIp, string? UserAgent);
public sealed record MfaStatus(
    bool Enrolled,
    string? Type,
    DateTime? ConfirmedAt,
    int RecoveryCodesRemaining,
    bool Required,
    DateTime? EnrolmentDueAt,
    IReadOnlyList<TrustedDeviceInfo> TrustedDevices);

public interface IMfaService
{
    Task<string> CreateChallengeAsync(int userId, MfaChallengePurpose purpose, CancellationToken ct = default); // returns raw token
    Task<bool> IsTrustedDeviceAsync(int userId, string? deviceToken, CancellationToken ct = default);
    Task<LoginOutcome> VerifyAsync(string mfaToken, string? code, string? recoveryCode, bool trustDevice, string? ip, string? userAgent, CancellationToken ct = default);
    Task<int> RevokeTrustedDevicesAsync(int userId, int? revokedByUserId, CancellationToken ct = default); // no SaveChanges; caller's unit of work

    Task<EnrolmentStart> StartEnrolmentWithPasswordAsync(int userId, string password, CancellationToken ct = default);
    Task<EnrolmentStart> StartEnrolmentWithTokenAsync(string mfaToken, CancellationToken ct = default);
    Task<EnrolmentResult> ConfirmEnrolmentAsync(int userId, string code, CancellationToken ct = default);
    Task<EnrolmentResult> ConfirmEnrolmentWithTokenAsync(string mfaToken, string code, string? ip, string? userAgent, CancellationToken ct = default);
    Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(int userId, string password, string code, CancellationToken ct = default);
    Task RemoveOwnFactorAsync(int userId, string password, string code, CancellationToken ct = default);
    Task<MfaStatus> GetStatusAsync(int userId, CancellationToken ct = default);
    Task ForgetOwnTrustedDeviceAsync(int userId, int trustedDeviceId, CancellationToken ct = default);
}
