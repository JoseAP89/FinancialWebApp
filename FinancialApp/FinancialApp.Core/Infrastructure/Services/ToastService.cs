using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FinancialApp.Infrastructure.Services
{
    /// <summary>
    /// Basic in-process toast service implementation.
    /// Components should subscribe to OnShow and OnDismiss to render and remove toasts.
    /// </summary>
    public class ToastService : IToastService
    {
        public event Action<IToastService.ToastMessage>? OnShow;
        public event Action<Guid>? OnDismiss;

        // Keep track of active toasts so components that subscribe later can synchronize
        // their UI with existing toasts.
        private readonly ConcurrentDictionary<Guid, IToastService.ToastMessage> _active = new();
        private readonly ConcurrentDictionary<Guid, Timer> _timers = new();

        /// <summary>
        /// Show a toast message and schedule automatic dismissal after durationMs.
        /// </summary>
        public Guid Show(string message, string? title, int durationMs = 5000, IToastService.ToastLevel level = IToastService.ToastLevel.Info)
        {
            var msg = new IToastService.ToastMessage
            {
                Id = Guid.NewGuid(),
                Message = message ?? string.Empty,
                Level = level,
                DurationMs = Math.Max(0, durationMs),
                Title = title
            };

            // Add to active set before notifying subscribers so they can query state if needed
            _active[msg.Id] = msg;

            // Fire event to notify UI
            try { OnShow?.Invoke(msg); } catch { }

            // Schedule automatic dismissal if duration provided
            if (msg.DurationMs > 0)
            {
                // Use ThreadPool timer so we don't capture SynchronizationContext here
                var timer = new Timer(state =>
                {
                    try { Dismiss(msg.Id); } catch { }
                }, null, msg.DurationMs, Timeout.Infinite);

                // Keep a reference so we can dispose the timer when the toast is dismissed early
                _timers[msg.Id] = timer;
            }

            return msg.Id;
        }

        /// <summary>
        /// Convenience helpers
        /// </summary>
        public Guid ShowSuccess(string message, string? title = "Action Completed", int durationMs = 5000) => Show(message,  title, durationMs, IToastService.ToastLevel.Success);
        public Guid ShowError(string message, string? title = "Action Failed", int durationMs = 5000) => Show(message,  title, durationMs, IToastService.ToastLevel.Error);
        public Guid ShowWarning(string message, string? title = "Action Warning", int durationMs = 5000) => Show(message,  title, durationMs, IToastService.ToastLevel.Warning);
        public Guid ShowInfo(string message, string? title = "Action Info", int durationMs = 5000) => Show(message,  title, durationMs, IToastService.ToastLevel.Info);

        /// <summary>
        /// Trigger dismissal event for a specific toast id.
        /// </summary>
        public void Dismiss(Guid id)
        {
            // Remove from active list first
            _active.TryRemove(id, out _);

            // Dispose any timer
            if (_timers.TryRemove(id, out var t))
            {
                try { t.Dispose(); } catch { }
            }

            try { OnDismiss?.Invoke(id); } catch { }
        }

        public IEnumerable<IToastService.ToastMessage> GetActiveToasts()
        {
            // Return a snapshot
            return _active.Values.ToList();
        }
    }
}
