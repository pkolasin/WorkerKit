using System.Data.Common;
using Dapper;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using WorkerKit.Abstractions.Data;
using WorkerKit.Sql;

namespace WorkerKit.Sql.Tests;

/// <summary>
/// Testcontainers coverage for SQL Server 2022 and PostgreSQL 17.
/// Skipped unless <c>WORKERKIT_INTEGRATION=1</c>. This process does not start Docker for the default suite.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SqlIntegrationTests
{
    [IntegrationFact]
    public async Task Sql_server_queue_bulk_and_migrations()
    {
        await using var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();
        await SqlScenario.RunAsync(new SqlServerDialect(), container.GetConnectionString());
    }

    [IntegrationFact]
    public async Task PostgreSQL_queue_bulk_and_migrations()
    {
        await using var container = new PostgreSqlBuilder("postgres:17").Build();
        await container.StartAsync();
        await SqlScenario.RunAsync(new PostgreSqlDialect(), container.GetConnectionString());
    }

    private static class SqlScenario
    {
        public static async Task RunAsync(IWorkerKitSqlDialect dialect, string connectionString)
        {
            var suffix = Guid.NewGuid().ToString("N")[..12];
            var work = "Item" + suffix;
            var batch = "Batch" + suffix;
            var probe = "Probe" + suffix;
            var schema = "register" + suffix;
            var bulk = new WorkerKitBulkCopy(dialect);
            await using var connection = dialect.CreateConnection(connectionString);
            await connection.OpenAsync();

            await CreateWorkTableAsync(dialect, connection, work);
            await CreateBatchTableAsync(dialect, connection, batch);
            await ExerciseLeaseAsync(dialect, connection, work);
            await ExerciseConcurrentClaimAsync(dialect, connectionString, "Race" + suffix);
            await ExercisePredicatesAsync(dialect, connection, work);
            await ExerciseBulkAsync(dialect, bulk, connection, connectionString, batch);
            await ExerciseMigrationsAsync(dialect, connectionString, schema, probe);
        }

        private static async Task ExerciseLeaseAsync(IWorkerKitSqlDialect dialect, DbConnection connection, string table)
        {
            var quoted = dialect.QuoteIdentifier(table);
            var insert = $"""
                INSERT INTO {quoted} ({dialect.QuoteIdentifier("Code")}, {dialect.QuoteIdentifier("Active")}, {dialect.QuoteIdentifier("AvailableAt")}, {dialect.QuoteIdentifier("Attempt")})
                VALUES (@Code, @Active, {dialect.UtcNowExpression}, 0);
                """;
            foreach (var code in new[] { "A", "B", "C" })
            {
                await connection.ExecuteAsync(insert, new { Code = code, Active = true });
            }

            var lease = new WorkLeaseSql(table);
            var first = (await connection.QueryAsync<LeaseRow>(dialect.BuildClaimSql(lease), new { Max = 2, LeaseSeconds = 60, Worker = "worker-a" })).ToArray();
            var second = (await connection.QueryAsync<LeaseRow>(dialect.BuildClaimSql(lease), new { Max = 2, LeaseSeconds = 60, Worker = "worker-b" })).ToArray();
            Assert.Equal(2, first.Length);
            Assert.All(first, row => Assert.Equal(1, row.Attempt));
            Assert.Single(second);
            Assert.Equal(1, second[0].Attempt);

            var expired = first[0];
            var until = dialect.QuoteIdentifier("LeaseUntil");
            var id = dialect.QuoteIdentifier("Id");
            var expire = dialect.Provider == WorkerKitSqlProvider.SqlServer
                ? $"UPDATE {quoted} SET {until} = DATEADD(second, -30, SYSUTCDATETIME()) WHERE {id} = @Id"
                : $"UPDATE {quoted} SET {until} = clock_timestamp() - interval '30 seconds' WHERE {id} = @Id";
            await connection.ExecuteAsync(expire, new { expired.Id });

            var reclaimed = await connection.QuerySingleAsync<LeaseRow>(
                dialect.BuildClaimByIdSql(lease),
                new { Id = expired.Id, LeaseSeconds = 30, Worker = "worker-c" });
            Assert.Equal(expired.Id, reclaimed.Id);
            Assert.Equal(2, reclaimed.Attempt);
            var beforeRenew = await connection.QuerySingleAsync<DateTime>(
                $"SELECT {until} FROM {quoted} WHERE {id} = @Id",
                new { expired.Id });

            var renewed = await connection.QuerySingleAsync<LeaseRow>(
                dialect.BuildRenewLeaseSql(lease),
                new { Id = expired.Id, LeaseSeconds = 300, Worker = "worker-c" });
            Assert.Equal(2, renewed.Attempt);
            var afterRenew = await connection.QuerySingleAsync<DateTime>(
                $"SELECT {until} FROM {quoted} WHERE {id} = @Id",
                new { expired.Id });
            Assert.True(afterRenew > beforeRenew);
            var wrongWorker = (await connection.QueryAsync<LeaseRow>(
                dialect.BuildRenewLeaseSql(lease),
                new { Id = expired.Id, LeaseSeconds = 300, Worker = "worker-z" })).ToArray();
            Assert.Empty(wrongWorker);

            var attempt = await connection.QuerySingleAsync<int>(
                $"SELECT {dialect.QuoteIdentifier("Attempt")} FROM {quoted} WHERE {id} = @Id",
                new { expired.Id });
            Assert.Equal(2, attempt);
        }

        private static async Task ExerciseConcurrentClaimAsync(
            IWorkerKitSqlDialect dialect,
            string connectionString,
            string table)
        {
            await using (var setup = dialect.CreateConnection(connectionString))
            {
                await setup.OpenAsync();
                await CreateWorkTableAsync(dialect, setup, table);
                var quoted = dialect.QuoteIdentifier(table);
                var insert = $"""
                    INSERT INTO {quoted} ({dialect.QuoteIdentifier("Code")}, {dialect.QuoteIdentifier("Active")}, {dialect.QuoteIdentifier("AvailableAt")}, {dialect.QuoteIdentifier("Attempt")})
                    VALUES (@Code, @Active, {dialect.UtcNowExpression}, 0);
                    """;
                for (var index = 0; index < 8; index++)
                {
                    await setup.ExecuteAsync(insert, new { Code = "P" + index, Active = true });
                }
            }

            var sql = dialect.BuildClaimSql(new WorkLeaseSql(table));
            var claimed = await Task.WhenAll(ClaimAsync("worker-left"), ClaimAsync("worker-right"));
            var ids = claimed.SelectMany(batch => batch).ToArray();
            Assert.Equal(8, ids.Distinct().Count());
            Assert.Equal(ids.Length, ids.Distinct().Count());

            async Task<long[]> ClaimAsync(string worker)
            {
                await using var connection = dialect.CreateConnection(connectionString);
                await connection.OpenAsync();
                var rows = await connection.QueryAsync<LeaseRow>(sql, new { Max = 8, LeaseSeconds = 120, Worker = worker });
                return rows.Select(row => row.Id).ToArray();
            }
        }

        private static async Task ExercisePredicatesAsync(IWorkerKitSqlDialect dialect, DbConnection connection, string table)
        {
            var quoted = dialect.QuoteIdentifier(table);
            await connection.ExecuteAsync(
                $"""
                INSERT INTO {quoted} ({dialect.QuoteIdentifier("Code")}, {dialect.QuoteIdentifier("Active")}, {dialect.QuoteIdentifier("AvailableAt")}, {dialect.QuoteIdentifier("Attempt")})
                VALUES (@Code, @Active, {dialect.UtcNowExpression}, 0);
                """,
                new { Code = "D", Active = false });

            var sql = $"""
                SELECT {dialect.QuoteIdentifier("Id")} AS {dialect.QuoteAlias("Id")},
                       {dialect.QuoteIdentifier("Active")} AS {dialect.QuoteAlias("Active")}
                FROM {quoted}
                WHERE {dialect.BooleanPredicate("Active", "Active")}
                  AND {dialect.ListPredicate("Code", "Codes")};
                """;
            var rows = (await connection.QueryAsync<FlagRow>(sql, new { Active = true, Codes = new[] { "A", "C", "D" } })).ToArray();
            Assert.Equal(2, rows.Length);
            Assert.All(rows, row => Assert.True(row.Active));

            var count = await connection.QuerySingleAsync<CountRow>(
                $"SELECT {dialect.CountExpression()} FROM {quoted} WHERE {dialect.BooleanPredicate("Active", "Active")};",
                new { Active = true });
            Assert.Equal(3, count.Count);
            Assert.IsType<int>(count.Count);
        }

        private static async Task ExerciseBulkAsync(
            IWorkerKitSqlDialect dialect,
            IWorkerKitBulkCopy bulk,
            DbConnection connection,
            string connectionString,
            string table)
        {
            var columns = new[]
            {
                BulkColumn<StagedRow>.For<string>("Code", row => row.Code),
                BulkColumn<StagedRow>.For<bool>("Active", row => row.Active),
                BulkColumn<StagedRow>.For<DateOnly>("ObservedOn", row => row.ObservedOn),
            };
            var empty = await bulk.BulkInsertAsync(connection, table, "Id", columns, []);
            Assert.Empty(empty);

            var inserted = await bulk.BulkInsertAsync(connection, table, "Id", columns,
            [
                new StagedRow("alpha", true, new DateOnly(2026, 4, 1)),
                new StagedRow("beta", false, new DateOnly(2026, 4, 2)),
            ]);
            Assert.Equal(2, inserted.Count);
            Assert.Equal(inserted.Distinct().Count(), inserted.Count);

            await bulk.BulkUpsertAsync(connection, table, ["Code"], columns,
            [
                new StagedRow("alpha", true, new DateOnly(2026, 4, 1)),
                new StagedRow("gamma", true, new DateOnly(2026, 4, 3)),
            ]);
            var count = await connection.QuerySingleAsync<CountRow>($"SELECT {dialect.CountExpression()} FROM {dialect.QuoteIdentifier(table)};");
            Assert.Equal(3, count.Count);

            var quoted = dialect.QuoteIdentifier(table);
            var alphaId = await connection.QuerySingleAsync<long>(
                $"SELECT {dialect.QuoteIdentifier("Id")} FROM {quoted} WHERE {dialect.QuoteIdentifier("Code")} = @Code",
                new { Code = "alpha" });
            var returned = await bulk.BulkUpsertReturningIdsAsync(connection, table, "Id", ["Code"], columns,
            [
                new StagedRow("alpha", true, new DateOnly(2026, 4, 1)),
                new StagedRow("delta", true, new DateOnly(2026, 4, 4)),
            ]);
            Assert.Equal(2, returned.Count);
            Assert.Contains(alphaId, returned);
            Assert.Equal(returned.Distinct().Count(), returned.Count);
            var afterReturning = await connection.QuerySingleAsync<CountRow>($"SELECT {dialect.CountExpression()} FROM {quoted};");
            Assert.Equal(4, afterReturning.Count);

            var observed = dialect.Provider == WorkerKitSqlProvider.SqlServer
                ? $"CONVERT(char(10), {dialect.QuoteIdentifier("ObservedOn")}, 23)"
                : $"{dialect.QuoteIdentifier("ObservedOn")}::text";
            var date = await connection.QuerySingleAsync<string>(
                $"SELECT {observed} FROM {dialect.QuoteIdentifier(table)} WHERE {dialect.QuoteIdentifier("Code")} = @Code",
                new { Code = "alpha" });
            Assert.Equal("2026-04-01", date.Trim());

            var shared = new StagedRow("shared", true, new DateOnly(2026, 5, 1));
            await Task.WhenAll(
                UpsertOneAsync(dialect, connectionString, table, columns, shared),
                UpsertOneAsync(dialect, connectionString, table, columns, shared));
            var sharedCount = await connection.QuerySingleAsync<int>(
                $"SELECT {dialect.CountExpression("Count")} FROM {dialect.QuoteIdentifier(table)} WHERE {dialect.QuoteIdentifier("Code")} = @Code",
                new { Code = "shared" });
            Assert.Equal(1, sharedCount);

            var spec = new InsertIfMissingSql(table, ["Code", "Active"], ["Code"]);
            var parameters = new { Code = "once", Active = true };
            var first = await WorkerKitInsert.InsertIfMissingAsync(dialect, connection, transaction: null, spec, parameters);
            var second = await WorkerKitInsert.InsertIfMissingAsync(dialect, connection, transaction: null, spec, parameters);
            Assert.NotNull(first);
            Assert.Equal(first, second);

            var duplicate = await Record.ExceptionAsync(() => connection.ExecuteAsync(
                $"""
                INSERT INTO {dialect.QuoteIdentifier(table)} ({dialect.QuoteIdentifier("Code")}, {dialect.QuoteIdentifier("Active")})
                VALUES (@Code, @Active);
                """,
                parameters));
            Assert.NotNull(duplicate);
            Assert.True(dialect.IsUniqueViolation(duplicate));
            Assert.False(dialect.IsTransient(duplicate));
        }

        private static async Task UpsertOneAsync(
            IWorkerKitSqlDialect dialect,
            string connectionString,
            string table,
            BulkColumn<StagedRow>[] columns,
            StagedRow row)
        {
            await using var connection = dialect.CreateConnection(connectionString);
            await connection.OpenAsync();
            await new WorkerKitBulkCopy(dialect).BulkUpsertAsync(connection, table, ["Code"], columns, [row]);
        }

        private static async Task ExerciseMigrationsAsync(
            IWorkerKitSqlDialect dialect,
            string connectionString,
            string schema,
            string probe)
        {
            var quoted = dialect.Provider == WorkerKitSqlProvider.SqlServer
                ? dialect.QuoteIdentifier("dbo." + probe)
                : dialect.QuoteIdentifier(probe);
            var migrations = dialect.Provider == WorkerKitSqlProvider.SqlServer
                ? new[]
                {
                    new SchemaMigration(1, $"CREATE TABLE {quoted} (Id int NOT NULL);\nGO\nINSERT INTO {quoted} (Id) VALUES (1);"),
                    new SchemaMigration(2, $"INSERT INTO {quoted} (Id) VALUES (2);\nGO\n"),
                }
                : new[]
                {
                    new SchemaMigration(1, $"CREATE TABLE {quoted} (id integer NOT NULL);\nINSERT INTO {quoted} (id) VALUES (1);"),
                    new SchemaMigration(2, $"INSERT INTO {quoted} (id) VALUES (2);"),
                };
            var runner = new SchemaMigrationRunner(dialect, new SchemaVersionStore(dialect), new ConnectionFactory(dialect, connectionString));
            await runner.MigrateAsync(schema, migrations);

            await using var connection = dialect.CreateConnection(connectionString);
            await connection.OpenAsync();
            Assert.Equal(2, await StoredVersionAsync(connection));
            Assert.Equal(2, await ProbeCountAsync(connection));

            await runner.MigrateAsync(schema, migrations);
            Assert.Equal(2, await StoredVersionAsync(connection));
            Assert.Equal(2, await ProbeCountAsync(connection));

            async Task<int> StoredVersionAsync(DbConnection open)
            {
                return await open.QuerySingleAsync<int>(dialect.BuildGetSchemaVersionSql(), new { SchemaName = schema });
            }

            async Task<int> ProbeCountAsync(DbConnection open)
            {
                var count = await open.QuerySingleAsync<CountRow>($"SELECT {dialect.CountExpression()} FROM {quoted};");
                return count.Count;
            }
        }

        private static async Task CreateWorkTableAsync(IWorkerKitSqlDialect dialect, DbConnection connection, string table)
        {
            var quoted = dialect.QuoteIdentifier(table);
            var sql = dialect.Provider == WorkerKitSqlProvider.SqlServer
                ? $"""
                  CREATE TABLE {quoted} (
                      {dialect.QuoteIdentifier("Id")} bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                      {dialect.QuoteIdentifier("Code")} nvarchar(100) NOT NULL,
                      {dialect.QuoteIdentifier("Active")} bit NOT NULL,
                      {dialect.QuoteIdentifier("AvailableAt")} datetime2 NOT NULL,
                      {dialect.QuoteIdentifier("LeaseUntil")} datetime2 NULL,
                      {dialect.QuoteIdentifier("Attempt")} int NOT NULL,
                      {dialect.QuoteIdentifier("LeasedBy")} nvarchar(100) NULL
                  );
                  """
                : $"""
                  CREATE TABLE {quoted} (
                      {dialect.QuoteIdentifier("Id")} bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                      {dialect.QuoteIdentifier("Code")} text NOT NULL,
                      {dialect.QuoteIdentifier("Active")} boolean NOT NULL,
                      {dialect.QuoteIdentifier("AvailableAt")} timestamptz NOT NULL,
                      {dialect.QuoteIdentifier("LeaseUntil")} timestamptz NULL,
                      {dialect.QuoteIdentifier("Attempt")} integer NOT NULL,
                      {dialect.QuoteIdentifier("LeasedBy")} text NULL
                  );
                  """;
            await connection.ExecuteAsync(sql);
        }

        private static async Task CreateBatchTableAsync(IWorkerKitSqlDialect dialect, DbConnection connection, string table)
        {
            var quoted = dialect.QuoteIdentifier(table);
            var unique = dialect.QuoteIdentifier("UQ_" + table + "_Code");
            var sql = dialect.Provider == WorkerKitSqlProvider.SqlServer
                ? $"""
                  CREATE TABLE {quoted} (
                      {dialect.QuoteIdentifier("Id")} bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                      {dialect.QuoteIdentifier("Code")} nvarchar(200) NOT NULL,
                      {dialect.QuoteIdentifier("Active")} bit NOT NULL,
                      {dialect.QuoteIdentifier("ObservedOn")} date NULL,
                      CONSTRAINT {unique} UNIQUE ({dialect.QuoteIdentifier("Code")})
                  );
                  """
                : $"""
                  CREATE TABLE {quoted} (
                      {dialect.QuoteIdentifier("Id")} bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                      {dialect.QuoteIdentifier("Code")} text NOT NULL,
                      {dialect.QuoteIdentifier("Active")} boolean NOT NULL,
                      {dialect.QuoteIdentifier("ObservedOn")} date NULL,
                      CONSTRAINT {unique} UNIQUE ({dialect.QuoteIdentifier("Code")})
                  );
                  """;
            await connection.ExecuteAsync(sql);
        }
    }

    private sealed class ConnectionFactory(IWorkerKitSqlDialect dialect, string connectionString) : IDbConnectionFactory
    {
        public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = dialect.CreateConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class LeaseRow
    {
        public long Id { get; set; }

        public int Attempt { get; set; }
    }

    private sealed class FlagRow
    {
        public long Id { get; set; }

        public bool Active { get; set; }
    }

    private sealed class CountRow
    {
        public int Count { get; set; }
    }

    private sealed class StagedRow(string code, bool active, DateOnly observedOn)
    {
        public string Code { get; set; } = code;

        public bool Active { get; set; } = active;

        public DateOnly ObservedOn { get; set; } = observedOn;
    }
}
