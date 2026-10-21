using Npgsql;
using WorkerKit.Sql;

namespace WorkerKit.Sql.Tests;

public sealed class ErrorClassificationTests
{
    [Theory]
    [InlineData(1205, WorkerKitSqlErrorKind.Deadlock, true)]
    [InlineData(1222, WorkerKitSqlErrorKind.LockTimeout, true)]
    [InlineData(-2, WorkerKitSqlErrorKind.Timeout, true)]
    [InlineData(2601, WorkerKitSqlErrorKind.UniqueViolation, false)]
    [InlineData(2627, WorkerKitSqlErrorKind.UniqueViolation, false)]
    [InlineData(547, WorkerKitSqlErrorKind.None, false)]
    public void Sql_server_numbers_classify_without_treating_unique_violations_as_transient(
        int number,
        WorkerKitSqlErrorKind kind,
        bool transient)
    {
        Assert.Equal(kind, WorkerKitSqlDialect.ClassifySqlServerError(number));
        var dialect = new SqlServerDialect();
        if (kind == WorkerKitSqlErrorKind.None)
        {
            Assert.False(dialect.IsTransient(new InvalidOperationException(number.ToString())));
            return;
        }

        Assert.Equal(transient, kind is not WorkerKitSqlErrorKind.UniqueViolation);
    }

    [Theory]
    [InlineData("40P01", WorkerKitSqlErrorKind.Deadlock, true)]
    [InlineData("40001", WorkerKitSqlErrorKind.SerializationFailure, true)]
    [InlineData("23505", WorkerKitSqlErrorKind.UniqueViolation, false)]
    [InlineData("55P03", WorkerKitSqlErrorKind.LockTimeout, true)]
    [InlineData("08006", WorkerKitSqlErrorKind.Connection, true)]
    [InlineData("08P01", WorkerKitSqlErrorKind.Connection, true)]
    [InlineData("42P01", WorkerKitSqlErrorKind.None, false)]
    [InlineData("", WorkerKitSqlErrorKind.None, false)]
    [InlineData(null, WorkerKitSqlErrorKind.None, false)]
    public void PostgreSQL_states_classify_connection_errors_and_unique_violations(
        string? state,
        WorkerKitSqlErrorKind kind,
        bool transient)
    {
        Assert.Equal(kind, WorkerKitSqlDialect.ClassifyPostgreSqlState(state));
        if (string.IsNullOrEmpty(state) || kind == WorkerKitSqlErrorKind.None)
        {
            return;
        }

        var exception = new PostgresException(state, "ERROR", "ERROR", state);
        var wrapped = new AggregateException(new InvalidOperationException("batch", exception));
        var dialect = new PostgreSqlDialect();
        Assert.Equal(kind, dialect.Classify(exception));
        Assert.Equal(kind, dialect.Classify(wrapped));
        Assert.Equal(transient, dialect.IsTransient(wrapped));
        Assert.Equal(kind == WorkerKitSqlErrorKind.UniqueViolation, dialect.IsUniqueViolation(exception));
        Assert.Equal(kind == WorkerKitSqlErrorKind.Deadlock, dialect.IsDeadlock(exception));
    }

    [Fact]
    public void An_unrelated_exception_is_not_transient()
    {
        var exception = new InvalidOperationException("not a provider error");
        Assert.Equal(WorkerKitSqlErrorKind.None, new SqlServerDialect().Classify(exception));
        Assert.False(new PostgreSqlDialect().IsTransient(exception));
        Assert.False(new SqlServerDialect().IsUniqueViolation(exception));
        Assert.False(new PostgreSqlDialect().IsDeadlock(exception));
    }
}
