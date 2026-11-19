using System.Globalization;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
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
        {
            var parameters = new Dictionary<string, string> { ["max"] = Max.ToString(CultureInfo.InvariantCulture) };
            if (allowClear)
                throw new BusinessRuleException(
                    MessageCodes.MasterDataCoaOrderOutOfRangeAllowClear,
                    $"CoA order must be between 1 and {Max}, or 0 to clear it.",
                    parameters);
            throw new BusinessRuleException(
                MessageCodes.MasterDataCoaOrderOutOfRange,
                $"CoA order must be between 1 and {Max}.",
                parameters);
        }
        return value;
    }
}
