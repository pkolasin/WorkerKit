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
/// <remarks>
/// The clock is a <see cref="FakeTimeProvider"/> registered as <see cref="TimeProvider"/>. Auto-advance stays at zero.
/// Telemetry is off unless <see cref="EnableTelemetry"/> is called with <see langword="true"/>.
/// </remarks>
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

    /// <summary>Adds an application callback. A later call runs after the callbacks already stored.</summary>
    /// <param name="configure">Callback invoked on the <see cref="WorkerAppBuilder"/> before the host is built.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
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
    /// <param name="configure">Host callback. Calls run in registration order.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public WorkerAppTesterBuilder ConfigureHost(Action<IHostApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureHost.Add(configure);
        return this;
    }

    /// <summary>Sets one in-memory configuration entry. The same key keeps the latest value.</summary>
    /// <param name="key">Configuration key.</param>
    /// <param name="value">Configuration value. <see langword="null"/> is stored.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    public WorkerAppTesterBuilder UseSetting(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _settings[key] = value;
        return this;
    }

    /// <summary>Sets each pair through <see cref="UseSetting"/>.</summary>
    /// <param name="settings">Configuration entries. A repeated key keeps the latest value.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>, or a key in <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A key in <paramref name="settings"/> is empty or whitespace.</exception>
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
    /// <param name="configure">Service callback. A later call runs after the callbacks already stored.</param>
    /// <returns>This builder.</returns>
    /// <remarks>The callback runs after this builder replaces <see cref="TimeProvider"/> and <see cref="ICommandOutput"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
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

    /// <summary>Sets the host environment name. The value used when this is not called is <c>Production</c>.</summary>
    /// <param name="environmentName">Environment name passed to the host.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environmentName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="environmentName"/> is empty or whitespace.</exception>
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
    /// <param name="enable"><see langword="true"/> turns telemetry on for the iteration host. The default argument is <see langword="true"/>.</param>
    /// <returns>This builder.</returns>
    /// <remarks>Telemetry stays off until this is called with <see langword="true"/>.</remarks>
    public WorkerAppTesterBuilder EnableTelemetry(bool enable = true)
    {
        _enableTelemetry = enable;
        return this;
    }

    /// <summary>Builds the iteration host once.</summary>
    /// <returns>The tester for the built host. The host is not started.</returns>
    /// <remarks>
    /// Turns operating-system integration off, then applies <see cref="ConfigureApp"/>, <see cref="ConfigureHost"/>,
    /// the in-memory settings, the <see cref="FakeTimeProvider"/>, the captured <see cref="ICommandOutput"/>, and
    /// <see cref="ConfigureServices"/>. The host content root is the temporary instance directory.
    /// A second call throws <see cref="InvalidOperationException"/>.
    /// If building throws, that directory is deleted and the exception is thrown again.
    /// </remarks>
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
/// <remarks>
/// <see cref="Time"/> is the <see cref="FakeTimeProvider"/> registered as <see cref="TimeProvider"/>.
/// Telemetry on <see cref="Host"/> is off unless <see cref="WorkerAppTesterBuilder.EnableTelemetry"/> was called with <see langword="true"/>.
/// </remarks>
/// <example>
/// <code>
/// await using var tester = WorkerAppTester.Create&lt;Program&gt;()
///     .UseSetting("Worker:PollIntervalSeconds", "5")
///     .Build();
/// WorkerIterationResult iteration = await tester.RunIterationAsync&lt;MyWorker&gt;();
/// CommandCapture capture = await tester.RunCommandAsync("version");
/// </code>
/// </example>
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

    /// <summary>
    /// Creates a builder for <typeparamref name="TProgram"/>.
    /// The instance directory is a new folder under the temp <c>workerkit-tests</c> directory, passed as <c>--instance-dir</c>.
    /// </summary>
    /// <typeparam name="TProgram">Program type passed to <see cref="WorkerApp.CreateBuilder{TProgram}"/>.</typeparam>
    /// <param name="identity">Identity overrides. <see langword="null"/> leaves them unset.</param>
    /// <returns>A builder that has not built the host.</returns>
    public static WorkerAppTesterBuilder Create<TProgram>(Action<WorkerAppOptions>? identity = null)
    {
        var instanceDirectory = CreateInstanceDirectory();
        var app = WorkerApp.CreateBuilder<TProgram>(["--instance-dir", instanceDirectory], identity);
        return new WorkerAppTesterBuilder(app, instanceDirectory);
    }

    /// <summary>Identity from <see cref="WorkerApp.CreateBuilder{TProgram}"/>.</summary>
    public WorkerAppIdentity Identity => _app.Identity;

    /// <summary>Command catalog used by <see cref="RunCommandAsync"/>.</summary>
    public WorkerCommands Commands => _app.Commands;

    /// <summary><see cref="FakeTimeProvider"/> registered as <see cref="TimeProvider"/>.</summary>
    public FakeTimeProvider Time { get; }

    /// <summary>Temporary instance directory passed as <c>--instance-dir</c>. Dispose deletes it.</summary>
    public string InstanceDirectory { get; }

    /// <summary>Built host. It is not started.</summary>
    public IHost Host { get; }

    /// <summary><see cref="IHost.Services"/> from <see cref="Host"/>.</summary>
    public IServiceProvider Services => Host.Services;

    /// <summary>
    /// Parses and invokes <paramref name="args"/>.
    /// The captured text is the command-line writers plus text written to <see cref="ICommandOutput"/>.
    /// </summary>
    /// <param name="args">Command tokens.</param>
    /// <returns>The exit code and the captured console text.</returns>
    /// <remarks>The <see cref="ICommandOutput"/> capture is cleared before the command runs.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Runs one iteration of the single hosted <typeparamref name="TWorker"/>.
    /// The service must implement <see cref="IWorkerIterations"/>.
    /// </summary>
    /// <typeparam name="TWorker">Hosted service type.</typeparam>
    /// <param name="cancellationToken">Token passed to <see cref="IWorkerIterations.RunIterationAsync"/>.</param>
    /// <returns>The iteration result.</returns>
    /// <remarks>
    /// Throws <see cref="InvalidOperationException"/> when <see cref="Services"/> does not contain exactly one
    /// <see cref="IHostedService"/> of <typeparamref name="TWorker"/>, or when that service does not implement
    /// <see cref="IWorkerIterations"/>.
    /// </remarks>
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

    /// <summary>
    /// Runs one queue batch of the single hosted <typeparamref name="TWorker"/>.
    /// The service must implement <see cref="IQueueBatches"/>.
    /// </summary>
    /// <typeparam name="TWorker">Hosted service type.</typeparam>
    /// <param name="cancellationToken">Token passed to <see cref="IQueueBatches.RunBatchAsync"/>.</param>
    /// <returns>The batch result.</returns>
    /// <remarks>
    /// Throws <see cref="InvalidOperationException"/> when <see cref="Services"/> does not contain exactly one
    /// <see cref="IHostedService"/> of <typeparamref name="TWorker"/>, or when that service does not implement
    /// <see cref="IQueueBatches"/>.
    /// </remarks>
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

    /// <summary>Disposes <see cref="Host"/> and deletes <see cref="InstanceDirectory"/>. A later call does nothing.</summary>
    /// <remarks>
    /// Directory deletion tries three times. <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/>
    /// on the first two tries wait and retry. The third failure leaves the directory in place.
    /// </remarks>
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

    /// <summary>
    /// Disposes <see cref="Host"/> with <see cref="IAsyncDisposable.DisposeAsync"/> when the host implements it,
    /// and with <see cref="IDisposable.Dispose"/> otherwise. Then deletes <see cref="InstanceDirectory"/>.
    /// A later call does nothing.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes after the host is disposed and directory deletion has finished.</returns>
    /// <remarks>
    /// Directory deletion tries three times. <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/>
    /// on the first two tries wait and retry. The third failure leaves the directory in place.
    /// </remarks>
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
