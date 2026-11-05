namespace MicroLIMS.Application.Helpers;

// Fixed ICP calibration rules. There is no method master any more, so these are system-wide.
public static class IcpDefaults
{
    public const decimal MinCorrelation = 0.999m;   // r at or above this passes
    public const int CalibrationValidHours = 24;    // a confirmed calibration is usable this long
}
