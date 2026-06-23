# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY EventosVivos.slnx ./
COPY src/Domain/EventosVivos.Domain.csproj src/Domain/
COPY src/Application/EventosVivos.Application.csproj src/Application/
COPY src/Infrastructure/EventosVivos.Infrastructure.csproj src/Infrastructure/
COPY src/Api/EventosVivos.Api.csproj src/Api/
COPY tests/Domain.Tests/EventosVivos.Domain.Tests.csproj tests/Domain.Tests/
COPY tests/Application.Tests/EventosVivos.Application.Tests.csproj tests/Application.Tests/
COPY tests/Infrastructure.Tests/EventosVivos.Infrastructure.Tests.csproj tests/Infrastructure.Tests/
COPY tests/Integration.Tests/EventosVivos.Integration.Tests.csproj tests/Integration.Tests/

# Restore dependencies
RUN dotnet restore EventosVivos.slnx

# Copy the remaining source code
COPY . .

# Build and publish the API project
RUN dotnet publish src/Api/EventosVivos.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Expose the default ASP.NET Core HTTP port
EXPOSE 8080

# Copy published output from build stage
COPY --from=build /app/publish .

# Run the API. Configuration (connection strings, JWT key, etc.) is supplied via environment variables at runtime.
ENTRYPOINT ["dotnet", "EventosVivos.Api.dll"]
