namespace MicroLIMS.Application.Validators;

public class EMValidator
{
    public List<string> Validate(int roomId, int stepNumber)
    {
        var errors = new List<string>();
        if (roomId <= 0) errors.Add("A valid room must be selected.");
        if (stepNumber is not (1 or 2)) errors.Add("Incubation step must be 1 or 2.");
        return errors;
    }
}
