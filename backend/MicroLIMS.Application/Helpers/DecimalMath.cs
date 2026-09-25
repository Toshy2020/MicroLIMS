namespace MicroLIMS.Application.Helpers;

public static class DecimalMath
{
    /// <summary>
    /// Computes the square root of a decimal value to the specified number of decimal places using Newton-Raphson iteration.
    /// </summary>
    public static decimal Sqrt(decimal x, int decimals = 10)
    {
        if (x < 0m)
            throw new ArgumentOutOfRangeException(nameof(x), "Cannot compute the square root of a negative number.");
        if (x == 0m)
            return 0m;

        // Seed with double precision sqrt for fast convergence
        decimal current = (decimal)Math.Sqrt((double)x);
        if (current == 0m)
            current = 1m;

        decimal previous;
        for (int i = 0; i < 100; i++)
        {
            previous = current;
            current = 0.5m * (previous + x / previous);
            if (current == previous || Math.Abs(current - previous) < 1e-25m)
                break;
        }

        return Math.Round(current, decimals, MidpointRounding.AwayFromZero);
    }
}
