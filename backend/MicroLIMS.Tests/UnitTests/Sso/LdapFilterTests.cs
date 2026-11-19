using MicroLIMS.Application.Services.Sso;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Sso;

public class LdapFilterTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("*", "\\2a")]
    [InlineData("(", "\\28")]
    [InlineData(")", "\\29")]
    [InlineData("\\", "\\5c")]
    [InlineData("a\0b", "a\\00b")]
    [InlineData("*)(uid=*", "\\2a\\29\\28uid=\\2a")]
    [InlineData("é", "\\c3\\a9")]
    [InlineData("中", "\\e4\\b8\\ad")]
    [InlineData("", "")]
    public void Escape_FollowsRfc4515(string input, string expected) =>
        Assert.Equal(expected, LdapFilter.Escape(input));

    [Fact]
    public void Escape_Null_IsEmpty() => Assert.Equal(string.Empty, LdapFilter.Escape(null));

    [Fact]
    public void Escape_NeverLeavesFilterSyntaxCharacters()
    {
        var escaped = LdapFilter.Escape("x*)(|(objectClass=*))\\\0");
        Assert.DoesNotContain('*', escaped);
        Assert.DoesNotContain('(', escaped);
        Assert.DoesNotContain(')', escaped);
        Assert.DoesNotContain('\0', escaped);
    }

    [Fact]
    public void Equality_EscapesTheValueInsideTheFilter()
    {
        Assert.Equal("(uid=alice)", LdapFilter.Equality("uid", "alice"));
        Assert.Equal("(uid=\\2a\\29\\28objectClass=\\2a)", LdapFilter.Equality("uid", "*)(objectClass=*"));
    }

    [Fact]
    public void EscapeBytes_EscapesEveryByte() =>
        Assert.Equal("\\01\\ab\\ff", LdapFilter.EscapeBytes(new byte[] { 0x01, 0xab, 0xff }));
}
