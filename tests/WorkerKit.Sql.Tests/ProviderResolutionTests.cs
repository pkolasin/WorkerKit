using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using WorkerKit.Abstractions.Data;
using WorkerKit.Sql;
using WorkerKit.Testing;

namespace WorkerKit.Sql.Tests;

public sealed class ProviderResolutionTests
{
    [Theory]
    [InlineData("SqlServer", WorkerKitSqlProvider.SqlServer)]
    [InlineData("postgresql", WorkerKitSqlProvider.PostgreSql)]
    [InlineData("Postgres", WorkerKitSqlProvider.PostgreSql)]
    public async Task Provider_is_read_when_the_dialect_is_resolved(string provider, WorkerKitSqlProvider expected)
    {
        await using var tester = WorkerAppTester.Create<ProviderResolutionTests>()
            .ConfigureHost(builder => builder.Services.AddWorkerKitSql())
            .UseSetting("Data:Provider", provider)
            .UseSetting("Data:ConnectionString", "Host=localhost;Database=register")
            .Build();

        var dialect = tester.Services.GetRequiredService<IWorkerKitSqlDialect>();
        var bulk = tester.Services.GetRequiredService<IWorkerKitBulkCopy>();
        var store = tester.Services.GetRequiredService<ISchemaVersionStore>();
        var runner = tester.Services.GetRequiredService<SchemaMigrationRunner>();
        var factory = tester.Services.GetRequiredService<IDbConnectionFactory>();

        Assert.Equal(expected, dialect.Provider);
        Assert.IsType(expected == WorkerKitSqlProvider.SqlServer ? typeof(SqlServerDialect) : typeof(PostgreSqlDialect), dialect);
        Assert.NotNull(bulk);
        Assert.NotNull(store);
        Assert.NotNull(runner);
        Assert.IsType<WorkerKitConnectionFactory>(factory);
    }

    [Fact]
    public async Task None_fails_at_resolution_rather_than_registration()
    {
        await using var tester = WorkerAppTester.Create<ProviderResolutionTests>()
            .ConfigureHost(builder => builder.Services.AddWorkerKitSql())
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => tester.Services.GetRequiredService<IWorkerKitSqlDialect>());

        Assert.Contains("resolved", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AddWorkerKitSql", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_existing_connection_factory_is_left_in_place()
    {
        var existing = new ExistingFactory();
        await using var tester = WorkerAppTester.Create<ProviderResolutionTests>()
            .ConfigureHost(builder =>
            {
                builder.Services.AddSingleton<IDbConnectionFactory>(existing);
                builder.Services.AddWorkerKitSql();
            })
            .UseSetting("Data:Provider", "SqlServer")
            .UseSetting("Data:ConnectionString", "Server=localhost;Database=register")
            .Build();

        Assert.Same(existing, tester.Services.GetRequiredService<IDbConnectionFactory>());
        Assert.Equal(WorkerKitSqlProvider.SqlServer, tester.Services.GetRequiredService<IWorkerKitSqlDialect>().Provider);
    }

    [Theory]
    [InlineData("SqlServer", true)]
    [InlineData("PostgreSQL", true)]
    [InlineData("postgres", true)]
    [InlineData("Dapper", false)]
    [InlineData("None", false)]
    [InlineData(null, false)]
    public void TryParseProvider_accepts_sql_server_and_postgresql(string? provider, bool expected)
    {
        var parsed = WorkerKitSql.TryParseProvider(provider, out var value);
        Assert.Equal(expected, parsed);
        if (expected)
        {
            Assert.Equal(
                provider!.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                    ? WorkerKitSqlProvider.SqlServer
                    : WorkerKitSqlProvider.PostgreSql,
                value);
        }
    }

    private sealed class ExistingFactory : IDbConnectionFactory
    {
        public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
