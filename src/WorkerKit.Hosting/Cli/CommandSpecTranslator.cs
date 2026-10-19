using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using WorkerKit.Abstractions.Commands;

namespace WorkerKit.Hosting.Cli;

/// <summary>
/// Builds a System.CommandLine command from a <see cref="CommandSpec"/> and remembers which
/// symbol belongs to each spec item. Parsing uses a closed set of delegates, not reflection.
/// </summary>
internal sealed class CommandSpecTranslator : CommandSymbolBinder
{
    private readonly Command _command;
    private readonly Dictionary<CommandParameter, Symbol> _symbols = new();
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    private CommandSpecTranslator(Command command)
    {
        _command = command;
    }

    public static Command Translate(CommandSpec spec, out IReadOnlyDictionary<CommandParameter, Symbol> symbols)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var command = new Command(spec.Name, spec.Description);
        foreach (var alias in spec.Aliases)
        {
            if (string.IsNullOrWhiteSpace(alias) || alias.StartsWith('-'))
            {
                throw new InvalidOperationException(
                    $"Command alias '{alias}' on {spec.Name} must be a single token and cannot start with '-'.");
            }

            command.Aliases.Add(alias);
        }

        var translator = new CommandSpecTranslator(command);
        translator.Reserve(spec.Name, spec.Aliases);
        foreach (var parameter in spec.Parameters)
        {
            parameter.Bind(translator);
        }

        symbols = translator._symbols;
        return command;
    }

    public override void BindOption<T>(CommandOption<T> option)
    {
        Reserve(option.Name, option.Aliases);
        var symbol = CreateOption(option);
        _command.Add(symbol);
        _symbols.Add(option, symbol);
    }

    public override void BindArgument<T>(CommandArgument<T> argument)
    {
        Reserve(argument.Name, []);
        var symbol = CreateArgument(argument);
        _command.Add(symbol);
        _symbols.Add(argument, symbol);
    }

    private void Reserve(string name, IReadOnlyList<string> aliases)
    {
        if (!_names.Add(name))
        {
            throw new InvalidOperationException($"Command '{_command.Name}' already defines '{name}'.");
        }

        foreach (var alias in aliases)
        {
            if (!_names.Add(alias))
            {
                throw new InvalidOperationException($"Command '{_command.Name}' already defines '{alias}'.");
            }
        }
    }

    private Option<T> CreateOption<T>(CommandOption<T> option)
    {
        if (option.ReadTokens is { } read)
        {
            return BuildOption(option, result => Read(result, read));
        }

        Option built = option switch
        {
            CommandOption<bool> value => BuildOption(value, parser: null),
            CommandOption<string> value => BuildOption(value, result => One<string>(result, value.Name, Text)),
            CommandOption<int> value => BuildOption(value, result => One<int>(result, value.Name, Int32)),
            CommandOption<long> value => BuildOption(value, result => One<long>(result, value.Name, Int64)),
            CommandOption<double> value => BuildOption(value, result => One<double>(result, value.Name, Double)),
            CommandOption<decimal> value => BuildOption(value, result => One<decimal>(result, value.Name, Decimal)),
            CommandOption<TimeSpan> value => BuildOption(value, result => One<TimeSpan>(result, value.Name, Time)),
            CommandOption<DateOnly> value => BuildOption(value, result => One<DateOnly>(result, value.Name, Day)),
            CommandOption<DateTimeOffset> value => BuildOption(value, result => One<DateTimeOffset>(result, value.Name, Instant)),
            CommandOption<FileInfo> value => BuildOption(value, result => One<FileInfo>(result, value.Name, File)),
            CommandOption<DirectoryInfo> value => BuildOption(value, result => One<DirectoryInfo>(result, value.Name, Directory)),
            CommandOption<string[]> value => BuildOption(value, result => Many<string>(result, value.Name, Text)),
            CommandOption<bool[]> value => BuildOption(value, result => Many<bool>(result, value.Name, Bool)),
            CommandOption<int[]> value => BuildOption(value, result => Many<int>(result, value.Name, Int32)),
            CommandOption<long[]> value => BuildOption(value, result => Many<long>(result, value.Name, Int64)),
            CommandOption<double[]> value => BuildOption(value, result => Many<double>(result, value.Name, Double)),
            CommandOption<decimal[]> value => BuildOption(value, result => Many<decimal>(result, value.Name, Decimal)),
            CommandOption<TimeSpan[]> value => BuildOption(value, result => Many<TimeSpan>(result, value.Name, Time)),
            CommandOption<DateOnly[]> value => BuildOption(value, result => Many<DateOnly>(result, value.Name, Day)),
            CommandOption<DateTimeOffset[]> value => BuildOption(value, result => Many<DateTimeOffset>(result, value.Name, Instant)),
            CommandOption<FileInfo[]> value => BuildOption(value, result => Many<FileInfo>(result, value.Name, File)),
            CommandOption<DirectoryInfo[]> value => BuildOption(value, result => Many<DirectoryInfo>(result, value.Name, Directory)),
            _ => throw Unsupported("option", option.Name, typeof(T)),
        };
        return (Option<T>)built;
    }

    private Argument<T> CreateArgument<T>(CommandArgument<T> argument)
    {
        if (argument.ReadTokens is { } read)
        {
            return BuildArgument(argument, result => Read(result, read));
        }

        Argument built = argument switch
        {
            CommandArgument<string> value => BuildArgument(value, result => One<string>(result, value.Name, Text)),
            CommandArgument<bool> value => BuildArgument(value, result => One<bool>(result, value.Name, Bool)),
            CommandArgument<int> value => BuildArgument(value, result => One<int>(result, value.Name, Int32)),
            CommandArgument<long> value => BuildArgument(value, result => One<long>(result, value.Name, Int64)),
            CommandArgument<double> value => BuildArgument(value, result => One<double>(result, value.Name, Double)),
            CommandArgument<decimal> value => BuildArgument(value, result => One<decimal>(result, value.Name, Decimal)),
            CommandArgument<TimeSpan> value => BuildArgument(value, result => One<TimeSpan>(result, value.Name, Time)),
            CommandArgument<DateOnly> value => BuildArgument(value, result => One<DateOnly>(result, value.Name, Day)),
            CommandArgument<DateTimeOffset> value => BuildArgument(value, result => One<DateTimeOffset>(result, value.Name, Instant)),
            CommandArgument<FileInfo> value => BuildArgument(value, result => One<FileInfo>(result, value.Name, File)),
            CommandArgument<DirectoryInfo> value => BuildArgument(value, result => One<DirectoryInfo>(result, value.Name, Directory)),
            CommandArgument<string[]> value => BuildArgument(value, result => Many<string>(result, value.Name, Text)),
            CommandArgument<bool[]> value => BuildArgument(value, result => Many<bool>(result, value.Name, Bool)),
            CommandArgument<int[]> value => BuildArgument(value, result => Many<int>(result, value.Name, Int32)),
            CommandArgument<long[]> value => BuildArgument(value, result => Many<long>(result, value.Name, Int64)),
            CommandArgument<double[]> value => BuildArgument(value, result => Many<double>(result, value.Name, Double)),
            CommandArgument<decimal[]> value => BuildArgument(value, result => Many<decimal>(result, value.Name, Decimal)),
            CommandArgument<TimeSpan[]> value => BuildArgument(value, result => Many<TimeSpan>(result, value.Name, Time)),
            CommandArgument<DateOnly[]> value => BuildArgument(value, result => Many<DateOnly>(result, value.Name, Day)),
            CommandArgument<DateTimeOffset[]> value => BuildArgument(value, result => Many<DateTimeOffset>(result, value.Name, Instant)),
            CommandArgument<FileInfo[]> value => BuildArgument(value, result => Many<FileInfo>(result, value.Name, File)),
            CommandArgument<DirectoryInfo[]> value => BuildArgument(value, result => Many<DirectoryInfo>(result, value.Name, Directory)),
            _ => throw Unsupported("argument", argument.Name, typeof(T)),
        };
        return (Argument<T>)built;
    }

    private static Option<T> BuildOption<T>(CommandOption<T> spec, Func<ArgumentResult, T>? parser)
    {
        var option = new Option<T>(spec.Name, spec.Aliases.ToArray())
        {
            Description = spec.Description,
            Required = spec.Required,
        };
        if (parser is not null)
        {
            option.CustomParser = parser;
        }

        ApplyDefault(spec, value => option.DefaultValueFactory = _ => value());
        return option;
    }

    private static Argument<T> BuildArgument<T>(CommandArgument<T> spec, Func<ArgumentResult, T> parser)
    {
        var argument = new Argument<T>(spec.Name)
        {
            Description = spec.Description,
            CustomParser = parser,
        };
        if (!spec.Required)
        {
            argument.Arity = new ArgumentArity(0, spec.ReadTokens is not null || typeof(T).IsArray ? 100_000 : 1);
        }
        else if (typeof(T).IsArray)
        {
            argument.Arity = new ArgumentArity(1, 100_000);
        }

        ApplyDefault(spec, value => argument.DefaultValueFactory = _ => value());
        return argument;
    }

    private static void ApplyDefault<T>(CommandValue<T> spec, Action<Func<T>> assign)
    {
        if (spec.DefaultFactory is not null)
        {
            var factory = spec.DefaultFactory;
            assign(() => factory());
            return;
        }

        if (spec.HasDefault)
        {
            var value = spec.DefaultValue;
            assign(() => value!);
        }
    }

    private static T Read<T>(ArgumentResult result, Func<IReadOnlyList<string>, T> read)
    {
        try
        {
            return read(Values(result));
        }
        catch (FormatException exception)
        {
            result.AddError(exception.Message);
            return default!;
        }
    }

    private static T One<T>(ArgumentResult result, string name, TryParse<T> parse)
    {
        if (result.Tokens.Count == 0)
        {
            result.AddError($"Missing value for {name}.");
            return default!;
        }

        var text = result.Tokens[0].Value;
        if (!parse(text, out var value, out var error))
        {
            result.AddError(error ?? $"Invalid value '{text}' for {name}.");
            return default!;
        }

        return value;
    }

    private static T[] Many<T>(ArgumentResult result, string name, TryParse<T> parse)
    {
        var values = new T[result.Tokens.Count];
        for (var index = 0; index < result.Tokens.Count; index++)
        {
            var text = result.Tokens[index].Value;
            if (!parse(text, out var value, out var error))
            {
                result.AddError(error ?? $"Invalid value '{text}' for {name}.");
                return [];
            }

            values[index] = value;
        }

        return values;
    }

    private static string[] Values(ArgumentResult result)
    {
        var values = new string[result.Tokens.Count];
        for (var index = 0; index < result.Tokens.Count; index++)
        {
            values[index] = result.Tokens[index].Value;
        }

        return values;
    }

    private NotSupportedException Unsupported(string kind, string name, Type type)
    {
        var typeName = type.FullName ?? type.Name;
        if (type.IsEnum)
        {
            var derived = kind == "option" ? "EnumCommandOption" : "EnumCommandArgument";
            return new NotSupportedException(
                $"Command '{_command.Name}' {kind} '{name}' of type {typeName} is not supported. Use {derived}<{type.Name}>.");
        }

        if (type.IsArray)
        {
            var derived = kind == "option" ? "EnumArrayCommandOption" : "EnumArrayCommandArgument";
            return new NotSupportedException(
                $"Command '{_command.Name}' {kind} '{name}' of type {typeName} is not supported. " +
                $"Enum arrays use {derived}<TEnum>. Supported arrays are string, bool, int, long, double, decimal, TimeSpan, DateOnly, DateTimeOffset, FileInfo, and DirectoryInfo.");
        }

        return new NotSupportedException(
            $"Command '{_command.Name}' {kind} '{name}' of type {typeName} is not supported. " +
            "Supported types are string, bool, int, long, double, decimal, TimeSpan, DateOnly, DateTimeOffset, FileInfo, DirectoryInfo, and arrays of those. " +
            "Enums use EnumCommandOption<TEnum> or EnumCommandArgument<TEnum>.");
    }

    private delegate bool TryParse<T>(string text, [MaybeNullWhen(false)] out T value, out string? error);

    private static bool Text(string text, out string value, out string? error)
    {
        value = text;
        error = null;
        return true;
    }

    private static bool Bool(string text, out bool value, out string? error)
    {
        if (bool.TryParse(text, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not true or false.";
        return false;
    }

    private static bool Int32(string text, out int value, out string? error)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not an integer.";
        return false;
    }

    private static bool Int64(string text, out long value, out string? error)
    {
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not an integer.";
        return false;
    }

    private static bool Double(string text, out double value, out string? error)
    {
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not a number.";
        return false;
    }

    private static bool Decimal(string text, out decimal value, out string? error)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not a decimal number.";
        return false;
    }

    private static bool Time(string text, out TimeSpan value, out string? error)
    {
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not a time span.";
        return false;
    }

    private static bool Day(string text, out DateOnly value, out string? error)
    {
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not a date.";
        return false;
    }

    private static bool Instant(string text, out DateTimeOffset value, out string? error)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
        {
            error = null;
            return true;
        }

        error = $"'{text}' is not a date and time.";
        return false;
    }

    private static bool File(string text, [MaybeNullWhen(false)] out FileInfo value, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            error = "A file path is required.";
            return false;
        }

        value = new FileInfo(text);
        error = null;
        return true;
    }

    private static bool Directory(string text, [MaybeNullWhen(false)] out DirectoryInfo value, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            error = "A directory path is required.";
            return false;
        }

        value = new DirectoryInfo(text);
        error = null;
        return true;
    }
}
