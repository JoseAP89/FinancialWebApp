using System;

namespace FinancialApp.Infrastructure.Services
{
    /// <summary>
    /// Simple toast service contract used by UI components to show brief messages to the user.
    /// UI toast component should subscribe to the OnShow/OnDismiss events to render toasts.
    /// </summary>
    public interface IToastService
    {
        /// <summary>
        /// Levels used to style the toast (info, success, warning, error).
        /// </summary>
        public enum ToastLevel
        {
            Info,
            Success,
            Warning,
            Error
        }

        /// <summary>
        /// Raised when a new toast should be displayed. Handler receives the ToastMessage payload.
        /// </summary>
        event Action<ToastMessage>? OnShow;

        /// <summary>
        /// Raised when a toast should be dismissed/removed. Handler receives the toast id.
        /// </summary>
        event Action<Guid>? OnDismiss;

        /// <summary>
        /// Show a toast message.
        /// Returns the id for the created toast.
        /// </summary>
        Guid Show(string message, string? title = null, int durationMs = 5000, ToastLevel level = ToastLevel.Info);

        /// <summary>
        /// Convenience helpers for common levels.
        /// </summary>
        Guid ShowSuccess(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Success);
        Guid ShowError(string message, string? title = null, int durationMs = 5000) => Show(message, title,durationMs, ToastLevel.Error);
        Guid ShowWarning(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Warning);
        Guid ShowInfo(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Info);

        /// <summary>
        /// Request dismissal of an active toast.
        /// </summary>
        void Dismiss(Guid id);

        /// <summary>
        /// Get currently active toasts. Useful for UI components that subscribe after
        /// a toast was shown so they can synchronize initial state.
        /// </summary>
        System.Collections.Generic.IEnumerable<ToastMessage> GetActiveToasts();

        /// <summary>
        /// Lightweight descriptor passed to UI subscribers.
        /// </summary>
        public sealed class ToastMessage
        {
            public Guid Id { get; init; }
            public string Message { get; init; } = string.Empty;
            public ToastLevel Level { get; init; }
            public int DurationMs { get; init; }
            public string? Title { get; init; }
        }
    }
}
