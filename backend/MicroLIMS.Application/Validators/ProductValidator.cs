namespace MicroLIMS.Application.Validators;

public class ProductValidator
{
    public List<string> Validate(decimal? plateCount)
    {
        var errors = new List<string>();
        if (plateCount is null || plateCount < 0) errors.Add("Plate count must be a non-negative number.");
        return errors;
    }
}
