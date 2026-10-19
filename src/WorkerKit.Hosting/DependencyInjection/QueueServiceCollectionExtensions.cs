using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkerKit.Hosting.Queues;
using WorkerKit.Hosting.Validation;

using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Queues;
using WorkerKit.Hosting.Workers;
namespace WorkerKit.Hosting.DependencyInjection;

public static class QueueServiceCollectionExtensions
{
    /// <summary>
    /// Registers named <see cref="QueueWorkerOptions"/> from <c>Queue:{workerName}</c>,
    /// a scoped <typeparamref name="THandler"/>, and the hosted <typeparamref name="TWorker"/>.
    /// Register <see cref="IWorkQueue{T}"/> first. <see cref="AddInMemoryQueueWorker{TWorker,TPayload,THandler}"/>
    /// does that with the example queue.
    /// </summary>
    public static IServiceCollection AddQueueWorker<TWorker, TPayload, THandler>(
        this IServiceCollection services,
        IConfiguration configuration,
        string workerName)
        where TWorker : QueueWorker<TPayload>
        where THandler : class, IWorkItemHandler<TPayload>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);

        var sectionPath = $"{QueueWorkerOptions.SectionName}:{workerName}";
        services.RememberSection(sectionPath);
        services.AddOptions<QueueWorkerOptions>(workerName)
            .Bind(configuration.GetSection(sectionPath))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<QueueWorkerOptions>, QueueWorkerOptionsValidator>());
        services.AddScoped<IWorkItemHandler<TPayload>, THandler>();
        services.AddHostedService<TWorker>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="InMemoryWorkQueue{T}"/> and then <see cref="AddQueueWorker{TWorker,TPayload,THandler}"/>.
    /// The in-memory queue is an example and is not durable.
    /// </summary>
    public static IServiceCollection AddInMemoryQueueWorker<TWorker, TPayload, THandler>(
        this IServiceCollection services,
        IConfiguration configuration,
        string workerName)
        where TWorker : QueueWorker<TPayload>
        where THandler : class, IWorkItemHandler<TPayload>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);

        services.AddSingleton(sp => new InMemoryWorkQueue<TPayload>(sp.GetRequiredService<TimeProvider>(), workerName));
        services.AddSingleton<IWorkQueue<TPayload>>(sp => sp.GetRequiredService<InMemoryWorkQueue<TPayload>>());
        services.AddSingleton<IQueuePendingSource>(sp => sp.GetRequiredService<InMemoryWorkQueue<TPayload>>());
        return services.AddQueueWorker<TWorker, TPayload, THandler>(configuration, workerName);
    }
}
