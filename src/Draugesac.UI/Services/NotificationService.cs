using Microsoft.JSInterop;

namespace Draugesac.UI.Services;

/// <summary>
/// Blazor notification service for displaying toast messages to users.
/// Provides thread-safe notification management with automatic cleanup.
/// </summary>
public class NotificationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly object _lockObject = new();

    public NotificationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    // Events for notifications
    public event Action<NotificationMessage>? NotificationAdded;
    public event Action<string>? NotificationRemoved;

    private readonly List<NotificationMessage> _notifications = new();

    /// <summary>
    /// Shows a success notification message.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="durationMs">Duration in milliseconds before auto-removal</param>
    public Task ShowSuccessAsync(string message, int durationMs = 5000)
    {
        ShowNotification(message, NotificationType.Success, durationMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows an error notification message.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="durationMs">Duration in milliseconds before auto-removal</param>
    public Task ShowErrorAsync(string message, int durationMs = 8000)
    {
        ShowNotification(message, NotificationType.Error, durationMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a warning notification message.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="durationMs">Duration in milliseconds before auto-removal</param>
    public Task ShowWarningAsync(string message, int durationMs = 6000)
    {
        ShowNotification(message, NotificationType.Warning, durationMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows an info notification message.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="durationMs">Duration in milliseconds before auto-removal</param>
    public Task ShowInfoAsync(string message, int durationMs = 5000)
    {
        ShowNotification(message, NotificationType.Info, durationMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a notification with the specified message, type, and duration.
    /// Thread-safe implementation to prevent race conditions.
    /// </summary>
    /// <param name="message">The notification message</param>
    /// <param name="type">The type of notification</param>
    /// <param name="durationMs">Duration before auto-removal in milliseconds</param>
    private void ShowNotification(string message, NotificationType type, int durationMs)
    {
        var notification = new NotificationMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = message ?? string.Empty,
            Type = type,
            Timestamp = DateTime.Now
        };

        lock (_lockObject)
        {
            _notifications.Add(notification);
        }

        NotificationAdded?.Invoke(notification);

        // Auto-remove after duration with proper error handling
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(durationMs);
                RemoveNotification(notification.Id);
            }
            catch (TaskCanceledException)
            {
                // Expected when application is shutting down
            }
            catch (Exception)
            {
                // Log error if logging is available, otherwise silently handle
                RemoveNotification(notification.Id);
            }
        });
    }

    /// <summary>
    /// Removes a notification by its ID.
    /// Thread-safe implementation.
    /// </summary>
    /// <param name="id">The notification ID to remove</param>
    public void RemoveNotification(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        bool wasRemoved;
        lock (_lockObject)
        {
            NotificationMessage? notification = _notifications.FirstOrDefault(n => n.Id == id);
            wasRemoved = notification != null && _notifications.Remove(notification);
        }

        if (wasRemoved)
        {
            NotificationRemoved?.Invoke(id);
        }
    }

    /// <summary>
    /// Gets a thread-safe copy of all current notifications.
    /// </summary>
    /// <returns>List of current notification messages</returns>
    public List<NotificationMessage> GetNotifications()
    {
        lock (_lockObject)
        {
            return _notifications.ToList();
        }
    }
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
