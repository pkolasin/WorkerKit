# WorkerKit.Abstractions

Contracts for a WorkerKit connector:

- queues (`IWorkQueue<T>`, `WorkItem<T>`, `IWorkItemHandler<T>`, `PermanentWorkItemException`)
- worker heartbeats and iteration observers
- `IInstanceContext`
- `WorkerAppIdentity`
- `IDbConnectionFactory` (no database implementation in this package; `WorkerKit.Sql` registers one for `SqlServer` and `PostgreSQL`)
- framework option types (`Health`, `Data`, `Instance`, `LogFile`, `Queue`, HTTP client endpoints)
- reflection-free commands (`IWorkerCommand`, `CommandSpec`, `CommandOption<T>`, `CommandArgument<T>`, `EnumCommandOption<TEnum>`, `EnumCommandArgument<TEnum>`, `ICommandArgs`)

This package does not reference the generic host, Serilog, OpenTelemetry, or System.CommandLine.
A command declares a static `CommandSpec`. `WorkerKit.Hosting` turns that spec into System.CommandLine and resolves the command from DI when it runs.

Repository: https://github.com/pkolasin/WorkerKit
