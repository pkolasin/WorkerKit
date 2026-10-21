namespace WorkerKit.Sql;

/// <summary>
/// Provider-specific SQL for a connector that stores work in SQL Server or PostgreSQL.
/// Identifiers are quoted. PostgreSQL aliases are quoted because an unquoted alias is folded to lower case.
/// List predicates do not use <c>IN @values</c> on PostgreSQL: Dapper expands a list parameter only for SqlClient.
/// Boolean predicates bind a <see cref="bool"/> rather than a <c>1</c>/<c>0</c> or <c>true</c>/<c>false</c> literal.
/// </summary>
public interface IWorkerKitSqlDialect
{
    /// <summary>Provider this dialect renders.</summary>
    WorkerKitSqlProvider Provider { get; }

    /// <summary>Provider boolean type: <c>bit</c> on SQL Server and <c>boolean</c> on PostgreSQL.</summary>
    string BooleanTypeName { get; }

    /// <summary>
    /// True when insert-if-missing is a plain insert and a unique violation means the row is already there.
    /// SQL Server is true. PostgreSQL is false because the statement uses <c>ON CONFLICT DO NOTHING</c>.
    /// </summary>
    bool RequiresDuplicateKeyRetry { get; }

    /// <summary>SQL expression for the current UTC time. SQL Server uses <c>SYSUTCDATETIME()</c>. PostgreSQL uses <c>clock_timestamp()</c>.</summary>
    string UtcNowExpression { get; }

    /// <summary>Quotes a table or column identifier. A schema prefix is <c>schema.table</c>.</summary>
    /// <param name="identifier">Unquoted identifier.</param>
    /// <returns>The quoted identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identifier"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is empty or is not a simple name.</exception>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// Quotes a result alias so the reader keeps the caller's casing.
    /// PostgreSQL folds an unquoted alias to lower case.
    /// </summary>
    /// <param name="alias">Alias text.</param>
    /// <returns>The quoted alias.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="alias"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="alias"/> is empty or is not a simple name.</exception>
    string QuoteAlias(string alias);

    /// <summary>
    /// Equality predicate that binds a <see cref="bool"/> parameter.
    /// The SQL does not contain a boolean literal.
    /// </summary>
    /// <param name="column">Column name.</param>
    /// <param name="parameterName">Parameter name, with or without a leading <c>@</c>.</param>
    /// <returns><c>column = @parameter</c> with the column quoted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="column"/> or <paramref name="parameterName"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty or is not a simple name.</exception>
    string BooleanPredicate(string column, string parameterName);

    /// <summary>
    /// Membership predicate for a bound list.
    /// SQL Server returns <c>column IN @parameter</c> because Dapper expands lists only on SqlClient.
    /// PostgreSQL returns <c>column = ANY(@parameter)</c> and the caller binds an array.
    /// </summary>
    /// <param name="column">Column name.</param>
    /// <param name="parameterName">Parameter name, with or without a leading <c>@</c>.</param>
    /// <returns>The provider-specific predicate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="column"/> or <paramref name="parameterName"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty or is not a simple name.</exception>
    string ListPredicate(string column, string parameterName);

    /// <summary>
    /// <c>COUNT</c> cast to a 32-bit integer and aliased.
    /// SQL Server <c>COUNT_BIG</c> and PostgreSQL <c>count</c> both return a 64-bit integer unless they are cast.
    /// Dapper then maps the column onto an <see cref="int"/> property.
    /// </summary>
    /// <param name="alias">Result alias. The default is <c>Count</c>.</param>
    /// <returns>The select expression, including <c>AS</c> and the quoted alias.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="alias"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="alias"/> is empty or is not a simple name.</exception>
    string CountExpression(string alias = "Count");

    /// <summary>Classifies <paramref name="exception"/>, including inner exceptions, for this provider.</summary>
    /// <param name="exception">Exception thrown by the provider.</param>
    /// <returns>The first classified kind, or <see cref="WorkerKitSqlErrorKind.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    WorkerKitSqlErrorKind Classify(Exception exception);

    /// <summary>
    /// True when the operation can be retried: deadlock, lock timeout, command timeout, serialization failure, or a PostgreSQL <c>08</c> connection error.
    /// A unique violation is false.
    /// </summary>
    /// <param name="exception">Exception thrown by the provider.</param>
    /// <returns>True when <see cref="Classify"/> is a transient kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    bool IsTransient(Exception exception);

    /// <summary>True when <paramref name="exception"/> is a unique-constraint or unique-index violation.</summary>
    /// <param name="exception">Exception thrown by the provider.</param>
    /// <returns>True when <see cref="Classify"/> is <see cref="WorkerKitSqlErrorKind.UniqueViolation"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    bool IsUniqueViolation(Exception exception);

    /// <summary>True when <paramref name="exception"/> is a deadlock.</summary>
    /// <param name="exception">Exception thrown by the provider.</param>
    /// <returns>True when <see cref="Classify"/> is <see cref="WorkerKitSqlErrorKind.Deadlock"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    bool IsDeadlock(Exception exception);

    /// <summary>
    /// Claims up to <see cref="WorkLeaseSql.MaxParameter"/> available rows whose lease is null or expired.
    /// SQL Server locks the candidates with <c>READPAST</c> and <c>UPDLOCK</c> and returns them with <c>OUTPUT</c>.
    /// PostgreSQL locks them with <c>FOR UPDATE SKIP LOCKED</c> and returns them with <c>RETURNING</c>.
    /// Both statements increment the attempt and set the lease and the worker.
    /// </summary>
    /// <param name="lease">Table, columns, and parameter names.</param>
    /// <returns>The claim statement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty, or no returning column was set.</exception>
    string BuildClaimSql(WorkLeaseSql lease);

    /// <summary>
    /// Reclaims one row by id when its lease is null or expired, and increments the attempt.
    /// A row another worker still holds is skipped.
    /// </summary>
    /// <param name="lease">Table, columns, and parameter names.</param>
    /// <returns>The claim-by-id statement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty, or no returning column was set.</exception>
    string BuildClaimByIdSql(WorkLeaseSql lease);

    /// <summary>
    /// Extends the lease of one row when <see cref="WorkLeaseSql.WorkerParameter"/> still holds it and the lease has not expired.
    /// The attempt column is not changed.
    /// </summary>
    /// <param name="lease">Table, columns, and parameter names.</param>
    /// <returns>The renew statement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty, or no returning column was set.</exception>
    string BuildRenewLeaseSql(WorkLeaseSql lease);

    /// <summary>
    /// Inserts one row when the key is missing.
    /// PostgreSQL appends <c>ON CONFLICT DO NOTHING</c>.
    /// SQL Server is a plain insert whose <c>OUTPUT</c> reads <c>inserted</c> and <see cref="InsertIfMissingSql.IdColumn"/>. <see cref="RequiresDuplicateKeyRetry"/> tells the caller to re-read after errors 2601 and 2627.
    /// The returned id is cast to <see cref="long"/>.
    /// </summary>
    /// <param name="insert">Table, columns, and key.</param>
    /// <returns>The insert statement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="insert"/> is null.</exception>
    /// <exception cref="ArgumentException">A key column is not one of the inserted columns, or a name is empty.</exception>
    string BuildInsertIfMissingSql(InsertIfMissingSql insert);

    /// <summary>Selects the surrogate id of the row whose key matches the bound parameters.</summary>
    /// <param name="insert">Table and key. <see cref="InsertIfMissingSql.Columns"/> is not used.</param>
    /// <returns>The select statement. The id is cast to <see cref="long"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="insert"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty.</exception>
    string BuildSelectIdByKeySql(InsertIfMissingSql insert);

    /// <summary>
    /// Splits a migration script into batches.
    /// SQL Server splits on a line whose trimmed text is <c>GO</c> and does not send that line.
    /// PostgreSQL returns one batch and rejects a <c>GO</c> separator.
    /// </summary>
    /// <param name="script">Migration script.</param>
    /// <returns>Batches in source order. Empty batches are omitted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="script"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The script contains <c>GO</c> and this provider does not use SQLCMD batches.</exception>
    IReadOnlyList<string> SplitBatches(string script);

    /// <summary>
    /// Command text that takes the schema lock.
    /// SQL Server calls <c>sp_getapplock</c>. PostgreSQL calls <c>pg_advisory_xact_lock</c>.
    /// <see cref="AcquireSchemaLockAsync"/> binds the parameters and runs it.
    /// </summary>
    /// <returns>The lock statement.</returns>
    string BuildSchemaLockSql();

    /// <summary>
    /// Takes the exclusive schema lock on <paramref name="transaction"/>.
    /// SQL Server waits in <c>sp_getapplock</c> with the transaction as the lock owner.
    /// PostgreSQL waits in <c>pg_advisory_xact_lock</c>, which releases when the transaction ends.
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Transaction that owns the lock.</param>
    /// <param name="schemaName">Logical schema name. It is not a database identifier. At most 200 characters.</param>
    /// <param name="cancellationToken">Token that cancels the lock command.</param>
    /// <returns>A task that completes when the lock is held.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/>, <paramref name="transaction"/>, or <paramref name="schemaName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="schemaName"/> is empty or longer than 200 characters.</exception>
    /// <exception cref="InvalidOperationException">SQL Server returns a negative <c>sp_getapplock</c> code.</exception>
    Task AcquireSchemaLockAsync(
        DbConnection connection,
        DbTransaction transaction,
        string schemaName,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an unopened connection for <see cref="Provider"/>.</summary>
    /// <param name="connectionString">Provider connection string.</param>
    /// <returns>A <c>SqlConnection</c> or an <c>NpgsqlConnection</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connectionString"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is empty or white space.</exception>
    DbConnection CreateConnection(string connectionString);

    /// <summary>
    /// New temporary table name for one staging operation.
    /// SQL Server names start with <c>#</c>. PostgreSQL names do not.
    /// </summary>
    /// <returns>The table name, without quotes.</returns>
    string CreateTemporaryTableName();

    /// <summary>Creates a temporary table with nullable columns chosen from <paramref name="columns"/>.</summary>
    /// <param name="tableName">Name from <see cref="CreateTemporaryTableName"/> or the caller's temporary name.</param>
    /// <param name="columns">Column names and CLR types.</param>
    /// <returns>The <c>CREATE</c> statement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tableName"/> or <paramref name="columns"/> is null.</exception>
    /// <exception cref="ArgumentException">The name is not valid for this provider, the column list is empty, or a CLR type has no mapping.</exception>
    string BuildCreateTemporaryTableSql(string tableName, IReadOnlyList<BulkColumnDefinition> columns);

    /// <summary>Drops a temporary table if it exists.</summary>
    /// <param name="tableName">Temporary table name.</param>
    /// <returns>The <c>DROP</c> statement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tableName"/> is null.</exception>
    /// <exception cref="ArgumentException">The name is not valid for this provider.</exception>
    string BuildDropTemporaryTableSql(string tableName);

    /// <summary>
    /// Inserts every staged row into <paramref name="destinationTable"/> and returns the new ids cast to <see cref="long"/>.
    /// The destination id is an identity or generated column and is not part of <paramref name="columns"/>.
    /// </summary>
    /// <param name="destinationTable">Permanent table.</param>
    /// <param name="stageTable">Temporary table that already holds the rows.</param>
    /// <param name="idColumn">Identity or generated id column.</param>
    /// <param name="columns">Data columns, in insert order.</param>
    /// <returns>The insert statement.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">A name is empty or <paramref name="columns"/> is empty.</exception>
    string BuildBulkInsertReturningIdsSql(
        string destinationTable,
        string stageTable,
        string idColumn,
        IReadOnlyList<string> columns);

    /// <summary>
    /// Inserts staged rows whose key is not already in <paramref name="destinationTable"/>, ordered by <paramref name="keyColumns"/>.
    /// The statement runs inside a short transaction. A unique violation or deadlock is the signal to re-select.
    /// </summary>
    /// <param name="destinationTable">Permanent table.</param>
    /// <param name="stageTable">Temporary table that already holds the rows.</param>
    /// <param name="columns">Data columns.</param>
    /// <param name="keyColumns">Unique key. Each one must also be in <paramref name="columns"/>.</param>
    /// <returns>The insert statement.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">A list is empty, a key column is missing, or a name is empty.</exception>
    string BuildInsertMissingSql(
        string destinationTable,
        string stageTable,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> keyColumns);

    /// <summary>
    /// Selects the ids of destination rows whose key is present in the stage table.
    /// The id is cast to <see cref="long"/> and the rows are ordered by the key.
    /// </summary>
    /// <param name="destinationTable">Permanent table.</param>
    /// <param name="stageTable">Temporary table.</param>
    /// <param name="idColumn">Id column.</param>
    /// <param name="keyColumns">Join key.</param>
    /// <returns>The select statement.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">The key list is empty or a name is empty.</exception>
    string BuildReselectIdsSql(
        string destinationTable,
        string stageTable,
        string idColumn,
        IReadOnlyList<string> keyColumns);

    /// <summary>Counts distinct staged keys. The count is cast to <see cref="int"/>.</summary>
    /// <param name="stageTable">Temporary table.</param>
    /// <param name="keyColumns">Key columns.</param>
    /// <returns>The select statement. The alias is <c>Count</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stageTable"/> or <paramref name="keyColumns"/> is null.</exception>
    /// <exception cref="ArgumentException">The key list is empty or a name is empty.</exception>
    string BuildCountStagedKeysSql(string stageTable, IReadOnlyList<string> keyColumns);

    /// <summary>Counts distinct staged keys that already exist in <paramref name="destinationTable"/>. The count is cast to <see cref="int"/>.</summary>
    /// <param name="destinationTable">Permanent table.</param>
    /// <param name="stageTable">Temporary table.</param>
    /// <param name="keyColumns">Join key.</param>
    /// <returns>The select statement. The alias is <c>Count</c>.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentException">The key list is empty or a name is empty.</exception>
    string BuildCountMatchedKeysSql(string destinationTable, string stageTable, IReadOnlyList<string> keyColumns);

    /// <summary>Creates <c>worker_kit_schema_version</c> when it is missing.</summary>
    /// <returns>The create statement.</returns>
    string BuildEnsureSchemaVersionTableSql();

    /// <summary>Reads the applied version for <c>@SchemaName</c>. The column is a 32-bit integer.</summary>
    /// <returns>The select statement.</returns>
    string BuildGetSchemaVersionSql();

    /// <summary>Inserts or updates the applied version for <c>@SchemaName</c> and <c>@Version</c>.</summary>
    /// <returns>The upsert statement.</returns>
    string BuildUpsertSchemaVersionSql();
}
