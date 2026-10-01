# Multi-stage build: the SDK image compiles, the much smaller ASP.NET runtime image runs the app as a non-root user.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so this layer is cached until dependencies change.
COPY Directory.Build.props Directory.Packages.props .editorconfig UrlShortener.slnx ./
COPY src/UrlShortener.Core/UrlShortener.Core.csproj src/UrlShortener.Core/
COPY src/UrlShortener.Infrastructure/UrlShortener.Infrastructure.csproj src/UrlShortener.Infrastructure/
COPY src/UrlShortener.Api/UrlShortener.Api.csproj src/UrlShortener.Api/
RUN dotnet restore src/UrlShortener.Api/UrlShortener.Api.csproj

COPY src/ src/
RUN dotnet publish src/UrlShortener.Api/UrlShortener.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The SQLite file lives in /data (mount a volume to keep it). Created as root, then owned by the built-in
# non-root "app" user ($APP_UID) that the container runs as.
RUN mkdir -p /data && chown "$APP_UID" /data
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Links="Data Source=/data/urlshortener.db"
VOLUME /data
EXPOSE 8080

# Probes for an orchestrator: GET /health/live (process up) and GET /health/ready (database reachable).
ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]
