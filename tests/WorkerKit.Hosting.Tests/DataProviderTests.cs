using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkerKit.Abstractions.Options;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting.Validation;

namespace WorkerKit.Hosting.Tests;

public sealed class DataProviderTests
{
    [Theory]
    [InlineData("None", null, true)]
    [InlineData("Dapper", "Server=localhost", true)]
    [InlineData("SqlServer", "Server=localhost", true)]
    [InlineData("PostgreSQL", "Host=localhost", true)]
    [InlineData("Postgres", "Host=localhost", true)]
    [InlineData("SqlServer", null, false)]
    [InlineData("PostgreSQL", " ", false)]
    [InlineData("Oracle", "Server=localhost", false)]
    public void Validator_accepts_sql_server_and_postgresql(string provider, string? connectionString, bool success)
    {
        var result = new DataOptionsValidator().Validate(null, new DataOptions
        {
            Provider = provider,
            ConnectionString = connectionString ?? "",
        });

        Assert.Equal(success, result.Succeeded);
        if (!success && provider is "SqlServer" or "PostgreSQL")
        {
            Assert.Contains("Data:ConnectionString", result.FailureMessage, StringComparison.Ordinal);
        }

        if (provider == "Oracle")
        {
            Assert.Contains("Data:Provider must be None, Dapper, SqlServer, or PostgreSQL.", result.FailureMessage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Check_config_names_the_sql_provider()
    {
        var output = new RecordingOutput();
        var root = Directory.CreateTempSubdirectory("ws-data-provider").FullName;
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: root,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Data:Provider"] = "PostgreSQL",
                    ["Data:ConnectionString"] = "Host=localhost;Password=secret",
                });
                builder.Services.AddSingleton<ICommandOutput>(output);
            }).Build();

        var code = host.Services.GetRequiredService<CheckConfigHandler>().Execute();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(
            output.Lines,
            line => line.Contains("Data:Provider is PostgreSQL. Call AddWorkerKitSql", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.Contains("SELECT 1", StringComparison.Ordinal));
        Assert.DoesNotContain(output.Lines, line => line.Contains("secret", StringComparison.Ordinal));
    }
}
