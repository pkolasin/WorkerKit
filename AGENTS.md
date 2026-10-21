# WorkerKit repository

This file is for agents changing https://github.com/pkolasin/WorkerKit. A generated connector has its own `AGENTS.md`. Narrative docs are indexed in [llms.txt](llms.txt).

## Build and test

SDK 10 (`global.json`) and the .NET 8 targeting pack. The libraries multi-target `net8.0` and `net10.0`. .NET 9 projects use the `net8.0` assets. There is no `net9.0` target.

```bash
dotnet test WorkerKit.sln --nologo -warnaserror
```

Keep that command green. `TreatWarningsAsErrors` is already set. Do not weaken either switch to get a change through.

In-repo projects reference WorkerKit with `ProjectReference`. A `PackageReference` to `WorkerKit.*` fails the build, because it would compile against a cached nupkg.

## Packages

Publishable projects are WorkerKit.Abstractions, WorkerKit.Hosting, WorkerKit.Soap, WorkerKit.Sql, WorkerKit.Testing, and WorkerKit.Templates. `src/ExampleHost` and `samples/` are not packed. Local and CI packs are `0.1.1-dev.<build>`. A version with no `-dev` suffix is produced only by the release workflow from a tag. CI and release pack `src/WorkerKit.Sql` with the other libraries.

## Public API and XML docs

WorkerKit.Abstractions, WorkerKit.Hosting, WorkerKit.Soap, WorkerKit.Sql, and WorkerKit.Testing set `GenerateDocumentationFile` and treat CS1591 as an error. Every public and protected member needs a `///` comment that matches the code: `summary`, and `param`, `typeparam`, `returns`, and `exception` where they apply.

Those projects also run `Microsoft.CodeAnalysis.PublicApiAnalyzers`. A new or changed public API belongs in that project's `PublicAPI.Unshipped.txt`. Do not edit `PublicAPI.Shipped.txt` for an unreleased change. The template package turns the analyzer off. Samples, tests, and ExampleHost do not ship XML docs.

## Do not

- Edit `.github/workflows`. Action versions and release publishing are maintained outside agent changes.
- Add Creditcheck, DataOr, or other private product names. Sample names in this repo are ExampleHost, VIES, Open-Meteo, and Outbound.
- Clear the NuGet global packages folder. Template tests and documentation pack tests use a private `NUGET_PACKAGES` directory and `WorkerKitIsolatedIntermediate`.
- Invent host behavior. `README.md` and the XML docs describe the commands, the instance lock, telemetry opt-in, and `IsEnabled`.
