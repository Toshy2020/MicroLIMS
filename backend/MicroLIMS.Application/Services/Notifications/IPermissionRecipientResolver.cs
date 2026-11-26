namespace MicroLIMS.Application.Services.Notifications;

public interface IPermissionRecipientResolver
{
    Task<IReadOnlyList<int>> ResolveAsync(int? sectionId, string permissionCode, CancellationToken ct = default);
}
