# Draugesac - Document Processing & Redaction System

A .NET 8.0 document processing system built with Clean Architecture principles, designed for document upload, processing, and automated redaction capabilities.

## 🏗️ Architecture

The solution follows Clean Architecture with clear separation of concerns:

- **Draugesac.Domain** - Core business entities and enums
- **Draugesac.Application** - Business logic and interfaces  
- **Draugesac.Api** - REST API presentation layer

## 🚀 Quick Start

### Using Docker Compose (Recommended)

```bash
# Start the application
docker-compose up -d

# View logs
docker-compose logs -f draugesac-api

# Access the API
open http://localhost:8080/swagger
```

### Using .NET CLI

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the API
cd src/Draugesac.Api
dotnet run
```

## 📋 Prerequisites

- Docker and Docker Compose (recommended)
- OR .NET 8.0 SDK for local development
- At least 2GB RAM for containerized deployment

## 🔧 Available Endpoints

| Method | Endpoint | Purpose | Status |
|--------|----------|---------|--------|
| `GET` | `/healthz` | Health check | ✅ Active |
| `GET` | `/api/test` | Application layer test | ✅ Active |
| `POST` | `/api/documents` | Upload documents | 🚧 Stub |
| `POST` | `/api/documents/{id}/redactions` | Start redaction jobs | 🚧 Stub |
| `GET` | `/api/documents/{id}` | Get document status | 🚧 Stub |

## 🐳 Docker Support

### Development
```bash
docker-compose up -d
```

### Production
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

See [docker-compose.md](docker-compose.md) for detailed Docker instructions.

## 📁 Project Structure

```
draugesac/
├── src/
│   ├── Draugesac.Domain/           # Core entities and enums
│   │   ├── Entities/Document.cs
│   │   └── Enums/DocumentStatus.cs
│   ├── Draugesac.Application/      # Business logic layer
│   │   ├── Interfaces/
│   │   │   ├── IDocumentRepository.cs
│   │   │   ├── IFileStore.cs
│   │   │   └── IMessagePublisher.cs
│   │   └── Class1.cs (DocumentService)
│   └── Draugesac.Api/              # API presentation layer
│       ├── Controllers/DocumentsController.cs
│       ├── Program.cs
│       ├── Dockerfile
│       └── .dockerignore
├── docker-compose.yml              # Development container config
├── docker-compose.prod.yml         # Production container config
├── docker-compose.override.yml     # Dev overrides
├── docker-compose.md               # Docker documentation
├── Draugesac.sln                   # Solution file
└── README.md                       # This file
```

## 🏃‍♂️ Development Workflow

### 1. Setup
```bash
git clone <repository-url>
cd draugesac
docker-compose up -d
```

### 2. Development
```bash
# View logs
docker-compose logs -f draugesac-api

# Rebuild after changes
docker-compose up -d --build

# Test endpoints
curl http://localhost:8080/healthz
curl http://localhost:8080/api/test
```

### 3. Testing
```bash
# Run tests (when implemented)
dotnet test

# API testing via Swagger UI
open http://localhost:8080/swagger
```

## 🔮 Planned Features

### Phase 1 - Infrastructure
- [ ] Database implementation (PostgreSQL)
- [ ] File storage implementation (AWS S3 / LocalStack)
- [ ] Message queue setup (RabbitMQ)
- [ ] Caching layer (Redis)

### Phase 2 - Core Features  
- [ ] Document upload with validation
- [ ] Asynchronous document processing
- [ ] Text extraction and analysis
- [ ] Automated redaction engine
- [ ] Download management

### Phase 3 - Advanced Features
- [ ] User authentication & authorization
- [ ] Audit logging and compliance
- [ ] Advanced redaction rules
- [ ] Batch processing capabilities
- [ ] API rate limiting

## 🚨 Current Status

The project is in **initial development phase** with:
- ✅ Clean Architecture foundation established
- ✅ Docker containerization complete
- ✅ API endpoints scaffolded (stubs)
- ✅ Health monitoring implemented
- 🚧 Business logic implementation pending
- 🚧 Infrastructure services pending

## 🛠️ Technology Stack

- **Framework**: .NET 8.0
- **Architecture**: Clean Architecture
- **API**: ASP.NET Core Web API
- **Documentation**: Swagger/OpenAPI
- **Containerization**: Docker & Docker Compose
- **Health Checks**: Built-in ASP.NET Core

### Planned Technologies
- **Database**: PostgreSQL
- **File Storage**: AWS S3 (LocalStack for development)
- **Message Queue**: RabbitMQ
- **Caching**: Redis
- **Monitoring**: Health checks, logging

## 📚 Documentation

- [Docker Setup Guide](docker-compose.md) - Comprehensive Docker instructions
- [API Build Guide](src/Draugesac.Api/docker-build.md) - Container build details
- [Swagger UI](http://localhost:8080/swagger) - Interactive API documentation (when running)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test with Docker Compose
5. Submit a pull request

## 📄 License

[Add your license information here]

## 🆘 Support

For issues and questions:
1. Check the [Docker documentation](docker-compose.md)
2. Review container logs: `docker-compose logs draugesac-api`
3. Verify health status: `curl http://localhost:8080/healthz`