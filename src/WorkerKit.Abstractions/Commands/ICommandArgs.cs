using System.Diagnostics.CodeAnalysis;

namespace WorkerKit.Abstractions.Commands;

/// <summary>Parsed options and arguments for one invocation of <see cref="IWorkerCommand"/>.</summary>
public interface ICommandArgs
{
    /// <summary>The parsed value, or the option default when the option was omitted.</summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option declared on the command.</param>
    /// <returns>The parsed value, or the option default when <paramref name="option"/> was omitted.</returns>
    [return: MaybeNull]
    T Get<T>(CommandOption<T> option);

    /// <summary>The parsed value, or the argument default when the argument was omitted.</summary>
    /// <typeparam name="T">Argument value type.</typeparam>
    /// <param name="argument">Argument declared on the command.</param>
    /// <returns>The parsed value, or the argument default when <paramref name="argument"/> was omitted.</returns>
    [return: MaybeNull]
    T Get<T>(CommandArgument<T> argument);

    /// <summary>True when this invocation included the option, including a flag with no value.</summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option to test.</param>
    /// <returns><see langword="true"/> when this invocation included <paramref name="option"/>, including a flag with no value; otherwise <see langword="false"/>.</returns>
    bool IsSet<T>(CommandOption<T> option);
}
