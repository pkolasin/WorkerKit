using System.Text.RegularExpressions;
using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting;

/// <summary>Name, directory, and default flag for one instance.</summary>
/// <param name="Name">Instance name.</param>
/// <param name="Directory">Instance directory.</param>
/// <param name="IsDefault">True when this is the default instance.</param>
public sealed record InstanceLocation(string Name, string Directory, bool IsDefault);

/// <summary>
/// Resolves the instance before <c>Host.CreateApplicationBuilder</c>, from arguments and the environment only.
/// </summary>
public static partial class InstanceLocator
{
    /// <summary>
    /// Environment variable that selects the instance directory.
    /// This is <see cref="AppIdentity.Current"/>'s <see cref="WorkerAppIdentity.InstanceDirectoryVariable"/>.
    /// </summary>
    public static string DirEnv => AppIdentity.Current.InstanceDirectoryVariable;

    /// <summary>
    /// Environment variable that selects the instance name.
    /// This is <see cref="AppIdentity.Current"/>'s <see cref="WorkerAppIdentity.InstanceVariable"/>.
    /// </summary>
    public static string NameEnv => AppIdentity.Current.InstanceVariable;

    /// <summary>
    /// Environment variable that selects the instances root.
    /// This is <see cref="AppIdentity.Current"/>'s <see cref="WorkerAppIdentity.InstancesRootVariable"/>.
    /// </summary>
    public static string RootEnv => AppIdentity.Current.InstancesRootVariable;

    /// <summary>Resolves the instance with <see cref="AppIdentity.Current"/>.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="env">Reads an environment variable by name.</param>
    /// <param name="installDir">Install directory used when no instance directory is selected.</param>
    /// <returns>The resolved location.</returns>
    public static InstanceLocation Resolve(string[] args, Func<string, string?> env, string installDir) =>
        Resolve(args, env, installDir, AppIdentity.Current);

    /// <summary>Resolves the instance from arguments and environment variables.</summary>
    /// <param name="args">Command-line arguments. <c>--instance-dir</c> and <c>--instance</c> are read.</param>
    /// <param name="env">Reads an environment variable by name.</param>
    /// <param name="installDir">Install directory used when no instance directory is selected.</param>
    /// <param name="identity">Identity that supplies the environment variable names and the default instances root.</param>
    /// <returns>The resolved location. <see cref="InstanceLocation.IsDefault"/> is true only when no directory was selected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/>, <paramref name="env"/>, or <paramref name="identity"/> is null.</exception>
    /// <remarks>
    /// The directory comes from <c>--instance-dir</c>, then <see cref="WorkerAppIdentity.InstanceDirectoryVariable"/>.
    /// The name comes from <c>--instance</c>, then <see cref="WorkerAppIdentity.InstanceVariable"/>.
    /// A name without a directory uses <see cref="WorkerAppIdentity.InstancesRootVariable"/> when that variable is set,
    /// otherwise <see cref="WorkerAppIdentity.DefaultInstancesRoot"/>, combined with the name.
    /// When neither a directory nor a name is set, the name is <c>default</c> and the directory is the full <paramref name="installDir"/>.
    /// Otherwise the name is the explicit name, or the directory's last segment, and it must pass <see cref="Validate"/>.
    /// </remarks>
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

    /// <summary>
    /// Returns <paramref name="name"/> when it is 1 to 40 characters: a lowercase letter or digit, then lowercase letters, digits, or hyphens.
    /// </summary>
    /// <param name="name">Instance name to check.</param>
    /// <returns>The same <paramref name="name"/> when it matches.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> does not match that pattern.</exception>
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
