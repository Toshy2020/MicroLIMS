using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// The reusable definition of a media (TSA, TSB, MAR, XLD, ...) - Section
// Head master data. Individual prepared lots are Media, which reference
// this. Incubation range and required temperature live here because
// they are the same for every lot of this media type.
public class MediaType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // short code used in lot numbers, e.g. "TSA"
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
