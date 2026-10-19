using System.CommandLine;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Workers;

namespace WorkerKit.Testing;

/// <summary>
/// Collects the connector's <c>ConfigureHost</c> delegates and builds a host for one test.
/// </summary>
public sealed class WorkerAppTesterBuilder
{
    private readonly WorkerAppBuilder _app;
    private readonly FakeTimeProvider _time = new();
    private readonly CommandOutputCapture _commandOutput = new();
    private readonly Dictionary<string, string?> _settings = [];
    private readonly List<Action<IHostApplicationBuilder>> _configureHost = [];
    private readonly string _instanceDirectory;
    private Action<WorkerAppBuilder>? _configureApp;
    private Action<IServiceCollection>? _configureServices;
    private string _environmentName = "Production";
    private bool _enableTelemetry;
    private bool _built;

    internal WorkerAppTesterBuilder(WorkerAppBuilder app, string instanceDirectory)
    {
        _app = app;
        _instanceDirectory = instanceDirectory;
    }

    /// <summary>Clock registered as <see cref="TimeProvider"/>. Auto-advance stays at zero, so delays wait for <see cref="FakeTimeProvider.Advance"/>.</summary>
    public FakeTimeProvider Time => _time;

    public WorkerAppTesterBuilder ConfigureApp(Action<WorkerAppBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var prior = _configureApp;
        _configureApp = prior is null
            ? configure
            : app =>
            {
                prior(app);
                configure(app);
            };
        return this;
    }

    /// <summary>Extra host delegate, run after <see cref="ConfigureApp"/> and before service replacements.</summary>
    public WorkerAppTesterBuilder ConfigureHost(Action<IHostApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureHost.Add(configure);
        return this;
    }

    public WorkerAppTesterBuilder UseSetting(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _settings[key] = value;
        return this;
    }

    public WorkerAppTesterBuilder UseSettings(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var (key, value) in settings)
        {
            UseSetting(key, value);
        }

        return this;
    }

    /// <summary>Runs after the connector registers services, so <c>Remove</c> and <c>Replace</c> win.</summary>
    public WorkerAppTesterBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var prior = _configureServices;
        _configureServices = prior is null
            ? configure
            : services =>
            {
                prior(services);
                configure(services);
            };
        return this;
    }

    public WorkerAppTesterBuilder Environment(string environmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        _environmentName = environmentName;
        return this;
    }

    /// <summary>
    /// Pass true to register the OpenTelemetry pipeline on the iteration host.
    /// Export still happens only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// Connector commands keep their own <c>EnableTelemetry</c> flag.
    /// </summary>
    public WorkerAppTesterBuilder EnableTelemetry(bool enable = true)
    {
        _enableTelemetry = enable;
        return this;
    }

    public WorkerAppTester Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("The test host can only be built once.");
        }

        _built = true;
        try
        {
            EnsureCatalog();
            var builder = _app.BuildHost(
                contentRoot: _instanceDirectory,
                environmentName: _environmentName,
                enableTelemetry: _enableTelemetry,
                operatingSystemIntegration: false);
            return new WorkerAppTester(_app, builder.Build(), _time, _instanceDirectory, _commandOutput);
        }
        catch
        {
            WorkerAppTester.DeleteQuiet(_instanceDirectory);
            throw;
        }
    }

    private void EnsureCatalog()
    {
        _app.UseOperatingSystemIntegration(false);
        _configureApp?.Invoke(_app);
        foreach (var configure in _configureHost)
        {
            _app.ConfigureHost(configure);
        }

        var settings = _settings.ToArray();
        var services = _configureServices;
        var time = _time;
        var output = _commandOutput;
        _app.ConfigureHost(builder =>
        {
            if (settings.Length > 0)
            {
                builder.Configuration.AddInMemoryCollection(settings);
            }

            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton<TimeProvider>(time);
            builder.Services.RemoveAll<ICommandOutput>();
            builder.Services.AddSingleton<ICommandOutput>(output);
            services?.Invoke(builder.Services);
        });
    }
}

/// <summary>
/// In-process connector host for tests. Dispose deletes the temporary instance directory.
/// The host is not started; <see cref="RunIterationAsync{TWorker}"/> and <see cref="RunBatchAsync{TWorker}"/>
/// call the workers directly. <see cref="RunCommandAsync"/> uses the command tree, and builds a host
/// only for commands that need one.
/// </summary>
public sealed class WorkerAppTester : IDisposable, IAsyncDisposable
{
    private readonly WorkerAppBuilder _app;
    private readonly CommandOutputCapture _commandOutput;
    private bool _disposed;

    internal WorkerAppTester(
        WorkerAppBuilder app,
        IHost host,
        FakeTimeProvider time,
        string instanceDirectory,
        CommandOutputCapture commandOutput)
    {
        _app = app;
        Host = host;
        Time = time;
        InstanceDirectory = instanceDirectory;
        _commandOutput = commandOutput;
    }

    public static WorkerAppTesterBuilder Create<TProgram>(Action<WorkerAppOptions>? identity = null)
    {
        var instanceDirectory = CreateInstanceDirectory();
        var app = WorkerApp.CreateBuilder<TProgram>(["--instance-dir", instanceDirectory], identity);
        return new WorkerAppTesterBuilder(app, instanceDirectory);
    }

    public WorkerAppIdentity Identity => _app.Identity;

    public WorkerCommands Commands => _app.Commands;

    public FakeTimeProvider Time { get; }

    public string InstanceDirectory { get; }

    public IHost Host { get; }

    public IServiceProvider Services => Host.Services;

    public async Task<CommandCapture> RunCommandAsync(params string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        _commandOutput.Reset();
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await Commands.Parse(args).InvokeAsync(new InvocationConfiguration
        {
            Output = output,
            Error = error,
        }).ConfigureAwait(false);
        return new CommandCapture(
            code,
            output.ToString() + _commandOutput.StandardOutput,
            error.ToString() + _commandOutput.StandardError);
    }

    public Task<WorkerIterationResult> RunIterationAsync<TWorker>(CancellationToken cancellationToken = default)
        where TWorker : class, IHostedService
    {
        var worker = One<TWorker>();
        if (worker is not IWorkerIterations iterations)
        {
            throw new InvalidOperationException($"{typeof(TWorker).Name} does not expose worker iterations. It must derive from PeriodicWorker.");
        }

        return iterations.RunIterationAsync(cancellationToken);
    }

    public Task<WorkerIterationResult> RunBatchAsync<TWorker>(CancellationToken cancellationToken = default)
        where TWorker : class, IHostedService
    {
        var worker = One<TWorker>();
        if (worker is not IQueueBatches batches)
        {
            throw new InvalidOperationException($"{typeof(TWorker).Name} does not expose queue batches. It must derive from QueueWorker.");
        }

        return batches.RunBatchAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Host.Dispose();
        DeleteQuiet(InstanceDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Host is IAsyncDisposable asyncHost)
        {
            await asyncHost.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            Host.Dispose();
        }

        DeleteQuiet(InstanceDirectory);
    }

    internal static void DeleteQuiet(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private TWorker One<TWorker>()
        where TWorker : class, IHostedService
    {
        var matches = Services.GetServices<IHostedService>().OfType<TWorker>().ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one hosted service of type {typeof(TWorker).Name}, found {matches.Length}.");
        }

        return matches[0];
    }

    private static string CreateInstanceDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "workerkit-tests");
        Directory.CreateDirectory(root);
        var name = "t" + Guid.NewGuid().ToString("N")[..16];
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

}

internal sealed class CommandOutputCapture : ICommandOutput
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public string StandardOutput => _output.ToString();

    public string StandardError => _error.ToString();

    public void WriteLine(string? value) => _output.WriteLine(value);

    public void WriteError(string? value) => _error.WriteLine(value);

    public void Reset()
    {
        _output.GetStringBuilder().Clear();
        _error.GetStringBuilder().Clear();
    }
}
