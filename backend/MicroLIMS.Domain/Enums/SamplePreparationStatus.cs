namespace MicroLIMS.Domain.Enums;

// Only meaningful for EM/After Cleaning, whose TestOrders do not exist
// until Room/Part + test-type checkboxes are confirmed after receiving.
// Product/RM/PM/Water samples go straight to Ready (TestOrders already exist).
public enum SamplePreparationStatus
{
    NeedsPreparation,
    Ready
}
