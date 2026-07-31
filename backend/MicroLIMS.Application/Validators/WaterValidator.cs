namespace MicroLIMS.Application.Validators;

public class WaterValidator
{
    public List<string> Validate(int samplingPointId, string testCode)
    {
        var errors = new List<string>();
        if (samplingPointId <= 0) errors.Add("A valid sampling point must be selected.");
        if (string.IsNullOrWhiteSpace(testCode)) errors.Add("Test code is required.");
        return errors;
    }
}
