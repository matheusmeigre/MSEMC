# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore (layer caching optimization)
COPY MSEMC.csproj .
RUN dotnet restore

# Copy remaining source and publish
COPY . .
RUN dotnet publish MSEMC.csproj -c Release -o /app --no-restore

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Security: run as non-root user + install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 1000 appgroup \
    && useradd --uid 1000 --gid 1000 --create-home appuser

WORKDIR /app
COPY --from=build /app .

# Set non-root user
USER appuser

# Railway injects $PORT dynamically — let ASP.NET Core bind to it at runtime.
# Do NOT hardcode ASPNETCORE_URLS here; use Program.cs or railway.toml instead.
EXPOSE 8080

# Health check (uses $PORT with fallback to 8080)
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:${PORT:-8080}/health || exit 1

ENTRYPOINT ["dotnet", "MSEMC.dll"]
