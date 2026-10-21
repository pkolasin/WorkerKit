namespace WorkerKit.Sql.Tests;

/// <summary>
/// Integration fact. Skipped unless <c>WORKERKIT_INTEGRATION</c> is <c>1</c>.
/// The default suite does not start a container.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WORKERKIT_INTEGRATION"), "1", StringComparison.Ordinal))
        {
            Skip = "Set WORKERKIT_INTEGRATION=1 to run Testcontainers tests. Docker is required.";
        }
    }
}
