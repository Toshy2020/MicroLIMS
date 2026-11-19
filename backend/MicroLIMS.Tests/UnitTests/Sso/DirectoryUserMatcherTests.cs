using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Domain.Entities;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Sso;

public class DirectoryUserMatcherTests
{
    [Fact]
    public void BySubject_Wins()
    {
        var subjectUser = new User { Id = 1, Username = "subj-user", DirectorySubjectId = "sub-1", IsActive = true };
        var usernameUser = new User { Id = 2, Username = "name-user", DirectoryUsername = "alice", IsActive = true };
        var identity = new DirectoryIdentity("sub-1", "alice", MfaSatisfied: true);

        var result = DirectoryUserMatcher.Match(identity, subjectUser, usernameUser);

        Assert.Equal(MatchFailure.None, result.Failure);
        Assert.Same(subjectUser, result.User);
        Assert.False(result.BindSubjectNow);
    }

    [Fact]
    public void ByUsername_FirstTime_BindsSubject()
    {
        var usernameUser = new User
        {
            Id = 2,
            Username = "alice.local",
            DirectoryUsername = "alice",
            DirectorySubjectId = null,
            IsActive = true
        };
        var identity = new DirectoryIdentity("sub-new", "alice", MfaSatisfied: false);

        var result = DirectoryUserMatcher.Match(identity, bySubject: null, byUsername: usernameUser);

        Assert.Equal(MatchFailure.None, result.Failure);
        Assert.Same(usernameUser, result.User);
        Assert.True(result.BindSubjectNow);
    }

    [Fact]
    public void ByUsername_DifferentStoredSubject_IsMismatch()
    {
        var usernameUser = new User
        {
            Id = 2,
            Username = "alice.local",
            DirectoryUsername = "alice",
            DirectorySubjectId = "sub-original",
            IsActive = true
        };
        var identity = new DirectoryIdentity("sub-different", "alice", MfaSatisfied: true);

        var result = DirectoryUserMatcher.Match(identity, bySubject: null, byUsername: usernameUser);

        Assert.Equal(MatchFailure.SubjectMismatch, result.Failure);
        Assert.Same(usernameUser, result.User);
        Assert.False(result.BindSubjectNow);
    }

    [Fact]
    public void NoUser_NotLinked()
    {
        var identity = new DirectoryIdentity("sub-unknown", "nobody", MfaSatisfied: false);

        var result = DirectoryUserMatcher.Match(identity, bySubject: null, byUsername: null);

        Assert.Equal(MatchFailure.NotLinked, result.Failure);
        Assert.Null(result.User);
        Assert.False(result.BindSubjectNow);
    }

    [Fact]
    public void InactiveUser_UserInactive()
    {
        var inactive = new User
        {
            Id = 3,
            Username = "inactive",
            DirectorySubjectId = "sub-3",
            IsActive = false
        };
        var identity = new DirectoryIdentity("sub-3", "inactive", MfaSatisfied: true);

        var bySubjectResult = DirectoryUserMatcher.Match(identity, bySubject: inactive, byUsername: null);
        Assert.Equal(MatchFailure.UserInactive, bySubjectResult.Failure);
        Assert.Same(inactive, bySubjectResult.User);

        var byUsernameResult = DirectoryUserMatcher.Match(identity, bySubject: null, byUsername: inactive);
        Assert.Equal(MatchFailure.UserInactive, byUsernameResult.Failure);
        Assert.Same(inactive, byUsernameResult.User);
    }

    [Fact]
    public void UsernameMatch_IsCaseInsensitive()
    {
        var usernameUser = new User
        {
            Id = 4,
            Username = "alice.local",
            DirectoryUsername = "Alice.Smith",
            DirectorySubjectId = null,
            IsActive = true
        };
        var identity = new DirectoryIdentity("sub-4", "alice.smith", MfaSatisfied: false);

        var result = DirectoryUserMatcher.Match(identity, bySubject: null, byUsername: usernameUser);

        Assert.Equal(MatchFailure.None, result.Failure);
        Assert.Same(usernameUser, result.User);
        Assert.True(result.BindSubjectNow);
    }
}
