using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class QaRoleTests
{
    [Fact]
    public void QaGrants_contains_only_known_codes()
    {
        var unknown = PermissionConstants.QaGrants.Except(PermissionConstants.All).ToList();
        Assert.Empty(unknown);
    }

    [Fact]
    public void QaGrants_has_no_duplicates()
    {
        Assert.Equal(PermissionConstants.QaGrants.Count, PermissionConstants.QaGrants.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RoleType_Qa_is_5()
    {
        Assert.Equal(5, (int)RoleType.Qa);
    }

    [Fact]
    public void Qa_only_permissions_are_enforced_and_not_granted_to_system_administrator()
    {
        var qaOnlyCodes = new[] { PermissionConstants.AuditReview, PermissionConstants.AccessReviewSign };

        foreach (var code in qaOnlyCodes)
        {
            Assert.Contains(code, PermissionConstants.All);
            Assert.Contains(code, PermissionConstants.Enforced);
            Assert.Contains(code, PermissionConstants.QaGrants);
            Assert.DoesNotContain(code, PermissionConstants.SystemAdministratorGrants);
        }
    }
}
