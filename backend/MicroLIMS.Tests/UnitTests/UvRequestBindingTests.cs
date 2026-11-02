using System.Text.Json;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Json;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// A non-nullable absorbance would bind an omitted field to 0, which an absorbance limit test accepts and passes.
public class UvRequestBindingTests
{
    [Theory]
    [InlineData("""{"weightG":null}""")]
    [InlineData("""{"weightG":null,"absorbance":null}""")]
    public void MissingAbsorbance_BindsToNull_NotZero(string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);

        var replicate = JsonSerializer.Deserialize<UvReplicateRequest>(json, options)!;

        Assert.Null(replicate.Absorbance);
    }
}
