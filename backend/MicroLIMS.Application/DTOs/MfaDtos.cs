namespace MicroLIMS.Application.DTOs;

public sealed record MfaVerifyRequest(
    string MfaToken,
    string? Code = null,
    string? RecoveryCode = null,
    bool TrustDevice = false);

public sealed record MfaTokenRequest(string MfaToken);

public sealed record MfaTokenConfirmRequest(string MfaToken, string Code);

public sealed record MfaPasswordRequest(string Password);

public sealed record MfaCodeRequest(string Code);

public sealed record MfaPasswordAndCodeRequest(string Password, string Code);
