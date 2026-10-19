# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props Directory.Build.targets ./
COPY src/WorkerKit.Abstractions/WorkerKit.Abstractions.csproj src/WorkerKit.Abstractions/
COPY src/WorkerKit.Hosting/WorkerKit.Hosting.csproj src/WorkerKit.Hosting/
COPY src/WorkerKit.Soap/WorkerKit.Soap.csproj src/WorkerKit.Soap/
COPY samples/Vies/WorkerKit.Samples.Vies.csproj samples/Vies/
COPY samples/OpenMeteo/WorkerKit.Samples.OpenMeteo.csproj samples/OpenMeteo/
COPY samples/Outbound/WorkerKit.Samples.Outbound.csproj samples/Outbound/
COPY src/ExampleHost/ExampleHost.csproj src/ExampleHost/
RUN dotnet restore src/ExampleHost/ExampleHost.csproj

COPY src/WorkerKit.Abstractions/ src/WorkerKit.Abstractions/
COPY src/WorkerKit.Hosting/ src/WorkerKit.Hosting/
COPY src/WorkerKit.Soap/ src/WorkerKit.Soap/
COPY samples/ samples/
COPY src/ExampleHost/ src/ExampleHost/
RUN dotnet publish src/ExampleHost/ExampleHost.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
# The runtime image already defines the non-root user app (uid $APP_UID, 1654).
# Stay root only long enough to hand /app and the instance volume to that user.
USER root
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /instance \
    && chown -R "$APP_UID:$APP_UID" /app /instance
USER $APP_UID
ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    WORKERKITEXAMPLE_INSTANCE_DIR=/instance
VOLUME /instance
ENTRYPOINT ["dotnet", "ExampleHost.dll"]
CMD ["run"]
