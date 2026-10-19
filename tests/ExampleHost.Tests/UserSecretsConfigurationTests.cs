using System.Reflection;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WorkerKit.Hosting;

using WorkerKit.Abstractions.Options;
using WorkerKit.Samples.OpenMeteo;
namespace ExampleHost.Tests;

public sealed class UserSecretsConfigurationTests : IDisposable
{
    private const string SecretUserAgent = "secret-agent/1";
    private const string EnvironmentUserAgent = "env-agent/1";
    private const string CommandLineUserAgent = "cli-agent/1";
    private const string SecretListenUrl = "http://127.0.0.1:9/health/";
    private const string EnvironmentVariable = "OpenMeteo__UserAgent";

    private readonly string _secretsPath;
    private readonly string? _previousSecrets;
    private readonly string? _previousEnvironmentValue;

    public UserSecretsConfigurationTests()
    {
        var secretsId = ExampleHosts.Identity.ApplicationAssembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId
            ?? throw new InvalidOperationException("Example host has no UserSecretsId.");
        _secretsPath = PathHelper.GetSecretsPathFromSecretsId(secretsId);
        _previousSecrets = File.Exists(_secretsPath) ? File.ReadAllText(_secretsPath) : null;
        _previousEnvironmentValue = Environment.GetEnvironmentVariable(EnvironmentVariable);

        Directory.CreateDirectory(Path.GetDirectoryName(_secretsPath)!);
        File.WriteAllText(_secretsPath, $$"""
            {
              "OpenMeteo": {
                "UserAgent": "{{SecretUserAgent}}"
              },
              "Health": {
                "ListenUrl": "{{SecretListenUrl}}"
              }
            }
            """);
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariable, _previousEnvironmentValue);
        if (_previousSecrets is null)
        {
            if (File.Exists(_secretsPath))
            {
                File.Delete(_secretsPath);
            }
        }
        else
        {
            File.WriteAllText(_secretsPath, _previousSecrets);
        }
    }

    [Fact]
    public void User_secrets_override_appsettings_in_development()
    {
        using var host = CreateHost("Development");

        var options = Options(host);

        Assert.Equal(SecretUserAgent, options.Forecast.UserAgent);
        Assert.Equal(SecretListenUrl, options.Health.ListenUrl);
    }

    [Fact]
    public void Environment_variables_override_user_secrets()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariable, EnvironmentUserAgent);
        using var host = CreateHost("Development");

        var options = Options(host);

        Assert.Equal(EnvironmentUserAgent, options.Forecast.UserAgent);
        Assert.Equal(SecretListenUrl, options.Health.ListenUrl);
    }

    [Fact]
    public void Command_line_overrides_user_secrets()
    {
        using var host = ExampleHosts.Create(
            ["--OpenMeteo:UserAgent", CommandLineUserAgent],
            contentRoot: RepoPaths.HostProject(),
            environmentName: "Development").Build();

        var options = Options(host);

        Assert.Equal(CommandLineUserAgent, options.Forecast.UserAgent);
        Assert.Equal(SecretListenUrl, options.Health.ListenUrl);
    }

    [Fact]
    public void Production_does_not_load_user_secrets()
    {
        using var host = CreateHost("Production");

        var options = Options(host);

        Assert.Equal("WorkerKitExample/1.0", options.Forecast.UserAgent);
        Assert.Equal("", options.Health.ListenUrl);
    }

    private static IHost CreateHost(string environmentName) =>
        ExampleHosts.Create(
            [],
            contentRoot: RepoPaths.HostProject(),
            environmentName: environmentName).Build();

    private static (OpenMeteoOptions Forecast, HealthOptions Health) Options(IHost host)
    {
        var services = host.Services;
        return (
            services.GetRequiredService<IOptions<OpenMeteoOptions>>().Value,
            services.GetRequiredService<IOptions<HealthOptions>>().Value);
    }
}
