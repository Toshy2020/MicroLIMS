namespace MicroLIMS.Application.Abstractions.Sso;

/// <summary>
/// One account as the company directory reports it. <paramref name="SubjectId"/> is the
/// directory's permanent id (Active Directory <c>objectGUID</c>), <paramref name="Disabled"/>
/// is true when the directory marks the account as disabled.
/// </summary>
public sealed record LdapEntry(string SubjectId, string Username, bool Disabled);

/// <summary>
/// The directory could not give an answer (unreachable, TLS refused, service account rejected,
/// misconfigured). Messages never contain a password.
/// </summary>
public sealed class DirectoryUnavailableException : Exception
{
    public DirectoryUnavailableException(string message) : base(message)
    {
    }

    public DirectoryUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Active Directory (LDAP) access. LDAPS or StartTLS only; implementations never fall back
/// to an unencrypted connection and never log, store or put a password in an exception message.
/// </summary>
public interface ILdapDirectory
{
    /// <summary>Service-account search by the configured username attribute. Null when not found.</summary>
    Task<LdapEntry?> FindAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Verifies the user's password by binding as the user. False means bad credentials (or no such
    /// account, or an empty password); anything else throws <see cref="DirectoryUnavailableException"/>.
    /// </summary>
    Task<bool> BindAsync(string username, string password, CancellationToken ct = default);

    /// <summary>Service-account lookup by permanent id. A missing account maps to null.</summary>
    Task<IReadOnlyDictionary<string, LdapEntry?>> FindManyBySubjectAsync(IReadOnlyCollection<string> subjectIds, CancellationToken ct = default);

    /// <summary>Service-account bind plus a search of the base DN. Throws when either fails.</summary>
    Task TestAsync(CancellationToken ct = default);
}
