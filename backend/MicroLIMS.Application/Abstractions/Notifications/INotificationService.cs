namespace MicroLIMS.Application.Abstractions.Notifications;

public interface INotificationService
{
    Task NotifyAsync(int userId, string message);
}
