using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SessionLocationGrouperTests
{
    [Fact]
    public void SampleWithNoLocations_GetsOneDefaultProductLocation_MappingEveryTestToZero()
    {
        var sample = new Sample
        {
            ReferenceNumber = "S-1",
            Item = new Item { Name = "Cream" },
            TestOrders = { new TestOrder { Id = 1, TestCode = "TAMC" }, new TestOrder { Id = 2, TestCode = "TYMC" } }
        };

        var result = SessionLocationGrouper.Group(sample);

        var only = Assert.Single(result);
        Assert.Equal(1, only.Id);
        Assert.Equal("Cream", only.LocationName);
        Assert.Equal("Product", only.LocationType);
        Assert.Equal(0, only.PrimarySampleLocationId);
        Assert.Equal(2, only.TestLocationMap.Count);
        Assert.All(only.TestLocationMap.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void DefaultLocation_FallsBackToTheReferenceNumber_WhenThereIsNoItem()
    {
        var sample = new Sample { ReferenceNumber = "S-9" };

        var only = Assert.Single(SessionLocationGrouper.Group(sample));

        Assert.Equal("S-9", only.LocationName);
    }

    [Fact]
    public void TwoWaterPointsWithTheSameDisplayName_StayTwoLocations()
    {
        var sample = new Sample
        {
            ReferenceNumber = "S-2",
            TestOrders = { new TestOrder { Id = 1, TestCode = "TAMC" } },
            Locations =
            {
                new SampleLocation { Id = 10, TestOrderId = 1, WaterSamplingPointId = 100, WaterSamplingPoint = new WaterSamplingPoint { Id = 100, Location = "WTU" } },
                new SampleLocation { Id = 11, TestOrderId = 1, WaterSamplingPointId = 101, WaterSamplingPoint = new WaterSamplingPoint { Id = 101, Location = "WTU" } }
            }
        };

        var result = SessionLocationGrouper.Group(sample);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2 }, result.Select(r => r.Id));
        Assert.All(result, r => Assert.Equal("WTU", r.LocationName));
    }

    [Fact]
    public void OnePhysicalLocationTestedByTwoTests_IsOneLocationWithBothTestsMapped()
    {
        var sample = new Sample
        {
            ReferenceNumber = "S-3",
            TestOrders = { new TestOrder { Id = 1, TestCode = "TAMC" }, new TestOrder { Id = 2, TestCode = "TYMC" } },
            Locations =
            {
                new SampleLocation { Id = 20, TestOrderId = 1, WaterSamplingPointId = 100, WaterSamplingPoint = new WaterSamplingPoint { Id = 100, Location = "WTU" } },
                new SampleLocation { Id = 21, TestOrderId = 2, WaterSamplingPointId = 100, WaterSamplingPoint = new WaterSamplingPoint { Id = 100, Location = "WTU" } }
            }
        };

        var only = Assert.Single(SessionLocationGrouper.Group(sample));

        Assert.Equal(20, only.PrimarySampleLocationId);
        Assert.Equal(20, only.TestLocationMap["TAMC"]);
        Assert.Equal(21, only.TestLocationMap["TYMC"]);
    }
}
