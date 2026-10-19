using System.Diagnostics.CodeAnalysis;
using WorkerKit.Abstractions.Commands;

namespace WorkerKit.Hosting.Cli;

internal sealed class ParsedCommandArgs : ICommandArgs
{
    private readonly ParseResult _parse;
    private readonly IReadOnlyDictionary<CommandParameter, Symbol> _symbols;

    public ParsedCommandArgs(ParseResult parse, IReadOnlyDictionary<CommandParameter, Symbol> symbols)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(symbols);
        _parse = parse;
        _symbols = symbols;
    }

    [return: MaybeNull]
    public T Get<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return _parse.GetValue((Option<T>)Require(option));
    }

    [return: MaybeNull]
    public T Get<T>(CommandArgument<T> argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return _parse.GetValue((Argument<T>)Require(argument));
    }

    public bool IsSet<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        var result = _parse.GetResult((Option)Require(option));
        return result is { Implicit: false };
    }

    private Symbol Require(CommandParameter parameter)
    {
        if (_symbols.TryGetValue(parameter, out var symbol))
        {
            return symbol;
        }

        throw new InvalidOperationException($"{parameter.Name} is not part of this command.");
    }
}
