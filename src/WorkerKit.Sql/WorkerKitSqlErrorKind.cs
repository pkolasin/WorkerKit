namespace WorkerKit.Sql;

/// <summary>
/// Classification of a provider exception a connector may retry or treat as an idempotent conflict.
/// Unique violations are classified so insert-if-missing can re-read the existing row.
/// They are not <see cref="IWorkerKitSqlDialect.IsTransient"/>: repeating the same insert does not make the duplicate go away.
/// </summary>
public enum WorkerKitSqlErrorKind
{
    /// <summary>The exception is not one of the classified provider codes.</summary>
    None = 0,

    /// <summary>SQL Server 1205 or PostgreSQL <c>40P01</c>.</summary>
    Deadlock = 1,

    /// <summary>SQL Server 1222 or PostgreSQL <c>55P03</c>.</summary>
    LockTimeout = 2,

    /// <summary>SQL Server command timeout, error number -2.</summary>
    Timeout = 3,

    /// <summary>SQL Server 2601 or 2627, or PostgreSQL <c>23505</c>.</summary>
    UniqueViolation = 4,

    /// <summary>PostgreSQL serialization failure <c>40001</c>.</summary>
    SerializationFailure = 5,

    /// <summary>PostgreSQL connection exception whose SQLSTATE starts with <c>08</c>.</summary>
    Connection = 6,
}
