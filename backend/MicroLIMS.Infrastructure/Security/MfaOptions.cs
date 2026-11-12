namespace MicroLIMS.Infrastructure.Security;

public sealed class MfaOptions
{
    public const string SectionName = "Mfa";
    public string? ActiveKeyId { get; set; }
    public Dictionary<string, string> EncryptionKeys { get; set; } = new();   // keyId -> base64 of 32 bytes
}
