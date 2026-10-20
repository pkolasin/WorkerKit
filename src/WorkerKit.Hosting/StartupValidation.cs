
namespace WorkerKit.Hosting;

/// <summary>Runs options validators registered as <see cref="IStartupValidator"/> without starting the host.</summary>
public static class StartupValidation
{
    /// <summary>
    /// Invokes every <see cref="IStartupValidator"/> and returns the failure messages.
    /// <see cref="OptionsValidationException"/> and an <see cref="AggregateException"/> of those failures are returned as messages.
    /// </summary>
    /// <param name="services">Provider that contains the validators.</param>
    /// <returns>Failure messages. Empty when every validator succeeds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IReadOnlyList<string> GetFailures(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        try
        {
            foreach (var validator in services.GetServices<IStartupValidator>())
            {
                validator.Validate();
            }

            return [];
        }
        catch (OptionsValidationException exception)
        {
            return exception.Failures.ToArray();
        }
        catch (AggregateException exception)
        {
            return exception.Flatten().InnerExceptions
                .SelectMany(inner => inner is OptionsValidationException options
                    ? options.Failures
                    : [inner.Message])
                .ToArray();
        }
    }
}
