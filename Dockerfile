# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY EventosVivos.slnx ./
COPY src/Domain/EventosVivos.Domain.csproj src/Domain/
COPY src/Application/EventosVivos.Application.csproj src/Application/
COPY src/Infrastructure/EventosVivos.Infrastructure.csproj src/Infrastructure/
COPY src/Api/EventosVivos.Api.csproj src/Api/

# Restore dependencies for the API project (test projects are excluded from the Docker context)
RUN dotnet restore src/Api/EventosVivos.Api.csproj

# Copy the remaining source code
COPY src/ src/

# Build and publish the API project
RUN dotnet publish src/Api/EventosVivos.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthchecks and container debugging
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Expose the default ASP.NET Core HTTP port
EXPOSE 8080

# Copy published output from build stage
COPY --from=build /app/publish .

# Run the API. Configuration (connection strings, JWT key, etc.) is supplied via environment variables at runtime.
ENTRYPOINT ["dotnet", "EventosVivos.Api.dll"]
