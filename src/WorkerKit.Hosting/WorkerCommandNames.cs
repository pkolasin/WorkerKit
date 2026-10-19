namespace WorkerKit.Hosting;

internal static class WorkerCommandNames
{
    private static readonly HashSet<string> BuiltIn = new(WorkerCommands.BuiltInNames, StringComparer.Ordinal);

    public static Command CreateGroup(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new Command(name, description);
    }

    public static void Reserve(HashSet<string> taken, Command command, bool builtIn)
    {
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(command);
        foreach (var name in Enumerate(command))
        {
            if (builtIn && BuiltIn.Contains(name))
            {
                throw new InvalidOperationException(
                    $"Command name '{name}' collides with a built-in command. Built-in commands are {string.Join(", ", WorkerCommands.BuiltInNames)}.");
            }

            if (!taken.Add(name))
            {
                throw new InvalidOperationException($"Command name '{name}' is already registered.");
            }
        }
    }

    private static IEnumerable<string> Enumerate(Command command)
    {
        yield return command.Name;
        foreach (var alias in command.Aliases)
        {
            if (!string.Equals(alias, command.Name, StringComparison.Ordinal))
            {
                yield return alias;
            }
        }
    }
}
