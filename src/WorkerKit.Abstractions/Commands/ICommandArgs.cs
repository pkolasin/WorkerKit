using System.Diagnostics.CodeAnalysis;

namespace WorkerKit.Abstractions.Commands;

/// <summary>Parsed options and arguments for one invocation of <see cref="IWorkerCommand"/>.</summary>
public interface ICommandArgs
{
    /// <summary>The parsed value, or the option default when the option was omitted.</summary>
    [return: MaybeNull]
    T Get<T>(CommandOption<T> option);

    /// <summary>The parsed value, or the argument default when the argument was omitted.</summary>
    [return: MaybeNull]
    T Get<T>(CommandArgument<T> argument);

    /// <summary>True when this invocation included the option, including a flag with no value.</summary>
    bool IsSet<T>(CommandOption<T> option);
}
