using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Services.Sso;

namespace MicroLIMS.Infrastructure.Sso;

/// <summary>
/// <see cref="ILdapDirectory"/> over <c>System.DirectoryServices.Protocols</c>. Every connection is
/// LDAPS or StartTLS and is bound explicitly after the encryption is in place: there is no
/// automatic bind, no plain fallback, and a failed TLS negotiation ends the attempt. Passwords are
/// passed only to the bind call and are removed from any message before it leaves this class.
/// </summary>
public sealed class LdapDirectory : ILdapDirectory
{
    // LDAP result code 49: invalid credentials (RFC 4511).
    private const int InvalidCredentialsCode = 49;
    private const string AccountDisabledAttribute = "userAccountControl";
    private const int AccountDisableFlag = 0x2;
    private const int FindManyBatchSize = 100;
    private const string SecretMask = "[redacted]";

    private readonly LdapOptions _options;
    private readonly ILogger<LdapDirectory> _logger;

    public LdapDirectory(SsoOptions options, ILogger<LdapDirectory>? logger = null)
    {
        _options = options.Ldap;
        _logger = logger ?? NullLogger<LdapDirectory>.Instance;
    }

    public async Task<LdapEntry?> FindAsync(string username, CancellationToken ct = default)
    {
        EnsureUsable();
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return await RunAsync(() =>
        {
            using var service = OpenServiceConnection();
            return FindUser(service, username);
        }, ct);
    }

    public async Task<bool> BindAsync(string username, string password, CancellationToken ct = default)
    {
        EnsureUsable();

        // An empty password makes a simple bind anonymous, which servers accept. It is never a valid sign-in.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return false;

        return await RunAsync(() =>
        {
            string userDn;
            using (var service = OpenServiceConnection())
            {
                var match = SearchUsers(service, username);
                if (match.Count != 1)
                    return false;
                userDn = match[0].DistinguishedName;
            }

            try
            {
                using var userConnection = OpenConnection(userDn, password);
                return true;
            }
            catch (LdapException ex) when (ex.ErrorCode == InvalidCredentialsCode)
            {
                return false;
            }
            catch (LdapException ex)
            {
                throw Unavailable("The directory refused the sign-in bind for a reason other than the credentials", ex, password);
            }
        }, ct);
    }

    public async Task<IReadOnlyDictionary<string, LdapEntry?>> FindManyBySubjectAsync(
        IReadOnlyCollection<string> subjectIds, CancellationToken ct = default)
    {
        EnsureUsable();
        var result = new Dictionary<string, LdapEntry?>(StringComparer.Ordinal);
        if (subjectIds.Count == 0)
            return result;

        return await RunAsync<IReadOnlyDictionary<string, LdapEntry?>>(() =>
        {
            using var service = OpenServiceConnection();
            var pending = subjectIds.Distinct(StringComparer.Ordinal).ToList();

            foreach (var batch in pending.Chunk(FindManyBatchSize))
            {
                var terms = new List<string>(batch.Length);
                var requested = new List<string>(batch.Length);
                foreach (var id in batch)
                {
                    // An id this directory could not have issued is left out of the answer, never
                    // reported as "missing", so a caller cannot mistake a bad stored value for a leaver.
                    if (!TrySubjectFilterValue(id, out var filterValue))
                        continue;
                    terms.Add($"({_options.SubjectAttribute.Trim()}={filterValue})");
                    requested.Add(id);
                    result[id] = null;
                }

                if (terms.Count == 0)
                    continue;

                var filter = terms.Count == 1 ? terms[0] : $"(|{string.Concat(terms)})";
                foreach (var entry in Search(service, _options.BaseDn!.Trim(), SearchScope.Subtree, filter, sizeLimit: 0))
                {
                    var parsed = ToEntry(entry, fallbackUsername: string.Empty);
                    if (parsed is not null && result.ContainsKey(parsed.SubjectId))
                        result[parsed.SubjectId] = parsed;
                }
            }

            return result;
        }, ct);
    }

    public async Task TestAsync(CancellationToken ct = default)
    {
        EnsureUsable();
        await RunAsync(() =>
        {
            using var service = OpenServiceConnection();
            // A base-scope read proves the base DN exists and the service account may read it.
            var request = new SearchRequest(_options.BaseDn!.Trim(), "(objectClass=*)", SearchScope.Base, "1.1");
            try
            {
                var response = (SearchResponse)service.SendRequest(request);
                if (response.Entries.Count == 0)
                    throw new DirectoryUnavailableException("The base DN returned no entry. Check Sso:Ldap:BaseDn.");
            }
            catch (DirectoryOperationException ex)
            {
                throw Unavailable("The base DN could not be read by the service account", ex);
            }
            catch (LdapException ex)
            {
                throw Unavailable("The directory search failed", ex);
            }

            return true;
        }, ct);
    }

    // ---- helpers --------------------------------------------------------

    private void EnsureUsable()
    {
        var problems = _options.Validate();
        if (problems.Count > 0)
            throw new DirectoryUnavailableException(string.Join(" ", problems));
    }

    private static async Task<T> RunAsync<T>(Func<T> work, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await Task.Run(work, ct);
    }

    private LdapConnection OpenServiceConnection()
    {
        try
        {
            return OpenConnection(_options.BindDn!.Trim(), _options.BindPassword!);
        }
        catch (LdapException ex) when (ex.ErrorCode == InvalidCredentialsCode)
        {
            throw new DirectoryUnavailableException("The directory rejected the service account. Check Sso:Ldap:BindDn and Sso:Ldap:BindPassword.");
        }
        catch (LdapException ex)
        {
            throw Unavailable("The service account could not bind", ex, _options.BindPassword);
        }
    }

    // Encrypt first, then bind. AutoBind is off so credentials can never be sent before TLS is up.
    private LdapConnection OpenConnection(string bindDn, string password)
    {
        var connection = new LdapConnection(
            new LdapDirectoryIdentifier(_options.Host!.Trim(), _options.Port),
            new NetworkCredential(bindDn, password),
            AuthType.Basic)
        {
            AutoBind = false,
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        try
        {
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
            if (_options.EffectiveUseLdaps)
                connection.SessionOptions.SecureSocketLayer = true;
            else
                connection.SessionOptions.StartTransportLayerSecurity(null);

            connection.Bind();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private LdapEntry? FindUser(LdapConnection connection, string username)
    {
        var matches = SearchUsers(connection, username);
        return matches.Count == 1 ? ToEntry(matches[0], username) : null;
    }

    private List<SearchResultEntry> SearchUsers(LdapConnection connection, string username)
    {
        var filter = LdapFilter.Equality(_options.UsernameAttribute.Trim(), username.Trim());
        // Two is enough to know the name is ambiguous, which is refused.
        return Search(connection, _options.BaseDn!.Trim(), SearchScope.Subtree, filter, sizeLimit: 2);
    }

    private List<SearchResultEntry> Search(LdapConnection connection, string baseDn, SearchScope scope, string filter, int sizeLimit)
    {
        var request = new SearchRequest(
            baseDn,
            filter,
            scope,
            _options.UsernameAttribute.Trim(),
            _options.SubjectAttribute.Trim(),
            AccountDisabledAttribute)
        {
            SizeLimit = sizeLimit,
            TimeLimit = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        try
        {
            var response = (SearchResponse)connection.SendRequest(request);
            return response.Entries.Cast<SearchResultEntry>().ToList();
        }
        catch (DirectoryOperationException ex) when (sizeLimit > 0 && ex.Response?.ResultCode == ResultCode.SizeLimitExceeded)
        {
            // More matches than the limit this class set: the name is ambiguous. Callers treat any
            // result that is not exactly one entry as a refusal. (An unlimited search that hits a
            // server-side limit is an error below, never an empty answer.)
            return new List<SearchResultEntry>();
        }
        catch (DirectoryOperationException ex)
        {
            throw Unavailable("The directory search failed", ex);
        }
        catch (LdapException ex)
        {
            throw Unavailable("The directory search failed", ex);
        }
    }

    private LdapEntry? ToEntry(SearchResultEntry entry, string fallbackUsername)
    {
        var subjectBytes = FirstValue(entry, _options.SubjectAttribute.Trim());
        if (subjectBytes is null || subjectBytes.Length == 0)
            return null;

        var subject = string.Equals(_options.SubjectAttribute.Trim(), "objectGUID", StringComparison.OrdinalIgnoreCase)
                      && subjectBytes.Length == 16
            ? new Guid(subjectBytes).ToString()
            : Encoding.UTF8.GetString(subjectBytes);

        var usernameBytes = FirstValue(entry, _options.UsernameAttribute.Trim());
        var username = usernameBytes is { Length: > 0 } ? Encoding.UTF8.GetString(usernameBytes) : fallbackUsername;

        var disabled = false;
        var uacBytes = FirstValue(entry, AccountDisabledAttribute);
        if (uacBytes is { Length: > 0 }
            && int.TryParse(Encoding.UTF8.GetString(uacBytes), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var uac))
        {
            disabled = (uac & AccountDisableFlag) != 0;
        }

        return new LdapEntry(subject, username, disabled);
    }

    private static byte[]? FirstValue(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute))
            return null;
        var values = entry.Attributes[attribute].GetValues(typeof(byte[]));
        return values.Length == 0 ? null : (byte[])values[0];
    }

    private bool TrySubjectFilterValue(string subjectId, out string filterValue)
    {
        filterValue = string.Empty;
        if (string.IsNullOrWhiteSpace(subjectId))
            return false;

        if (string.Equals(_options.SubjectAttribute.Trim(), "objectGUID", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(subjectId, out var guid))
                return false;
            filterValue = LdapFilter.EscapeBytes(guid.ToByteArray());
            return true;
        }

        filterValue = LdapFilter.Escape(subjectId);
        return true;
    }

    private DirectoryUnavailableException Unavailable(string what, Exception ex, params string?[] secrets)
    {
        var detail = ex.Message;
        foreach (var secret in secrets.Append(_options.BindPassword))
        {
            if (!string.IsNullOrEmpty(secret))
                detail = detail.Replace(secret, SecretMask, StringComparison.Ordinal);
        }

        var code = ex is LdapException le ? $" (LDAP error {le.ErrorCode})" : string.Empty;
        _logger.LogWarning("{What}: {ExceptionType}{Code}.", what, ex.GetType().Name, code);
        return new DirectoryUnavailableException($"{what}{code}: {detail}");
    }
}
