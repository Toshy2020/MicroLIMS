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

    // FindManyBySubject: each call's subject ids, in order; throws when a call asks for any id in ThrowWhenAskedFor.
    public List<IReadOnlyCollection<string>> FindManyCalls { get; } = new();
    public HashSet<string> ThrowWhenAskedFor { get; } = new(StringComparer.Ordinal);

    // When true an id the fake has no account for is omitted from the answer (the directory could not have
    // issued it) instead of mapping to null (the account is gone). Lets a test share a database with others.
    public bool UnknownIdsAreOmitted { get; set; }

    public FakeLdapDirectory WithMissingSubject(string subjectId)
    {
        _missing.Add(subjectId);
        return this;
    }

    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
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
        FindManyCalls.Add(subjectIds.ToList());
        if (ToThrow is not null) throw ToThrow;
        if (subjectIds.Any(ThrowWhenAskedFor.Contains))
            throw new DirectoryUnavailableException("Fake directory stopped answering.");
        var bySubject = _accounts.Values.ToDictionary(v => v.Entry.SubjectId, v => (LdapEntry?)v.Entry);
        var result = new Dictionary<string, LdapEntry?>(StringComparer.Ordinal);
        foreach (var id in subjectIds)
        {
            if (bySubject.TryGetValue(id, out var e)) result[id] = e;
            else if (!UnknownIdsAreOmitted || _missing.Contains(id)) result[id] = null;
        }
        return Task.FromResult<IReadOnlyDictionary<string, LdapEntry?>>(result);
    }

    public Task TestAsync(CancellationToken ct = default)
    {
        TestCalls++;
        return TestThrows is null ? Task.CompletedTask : Task.FromException(TestThrows);
    }
}
