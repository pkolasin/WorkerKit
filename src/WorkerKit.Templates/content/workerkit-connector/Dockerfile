# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:__DOTNET_IMAGE_TAG__ AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props Directory.Build.targets ./
COPY src/Contoso.Billing.Host/Contoso.Billing.Host.csproj src/Contoso.Billing.Host/
RUN dotnet restore src/Contoso.Billing.Host/Contoso.Billing.Host.csproj

COPY src/Contoso.Billing.Host/ src/Contoso.Billing.Host/
RUN dotnet publish src/Contoso.Billing.Host/Contoso.Billing.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:__DOTNET_IMAGE_TAG__ AS final
# The runtime image already defines the non-root user app (uid $APP_UID).
USER root
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /instance \
    && chown -R "$APP_UID:$APP_UID" /app /instance
USER $APP_UID
ENV DOTNET_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    CONTOSOBILLING_INSTANCE_DIR=/instance
VOLUME /instance
ENTRYPOINT ["dotnet", "Contoso.Billing.Host.dll"]
CMD ["run"]
