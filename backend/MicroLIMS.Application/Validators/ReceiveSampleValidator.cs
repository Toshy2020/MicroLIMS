using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Validators;

// Backend prevents: invalid workflow order, missing mandatory data,
// duplicate records, unauthorized actions.
public class ReceiveSampleValidator
{
    public List<string> Validate(ItemBasedReceiveRequest request)
    {
        var errors = new List<string>();

        if (request.ItemId <= 0) errors.Add("A valid Item must be selected.");
        if (request.CauseOfTestingId <= 0) errors.Add("Cause of testing is required.");
        if (string.IsNullOrWhiteSpace(request.BatchNumber)) errors.Add("Batch number is required.");
        if (string.IsNullOrWhiteSpace(request.ControlNumber)) errors.Add("Control number is required.");
        if (string.IsNullOrWhiteSpace(request.SampledBy)) errors.Add("Sampled By is required.");

        return errors;
    }
}
