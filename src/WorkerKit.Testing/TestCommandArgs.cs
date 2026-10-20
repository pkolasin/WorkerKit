using System.Diagnostics.CodeAnalysis;
using WorkerKit.Abstractions.Commands;

namespace WorkerKit.Testing;

/// <summary>In-memory <see cref="ICommandArgs"/> for a command exercised without the parser.</summary>
/// <remarks>Options and arguments are stored and read by reference, not by name.</remarks>
public sealed class TestCommandArgs : ICommandArgs
{
    private readonly Dictionary<CommandParameter, object?> _values = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CommandParameter> _set = new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates args with one option value stored and marked set.</summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option instance to store.</param>
    /// <param name="value">Value stored for <paramref name="option"/>.</param>
    /// <returns>A new <see cref="TestCommandArgs"/> from <see cref="Set{T}(CommandOption{T}, T)"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public static TestCommandArgs With<T>(CommandOption<T> option, T value) => new TestCommandArgs().Set(option, value);

    /// <summary>Creates args with one argument value stored.</summary>
    /// <typeparam name="T">Argument value type.</typeparam>
    /// <param name="argument">Argument instance to store.</param>
    /// <param name="value">Value stored for <paramref name="argument"/>.</param>
    /// <returns>A new <see cref="TestCommandArgs"/> from <see cref="Set{T}(CommandArgument{T}, T)"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
    public static TestCommandArgs With<T>(CommandArgument<T> argument, T value) => new TestCommandArgs().Set(argument, value);

    /// <summary>Stores <paramref name="value"/> and marks <paramref name="option"/> as set.</summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option instance to store.</param>
    /// <param name="value">Value stored for <paramref name="option"/>. A <see langword="null"/> value is stored.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public TestCommandArgs Set<T>(CommandOption<T> option, T value)
    {
        ArgumentNullException.ThrowIfNull(option);
        _values[option] = value;
        _set.Add(option);
        return this;
    }

    /// <summary>Stores <paramref name="value"/> for <paramref name="argument"/>.</summary>
    /// <typeparam name="T">Argument value type.</typeparam>
    /// <param name="argument">Argument instance to store.</param>
    /// <param name="value">Value stored for <paramref name="argument"/>. A <see langword="null"/> value is stored.</param>
    /// <returns>This instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
    public TestCommandArgs Set<T>(CommandArgument<T> argument, T value)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _values[argument] = value;
        return this;
    }

    /// <summary>
    /// Stored option value. A stored <see langword="null"/> returns <see langword="default"/> and does not use the option default.
    /// An omitted option returns <see cref="CommandValue{T}.TryGetDefault"/>, or <see langword="default"/> when that returns <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option instance to read.</param>
    /// <returns>The stored value, the option default, or <see langword="default"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Stored argument value. A stored <see langword="null"/> returns <see langword="default"/> and does not use the argument default.
    /// An omitted argument returns <see cref="CommandValue{T}.TryGetDefault"/>, or <see langword="default"/> when that returns <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T">Argument value type.</typeparam>
    /// <param name="argument">Argument instance to read.</param>
    /// <returns>The stored value, the argument default, or <see langword="default"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
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

    /// <summary><see langword="true"/> when <see cref="Set{T}(CommandOption{T}, T)"/> stored <paramref name="option"/>. A default alone is not set.</summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option instance to test.</param>
    /// <returns><see langword="true"/> when <paramref name="option"/> is in the set of stored options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public bool IsSet<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return _set.Contains(option);
    }
}
