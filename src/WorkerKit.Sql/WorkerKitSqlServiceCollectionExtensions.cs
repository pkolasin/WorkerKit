namespace WorkerKit.Sql;

/// <summary>
/// Registers SQL Server and PostgreSQL services.
/// <c>Data:Provider</c> is read when <see cref="IWorkerKitSqlDialect"/> is first resolved, not while this method runs.
/// A test that calls <c>UseSetting</c> after <c>ConfigureHost</c> therefore still selects the provider.
/// </summary>
public static class WorkerKitSqlServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IWorkerKitSqlDialect"/>, <see cref="IDbConnectionFactory"/>, <see cref="IWorkerKitBulkCopy"/>,
    /// <see cref="ISchemaVersionStore"/>, and <see cref="SchemaMigrationRunner"/>.
    /// Existing registrations are left in place.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <remarks>
    /// The dialect factory reads <see cref="IOptionsMonitor{TOptions}"/> for <see cref="DataOptions"/> on first resolution.
    /// Set <c>Data:Provider</c> to <c>SqlServer</c> or <c>PostgreSQL</c> and set <c>Data:ConnectionString</c>.
    /// </remarks>
    public static IServiceCollection AddWorkerKitSql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<DataOptions>().BindConfiguration(DataOptions.SectionName);
        services.TryAddSingleton<IWorkerKitSqlDialect>(static serviceProvider =>
            WorkerKitSql.CreateDialect(serviceProvider.GetRequiredService<IOptionsMonitor<DataOptions>>().CurrentValue.Provider));
        services.TryAddSingleton<IDbConnectionFactory, WorkerKitConnectionFactory>();
        services.TryAddSingleton<IWorkerKitBulkCopy, WorkerKitBulkCopy>();
        services.TryAddSingleton<ISchemaVersionStore, SchemaVersionStore>();
        services.TryAddSingleton<SchemaMigrationRunner>();
        return services;
    }
}
