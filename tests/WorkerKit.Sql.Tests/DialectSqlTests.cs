using System.Text.RegularExpressions;
using WorkerKit.Sql;

namespace WorkerKit.Sql.Tests;

public sealed class DialectSqlTests
{
    public static TheoryData<IWorkerKitSqlDialect> Dialects { get; } = new()
    {
        new SqlServerDialect(),
        new PostgreSqlDialect(),
    };

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Quotes_simple_names_and_rejects_anything_else(IWorkerKitSqlDialect dialect)
    {
        var open = dialect.Provider == WorkerKitSqlProvider.SqlServer ? "[" : "\"";
        var close = dialect.Provider == WorkerKitSqlProvider.SqlServer ? "]" : "\"";
        Assert.Equal($"{open}Item{close}", dialect.QuoteIdentifier("Item"));
        Assert.Equal($"{open}dbo{close}.{open}Item{close}", dialect.QuoteIdentifier("dbo.Item"));
        Assert.Equal(dialect.QuoteIdentifier("Attempt"), dialect.QuoteAlias("Attempt"));
        Assert.Throws<ArgumentException>(() => dialect.QuoteIdentifier("bad-name"));
        Assert.Throws<ArgumentException>(() => dialect.QuoteIdentifier("1Item"));
        Assert.Throws<ArgumentException>(() => dialect.QuoteAlias(""));
    }

    [Fact]
    public void Boolean_and_list_predicates_follow_the_provider()
    {
        var sqlServer = new SqlServerDialect();
        var postgres = new PostgreSqlDialect();

        Assert.Equal("bit", sqlServer.BooleanTypeName);
        Assert.Equal("boolean", postgres.BooleanTypeName);
        Assert.Equal("[Active] = @Active", sqlServer.BooleanPredicate("Active", "@Active"));
        Assert.Equal("\"Active\" = @Active", postgres.BooleanPredicate("Active", "Active"));
        Assert.DoesNotContain("true", postgres.BooleanPredicate("Active", "Active"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("false", sqlServer.BooleanPredicate("Active", "Active"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("[Code] IN @Codes", sqlServer.ListPredicate("Code", "Codes"));
        Assert.Equal("\"Code\" = ANY(@Codes)", postgres.ListPredicate("Code", "@Codes"));
        Assert.Equal("CAST(COUNT_BIG(*) AS int) AS [Count]", sqlServer.CountExpression());
        Assert.Equal("COUNT(*)::int AS \"Count\"", postgres.CountExpression("Count"));
        Assert.True(sqlServer.RequiresDuplicateKeyRetry);
        Assert.False(postgres.RequiresDuplicateKeyRetry);
        Assert.Equal("SYSUTCDATETIME()", sqlServer.UtcNowExpression);
        Assert.Equal("clock_timestamp()", postgres.UtcNowExpression);
    }

    [Fact]
    public void Lease_statements_use_the_provider_lock_and_returning_clause()
    {
        var lease = new WorkLeaseSql("WorkItem");
        var sqlServer = new SqlServerDialect();
        var postgres = new PostgreSqlDialect();

        var claim = sqlServer.BuildClaimSql(lease);
        Assert.Contains("READPAST", claim, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK", claim, StringComparison.Ordinal);
        Assert.Contains("ROWLOCK", claim, StringComparison.Ordinal);
        Assert.Contains("TOP (@Max)", claim, StringComparison.Ordinal);
        Assert.Contains("OUTPUT", claim, StringComparison.Ordinal);
        Assert.Contains("[Attempt] = target.[Attempt] + 1", claim, StringComparison.Ordinal);
        Assert.DoesNotContain("SKIP LOCKED", claim, StringComparison.Ordinal);

        var reclaim = sqlServer.BuildClaimByIdSql(lease);
        Assert.Contains("READPAST", reclaim, StringComparison.Ordinal);
        Assert.Contains("[Attempt] = target.[Attempt] + 1", reclaim, StringComparison.Ordinal);
        Assert.Contains("@Id", reclaim, StringComparison.Ordinal);

        var renew = sqlServer.BuildRenewLeaseSql(lease);
        Assert.Contains("UPDLOCK", renew, StringComparison.Ordinal);
        Assert.Contains("ROWLOCK", renew, StringComparison.Ordinal);
        Assert.DoesNotContain("READPAST", renew, StringComparison.Ordinal);
        Assert.DoesNotContain("+ 1", renew, StringComparison.Ordinal);
        Assert.Contains("target.[LeasedBy] = @Worker", renew, StringComparison.Ordinal);

        var postgresClaim = postgres.BuildClaimSql(lease);
        Assert.Contains("FOR UPDATE SKIP LOCKED", postgresClaim, StringComparison.Ordinal);
        Assert.Contains("RETURNING", postgresClaim, StringComparison.Ordinal);
        Assert.Contains("\"Attempt\" AS \"Attempt\"", postgresClaim, StringComparison.Ordinal);
        Assert.Contains("LIMIT @Max", postgresClaim, StringComparison.Ordinal);
        Assert.Contains("target.\"Attempt\" + 1", postgresClaim, StringComparison.Ordinal);

        var postgresRenew = postgres.BuildRenewLeaseSql(lease);
        Assert.DoesNotContain("+ 1", postgresRenew, StringComparison.Ordinal);
        Assert.Contains("target.\"LeasedBy\" = @Worker", postgresRenew, StringComparison.Ordinal);
        Assert.Contains("RETURNING", postgresRenew, StringComparison.Ordinal);
    }

    [Fact]
    public void Insert_if_missing_uses_conflict_handling_only_on_postgresql()
    {
        var insert = new InsertIfMissingSql("Register", ["Code", "Active"], ["Code"]);
        var sqlServer = new SqlServerDialect().BuildInsertIfMissingSql(insert);
        var postgres = new PostgreSqlDialect().BuildInsertIfMissingSql(insert);

        Assert.Contains("OUTPUT CAST([Id] AS bigint) AS [Id]", sqlServer, StringComparison.Ordinal);
        Assert.DoesNotContain("ON CONFLICT", sqlServer, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (\"Code\") DO NOTHING", postgres, StringComparison.Ordinal);
        Assert.Contains("RETURNING \"Id\"::bigint AS \"Id\"", postgres, StringComparison.Ordinal);
        Assert.Contains("\"Code\" = @Code", new PostgreSqlDialect().BuildSelectIdByKeySql(insert), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new SqlServerDialect().BuildInsertIfMissingSql(
            new InsertIfMissingSql("Register", ["Code"], ["Missing"])));
    }

    [Fact]
    public void Bulk_and_schema_sql_follow_the_provider()
    {
        var sqlServer = new SqlServerDialect();
        var postgres = new PostgreSqlDialect();
        var columns = new BulkColumnDefinition[] { new("Code", typeof(string)), new("Active", typeof(bool)), new("ObservedOn", typeof(DateOnly)) };

        var sqlTemp = sqlServer.CreateTemporaryTableName();
        var pgTemp = postgres.CreateTemporaryTableName();
        Assert.StartsWith("#wk", sqlTemp, StringComparison.Ordinal);
        Assert.StartsWith("wk", pgTemp, StringComparison.Ordinal);
        Assert.DoesNotContain('#', pgTemp);

        var sqlCreate = sqlServer.BuildCreateTemporaryTableSql(sqlTemp, columns);
        var pgCreate = postgres.BuildCreateTemporaryTableSql(pgTemp, columns);
        Assert.Contains("nvarchar(max)", sqlCreate, StringComparison.Ordinal);
        Assert.Contains("bit", sqlCreate, StringComparison.Ordinal);
        Assert.Contains("date", sqlCreate, StringComparison.Ordinal);
        Assert.DoesNotContain("TEMP", sqlCreate, StringComparison.Ordinal);
        Assert.StartsWith("CREATE TEMP TABLE", pgCreate, StringComparison.Ordinal);
        Assert.Contains("boolean", pgCreate, StringComparison.Ordinal);
        Assert.Contains("text", pgCreate, StringComparison.Ordinal);

        var missing = sqlServer.BuildInsertMissingSql("Register", sqlTemp, ["Code", "Active"], ["Code"]);
        Assert.Contains("WITH (UPDLOCK, ROWLOCK)", missing, StringComparison.Ordinal);
        Assert.Contains("ORDER BY stage.[Code]", missing, StringComparison.Ordinal);
        Assert.Contains("ROW_NUMBER()", missing, StringComparison.Ordinal);
        var pgMissing = postgres.BuildInsertMissingSql("Register", pgTemp, ["Code", "Active"], ["Code"]);
        Assert.Contains("ORDER BY stage.\"Code\"", pgMissing, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDLOCK", pgMissing, StringComparison.Ordinal);

        Assert.Contains("OUTPUT CAST(inserted.[Id] AS bigint) AS [Id]", sqlServer.BuildBulkInsertReturningIdsSql("Register", sqlTemp, "Id", ["Code"]), StringComparison.Ordinal);
        Assert.Contains("RETURNING \"Id\"::bigint AS \"Id\"", postgres.BuildBulkInsertReturningIdsSql("Register", pgTemp, "Id", ["Code"]), StringComparison.Ordinal);
        Assert.Contains("CAST(COUNT_BIG(*) AS int)", sqlServer.BuildCountStagedKeysSql(sqlTemp, ["Code"]), StringComparison.Ordinal);
        Assert.Contains("COUNT(*)::int", postgres.BuildCountMatchedKeysSql("Register", pgTemp, ["Code"]), StringComparison.Ordinal);

        Assert.Contains("sp_getapplock", sqlServer.BuildSchemaLockSql(), StringComparison.Ordinal);
        Assert.Contains("pg_advisory_xact_lock(@Key1, @Key2)", postgres.BuildSchemaLockSql(), StringComparison.Ordinal);
        Assert.Contains("nvarchar(200)", sqlServer.BuildEnsureSchemaVersionTableSql(), StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS", postgres.BuildEnsureSchemaVersionTableSql(), StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (schema_name)", postgres.BuildUpsertSchemaVersionSql(), StringComparison.Ordinal);
        Assert.Contains("SYSUTCDATETIME()", sqlServer.BuildUpsertSchemaVersionSql(), StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSQL_sql_contains_no_T_SQL_keywords()
    {
        var dialect = new PostgreSqlDialect();
        var lease = new WorkLeaseSql("WorkItem");
        var insert = new InsertIfMissingSql("Register", ["Code", "Active"], ["Code"]);
        var stage = dialect.CreateTemporaryTableName();
        var columns = new[] { "Code", "Active" };
        var corpus = string.Join(
            "\n",
            dialect.BooleanPredicate("Active", "Active"),
            dialect.ListPredicate("Code", "Codes"),
            dialect.CountExpression(),
            dialect.UtcNowExpression,
            dialect.BuildClaimSql(lease),
            dialect.BuildClaimByIdSql(lease),
            dialect.BuildRenewLeaseSql(lease),
            dialect.BuildInsertIfMissingSql(insert),
            dialect.BuildSelectIdByKeySql(insert),
            dialect.BuildSchemaLockSql(),
            dialect.BuildCreateTemporaryTableSql(stage, [new BulkColumnDefinition("Code", typeof(string)), new BulkColumnDefinition("Active", typeof(bool))]),
            dialect.BuildDropTemporaryTableSql(stage),
            dialect.BuildBulkInsertReturningIdsSql("Register", stage, "Id", columns),
            dialect.BuildInsertMissingSql("Register", stage, columns, ["Code"]),
            dialect.BuildReselectIdsSql("Register", stage, "Id", ["Code"]),
            dialect.BuildCountStagedKeysSql(stage, ["Code"]),
            dialect.BuildCountMatchedKeysSql("Register", stage, ["Code"]),
            dialect.BuildEnsureSchemaVersionTableSql(),
            dialect.BuildGetSchemaVersionSql(),
            dialect.BuildUpsertSchemaVersionSql(),
            stage);

        Assert.DoesNotContain('#', corpus);
        foreach (var keyword in new[]
        {
            "READPAST", "UPDLOCK", "ROWLOCK", "HOLDLOCK", "NOLOCK", "XLOCK",
            "OUTPUT", "INSERTED", "DELETED", "SYSUTCDATETIME", "GETUTCDATE", "GETDATE",
            "DATEADD", "sp_getapplock", "NVARCHAR", "DATETIME2", "MERGE", "TOP", "NOCOUNT",
        })
        {
            Assert.False(
                Regex.IsMatch(corpus, $@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                $"PostgreSQL SQL contains T-SQL keyword {keyword}.");
        }

        Assert.Contains("SKIP LOCKED", corpus, StringComparison.Ordinal);
        Assert.Contains("RETURNING", corpus, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT", corpus, StringComparison.Ordinal);
        Assert.Contains("= ANY(", corpus, StringComparison.Ordinal);
        Assert.Contains("::int", corpus, StringComparison.Ordinal);
        Assert.Contains("pg_advisory_xact_lock", corpus, StringComparison.Ordinal);
        Assert.Contains("clock_timestamp", corpus, StringComparison.Ordinal);
        Assert.Contains("boolean", corpus, StringComparison.Ordinal);
    }

    [Fact]
    public void Go_splits_sql_server_batches_and_is_rejected_on_postgresql()
    {
        var script = "SELECT 1\r\nGO\r\nSELECT 2\n  go  \n";
        var batches = new SqlServerDialect().SplitBatches(script);
        Assert.Equal(2, batches.Count);
        Assert.Equal("SELECT 1", batches[0]);
        Assert.Equal("SELECT 2", batches[1]);
        Assert.Empty(new SqlServerDialect().SplitBatches("  GO  \n"));
        var exception = Assert.Throws<InvalidOperationException>(() => new PostgreSqlDialect().SplitBatches("SELECT 1\nGO\n"));
        Assert.Contains("GO", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["SELECT 1"], new PostgreSqlDialect().SplitBatches("SELECT 1"));
    }

    [Fact]
    public void Advisory_lock_key_is_stable()
    {
        var first = WorkerKitSqlDialect.AdvisoryLockKey(" register ");
        var second = WorkerKitSqlDialect.AdvisoryLockKey("register");
        Assert.Equal(first, second);
        Assert.NotEqual(first, WorkerKitSqlDialect.AdvisoryLockKey("other"));
        Assert.Throws<ArgumentException>(() => WorkerKitSqlDialect.AdvisoryLockKey(""));
        Assert.Throws<ArgumentException>(() => WorkerKitSqlDialect.AdvisoryLockKey(new string('a', 201)));
    }
}
