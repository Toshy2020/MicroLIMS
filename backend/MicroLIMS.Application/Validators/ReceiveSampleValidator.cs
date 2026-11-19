using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Validators;

// Backend prevents: invalid workflow order, missing mandatory data,
// duplicate records, unauthorized actions.
public class ReceiveSampleValidator
{
    public List<string> Validate(ItemBasedReceiveRequest request) =>
        ValidateFields(request).Select(e => e.Message).ToList();

    public List<FieldError> ValidateFields(ItemBasedReceiveRequest request, int? row = null)
    {
        var errors = new List<FieldError>();

        if (request.ItemId <= 0) errors.Add(new FieldError("itemId", "A valid Item must be selected.", row));
        if (request.CauseOfTestingId <= 0) errors.Add(new FieldError("causeOfTestingId", "Cause of testing is required.", row));
        if (string.IsNullOrWhiteSpace(request.BatchNumber)) errors.Add(new FieldError("batchNumber", "Batch number is required.", row));
        if (string.IsNullOrWhiteSpace(request.ControlNumber)) errors.Add(new FieldError("controlNumber", "Control number is required.", row));
        if (string.IsNullOrWhiteSpace(request.SampledBy)) errors.Add(new FieldError("sampledBy", "Sampled By is required.", row));

        return errors;
    }
}
