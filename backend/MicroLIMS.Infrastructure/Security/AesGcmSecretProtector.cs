using System.Security.Cryptography;
using System.Text;
using MicroLIMS.Application.Abstractions.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MicroLIMS.Infrastructure.Security;

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly Dictionary<string, byte[]> _validKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _invalidKeyIds = new(StringComparer.Ordinal);
    private readonly string? _activeKeyId;
    private readonly byte[]? _activeKey;

    [ActivatorUtilitiesConstructor]
    public AesGcmSecretProtector(IOptions<MfaOptions> options)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    public AesGcmSecretProtector(MfaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuredKeys = options.EncryptionKeys;
        if (configuredKeys is not null)
        {
            foreach (var (keyId, base64Key) in configuredKeys)
            {
                if (string.IsNullOrWhiteSpace(keyId) || keyId.Contains(':'))
                {
                    continue;
                }

                if (TryDecodeKey(base64Key, out var keyBytes))
                {
                    _validKeys[keyId] = keyBytes;
                }
                else
                {
                    _invalidKeyIds.Add(keyId);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ActiveKeyId)
            && !options.ActiveKeyId.Contains(':')
            && _validKeys.TryGetValue(options.ActiveKeyId, out var activeKey))
        {
            _activeKeyId = options.ActiveKeyId;
            _activeKey = activeKey;
        }
    }

    public bool IsConfigured => _activeKeyId is not null && _activeKey is not null;

    public string Protect(ReadOnlySpan<byte> plaintext)
    {
        if (_activeKeyId is null || _activeKey is null)
        {
            throw new SecretProtectionException("Secret protection is not configured.");
        }

        Span<byte> nonce = stackalloc byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        Span<byte> tag = stackalloc byte[TagSizeBytes];
        var ciphertext = new byte[plaintext.Length];
        var associatedData = Encoding.UTF8.GetBytes(_activeKeyId);

        using var aes = new AesGcm(_activeKey, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return $"{_activeKeyId}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(ciphertext)}:{Convert.ToBase64String(tag)}";
    }

    public byte[] Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            throw new SecretProtectionException("Protected value is malformed.");
        }

        var parts = protectedValue.Split(':');
        if (parts.Length != 4 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new SecretProtectionException("Protected value is malformed.");
        }

        var keyId = parts[0];
        if (_invalidKeyIds.Contains(keyId))
        {
            throw new SecretProtectionException($"Encryption key '{keyId}' is invalid.");
        }

        if (!_validKeys.TryGetValue(keyId, out var key))
        {
            throw new SecretProtectionException($"Unknown encryption key id '{keyId}'.");
        }

        byte[] nonce;
        byte[] ciphertext;
        byte[] tag;
        try
        {
            nonce = Convert.FromBase64String(parts[1]);
            ciphertext = Convert.FromBase64String(parts[2]);
            tag = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException ex)
        {
            throw new SecretProtectionException("Protected value is malformed.", ex);
        }

        if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
        {
            throw new SecretProtectionException("Protected value is malformed.");
        }

        var plaintext = new byte[ciphertext.Length];
        var associatedData = Encoding.UTF8.GetBytes(keyId);

        try
        {
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException ex)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new SecretProtectionException($"Secret decryption failed for key id '{keyId}'.", ex);
        }
    }

    private static bool TryDecodeKey(string? base64Key, out byte[] keyBytes)
    {
        keyBytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length != KeySizeBytes)
        {
            CryptographicOperations.ZeroMemory(decoded);
            return false;
        }

        keyBytes = decoded;
        return true;
    }
}
