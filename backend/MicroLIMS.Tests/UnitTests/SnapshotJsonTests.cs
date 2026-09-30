using System.Text.Json;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Stored snapshots are read by the frontend as is, so enums must be written
// by name (a recipe snapshot with standardizationMode 0 made the titrant
// dialog treat a primary-standard titrant as standardized against a VS).
public class SnapshotJsonTests
{
    private record Probe(StandardizationMode? StandardizationMode, ShelfLifeUnit ShelfLifeUnit);

    [Fact]
    public void Serialize_WritesEnumsByName()
    {
        var json = JsonSerializer.Serialize(new Probe(StandardizationMode.PrimaryStandard, ShelfLifeUnit.Days), SnapshotJson.Options);

        Assert.Equal("{\"standardizationMode\":\"PrimaryStandard\",\"shelfLifeUnit\":\"Days\"}", json);
    }

    [Fact]
    public void Deserialize_StillReadsSnapshotsWrittenWithNumericEnums()
    {
        var probe = JsonSerializer.Deserialize<Probe>("{\"standardizationMode\":1,\"shelfLifeUnit\":0}", SnapshotJson.Options)!;

        Assert.Equal(StandardizationMode.AgainstVolumetricSolution, probe.StandardizationMode);
        Assert.Equal(ShelfLifeUnit.Hours, probe.ShelfLifeUnit);
    }
}
