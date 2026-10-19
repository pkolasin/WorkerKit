using System.Text.RegularExpressions;
using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting;

public sealed record InstanceLocation(string Name, string Directory, bool IsDefault);

/// <summary>
/// Resolves the instance before <c>Host.CreateApplicationBuilder</c>, from arguments and the environment only.
/// </summary>
public static partial class InstanceLocator
{
    public static string DirEnv => AppIdentity.Current.InstanceDirectoryVariable;

    public static string NameEnv => AppIdentity.Current.InstanceVariable;

    public static string RootEnv => AppIdentity.Current.InstancesRootVariable;

    public static InstanceLocation Resolve(string[] args, Func<string, string?> env, string installDir) =>
        Resolve(args, env, installDir, AppIdentity.Current);

    public static InstanceLocation Resolve(
        string[] args,
        Func<string, string?> env,
        string installDir,
        WorkerAppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(identity);

        var dir = ReadArg(args, "--instance-dir") ?? ReadEnv(env, identity.InstanceDirectoryVariable);
        var name = ReadArg(args, "--instance") ?? ReadEnv(env, identity.InstanceVariable);
        if (dir is null && name is not null)
        {
            dir = Path.Combine(ReadEnv(env, identity.InstancesRootVariable) ?? identity.DefaultInstancesRoot, name);
        }

        if (dir is null)
        {
            return new InstanceLocation("default", Path.GetFullPath(installDir), IsDefault: true);
        }

        dir = Path.GetFullPath(dir);
        var trimmed = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var inferred = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(inferred))
        {
            inferred = trimmed;
        }

        return new InstanceLocation(Validate(name ?? inferred), dir, IsDefault: false);
    }

    public static string Validate(string name)
    {
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException(
                $"Instance name '{name}' is invalid. Use 1-40 characters: a lowercase letter or digit, then lowercase letters, digits, or hyphens.");
        }

        return name;
    }

    private static string? ReadArg(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg.Equals(optionName, StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Missing value for {optionName}.");
                }

                return args[index + 1];
            }

            var prefix = optionName + "=";
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                var value = arg[prefix.Length..];
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Missing value for {optionName}.");
                }

                return value;
            }
        }

        return null;
    }

    private static string? ReadEnv(Func<string, string?> env, string name)
    {
        var value = env(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
