using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// The GPT pass/fail rules for one of the 4 fixed MediaClass values -
// exactly one row per class, enforced by a unique index on Class (see
// MediaTypeConfiguration). Individual prepared lots are Media, which
// reference this by MediaTypeId (i.e. by class) but get their actual
// product identity from the linked Material (see Media.MaterialId).
public class MediaType
{
    public int Id { get; set; }
    public MediaClass Class { get; set; }
    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
    public decimal RequiredTemperatureMin { get; set; }
    public decimal RequiredTemperatureMax { get; set; }

    // Which TestCodes this media type is approved for use with.
    public List<string> ApprovedTestCodes { get; set; } = new();

    // General Agar only: Recovery% pass/fail band, e.g. 70-200.
    public decimal? RecoveryPercentMin { get; set; }
    public decimal? RecoveryPercentMax { get; set; }
}
