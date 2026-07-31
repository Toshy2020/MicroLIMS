namespace MicroLIMS.Infrastructure.Notifications;

public interface INotificationService
{
    Task NotifyAsync(int userId, string message);
}
