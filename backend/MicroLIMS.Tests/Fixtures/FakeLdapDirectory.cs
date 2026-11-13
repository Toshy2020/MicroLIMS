using MicroLIMS.Application.Abstractions.Sso;

namespace MicroLIMS.Tests.Fixtures;

// In-memory stand-in for the company directory. Records which calls were made (never the
// passwords) so tests can prove when the directory was, and was not, contacted.
public sealed class FakeLdapDirectory : ILdapDirectory
{
    private readonly Dictionary<string, (string Password, LdapEntry Entry)> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public List<string> BindCalls { get; } = new();
    public List<string> FindCalls { get; } = new();
    public int TestCalls { get; private set; }
    public Exception? ToThrow { get; set; }
    public Exception? TestThrows { get; set; }

    public FakeLdapDirectory WithAccount(string username, string password, string subjectId, bool disabled = false)
    {
        _accounts[username] = (password, new LdapEntry(subjectId, username, disabled));
        return this;
    }

    public Task<LdapEntry?> FindAsync(string username, CancellationToken ct = default)
    {
        FindCalls.Add(username);
        if (ToThrow is not null) throw ToThrow;
        return Task.FromResult(_accounts.TryGetValue(username, out var a) ? a.Entry : null);
    }

    public Task<bool> BindAsync(string username, string password, CancellationToken ct = default)
    {
        BindCalls.Add(username);
        if (ToThrow is not null) throw ToThrow;
        return Task.FromResult(!string.IsNullOrEmpty(password)
            && _accounts.TryGetValue(username, out var a) && a.Password == password);
    }

    public Task<IReadOnlyDictionary<string, LdapEntry?>> FindManyBySubjectAsync(IReadOnlyCollection<string> subjectIds, CancellationToken ct = default)
    {
        if (ToThrow is not null) throw ToThrow;
        var bySubject = _accounts.Values.ToDictionary(v => v.Entry.SubjectId, v => (LdapEntry?)v.Entry);
        IReadOnlyDictionary<string, LdapEntry?> result = subjectIds.ToDictionary(
            id => id, id => bySubject.TryGetValue(id, out var e) ? e : null);
        return Task.FromResult(result);
    }

    public Task TestAsync(CancellationToken ct = default)
    {
        TestCalls++;
        return TestThrows is null ? Task.CompletedTask : Task.FromException(TestThrows);
    }
}
