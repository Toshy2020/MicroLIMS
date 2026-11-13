namespace MicroLIMS.Application.DTOs;

public sealed record MfaVerifyRequest(
    string MfaToken,
    string? Code = null,
    string? RecoveryCode = null,
    bool TrustDevice = false);
