using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class OosOutcomeMapperTests
{
    [Theory]
    [InlineData(ResultStatus.ActionLimitExceeded, true, OosOutcomeClass.ActionLimitExceeded)]
    [InlineData(ResultStatus.AlertLimitExceeded, true, OosOutcomeClass.AlertLimitExceeded)]
    [InlineData(null, true, OosOutcomeClass.Fails)]
    [InlineData(null, false, OosOutcomeClass.Conforms)]
    public void Map_preserves_the_opening_classification(ResultStatus? status, bool fails, OosOutcomeClass expected)
    {
        Assert.Equal(expected, OosOutcomeMapper.Map(new TestOrderOutcome(fails, status)));
    }
}
