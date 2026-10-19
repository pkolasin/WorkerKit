using WorkerKit.Hosting.Configuration;

namespace WorkerKit.Hosting.DependencyInjection;

public static class OptionsRegistrationExtensions
{
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration section)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);
        if (section is IConfigurationSection { Path: { Length: > 0 } path })
        {
            services.RememberSection(path);
        }

        return services.AddOptions<TOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    public static void RememberSection(this IServiceCollection services, string sectionPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        OptionsSectionCatalog.GetOrCreate(services).Remember(sectionPath);
    }
}
