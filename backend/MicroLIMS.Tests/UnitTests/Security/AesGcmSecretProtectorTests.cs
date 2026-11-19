using System.Security.Cryptography;
using System.Text;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Security;

public class AesGcmSecretProtectorTests
{
    private static string RandomBase64Key(int byteLength = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength));

    private static AesGcmSecretProtector CreateProtector(string? activeKeyId, params (string KeyId, string Base64Key)[] keys)
    {
        var options = new MfaOptions
        {
            ActiveKeyId = activeKeyId,
            EncryptionKeys = keys.ToDictionary(k => k.KeyId, k => k.Base64Key)
        };
        return new AesGcmSecretProtector(Options.Create(options));
    }

    private static string FlipFirstByteInBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        bytes[0] ^= 0xFF;
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public void RoundTrip()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var plaintext = Encoding.UTF8.GetBytes("JBSWY3DPEHPK3PXP-totp-secret");

        var protectedValue = protector.Protect(plaintext);
        var unprotected = protector.Unprotect(protectedValue);

        Assert.True(protector.IsConfigured);
        Assert.Equal(plaintext, unprotected);
        Assert.Equal(Array.Empty<byte>(), protector.Unprotect(protector.Protect(ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void ProtectedValue_HasFourParts_AndStartsWithActiveKeyId()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var plaintext = RandomNumberGenerator.GetBytes(20);

        var protectedValue = protector.Protect(plaintext);
        var parts = protectedValue.Split(':');

        Assert.Equal(4, parts.Length);
        Assert.Equal("k1", parts[0]);
        Assert.Equal(12, Convert.FromBase64String(parts[1]).Length);
        Assert.Equal(plaintext.Length, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(16, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void TwoProtectsOfSameValue_Differ()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var plaintext = Encoding.UTF8.GetBytes("same-secret-bytes");

        var first = protector.Protect(plaintext);
        var second = protector.Protect(plaintext);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.Split(':')[1], second.Split(':')[1]);
    }

    [Fact]
    public void TamperedCiphertext_Throws()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var parts = protector.Protect(Encoding.UTF8.GetBytes("sensitive-secret")).Split(':');
        parts[2] = FlipFirstByteInBase64(parts[2]);

        var ex = Assert.Throws<SecretProtectionException>(() => protector.Unprotect(string.Join(':', parts)));
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void TamperedTag_Throws()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var parts = protector.Protect(Encoding.UTF8.GetBytes("sensitive-secret")).Split(':');
        parts[3] = FlipFirstByteInBase64(parts[3]);

        var ex = Assert.Throws<SecretProtectionException>(() => protector.Unprotect(string.Join(':', parts)));
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void TamperedNonce_Throws()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var parts = protector.Protect(Encoding.UTF8.GetBytes("sensitive-secret")).Split(':');
        parts[1] = FlipFirstByteInBase64(parts[1]);

        var ex = Assert.Throws<SecretProtectionException>(() => protector.Unprotect(string.Join(':', parts)));
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void KeyIdSwappedToAnotherKnownKey_Throws()
    {
        var sharedKey = RandomBase64Key();
        var protector = CreateProtector("k1", ("k1", sharedKey), ("k2", sharedKey));
        var parts = protector.Protect(Encoding.UTF8.GetBytes("sensitive-secret")).Split(':');
        parts[0] = "k2";

        var ex = Assert.Throws<SecretProtectionException>(() => protector.Unprotect(string.Join(':', parts)));
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void UnknownKeyId_Throws()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));
        var parts = protector.Protect(Encoding.UTF8.GetBytes("sensitive-secret")).Split(':');
        parts[0] = "k99";
        var tampered = string.Join(':', parts);

        var ex = Assert.Throws<SecretProtectionException>(() => protector.Unprotect(tampered));

        Assert.Contains("k99", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(parts[1], ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(parts[2], ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(parts[3], ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b:c")]
    [InlineData("k1:###:AAAA:AAAA")]
    [InlineData("k1:AAAA:AAAA:AAAA:AAAA")]
    public void MalformedValue_Throws(string malformed)
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key()));

        Assert.Throws<SecretProtectionException>(() => protector.Unprotect(malformed));
    }

    [Fact]
    public void OldKeyStillDecrypts_AfterActiveKeyChanges()
    {
        var k1 = RandomBase64Key();
        var k2 = RandomBase64Key();
        var plaintext = Encoding.UTF8.GetBytes("rotated-secret-payload");

        var oldProtector = CreateProtector("k1", ("k1", k1));
        var protectedWithK1 = oldProtector.Protect(plaintext);

        var rotatedProtector = CreateProtector("k2", ("k1", k1), ("k2", k2));
        var protectedWithK2 = rotatedProtector.Protect(plaintext);

        Assert.StartsWith("k2:", protectedWithK2, StringComparison.Ordinal);
        Assert.Equal(plaintext, rotatedProtector.Unprotect(protectedWithK1));
        Assert.Equal(plaintext, rotatedProtector.Unprotect(protectedWithK2));
    }

    [Fact]
    public void KeyNot32Bytes_IsNotConfigured()
    {
        var protector = CreateProtector("k1", ("k1", RandomBase64Key(16)));

        Assert.False(protector.IsConfigured);
        Assert.Throws<SecretProtectionException>(() => protector.Protect(Encoding.UTF8.GetBytes("secret")));
    }

    [Fact]
    public void ActiveKeyIdMissingFromKeys_IsNotConfigured()
    {
        var protector = CreateProtector("k2", ("k1", RandomBase64Key()));

        Assert.False(protector.IsConfigured);
        Assert.Throws<SecretProtectionException>(() => protector.Protect(Encoding.UTF8.GetBytes("secret")));
    }

    [Fact]
    public void KeyIdContainingColon_IsNotConfigured()
    {
        var protector = CreateProtector("k:1", ("k:1", RandomBase64Key()));

        Assert.False(protector.IsConfigured);
        Assert.Throws<SecretProtectionException>(() => protector.Protect(Encoding.UTF8.GetBytes("secret")));
    }

    [Fact]
    public void NoKeys_IsConfiguredFalse_AndProtectThrows()
    {
        var protector = CreateProtector(null);

        Assert.False(protector.IsConfigured);
        var ex = Assert.Throws<SecretProtectionException>(() => protector.Protect(Encoding.UTF8.GetBytes("secret")));
        Assert.Equal("Secret protection is not configured.", ex.Message);
    }

    [Fact]
    public void ExceptionMessages_DoNotContainKeyOrPlaintext()
    {
        var k1 = RandomBase64Key();
        var k2 = RandomBase64Key();
        var protector = CreateProtector("k1", ("k1", k1), ("k2", RandomBase64Key(16)));
        const string secretText = "SUPER_SECRET_TOTP_PLAINTEXT_VALUE";
        var plaintext = Encoding.UTF8.GetBytes(secretText);
        var protectedValue = protector.Protect(plaintext);
        var parts = protectedValue.Split(':');

        var messages = new List<string>
        {
            Assert.Throws<SecretProtectionException>(() =>
                protector.Unprotect($"unknown-id:{parts[1]}:{parts[2]}:{parts[3]}")).Message,
            Assert.Throws<SecretProtectionException>(() =>
                protector.Unprotect($"k2:{parts[1]}:{parts[2]}:{parts[3]}")).Message,
            Assert.Throws<SecretProtectionException>(() =>
                protector.Unprotect($"{parts[0]}:{parts[1]}:{FlipFirstByteInBase64(parts[2])}:{parts[3]}")).Message,
            Assert.Throws<SecretProtectionException>(() =>
                CreateProtector(null).Protect(plaintext)).Message
        };

        foreach (var message in messages)
        {
            Assert.DoesNotContain(k1, message, StringComparison.Ordinal);
            Assert.DoesNotContain(k2, message, StringComparison.Ordinal);
            Assert.DoesNotContain(secretText, message, StringComparison.Ordinal);
            Assert.DoesNotContain(parts[1], message, StringComparison.Ordinal);
            Assert.DoesNotContain(parts[2], message, StringComparison.Ordinal);
            Assert.DoesNotContain(parts[3], message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Registered_AsSingleton_AndResolvesFromConfiguration()
    {
        var key = RandomBase64Key();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mfa:ActiveKeyId"] = "k1",
                ["Mfa:EncryptionKeys:k1"] = key
            })
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationServices(config);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<ISecretProtector>();
        var second = provider.GetRequiredService<ISecretProtector>();

        Assert.Same(first, second);
        Assert.IsType<AesGcmSecretProtector>(first);
        Assert.True(first.IsConfigured);

        var payload = Encoding.UTF8.GetBytes("di-round-trip");
        Assert.Equal(payload, first.Unprotect(first.Protect(payload)));
    }
}
