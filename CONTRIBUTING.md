# Contributing

WorkerKit lives at https://github.com/pkolasin/WorkerKit. The libraries under `src/WorkerKit.*` are the packages. `src/ExampleHost` and `samples/` are examples and are not published.

## Build and test

Install the .NET SDK 10 (see `global.json`) and the .NET 8 targeting pack, then run:

```bash
dotnet test WorkerKit.sln --nologo -warnaserror
```

A local pack is a prerelease, `0.1.1-dev.local`, unless `WorkerKitBuildNumber` is set. Publishing to nuget.org is done by the release workflow from a `v*` tag, not from a pull request.

## Pull requests

Open a pull request against `main`. Keep the test command above green. Describe the behavior change in the pull request. Security reports go through [SECURITY.md](SECURITY.md), not a public issue.
