namespace WorkerKit.Testing;

/// <summary>
/// Compares rendered <c>help --markdown</c> with a checked-in CLI reference.
/// </summary>
public static class CliReference
{
    /// <summary>When this variable is <c>1</c>, <see cref="AssertUpToDate"/> writes the file instead of throwing.</summary>
    public const string UpdateVariable = "WORKERKIT_UPDATE_CLI_DOC";

    /// <summary>
    /// Compares <paramref name="actual"/> with <paramref name="path"/>. Line endings are normalized to LF.
    /// A missing file or a mismatch throws <see cref="WorkerKitAssertException"/> and includes <paramref name="regenerateCommand"/>.
    /// Pass <paramref name="update"/> to force or suppress a rewrite. When it is null, <see cref="UpdateVariable"/> decides.
    /// </summary>
    public static void AssertUpToDate(string path, string actual, string regenerateCommand, bool? update = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentException.ThrowIfNullOrWhiteSpace(regenerateCommand);

        var normalized = actual.ReplaceLineEndings("\n");
        var rewrite = update ?? string.Equals(
            Environment.GetEnvironmentVariable(UpdateVariable),
            "1",
            StringComparison.Ordinal);
        if (rewrite)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, normalized);
            return;
        }

        if (!File.Exists(path))
        {
            throw new WorkerKitAssertException(
                $"Missing {path}. Regenerate it with: {regenerateCommand}{Environment.NewLine}Or set {UpdateVariable}=1 to write the file.");
        }

        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (!string.Equals(expected, normalized, StringComparison.Ordinal))
        {
            throw new WorkerKitAssertException(
                $"{path} does not match the generated CLI reference. Regenerate it with: {regenerateCommand}{Environment.NewLine}"
                + $"Or set {UpdateVariable}=1 to rewrite the file.{Environment.NewLine}{Environment.NewLine}{normalized}");
        }
    }
}
