using WorkerKit.Hosting.Configuration;

namespace WorkerKit.Hosting.DependencyInjection;

/// <summary>Binds option types and records their section paths for <c>check-config</c>.</summary>
public static class OptionsRegistrationExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to <paramref name="section"/>, validates data annotations, and validates on start.
    /// A section with a path is recorded on the <see cref="OptionsSectionCatalog"/>.
    /// </summary>
    /// <typeparam name="TOptions">Options type to bind.</typeparam>
    /// <param name="services">Service collection to update.</param>
    /// <param name="section">Configuration section to bind.</param>
    /// <returns>The options builder for <typeparamref name="TOptions"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="section"/> is null.</exception>
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

    /// <summary>Records <paramref name="sectionPath"/> on the <see cref="OptionsSectionCatalog"/> for <paramref name="services"/>.</summary>
    /// <param name="services">Service collection that holds the catalog.</param>
    /// <param name="sectionPath">Section path to record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static void RememberSection(this IServiceCollection services, string sectionPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        OptionsSectionCatalog.GetOrCreate(services).Remember(sectionPath);
    }
}
