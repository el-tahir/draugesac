# Docker Build Instructions for Draugesac.Api

This document provides instructions for building and running the Draugesac.Api application using Docker.

## Prerequisites

- Docker installed on your system
- .NET 8.0 SDK (for local development)

## Building the Docker Image

### From the Solution Root

The Dockerfile is designed to be built from the solution root directory to properly handle project dependencies.

```bash
# Navigate to the solution root (where Draugesac.sln is located)
cd /path/to/draugesac

# Build the Docker image
docker build -f src/Draugesac.Api/Dockerfile -t draugesac-api:latest .
```

### Build Arguments and Tags

```bash
# Build with a specific tag
docker build -f src/Draugesac.Api/Dockerfile -t draugesac-api:v1.0.0 .

# Build for a specific environment
docker build -f src/Draugesac.Api/Dockerfile -t draugesac-api:production .
```

## Running the Container

### Basic Run

```bash
# Run the container
docker run -p 8080:8080 draugesac-api:latest
```

### Run with Environment Variables

```bash
# Run with custom configuration
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ASPNETCORE_URLS=http://+:8080 \
  draugesac-api:latest
```

### Run in Development Mode

```bash
# Run with development settings and volume mounting
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -v $(pwd)/src/Draugesac.Api/appsettings.Development.json:/app/appsettings.Development.json \
  draugesac-api:latest
```

## Docker Compose (Future)

When infrastructure services are added, you can use Docker Compose:

```yaml
# docker-compose.yml (example)
version: '3.8'
services:
  api:
    build:
      context: .
      dockerfile: src/Draugesac.Api/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - database
      - messagequeue
```

## Health Checks

The container includes a health check endpoint at `/healthz`:

```bash
# Check if the container is healthy
curl http://localhost:8080/healthz
```

## API Documentation

Once running, access the Swagger UI at:
- http://localhost:8080/swagger

## Available Endpoints

- `GET /healthz` - Health check endpoint
- `GET /api/test` - Test endpoint showing application layer access
- `POST /api/documents` - Upload documents (stub)
- `POST /api/documents/{id}/redactions` - Start redaction jobs (stub)
- `GET /api/documents/{id}` - Get document status (stub)

## Troubleshooting

### Build Issues

1. **Context Issues**: Make sure you're building from the solution root
2. **Dependency Issues**: Ensure all project references are properly restored
3. **Permission Issues**: Check Docker daemon permissions

### Runtime Issues

1. **Port Conflicts**: Use a different port mapping if 8080 is in use
2. **Health Check Failures**: Check logs with `docker logs <container-id>`
3. **Environment Issues**: Verify environment variables are set correctly

### Logs

```bash
# View container logs
docker logs <container-id>

# Follow logs in real-time
docker logs -f <container-id>
```

## Security Notes

- The container runs as a non-root user (`appuser`)
- Only the published application files are included in the final image
- No sensitive development files are copied (see .dockerignore)
- Health checks ensure the application is responsive

## Image Size Optimization

The multi-stage build ensures:
- Only runtime dependencies in the final image
- No SDK or build tools in production
- Minimal attack surface
- Faster startup times 