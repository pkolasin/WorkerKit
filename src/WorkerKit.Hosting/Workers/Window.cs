namespace WorkerKit.Hosting.Workers;

/// <summary>
/// When the next hosted iteration of a <see cref="PeriodicWorker"/> may start.
/// <see cref="DailyAt"/> is a local time of day. The hosted loop waits until <see cref="NextOccurrence"/>.
/// <see cref="PeriodicWorker.RunIterationAsync"/> does not wait.
/// </summary>
public abstract class Window
{
    /// <summary>Next instant at or after <paramref name="now"/> when the worker may run.</summary>
    /// <param name="now">Instant compared with the schedule. It is usually <see cref="TimeProvider.GetUtcNow"/>.</param>
    /// <returns>An instant greater than or equal to <paramref name="now"/>.</returns>
    public abstract DateTimeOffset NextOccurrence(DateTimeOffset now);

    /// <summary>
    /// Once a day at <paramref name="time"/> in <paramref name="timeZone"/>.
    /// When the current instant is exactly that local time, the next occurrence is the current instant.
    /// A local time that does not exist, such as a spring-forward gap, moves forward to the first valid minute.
    /// A local time that exists twice uses the earlier instant, then the later one after that earlier instant has passed.
    /// </summary>
    /// <param name="time">Local time of day.</param>
    /// <param name="timeZone">Time zone that interprets <paramref name="time"/>.</param>
    /// <returns>The daily window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZone"/> is null.</exception>
    public static Window DailyAt(TimeOnly time, TimeZoneInfo timeZone) => new DailyWindow(time, timeZone);

    private sealed class DailyWindow : Window
    {
        private readonly TimeOnly _time;
        private readonly TimeZoneInfo _timeZone;

        public DailyWindow(TimeOnly time, TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);
            _time = time;
            _timeZone = timeZone;
        }

        public override DateTimeOffset NextOccurrence(DateTimeOffset now)
        {
            var localNow = TimeZoneInfo.ConvertTime(now, _timeZone);
            var date = DateOnly.FromDateTime(localNow.DateTime);
            for (var day = 0; day < 4; day++)
            {
                if (TryResolve(date.AddDays(day), now, out var instant))
                {
                    return instant;
                }
            }

            throw new InvalidOperationException(
                $"Could not find the next {_time:HH:mm} occurrence in time zone '{_timeZone.Id}'.");
        }

        private bool TryResolve(DateOnly date, DateTimeOffset notBefore, out DateTimeOffset instant)
        {
            var local = date.ToDateTime(_time);
            if (_timeZone.IsInvalidTime(local))
            {
                local = FirstValidLocal(local, _timeZone);
            }

            if (_timeZone.IsAmbiguousTime(local))
            {
                foreach (var offset in _timeZone.GetAmbiguousTimeOffsets(local).OrderByDescending(value => value))
                {
                    var candidate = new DateTimeOffset(local, offset);
                    if (candidate >= notBefore)
                    {
                        instant = candidate;
                        return true;
                    }
                }

                instant = default;
                return false;
            }

            var single = new DateTimeOffset(local, _timeZone.GetUtcOffset(local));
            if (single >= notBefore)
            {
                instant = single;
                return true;
            }

            instant = default;
            return false;
        }

        private static DateTime FirstValidLocal(DateTime local, TimeZoneInfo zone)
        {
            var probe = local;
            for (var minute = 0; minute < 180; minute++)
            {
                probe = probe.AddMinutes(1);
                if (!zone.IsInvalidTime(probe))
                {
                    return probe;
                }
            }

            throw new InvalidOperationException(
                $"Local time {local:yyyy-MM-dd HH:mm} does not exist in time zone '{zone.Id}'.");
        }
    }
}
