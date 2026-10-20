namespace WorkerKit.Hosting.Workers;

/// <summary>
/// <c>TimeProvider.Delay(TimeSpan, CancellationToken)</c> exists on .NET 9 and later.
/// This keeps the same behavior on .NET 8: <see cref="TimeProvider.System"/> uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>,
/// and any other provider uses <see cref="TimeProvider.CreateTimer(TimerCallback, object?, TimeSpan, TimeSpan)"/>.
/// </summary>
internal static class TimeProviderDelay
{
    public static Task Delay(this TimeProvider time, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(time);
        if (delay < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (delay == TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        if (time == TimeProvider.System)
        {
            return Task.Delay(delay, cancellationToken);
        }

        var state = new DelayState(cancellationToken);
        state.Timer = time.CreateTimer(static state => ((DelayState)state!).Complete(), state, delay, Timeout.InfiniteTimeSpan);
        return state.Task;
    }

    private sealed class DelayState
    {
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenRegistration _registration;
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ITimer? _timer;

        public DelayState(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            if (cancellationToken.CanBeCanceled)
            {
                _registration = cancellationToken.UnsafeRegister(static state => ((DelayState)state!).Cancel(), this);
            }
        }

        public Task Task => _completed.Task;

        public ITimer Timer
        {
            set
            {
                _timer = value;
                if (_completed.Task.IsCompleted)
                {
                    _timer.Dispose();
                }
            }
        }

        public void Complete()
        {
            if (_completed.TrySetResult())
            {
                _registration.Dispose();
                _timer?.Dispose();
            }
        }

        public void Cancel()
        {
            if (_completed.TrySetCanceled(_cancellationToken))
            {
                _timer?.Dispose();
            }
        }
    }
}
