using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using WorkerKit.Abstractions.Commands;
using WorkerKit.Hosting.Cli;
using WorkerKit.Testing;

namespace WorkerKit.Hosting.Tests;

public sealed class CommandSpecTests
{
    [Fact]
    public void Spec_translates_types_required_defaults_aliases_and_arguments()
    {
        var command = BuildTyped();

        Assert.Equal("Typed command.", command.Description);
        Assert.Contains("t", command.Aliases);

        var count = Option<int>(command, "--count");
        Assert.True(count.Required);
        Assert.False(count.HasDefaultValue);
        Assert.NotNull(count.CustomParser);

        var force = Option<bool>(command, "--force");
        Assert.False(force.Required);
        Assert.Equal(0, force.Arity.MinimumNumberOfValues);
        Assert.Equal(1, force.Arity.MaximumNumberOfValues);
        Assert.Null(force.CustomParser);

        var label = Option<string>(command, "--label");
        Assert.Contains("-l", label.Aliases);
        Assert.True(label.HasDefaultValue);
        Assert.Equal("ada", label.GetDefaultValue());

        var delay = Option<TimeSpan>(command, "--delay");
        Assert.Equal(TimeSpan.FromSeconds(5), delay.GetDefaultValue());

        Assert.Equal(typeof(DateOnly), Option<DateOnly>(command, "--day").ValueType);
        Assert.Equal(typeof(DateTimeOffset), Option<DateTimeOffset>(command, "--at").ValueType);
        Assert.Equal(typeof(double), Option<double>(command, "--ratio").ValueType);
        Assert.Equal(typeof(decimal), Option<decimal>(command, "--amount").ValueType);
        Assert.Equal(typeof(long), Option<long>(command, "--size").ValueType);
        Assert.Equal(typeof(DirectoryInfo), Option<DirectoryInfo>(command, "--folder").ValueType);
        Assert.Equal(typeof(string[]), Option<string[]>(command, "--tag").ValueType);
        Assert.Equal(typeof(SampleColor), Option<SampleColor>(command, "--color").ValueType);

        var file = Assert.Single(command.Arguments.OfType<Argument<FileInfo>>());
        Assert.Equal("file", file.Name);
        Assert.Equal(1, file.Arity.MinimumNumberOfValues);
        Assert.Equal("Input file.", file.Description);

        var words = Build("repeat");
        var wordArgument = Assert.Single(words.Arguments.OfType<Argument<string[]>>());
        Assert.Equal(0, wordArgument.Arity.MinimumNumberOfValues);
        Assert.True(wordArgument.Arity.MaximumNumberOfValues > 1);
    }

    [Fact]
    public void Parse_reads_each_supported_value()
    {
        var args = new[]
        {
            "typed", "--count", "3", "--force", "-l", "bee",
            "--day", "2026-10-19", "--at", "2026-10-19T12:00:00Z",
            "--ratio", "1.5", "--amount", "2.25", "--size", "9",
            "--folder", "/tmp", "--tag", "a", "--tag", "b", "--color", "green",
            "notes.txt",
        };
        var parse = WorkerCommands.Create(args).Add<TypedCommand>().Parse(args);
        var command = parse.CommandResult.Command;

        Assert.Empty(parse.Errors);
        Assert.Equal("typed", command.Name);
        Assert.Equal(3, parse.GetValue(Option<int>(command, "--count")));
        Assert.True(parse.GetValue(Option<bool>(command, "--force")));
        Assert.Equal("bee", parse.GetValue(Option<string>(command, "--label")));
        Assert.Equal(TimeSpan.FromSeconds(5), parse.GetValue(Option<TimeSpan>(command, "--delay")));
        Assert.Equal(new DateOnly(2026, 10, 19), parse.GetValue(Option<DateOnly>(command, "--day")));
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), parse.GetValue(Option<DateTimeOffset>(command, "--at")));
        Assert.Equal(1.5, parse.GetValue(Option<double>(command, "--ratio")));
        Assert.Equal(2.25m, parse.GetValue(Option<decimal>(command, "--amount")));
        Assert.Equal(9L, parse.GetValue(Option<long>(command, "--size")));
        Assert.Equal(new DirectoryInfo("/tmp").FullName, parse.GetValue(Option<DirectoryInfo>(command, "--folder"))?.FullName);
        var tags = parse.GetValue(Option<string[]>(command, "--tag"));
        Assert.Equal(new[] { "a", "b" }, tags);
        Assert.Equal(SampleColor.Green, parse.GetValue(Option<SampleColor>(command, "--color")));
        Assert.Equal("notes.txt", parse.GetValue(command.Arguments.OfType<Argument<FileInfo>>().Single())?.Name);

        var alias = new[] { "t", "--count", "1", "notes.txt" };
        var aliased = WorkerCommands.Create(alias).Add<TypedCommand>().Parse(alias);
        Assert.Empty(aliased.Errors);
        Assert.Equal("typed", aliased.CommandResult.Command.Name);
        Assert.Equal(1, aliased.GetValue(Option<int>(aliased.CommandResult.Command, "--count")));
        Assert.Equal("ada", aliased.GetValue(Option<string>(aliased.CommandResult.Command, "--label")));
    }

    [Fact]
    public void Missing_required_values_and_bad_tokens_fail_parse()
    {
        var missing = Parse(["typed"]);
        Assert.Contains(missing.Errors, error => error.Message.Contains("--count", StringComparison.Ordinal));

        var badNumber = Parse(["typed", "--count", "no", "notes.txt"]);
        Assert.Contains(badNumber.Errors, error => error.Message.Contains("no", StringComparison.Ordinal));

        var badColor = Parse(["typed", "--count", "1", "--color", "purple", "notes.txt"]);
        Assert.Contains(badColor.Errors, error => error.Message.Contains("purple", StringComparison.Ordinal));
    }

    [Fact]
    public void Help_lists_the_translated_command()
    {
        var previous = Environment.GetEnvironmentVariable(AppIdentity.Current.InstanceVariable);
        Environment.SetEnvironmentVariable(AppIdentity.Current.InstanceVariable, "Not-A-Valid-Name");
        try
        {
            var commands = WorkerCommands.Create([]);
            commands.Add<TypedCommand>();
            var output = new StringWriter();
            var code = commands.Parse(["help", "typed"]).Invoke(new InvocationConfiguration
            {
                Output = output,
                Error = new StringWriter(),
            });

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("Typed command.", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("--count", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("How many.", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppIdentity.Current.InstanceVariable, previous);
        }
    }

    [Fact]
    public void Unsupported_types_fail_when_the_command_is_registered()
    {
        _ = GuidCommand.Spec;
        _ = PlainEnumCommand.Spec;
        _ = PlainEnumArgumentCommand.Spec;
        _ = PlainEnumArrayCommand.Spec;

        var guid = Assert.Throws<NotSupportedException>(() => WorkerCommands.Create([]).Add<GuidCommand>());
        Assert.Contains("guid", guid.Message, StringComparison.Ordinal);
        Assert.Contains("--id", guid.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(Guid).FullName!, guid.Message, StringComparison.Ordinal);
        Assert.Contains("EnumCommandOption", guid.Message, StringComparison.Ordinal);

        var grouped = Assert.Throws<NotSupportedException>(() =>
            WorkerCommands.Create([]).AddGroup("registry", "Registry commands.", group => group.Add<GuidCommand>()));
        Assert.Contains("guid", grouped.Message, StringComparison.Ordinal);
        Assert.Contains("--id", grouped.Message, StringComparison.Ordinal);

        var enumOption = Assert.Throws<NotSupportedException>(() => WorkerCommands.Create([]).Add<PlainEnumCommand>());
        Assert.Contains("shade", enumOption.Message, StringComparison.Ordinal);
        Assert.Contains("--color", enumOption.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(SampleColor).FullName!, enumOption.Message, StringComparison.Ordinal);
        Assert.Contains("EnumCommandOption<SampleColor>", enumOption.Message, StringComparison.Ordinal);

        var enumArgument = Assert.Throws<NotSupportedException>(() => WorkerCommands.Create([]).Add<PlainEnumArgumentCommand>());
        Assert.Contains("paint", enumArgument.Message, StringComparison.Ordinal);
        Assert.Contains("color", enumArgument.Message, StringComparison.Ordinal);
        Assert.Contains("EnumCommandArgument<SampleColor>", enumArgument.Message, StringComparison.Ordinal);

        var enumArray = Assert.Throws<NotSupportedException>(() => WorkerCommands.Create([]).Add<PlainEnumArrayCommand>());
        Assert.Contains("shades", enumArray.Message, StringComparison.Ordinal);
        Assert.Contains("--color", enumArray.Message, StringComparison.Ordinal);
        Assert.Contains("EnumArrayCommandOption<TEnum>", enumArray.Message, StringComparison.Ordinal);

        var defaults = Assert.Throws<InvalidOperationException>(() => WorkerCommands.Create([]).Add<BothDefaultsCommand>());
        Assert.Contains("DefaultValue", defaults.Message, StringComparison.Ordinal);

        var duplicate = Assert.Throws<InvalidOperationException>(() => WorkerCommands.Create([]).Add<DuplicateOptionCommand>());
        Assert.Contains("--name", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_flags_enum_accepts_one_named_member()
    {
        var read = new[] { "flags", "--mode", "read" };
        var parsed = WorkerCommands.Create(read).Add<FlagsCommand>().Parse(read);
        Assert.Empty(parsed.Errors);
        Assert.Equal(SampleFlags.Read, parsed.GetValue(Option<SampleFlags>(parsed.CommandResult.Command, "--mode")));

        var combined = ParseFlags(["flags", "--mode", "Read, Write"]);
        Assert.Contains(combined.Errors, error => error.Message.Contains("Read, Write", StringComparison.Ordinal));

        var number = ParseFlags(["flags", "--mode", "3"]);
        Assert.Contains(number.Errors, error => error.Message.Contains("3", StringComparison.Ordinal));
    }

    [Fact]
    public void Enum_arguments_parse_one_named_member()
    {
        var args = new[] { "paint", "green" };
        var parsed = WorkerCommands.Create(args).Add<EnumArgumentCommand>().Parse(args);
        Assert.Empty(parsed.Errors);
        Assert.Equal(
            SampleColor.Green,
            parsed.GetValue(parsed.CommandResult.Command.Arguments.OfType<Argument<SampleColor>>().Single()));
    }

    [Fact]
    public async Task A_command_executes_with_a_constructor_dependency()
    {
        DiCommand.Reset();
        var app = WorkerApp.CreateBuilder<CommandSpecTests>(["di", "--ico", "27074358"]);
        app.Commands.Add<DiCommand>();
        app.ConfigureHost(builder => builder.Services.AddSingleton<ILookupClient>(new EchoClient()));

        var code = await app.RunAsync();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal("27074358", DiCommand.Seen);
    }

    [Fact]
    public async Task A_command_executes_in_isolation_with_test_args()
    {
        var args = TestCommandArgs.With(LookupCommand.Ico, "27074358");
        var code = await new LookupCommand().ExecuteAsync(args, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(args.IsSet(LookupCommand.Ico));
        Assert.Equal("27074358", args.Get(LookupCommand.Ico));
        Assert.False(args.IsSet(TypedCommand.Force));
        Assert.Equal("ada", args.Get(TypedCommand.Label));
        Assert.Equal(TimeSpan.FromSeconds(5), args.Get(TypedCommand.Delay));

        var omitted = new TestCommandArgs();
        Assert.Null(omitted.Get(LookupCommand.Ico));
        Assert.False(omitted.IsSet(LookupCommand.Ico));
    }

    private static Command BuildTyped() => Build("typed");

    private static Command Build(string name)
    {
        var commands = WorkerCommands.Create([]);
        commands.Add<TypedCommand>();
        commands.Add<RepeatCommand>();
        return commands.Build().Subcommands.Single(command => command.Name == name);
    }

    private static ParseResult Parse(string[] args) =>
        WorkerCommands.Create(args).Add<TypedCommand>().Parse(args);

    private static ParseResult ParseFlags(string[] args) =>
        WorkerCommands.Create(args).Add<FlagsCommand>().Parse(args);

    private static Option<T> Option<T>(Command command, string name) =>
        command.Options.OfType<Option<T>>().Single(option => option.Name == name);

    public enum SampleColor
    {
        Red,
        Green,
        Blue,
    }

    public sealed class TypedCommand : IWorkerCommand
    {
        public static readonly CommandOption<int> Count = new("--count", "How many.") { Required = true };

        public static readonly CommandOption<bool> Force = new("--force", "Overwrite.");

        public static readonly CommandOption<string> Label = new("--label", "Label.")
        {
            Aliases = ["-l"],
            DefaultValue = "ada",
        };

        public static readonly CommandOption<TimeSpan> Delay = new("--delay", "Delay.")
        {
            DefaultFactory = static () => TimeSpan.FromSeconds(5),
        };

        public static readonly CommandOption<DateOnly> Day = new("--day", "Day.");

        public static readonly CommandOption<DateTimeOffset> At = new("--at", "Instant.");

        public static readonly CommandOption<double> Ratio = new("--ratio", "Ratio.");

        public static readonly CommandOption<decimal> Amount = new("--amount", "Amount.");

        public static readonly CommandOption<long> Size = new("--size", "Size.");

        public static readonly CommandOption<DirectoryInfo> Folder = new("--folder", "Folder.");

        public static readonly CommandOption<string[]> Tags = new("--tag", "Tags.");

        public static readonly EnumCommandOption<SampleColor> Color = new("--color", "Color.");

        public static readonly CommandArgument<FileInfo> File = new("file", "Input file.");

        public static CommandSpec Spec { get; } = new("typed", "Typed command.", "t")
        {
            Count,
            Force,
            Label,
            Delay,
            Day,
            At,
            Ratio,
            Amount,
            Size,
            Folder,
            Tags,
            Color,
            File,
        };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class RepeatCommand : IWorkerCommand
    {
        public static readonly CommandArgument<string[]> Words = new("words", "Words.") { Required = false };

        public static CommandSpec Spec { get; } = new("repeat", "Repeat words.") { Words };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    [Flags]
    public enum SampleFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    public sealed class FlagsCommand : IWorkerCommand
    {
        public static readonly EnumCommandOption<SampleFlags> Mode = new("--mode", "Access mode.");

        public static CommandSpec Spec { get; } = new("flags", "One flags value.") { Mode };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class EnumArgumentCommand : IWorkerCommand
    {
        public static readonly EnumCommandArgument<SampleColor> Color = new("color", "Color.");

        public static CommandSpec Spec { get; } = new("paint", "One color argument.") { Color };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class PlainEnumCommand : IWorkerCommand
    {
        public static readonly CommandOption<SampleColor> Color = new("--color", "Color.");

        public static CommandSpec Spec { get; } = new("shade", "Plain enum option.") { Color };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class PlainEnumArgumentCommand : IWorkerCommand
    {
        public static readonly CommandArgument<SampleColor> Color = new("color", "Color.");

        public static CommandSpec Spec { get; } = new("paint", "Plain enum argument.") { Color };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class PlainEnumArrayCommand : IWorkerCommand
    {
        public static readonly CommandOption<SampleColor[]> Color = new("--color", "Colors.");

        public static CommandSpec Spec { get; } = new("shades", "Plain enum array.") { Color };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class GuidCommand : IWorkerCommand
    {
        private static readonly CommandOption<Guid> Id = new("--id", "Identifier.");

        public static CommandSpec Spec { get; } = new("guid", "Unsupported value.") { Id };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class BothDefaultsCommand : IWorkerCommand
    {
        private static readonly CommandOption<string> Name = new("--name", "Name.")
        {
            DefaultValue = "a",
            DefaultFactory = static () => "b",
        };

        public static CommandSpec Spec { get; } = new("both", "Conflicting defaults.") { Name };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class DuplicateOptionCommand : IWorkerCommand
    {
        private static readonly CommandOption<string> First = new("--name", "First.");

        private static readonly CommandOption<string> Second = new("--name", "Second.");

        public static CommandSpec Spec { get; } = new("dup", "Duplicate options.") { First, Second };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public interface ILookupClient
    {
        string Echo(string value);
    }

    private sealed class EchoClient : ILookupClient
    {
        public string Echo(string value) => value;
    }

    public sealed class DiCommand(ILookupClient client) : IWorkerCommand
    {
        public static readonly CommandOption<string> Ico = new("--ico", "Record id to look up.") { Required = true };

        public static CommandSpec Spec { get; } = new("di", "Uses a client.") { Ico };

        public static string? Seen { get; private set; }

        public static void Reset() => Seen = null;

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            Seen = client.Echo(args.Get(Ico) ?? "");
            return Task.FromResult(ExitCodes.Success);
        }
    }

    public sealed class LookupCommand : IWorkerCommand
    {
        public static readonly CommandOption<string> Ico = new("--ico", "Record id to look up.") { Required = true };

        public static CommandSpec Spec { get; } = new("lookup", "Look up one subject.") { Ico };

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(args.Get(Ico) == "27074358" ? ExitCodes.Success : ExitCodes.Error);
    }
}
