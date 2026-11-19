using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Domain.Entities;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Sso;

public class DirectoryLinkStateTests
{
    private static readonly DateTime T0 = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoCompanyUsername_IsNotLinked_EvenWithStaleSubjectAndRefusal()
    {
        Assert.Equal(DirectoryLinkState.NotLinked,
            DirectoryLinkStates.Compute(null, "sub-1", T0, null));
    }

    [Fact]
    public void CompanyUsername_NoSubject_IsLinkedNeverSignedIn()
    {
        Assert.Equal(DirectoryLinkState.LinkedNeverSignedIn,
            DirectoryLinkStates.Compute("jane@corp.test", null, null, null));
    }

    [Fact]
    public void CompanyUsername_WithSubject_IsLinked()
    {
        Assert.Equal(DirectoryLinkState.Linked,
            DirectoryLinkStates.Compute("jane@corp.test", "sub-1", null, T0));
    }

    [Fact]
    public void Refused_NeverSignedIn_IsSignInRefused()
    {
        Assert.Equal(DirectoryLinkState.SignInRefused,
            DirectoryLinkStates.Compute("jane@corp.test", "sub-1", T0, null));
    }

    [Fact]
    public void Refused_AfterLastLogin_IsSignInRefused()
    {
        Assert.Equal(DirectoryLinkState.SignInRefused,
            DirectoryLinkStates.Compute("jane@corp.test", "sub-1", T0.AddMinutes(1), T0));
    }

    [Fact]
    public void Refused_BeforeLastLogin_IsNoLongerRefused()
    {
        Assert.Equal(DirectoryLinkState.Linked,
            DirectoryLinkStates.Compute("jane@corp.test", "sub-1", T0, T0.AddMinutes(1)));
        Assert.Equal(DirectoryLinkState.LinkedNeverSignedIn,
            DirectoryLinkStates.Compute("jane@corp.test", null, T0, T0.AddMinutes(1)));
    }

    [Fact]
    public void Refused_AtSameInstantAsLastLogin_IsNotRefused()
    {
        Assert.Equal(DirectoryLinkState.Linked,
            DirectoryLinkStates.Compute("jane@corp.test", "sub-1", T0, T0));
    }

    [Fact]
    public void Of_ReadsTheUsersFields()
    {
        var user = new User
        {
            DirectoryUsername = "jane@corp.test",
            DirectorySubjectId = "sub-1",
            DirectorySignInRefusedAt = T0.AddHours(1),
            LastLoginAt = T0
        };
        Assert.Equal(DirectoryLinkState.SignInRefused, DirectoryLinkStates.Of(user));
    }
}
