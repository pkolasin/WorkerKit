# WorkerKit.Sql

SQL Server and PostgreSQL helpers for a WorkerKit connector. The host does not reference a SQL client. Reference this package from the connector and call `AddWorkerKitSql`.

`Data:Provider` is `SqlServer`, `PostgreSQL`, or the alias `Postgres`. The dialect and `IDbConnectionFactory` read that value when they are first resolved, not when `AddWorkerKitSql` runs. A test can call `UseSetting("Data:Provider", ...)` after `ConfigureHost` and still select the provider. `Data:ConnectionString` is required to open a connection. `None` skips the database probe. `Dapper` still means the connector registers its own `IDbConnectionFactory`.

```csharp
builder.Services.AddWorkerKitSql();
```

```json
{
  "Data": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=register"
  }
}
```

## Dialect

`IWorkerKitSqlDialect` quotes identifiers. PostgreSQL aliases are quoted because an unquoted alias is folded to lower case. List predicates are `column IN @values` on SQL Server and `column = ANY(@values)` on PostgreSQL: Dapper expands a list parameter only for SqlClient. Boolean predicates bind a `bool` (`bit` or `boolean`). Do not embed `1`/`0` or `true`/`false`.

`CountExpression` is `CAST(COUNT_BIG(*) AS int)` or `COUNT(*)::int`. Map it onto an `int` property. A provider `COUNT` without the cast is a 64-bit integer.

Claim SQL takes available rows whose lease is null or expired. SQL Server uses `READPAST` and `UPDLOCK` and returns rows with `OUTPUT`. PostgreSQL uses `FOR UPDATE SKIP LOCKED` and `RETURNING`. Both increment the attempt. `BuildClaimByIdSql` reclaims one expired row and increments the attempt. `BuildRenewLeaseSql` extends a lease the named worker still holds and does not increment the attempt.

`WorkerKitInsert.InsertIfMissingAsync` inserts a row when its key is missing. PostgreSQL uses `ON CONFLICT DO NOTHING` and re-reads the id. SQL Server inserts and, on errors 2601 or 2627, re-reads the id. Call the SQL Server path without a transaction: a duplicate key aborts the transaction that contained the insert. The returned id is cast to `long`.

## Bulk copy

`IWorkerKitBulkCopy` stages rows into a temporary table. SQL Server uses `SqlBulkCopy` into a `#temp` table. PostgreSQL uses binary `COPY` into a `TEMP` table. An empty row list does not touch the database.

`BulkInsertAsync` returns the generated ids. `BulkUpsertAsync` inserts keys that are not already present, ordered by the key, inside a short transaction. A unique violation or a deadlock retries that insert once, then re-selects. The destination needs a unique constraint on the key. `BulkUpsertReturningIdsAsync` returns those ids ordered by the key.

On a register ingestion workload, PostgreSQL binary `COPY` plus `SKIP LOCKED` scaled as parallel writers were added. The same shape on SQL Server (`SqlBulkCopy` and `READPAST`/`UPDLOCK`) did not.

## Schema migrations

`SchemaMigrationRunner` applies `SchemaMigration` scripts whose version is greater than the stored version. It does not downgrade. SQL Server takes `sp_getapplock` and splits the script on a line whose trimmed text is `GO`. PostgreSQL takes `pg_advisory_xact_lock` and rejects `GO`. The version table is `worker_kit_schema_version`.

## Dapper results

Map query results to a class with settable properties. A positional record can fail when the provider returns a different CLR type than the constructor parameter. PostgreSQL `bigint` and `date` arrive as `long` and `DateOnly`. SQL Server `COUNT` and `date` arrive as `long` and `DateTime` unless the statement casts them. Cast counts with `CountExpression`.

## Tests

Unit tests cover both dialects and do not need Docker. One test asserts that every PostgreSQL statement contains no T-SQL keywords. Integration tests use Testcontainers against `mcr.microsoft.com/mssql/server:2022-latest` and `postgres:17`. They are marked `Category=Integration` and skip unless `WORKERKIT_INTEGRATION` is `1`, so the default `dotnet test` run does not start Docker.

```bash
dotnet test WorkerKit.sln --nologo -warnaserror
```

```bat
set WORKERKIT_INTEGRATION=1
dotnet test tests/WorkerKit.Sql.Tests/WorkerKit.Sql.Tests.csproj --nologo -warnaserror --filter Category=Integration
```

Repository: https://github.com/pkolasin/WorkerKit
