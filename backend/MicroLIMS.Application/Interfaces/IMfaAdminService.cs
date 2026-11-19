namespace MicroLIMS.Application.Interfaces;

public interface IMfaAdminService
{
    Task ResetAsync(int targetUserId, string reason, int actingUserId, CancellationToken ct = default);
    Task AssignHardwareTokenAsync(int targetUserId, string serialNumber, string seed, bool seedIsHex, int stepSeconds, string reason, int actingUserId, CancellationToken ct = default);
    Task<IReadOnlyList<TrustedDeviceInfo>> ListTrustedDevicesAsync(int targetUserId, CancellationToken ct = default);
    Task RevokeTrustedDeviceAsync(int targetUserId, int deviceId, int actingUserId, CancellationToken ct = default);
}
