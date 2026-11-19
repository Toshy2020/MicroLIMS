using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Verified identity claims extracted from an external directory or OIDC sign-in.
/// </summary>
public sealed record DirectoryIdentity(string SubjectId, string Username, bool MfaSatisfied);

public enum MatchFailure
{
    None,
    NotLinked,
    SubjectMismatch,
    UserInactive
}

/// <summary>
/// Outcome of matching an external <see cref="DirectoryIdentity"/> against local users.
/// </summary>
public sealed record MatchResult(User? User, MatchFailure Failure, bool BindSubjectNow);

/// <summary>
/// Matches an external identity to a pre-provisioned MicroLIMS user without creating new accounts.
/// </summary>
public static class DirectoryUserMatcher
{
    public static MatchResult Match(DirectoryIdentity identity, User? bySubject, User? byUsername)
    {
        if (bySubject is not null)
        {
            return bySubject.IsActive
                ? new MatchResult(bySubject, MatchFailure.None, BindSubjectNow: false)
                : new MatchResult(bySubject, MatchFailure.UserInactive, BindSubjectNow: false);
        }

        if (byUsername is not null)
        {
            if (!byUsername.IsActive)
                return new MatchResult(byUsername, MatchFailure.UserInactive, BindSubjectNow: false);

            if (string.IsNullOrWhiteSpace(byUsername.DirectorySubjectId))
                return new MatchResult(byUsername, MatchFailure.None, BindSubjectNow: true);

            return string.Equals(byUsername.DirectorySubjectId, identity.SubjectId, StringComparison.Ordinal)
                ? new MatchResult(byUsername, MatchFailure.None, BindSubjectNow: false)
                : new MatchResult(byUsername, MatchFailure.SubjectMismatch, BindSubjectNow: false);
        }

        return new MatchResult(null, MatchFailure.NotLinked, BindSubjectNow: false);
    }
}
