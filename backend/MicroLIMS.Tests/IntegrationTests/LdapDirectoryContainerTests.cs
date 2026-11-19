using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Infrastructure.Sso;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Runs against a real OpenLDAP container (CI starts it and sets MICROLIMS_TEST_LDAP; the seed users are in
// Fixtures/ldap/seed.ldif). Skipped with a reason when no container is available.
public class LdapDirectoryContainerTests
{
    private static LdapDirectory Directory(bool startTls = false, int? port = null) =>
        new(new SsoOptions { Ldap = LdapTestConfiguration.ToOptions(startTls, port) });

    [LdapContainerFact]
    public async Task RealDirectory_BindSucceedsWithCorrectPassword_FailsWithWrong()
    {
        var directory = Directory();

        Assert.True(await directory.BindAsync("alice", "Alice-Pass-1!"));
        Assert.False(await directory.BindAsync("alice", "Alice-Pass-2!"));
        Assert.False(await directory.BindAsync("alice", ""));          // empty password is an anonymous bind: never a success
        Assert.False(await directory.BindAsync("nobody", "Alice-Pass-1!"));
        Assert.False(await directory.BindAsync("alice", "Bob-Pass-1!"));

        var entry = await directory.FindAsync("alice");
        Assert.NotNull(entry);
        Assert.Equal("alice", entry.Username);
        Assert.False(string.IsNullOrWhiteSpace(entry.SubjectId));
        Assert.False(entry.Disabled);

        var many = await directory.FindManyBySubjectAsync(new[] { entry.SubjectId });
        Assert.Equal(entry.SubjectId, many[entry.SubjectId]!.SubjectId);

        await directory.TestAsync();
    }

    [LdapContainerFact]
    public async Task UsernameWithFilterCharacters_IsEscaped()
    {
        var directory = Directory();

        foreach (var hostile in new[] { "*", "al*", "alice)(uid=*", "*)(objectClass=*", "alice\\", "(uid=alice)", "alice\0" })
        {
            Assert.Null(await directory.FindAsync(hostile));
            Assert.False(await directory.BindAsync(hostile, "Alice-Pass-1!"));
        }
    }

    [LdapContainerFact]
    public async Task PlainConnection_IsRefused()
    {
        // The plain port speaks no TLS. LDAPS against it must fail, and must never fall back to a plain bind.
        var plainPort = int.Parse(LdapTestConfiguration.Parse()["PlainPort"]);
        var directory = Directory(startTls: false, port: plainPort);

        await Assert.ThrowsAsync<DirectoryUnavailableException>(() => directory.BindAsync("alice", "Alice-Pass-1!"));
        await Assert.ThrowsAsync<DirectoryUnavailableException>(() => directory.TestAsync());

        // The configuration that would describe a plain connection is refused outright.
        var options = LdapTestConfiguration.ToOptions(port: plainPort);
        options.UseLdaps = false;
        var plain = new LdapDirectory(new SsoOptions { Ldap = options });
        var ex = await Assert.ThrowsAsync<DirectoryUnavailableException>(() => plain.BindAsync("alice", "Alice-Pass-1!"));
        Assert.Contains("LDAPS or StartTLS", ex.Message);
    }
}
