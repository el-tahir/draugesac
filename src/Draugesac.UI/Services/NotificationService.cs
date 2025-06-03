using Microsoft.JSInterop;

namespace Draugesac.UI.Services;

public class NotificationService
{
    private readonly IJSRuntime _jsRuntime;

    public NotificationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // Events for notifications
    public event Action<NotificationMessage>? NotificationAdded;
    public event Action<string>? NotificationRemoved;

    private readonly List<NotificationMessage> _notifications = new();

    public async Task ShowSuccessAsync(string message, int durationMs = 5000)
    {
        ShowNotification(message, NotificationType.Success, durationMs);
    }

    public async Task ShowErrorAsync(string message, int durationMs = 8000)
    {
        ShowNotification(message, NotificationType.Error, durationMs);
    }

    public async Task ShowWarningAsync(string message, int durationMs = 6000)
    {
        ShowNotification(message, NotificationType.Warning, durationMs);
    }

    public async Task ShowInfoAsync(string message, int durationMs = 5000)
    {
        ShowNotification(message, NotificationType.Info, durationMs);
    }

    private void ShowNotification(string message, NotificationType type, int durationMs)
    {
        var notification = new NotificationMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = message,
            Type = type,
            Timestamp = DateTime.Now
        };

        _notifications.Add(notification);
        NotificationAdded?.Invoke(notification);

        // Auto-remove after duration - properly awaited
        _ = Task.Run(async () =>
        {
            await Task.Delay(durationMs);
            RemoveNotification(notification.Id);
        });
    }

    public void RemoveNotification(string id)
    {
        NotificationMessage? notification = _notifications.FirstOrDefault(n => n.Id == id);
        if (notification != null)
        {
            _notifications.Remove(notification);
            NotificationRemoved?.Invoke(id);
        }
    }

    public List<NotificationMessage> GetNotifications() => _notifications.ToList();
}

public class NotificationMessage
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum NotificationType
{
    Success,
    Error,
    Warning,
    Info
}
