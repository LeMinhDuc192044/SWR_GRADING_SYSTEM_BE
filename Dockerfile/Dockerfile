# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything and restore. Simpler than copying individual .csproj files
# one-by-one, at the cost of a slightly less cacheable Docker layer.
COPY . .
RUN dotnet restore

# Publish only the WebApi project (the entry point), even though the solution
# has multiple projects (Domain, Application, Infrastructure).
RUN dotnet publish WebApi/WebApi.csproj -c Release -o /app/publish --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Informational only — Render ignores this for routing, but keeps the file
# accurate for anyone reading it. Render injects the real PORT at runtime.
EXPOSE 8080

# Shell form (not exec/array form) so ${PORT} is expanded by the shell at
# container startup, when Render actually sets it — a plain ENTRYPOINT array
# would try to pass the literal string "$PORT" to dotnet instead of its value.
ENTRYPOINT ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet WebApi.dll
