
namespace WorkerKit.Hosting;

public static class StartupValidation
{
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
