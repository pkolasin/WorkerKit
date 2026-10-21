namespace WorkerKit.Sql;

/// <summary>
/// Table and parameter names for a leased work queue.
/// <see cref="IWorkerKitSqlDialect.BuildClaimSql"/> claims available rows whose lease is missing or expired and increments the attempt.
/// <see cref="IWorkerKitSqlDialect.BuildClaimByIdSql"/> does that for one id.
/// <see cref="IWorkerKitSqlDialect.BuildRenewLeaseSql"/> extends a lease the caller still holds and does not increment the attempt.
/// </summary>
public sealed class WorkLeaseSql
{
    /// <summary>Stores <paramref name="table"/> and the default column and parameter names.</summary>
    /// <param name="table">Queue table name. A schema prefix is <c>schema.table</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="table"/> is empty or white space.</exception>
    public WorkLeaseSql(string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        Table = table;
    }

    /// <summary>Queue table the statements update.</summary>
    public string Table { get; }

    /// <summary>Primary key column.</summary>
    /// <remarks>The initializer is <c>Id</c>.</remarks>
    public string IdColumn { get; init; } = "Id";

    /// <summary>Column that says when the row may be claimed.</summary>
    /// <remarks>The initializer is <c>AvailableAt</c>.</remarks>
    public string AvailableAtColumn { get; init; } = "AvailableAt";

    /// <summary>Column that stores the lease expiry.</summary>
    /// <remarks>The initializer is <c>LeaseUntil</c>. Null or a value in the past means the row can be claimed.</remarks>
    public string LeaseUntilColumn { get; init; } = "LeaseUntil";

    /// <summary>Column incremented by claim and claim-by-id.</summary>
    /// <remarks>The initializer is <c>Attempt</c>. Renew does not change it. The column is <c>NOT NULL</c>.</remarks>
    public string AttemptColumn { get; init; } = "Attempt";

    /// <summary>Column that stores the worker holding the lease.</summary>
    /// <remarks>The initializer is <c>LeasedBy</c>.</remarks>
    public string LeasedByColumn { get; init; } = "LeasedBy";

    /// <summary>Columns returned by the statement, in order.</summary>
    /// <remarks>The initializer is <c>Id</c> and <c>Attempt</c>. Aliases keep this casing. PostgreSQL would fold an unquoted alias.</remarks>
    public IReadOnlyList<string> ReturningColumns { get; init; } = ["Id", "Attempt"];

    /// <summary>Name of the parameter that limits a batch claim.</summary>
    /// <remarks>The initializer is <c>Max</c>. Bind an <see cref="int"/>.</remarks>
    public string MaxParameter { get; init; } = "Max";

    /// <summary>Name of the parameter that holds the lease length in seconds.</summary>
    /// <remarks>The initializer is <c>LeaseSeconds</c>. Bind an <see cref="int"/>.</remarks>
    public string LeaseSecondsParameter { get; init; } = "LeaseSeconds";

    /// <summary>Name of the parameter that identifies the worker.</summary>
    /// <remarks>The initializer is <c>Worker</c>.</remarks>
    public string WorkerParameter { get; init; } = "Worker";

    /// <summary>Name of the parameter that identifies one row for claim-by-id and renew.</summary>
    /// <remarks>The initializer is <c>Id</c>.</remarks>
    public string IdParameter { get; init; } = "Id";
}
