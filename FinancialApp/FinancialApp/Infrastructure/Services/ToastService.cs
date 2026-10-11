using System;
using MudBlazor;

namespace FinancialApp.Infrastructure.Services
{
    /// <summary>
    /// Toast service implementation backed by MudBlazor's <see cref="ISnackbar"/>.
    /// Every call maps the requested <see cref="IToastService.ToastLevel"/> onto the equivalent
    /// MudBlazor <see cref="Severity"/> so information, success, warning and error messages
    /// are rendered as snackbars by the <see cref="MudSnackbarProvider"/> in the main layout.
    /// </summary>
    public class ToastService : IToastService
    {
        /// <summary>
        /// Default time, in milliseconds, the snackbar stays visible.
        /// </summary>
        private const int DefaultDurationMs = 5000;

        private readonly ISnackbar _snackbar;

        public ToastService(ISnackbar snackbar)
        {
            _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
        }

        /// <inheritdoc />
        public void Show(string message, string? title = null, int durationMs = DefaultDurationMs, IToastService.ToastLevel level = IToastService.ToastLevel.Info)
        {
            var severity = level switch
            {
                IToastService.ToastLevel.Success => Severity.Success,
                IToastService.ToastLevel.Warning => Severity.Warning,
                IToastService.ToastLevel.Error => Severity.Error,
                _ => Severity.Info
            };

            // MudBlazor snackbars show a single message, so surface the optional title as a prefix.
            var text = string.IsNullOrWhiteSpace(title)
                ? message ?? string.Empty
                : $"{title}: {message}";

            _snackbar.Add(text, severity, options =>
            {
                options.VisibleStateDuration = Math.Max(0, durationMs);
                options.ShowCloseIcon = true;
                options.CloseAfterNavigation = true;
            });
        }
    }
}
