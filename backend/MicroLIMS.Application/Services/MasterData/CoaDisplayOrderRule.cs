namespace MicroLIMS.Application.Services.MasterData;

// Validates the Test Master "CoA order" (TestDefinition.CoaDisplayOrder).
public static class CoaDisplayOrderRule
{
    public const int Max = 9999;

    // Create: null = no order; 1-9999 = the order.
    public static int? ForCreate(int? value) => value is null ? null : Validate(value.Value, allowClear: false);

    // Update: 0 clears the order; 1-9999 sets it (null = keep is handled by the caller).
    public static int? ForUpdate(int value) => value == 0 ? null : Validate(value, allowClear: true);

    private static int Validate(int value, bool allowClear)
    {
        if (value < 1 || value > Max)
            throw new InvalidOperationException(allowClear
                ? $"CoA order must be between 1 and {Max}, or 0 to clear it."
                : $"CoA order must be between 1 and {Max}.");
        return value;
    }
}
