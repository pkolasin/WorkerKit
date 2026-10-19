# WorkerKit.Templates

Template package for a WorkerKit connector. Install the nupkg, then generate a project:

```bash
dotnet new install WorkerKit.Templates.<version>.nupkg
dotnet new workerkit-connector -n Contoso.Billing
```

`-n` is the product name and the C# namespace. It must match `^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$`. The host project is `src/<Name>.Host`, and the environment prefix is derived from that name (`Contoso.Billing` becomes `CONTOSOBILLING`).

```text
--env-prefix <prefix>       letters and digits; empty derives the prefix from the name
--framework net10.0|net8.0  host and test target framework (default net10.0)
--workerkit-version <ver>   WorkerKit package version (default is this template package's version)
--queue                     add a sample in-memory queue worker (default off)
--docker                    include Dockerfile and .dockerignore (default on)
```

`dotnet publish` of the generated host writes `deploy/` and `workerkit.app.json` from the WorkerKit.Hosting package. Build against a local WorkerKit checkout with `-p:UseLocalWorkerKit=<path>`.

Repository: https://github.com/pkolasin/WorkerKit
