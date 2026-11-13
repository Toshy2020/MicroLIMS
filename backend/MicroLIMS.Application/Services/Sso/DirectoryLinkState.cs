using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// How far a user's company-account link has got, as shown on the Users list.
/// </summary>
public enum DirectoryLinkState
{
    NotLinked,
    LinkedNeverSignedIn,
    Linked,
    SignInRefused
}

public static class DirectoryLinkStates
{
    public static DirectoryLinkState Of(User user) => Compute(
        user.DirectoryUsername,
        user.DirectorySubjectId,
        user.DirectorySignInRefusedAt,
        user.LastLoginAt);

    // A refusal counts only while no sign-in has succeeded since it.
    public static DirectoryLinkState Compute(
        string? directoryUsername,
        string? directorySubjectId,
        DateTime? directorySignInRefusedAt,
        DateTime? lastLoginAt)
    {
        if (directoryUsername is null)
            return DirectoryLinkState.NotLinked;

        if (directorySignInRefusedAt is { } refusedAt
            && (lastLoginAt is null || refusedAt > lastLoginAt.Value))
        {
            return DirectoryLinkState.SignInRefused;
        }

        return directorySubjectId is null
            ? DirectoryLinkState.LinkedNeverSignedIn
            : DirectoryLinkState.Linked;
    }
}
