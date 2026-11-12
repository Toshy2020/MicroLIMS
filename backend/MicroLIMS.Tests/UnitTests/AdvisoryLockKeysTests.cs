using System.Reflection;
using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class AdvisoryLockKeysTests
{
    [Fact]
    public void All_keys_are_distinct()
    {
        var values = typeof(AdvisoryLockKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(long))
            .Select(f => (long)f.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(10, values.Length);
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}
