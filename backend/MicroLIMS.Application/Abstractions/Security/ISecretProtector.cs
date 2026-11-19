namespace MicroLIMS.Application.Abstractions.Security;

public interface ISecretProtector
{
    bool IsConfigured { get; }
    string Protect(ReadOnlySpan<byte> plaintext);     // "keyId:base64(nonce):base64(ciphertext):base64(tag)"
    byte[] Unprotect(string protectedValue);          // throws SecretProtectionException on unknown key or tamper
}
