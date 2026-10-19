using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting.Cli.Help;

/// <summary>
/// Hand-written tail of the CLI reference. These switches are not options on the command tree:
/// <c>run</c> and <c>check-config</c> forward unmatched tokens to the generic host.
/// </summary>
public static class HostConfigurationNotes
{
    public const string Heading = "Host configuration overrides";

    public static string For(WorkerAppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return $"""
            `run` and `check-config` leave unmatched tokens in place (`TreatUnmatchedTokensAsErrors` is false) and pass the original arguments to the generic host. Those overrides are not options in the tree above.

            - `--Section:Key value` sets a configuration key. Example: `--Worker:PollIntervalSeconds 30`.
            - `Section__Key` is the environment-variable form. Example: `Worker__PollIntervalSeconds=30`.
            - `--environment <name>` selects the host environment. `DOTNET_ENVIRONMENT` is the same setting. An instance `appsettings.json` cannot set it.
            - `Telemetry:ServiceName` overrides the OpenTelemetry service name. The default is `{identity.Name}`.
            - `OTEL_EXPORTER_OTLP_ENDPOINT` turns on the OTLP exporter. With it unset, telemetry still records traces and metrics in process and does not export them. `OTEL_EXPORTER_OTLP_PROTOCOL` is `grpc` (the default) or `http/protobuf`. `OTEL_EXPORTER_OTLP_HEADERS` is a comma-separated list of `key=value` pairs. Log export uses `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`, `OTEL_EXPORTER_OTLP_LOGS_PROTOCOL`, and `OTEL_EXPORTER_OTLP_LOGS_HEADERS` when those are set.
            - `--telemetry` records traces and metrics for one connector command. The default is off. A command can turn the same pipeline on for every invocation with `EnableTelemetry`. `run` always records telemetry and ignores this flag. `check-config` accepts the flag and ignores it. `help` and `version` accept the flag, ignore it, and still do not build a host. A short command flushes traces, metrics, and the Serilog OTLP sink before it returns.

            Instance options and `--telemetry` are recursive on the root command, so every command accepts them. Built-in help (`<command> --help`) shows `--instance-dir`, `--instance`, and `--telemetry`. A subcommand's `Options` collection does not include those recursive ancestors, so the tree above lists them on the root only.

            - `--instance-dir <path>`, or `{identity.InstanceDirectoryVariable}`.
            - `--instance <name>`, or `{identity.InstanceVariable}`. With no directory, the directory is `{identity.InstancesRootVariable}/<name>`.
            - `{identity.InstancesRootVariable}` defaults to `%ProgramData%\\{identity.Name}\\instances` on Windows and `{identity.LinuxInstancesRoot}` on Linux.
            - With nothing set, the instance is `default` and its directory is the install directory.
            """;
    }
}
