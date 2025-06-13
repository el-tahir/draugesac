# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY Draugesac.sln ./

# Copy project files for restore
COPY src/Draugesac.Domain/Draugesac.Domain.csproj src/Draugesac.Domain/
COPY src/Draugesac.Application/Draugesac.Application.csproj src/Draugesac.Application/
COPY src/Draugesac.Infrastructure/Draugesac.Infrastructure.csproj src/Draugesac.Infrastructure/
COPY src/Draugesac.Api/Draugesac.Api.csproj src/Draugesac.Api/

# Restore dependencies
RUN dotnet restore src/Draugesac.Api/Draugesac.Api.csproj

# Copy source code
COPY src/Draugesac.Domain/ src/Draugesac.Domain/
COPY src/Draugesac.Application/ src/Draugesac.Application/
COPY src/Draugesac.Infrastructure/ src/Draugesac.Infrastructure/
COPY src/Draugesac.Api/ src/Draugesac.Api/

# Build the application
WORKDIR /src/src/Draugesac.Api
RUN dotnet build Draugesac.Api.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish Draugesac.Api.csproj -c Release -o /app/publish --no-restore

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create a non-root user for security
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published application
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Draugesac.Api.dll"] 