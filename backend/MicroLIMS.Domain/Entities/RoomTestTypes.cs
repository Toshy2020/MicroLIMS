namespace MicroLIMS.Domain.Entities;

// The sample types an EM room can be configured for
// (RoomTestConfiguration.TestType). Passive and surface samples report a
// fixed unit (see IncubationStepRecorder.DeriveBatchLocationUnit); the
// others report whatever unit the lab entered on the configuration, so
// that unit is required for them.
public static class RoomTestTypes
{
    public const string PassiveAirSample = "PassiveAirSample";
    public const string SurfaceAirSample = "SurfaceAirSample";
    public const string ActiveAirSample = "ActiveAirSample";
    public const string CompressedAir = "CompressedAir";
    public const string Drains = "Drains";

    public static readonly IReadOnlyList<string> All =
        [PassiveAirSample, SurfaceAirSample, ActiveAirSample, CompressedAir, Drains];

    public static bool IsKnown(string? testType) => testType != null && All.Contains(testType);

    public static bool UsesConfiguredUnit(string? testType) =>
        testType is ActiveAirSample or CompressedAir or Drains;
}
