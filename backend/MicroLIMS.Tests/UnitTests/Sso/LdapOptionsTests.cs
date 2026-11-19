using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Infrastructure.Sso;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Sso;

public class LdapOptionsTests
{
    private static LdapOptions Valid() => new()
    {
        Host = "dc.corp.test", BaseDn = "DC=corp,DC=test", BindDn = "CN=svc,DC=corp,DC=test", BindPassword = "svc-secret"
    };

    [Fact]
    public void Defaults_AreLdapsOnPort636()
    {
        Assert.Empty(Valid().Validate());
        Assert.True(Valid().EffectiveUseLdaps);
        Assert.Equal(636, Valid().Port);
    }

    [Fact]
    public void StartTls_IsAccepted()
    {
        var o = Valid();
        o.Port = 389;
        o.UseStartTls = true;
        Assert.Empty(o.Validate());
        Assert.False(o.EffectiveUseLdaps);
    }

    [Fact]
    public void PlainConnection_IsRefused()
    {
        var o = Valid();
        o.UseLdaps = false; // neither LDAPS nor StartTLS
        Assert.Contains(LdapOptions.PlainConnectionRefused, o.Validate());
    }

    [Fact]
    public void BothLdapsAndStartTls_IsRefused()
    {
        var o = Valid();
        o.UseLdaps = true;
        o.UseStartTls = true;
        Assert.NotEmpty(o.Validate());
    }

    [Theory]
    [InlineData("ldap://dc.corp.test")]
    [InlineData("dc.corp.test/path")]
    public void HostWithSchemeOrPath_IsRefused(string host)
    {
        var o = Valid();
        o.Host = host;
        Assert.Contains(o.Validate(), p => p.Contains("Sso:Ldap:Host"));
    }

    [Theory]
    [InlineData("uid)(objectClass=*")]
    [InlineData("a b")]
    [InlineData("")]
    public void AttributeNames_AreRestricted(string attribute)
    {
        var o = Valid();
        o.UsernameAttribute = attribute;
        Assert.Contains(o.Validate(), p => p.Contains("Sso:Ldap:UsernameAttribute"));
    }

    [Fact]
    public async Task LdapDirectory_WithPlainConfiguration_RefusesBeforeAnyConnection()
    {
        var o = Valid();
        o.UseLdaps = false;
        var directory = new LdapDirectory(new SsoOptions { Ldap = o });

        var ex = await Assert.ThrowsAsync<DirectoryUnavailableException>(() => directory.BindAsync("alice", "pw"));
        Assert.Contains("LDAPS or StartTLS", ex.Message);
        Assert.DoesNotContain("svc-secret", ex.Message);
        await Assert.ThrowsAsync<DirectoryUnavailableException>(() => directory.FindAsync("alice"));
        await Assert.ThrowsAsync<DirectoryUnavailableException>(() => directory.TestAsync());
    }

    [Fact]
    public async Task LdapDirectory_EmptyPassword_IsNeverABindSuccess()
    {
        // Valid configuration, but an empty password must be refused without touching the network.
        var directory = new LdapDirectory(new SsoOptions { Ldap = Valid() });
        Assert.False(await directory.BindAsync("alice", ""));
        Assert.False(await directory.BindAsync("", "pw"));
    }
}
