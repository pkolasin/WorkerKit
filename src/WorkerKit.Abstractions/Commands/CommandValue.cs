using System.Diagnostics.CodeAnalysis;

namespace WorkerKit.Abstractions.Commands;

/// <summary>Shared default-value and parse-delegate state for options and arguments.</summary>
public abstract class CommandValue<T> : CommandParameter
{
    private T? _defaultValue;
    private bool _hasDefault;

    protected CommandValue(string name, string description)
        : base(name, description)
    {
    }

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
    public Func<T>? DefaultFactory { get; init; }

    public bool HasDefault => _hasDefault;

    internal Func<IReadOnlyList<string>, T>? ReadTokens { get; private set; }

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
