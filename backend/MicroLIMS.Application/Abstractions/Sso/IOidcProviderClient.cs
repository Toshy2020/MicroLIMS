using MicroLIMS.Application.Services.Sso;

namespace MicroLIMS.Application.Abstractions.Sso;

/// <summary>
/// Outbound OpenID Connect protocol client used by <see cref="OidcSignInService"/>.
/// </summary>
public interface IOidcProviderClient
{
    Task<Uri> BuildAuthorizeUriAsync(
        string state,
        string nonce,
        string codeChallenge,
        string redirectUri,
        CancellationToken ct = default);

    Task<DirectoryIdentity> RedeemAsync(
        string code,
        string codeVerifier,
        string expectedNonce,
        string redirectUri,
        CancellationToken ct = default);

    Task TestAsync(CancellationToken ct = default);
}

/// <summary>
/// Thrown when an OIDC provider response, token, or claim fails validation.
/// </summary>
public sealed class OidcValidationException : Exception
{
    public const string CodeTokenExchangeFailed = "token_exchange_failed";
    public const string CodeMissingIdToken = "missing_id_token";
    public const string CodeInvalidToken = "invalid_token";
    public const string CodeNonceMismatch = "nonce_mismatch";
    public const string CodeMfaClaimMissing = "mfa_claim_missing";
    public const string CodeUsernameClaimMissing = "username_claim_missing";
    public const string CodeSubjectClaimMissing = "subject_claim_missing";
    public const string CodeProviderError = "provider_error";

    public string ReasonCode { get; }

    public OidcValidationException(string reasonCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ReasonCode = reasonCode;
    }
}
