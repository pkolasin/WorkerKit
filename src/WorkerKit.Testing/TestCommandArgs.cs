using System.Diagnostics.CodeAnalysis;
using WorkerKit.Abstractions.Commands;

namespace WorkerKit.Testing;

/// <summary>In-memory <see cref="ICommandArgs"/> for a command exercised without the parser.</summary>
public sealed class TestCommandArgs : ICommandArgs
{
    private readonly Dictionary<CommandParameter, object?> _values = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CommandParameter> _set = new(ReferenceEqualityComparer.Instance);

    public static TestCommandArgs With<T>(CommandOption<T> option, T value) => new TestCommandArgs().Set(option, value);

    public static TestCommandArgs With<T>(CommandArgument<T> argument, T value) => new TestCommandArgs().Set(argument, value);

    public TestCommandArgs Set<T>(CommandOption<T> option, T value)
    {
        ArgumentNullException.ThrowIfNull(option);
        _values[option] = value;
        _set.Add(option);
        return this;
    }

    public TestCommandArgs Set<T>(CommandArgument<T> argument, T value)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _values[argument] = value;
        return this;
    }

    [return: MaybeNull]
    public T Get<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (_values.TryGetValue(option, out var value))
        {
            return value is null ? default : (T)value;
        }

        return option.TryGetDefault(out var fallback) ? fallback : default;
    }

    [return: MaybeNull]
    public T Get<T>(CommandArgument<T> argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (_values.TryGetValue(argument, out var value))
        {
            return value is null ? default : (T)value;
        }

        return argument.TryGetDefault(out var fallback) ? fallback : default;
    }

    public bool IsSet<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return _set.Contains(option);
    }
}
