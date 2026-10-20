using System.Diagnostics.CodeAnalysis;

namespace WorkerKit.Abstractions.Commands;

/// <summary>Shared default-value and parse-delegate state for options and arguments.</summary>
/// <typeparam name="T">Value type stored by the option or argument.</typeparam>
public abstract class CommandValue<T> : CommandParameter
{
    private T? _defaultValue;
    private bool _hasDefault;

    /// <summary>Creates a value with a name and a description.</summary>
    /// <param name="name">Value name passed to <see cref="CommandParameter(string, string)"/>.</param>
    /// <param name="description">Value description passed to <see cref="CommandParameter(string, string)"/>.</param>
    protected CommandValue(string name, string description)
        : base(name, description)
    {
    }

    /// <summary>Value used when the option or argument is omitted.</summary>
    /// <remarks>
    /// The init accessor sets <see cref="HasDefault"/> to <see langword="true"/>, including when the assigned value is <see langword="null"/>.
    /// Setting this and <see cref="DefaultFactory"/> together is rejected when the command is bound.
    /// </remarks>
    public T? DefaultValue
    {
        get => _defaultValue;
        init
        {
            _hasDefault = true;
            _defaultValue = value;
        }
    }

    /// <summary>Called when the value was omitted. Cannot be combined with <see cref="DefaultValue"/>.</summary>
    /// <returns>The factory invoked when the value was omitted, or <see langword="null"/> when none was set.</returns>
    /// <remarks>The property has no initializer, so it is <see langword="null"/> until set.</remarks>
    public Func<T>? DefaultFactory { get; init; }

    /// <summary><see langword="true"/> when <see cref="DefaultValue"/> was assigned.</summary>
    /// <remarks>
    /// <see cref="DefaultFactory"/> does not change this property.
    /// <see cref="TryGetDefault"/> still returns <see langword="true"/> when only the factory is set.
    /// </remarks>
    public bool HasDefault => _hasDefault;

    internal Func<IReadOnlyList<string>, T>? ReadTokens { get; private set; }

    /// <summary>Gets the value used when the option or argument is omitted.</summary>
    /// <param name="value">
    /// The <see cref="DefaultFactory"/> result when that factory is set;
    /// otherwise <see cref="DefaultValue"/> when <see cref="HasDefault"/> is <see langword="true"/>;
    /// otherwise the default of <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <see cref="DefaultFactory"/> is set or <see cref="HasDefault"/> is <see langword="true"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetDefault([MaybeNull] out T value)
    {
        if (DefaultFactory is not null)
        {
            value = DefaultFactory();
            return true;
        }

        if (_hasDefault)
        {
            value = _defaultValue!;
            return true;
        }

        value = default;
        return false;
    }

    internal void UseReader(Func<IReadOnlyList<string>, T> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ReadTokens = reader;
    }

    internal void EnsureDefaults()
    {
        if (_hasDefault && DefaultFactory is not null)
        {
            throw new InvalidOperationException($"{Name} cannot set both DefaultValue and DefaultFactory.");
        }
    }
}
