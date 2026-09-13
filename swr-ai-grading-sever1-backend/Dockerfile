# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files first
COPY ["src/WebApi/WebApi.csproj", "src/WebApi/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/WebApi/WebApi.csproj"

# Copy everything
COPY . .

# Build
RUN dotnet build "src/WebApi/WebApi.csproj" \
    -c Release \
    --no-restore \
    -o /app/build

# Publish
RUN dotnet publish "src/WebApi/WebApi.csproj" \
    -c Release \
    --no-restore \
    -o /app/publish


# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

# Render uses PORT, so ASP.NET listens on all interfaces
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "WebApi.dll"]