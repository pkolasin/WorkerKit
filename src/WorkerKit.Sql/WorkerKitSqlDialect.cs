using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace WorkerKit.Sql;

/// <summary>
/// Shared SQL Server and PostgreSQL dialect.
/// <see cref="SqlServerDialect"/> and <see cref="PostgreSqlDialect"/> select the provider.
/// The provider is fixed for the instance. <see cref="WorkerKitSql.CreateDialect"/> reads <c>Data:Provider</c> when the service is resolved.
/// </summary>
public abstract partial class WorkerKitSqlDialect : IWorkerKitSqlDialect
{
    private readonly WorkerKitSqlProvider _provider;

    /// <summary>Stores <paramref name="provider"/>.</summary>
    /// <param name="provider">Provider this instance renders.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="provider"/> is not a defined <see cref="WorkerKitSqlProvider"/> value.</exception>
    protected WorkerKitSqlDialect(WorkerKitSqlProvider provider)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider));
        }

        _provider = provider;
    }

    /// <summary>Provider passed to the constructor.</summary>
    public WorkerKitSqlProvider Provider => _provider;

    /// <summary><c>bit</c> for SQL Server and <c>boolean</c> for PostgreSQL.</summary>
    public string BooleanTypeName => IsSqlServer ? "bit" : "boolean";

    /// <summary>True for SQL Server, whose insert-if-missing statement has no <c>ON CONFLICT</c> clause.</summary>
    public bool RequiresDuplicateKeyRetry => IsSqlServer;

    /// <summary><c>SYSUTCDATETIME()</c> for SQL Server and <c>clock_timestamp()</c> for PostgreSQL.</summary>
    public string UtcNowExpression => IsSqlServer ? "SYSUTCDATETIME()" : "clock_timestamp()";

    /// <inheritdoc />
    public string QuoteIdentifier(string identifier) => SqlText.Quote(identifier, Open, Close);

    /// <inheritdoc />
    public string QuoteAlias(string alias) => QuoteIdentifier(alias);

    /// <inheritdoc />
    public string BooleanPredicate(string column, string parameterName) =>
        $"{QuoteIdentifier(column)} = {SqlText.Parameter(parameterName)}";

    /// <inheritdoc />
    public string ListPredicate(string column, string parameterName)
    {
        var quoted = QuoteIdentifier(column);
        var parameter = SqlText.Parameter(parameterName);
        return IsSqlServer ? $"{quoted} IN {parameter}" : $"{quoted} = ANY({parameter})";
    }

    /// <inheritdoc />
    public string CountExpression(string alias = "Count")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        var cast = IsSqlServer ? "CAST(COUNT_BIG(*) AS int)" : "COUNT(*)::int";
        return $"{cast} AS {QuoteAlias(alias)}";
    }

    /// <inheritdoc />
    public WorkerKitSqlErrorKind Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return ClassifyWalk(exception, 0);
    }

    /// <inheritdoc />
    public bool IsTransient(Exception exception) => Classify(exception) is WorkerKitSqlErrorKind.Deadlock
        or WorkerKitSqlErrorKind.LockTimeout
        or WorkerKitSqlErrorKind.Timeout
        or WorkerKitSqlErrorKind.SerializationFailure
        or WorkerKitSqlErrorKind.Connection;

    /// <inheritdoc />
    public bool IsUniqueViolation(Exception exception) => Classify(exception) == WorkerKitSqlErrorKind.UniqueViolation;

    /// <inheritdoc />
    public bool IsDeadlock(Exception exception) => Classify(exception) == WorkerKitSqlErrorKind.Deadlock;

    /// <summary>Classifies a SQL Server error number. 1205, 1222, -2, 2601, and 2627 are recognized.</summary>
    /// <param name="number">Value of <c>SqlException.Number</c>.</param>
    /// <returns>The kind for that number, or <see cref="WorkerKitSqlErrorKind.None"/>.</returns>
    public static WorkerKitSqlErrorKind ClassifySqlServerError(int number) => number switch
    {
        1205 => WorkerKitSqlErrorKind.Deadlock,
        1222 => WorkerKitSqlErrorKind.LockTimeout,
        -2 => WorkerKitSqlErrorKind.Timeout,
        2601 or 2627 => WorkerKitSqlErrorKind.UniqueViolation,
        _ => WorkerKitSqlErrorKind.None,
    };

    /// <summary>
    /// Classifies a PostgreSQL SQLSTATE.
    /// <c>40P01</c>, <c>40001</c>, <c>23505</c>, <c>55P03</c>, and every state that starts with <c>08</c> are recognized.
    /// </summary>
    /// <param name="sqlState">Five-character SQLSTATE, or null.</param>
    /// <returns>The kind for that state, or <see cref="WorkerKitSqlErrorKind.None"/>.</returns>
    public static WorkerKitSqlErrorKind ClassifyPostgreSqlState(string? sqlState)
    {
        if (string.IsNullOrEmpty(sqlState))
        {
            return WorkerKitSqlErrorKind.None;
        }

        if (sqlState.StartsWith("08", StringComparison.Ordinal))
        {
            return WorkerKitSqlErrorKind.Connection;
        }

        return sqlState switch
        {
            "40P01" => WorkerKitSqlErrorKind.Deadlock,
            "40001" => WorkerKitSqlErrorKind.SerializationFailure,
            "23505" => WorkerKitSqlErrorKind.UniqueViolation,
            "55P03" => WorkerKitSqlErrorKind.LockTimeout,
            _ => WorkerKitSqlErrorKind.None,
        };
    }

    /// <summary>Stable <c>pg_advisory_xact_lock</c> key for <paramref name="schemaName"/>.</summary>
    /// <param name="schemaName">Logical schema name.</param>
    /// <returns>The two key arguments.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schemaName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="schemaName"/> is empty or longer than 200 characters.</exception>
    public static (int Key1, int Key2) AdvisoryLockKey(string schemaName)
    {
        var normalized = SqlText.NormalizeSchemaName(schemaName);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("WorkerKit.Schema:" + normalized));
        return (
            BinaryPrimitives.ReadInt32LittleEndian(bytes),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
    }

    /// <inheritdoc />
    public DbConnection CreateConnection(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return IsSqlServer
            ? new SqlConnection(connectionString)
            : new NpgsqlConnection(connectionString);
    }

    /// <inheritdoc />
    public string CreateTemporaryTableName()
    {
        var token = Guid.NewGuid().ToString("N")[..16];
        return IsSqlServer ? "#wk" + token : "wk" + token;
    }

    private bool IsSqlServer => _provider == WorkerKitSqlProvider.SqlServer;

    private char Open => IsSqlServer ? '[' : '"';

    private char Close => IsSqlServer ? ']' : '"';

    private string QuoteTemporary(string name) => SqlText.QuoteTemporary(name, Open, Close, IsSqlServer);

    private WorkerKitSqlErrorKind ClassifyWalk(Exception exception, int depth)
    {
        if (depth > 16)
        {
            return WorkerKitSqlErrorKind.None;
        }

        var kind = ClassifyOne(exception);
        if (kind != WorkerKitSqlErrorKind.None)
        {
            return kind;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                kind = ClassifyWalk(inner, depth + 1);
                if (kind != WorkerKitSqlErrorKind.None)
                {
                    return kind;
                }
            }
        }

        return exception.InnerException is null
            ? WorkerKitSqlErrorKind.None
            : ClassifyWalk(exception.InnerException, depth + 1);
    }

    private WorkerKitSqlErrorKind ClassifyOne(Exception exception)
    {
        if (IsSqlServer && exception is SqlException sql)
        {
            var kind = ClassifySqlServerError(sql.Number);
            if (kind != WorkerKitSqlErrorKind.None)
            {
                return kind;
            }

            foreach (SqlError error in sql.Errors)
            {
                kind = ClassifySqlServerError(error.Number);
                if (kind != WorkerKitSqlErrorKind.None)
                {
                    return kind;
                }
            }
        }

        if (!IsSqlServer && exception is PostgresException postgres)
        {
            return ClassifyPostgreSqlState(postgres.SqlState);
        }

        return WorkerKitSqlErrorKind.None;
    }
}
