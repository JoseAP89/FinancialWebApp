namespace FinancialApp.Infrastructure.Services
{
    /// <summary>
    /// Simple toast service contract used by UI components to show brief messages to the user.
    /// The concrete implementation is responsible for rendering the messages, for example
    /// by turning them into MudBlazor snackbars.
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
        /// Show a toast message.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="title">Optional title shown together with the message.</param>
        /// <param name="durationMs">How long the toast stays visible, in milliseconds.</param>
        /// <param name="level">The severity used to style the toast.</param>
        void Show(string message, string? title = null, int durationMs = 5000, ToastLevel level = ToastLevel.Info);

        /// <summary>
        /// Convenience helpers for common levels.
        /// </summary>
        void ShowSuccess(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Success);
        void ShowError(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Error);
        void ShowWarning(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Warning);
        void ShowInfo(string message, string? title = null, int durationMs = 5000) => Show(message, title, durationMs, ToastLevel.Info);
    }
}
