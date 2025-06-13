# Draugesac

**A cloud-native document redaction system built with .NET 8, AWS services, and Docker.**

Draugesac is a microservices-based application that allows users to upload documents, specify sensitive phrases for redaction, and download sanitized versions. The system uses AWS Lambda for serverless document processing, S3 for storage, DynamoDB for metadata, and SQS for asynchronous job processing.

## 🏗️ Architecture

- **Frontend**: Blazor Server UI for user interactions
- **Backend API**: ASP.NET Core 8 Web API for document management
- **Processing**: AWS Lambda function for document redaction
- **Storage**: AWS S3 for document files
- **Database**: AWS DynamoDB for document metadata
- **Messaging**: AWS SQS for asynchronous job processing
- **Infrastructure**: AWS CDK for infrastructure as code

## 🚀 Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [AWS CLI](https://aws.amazon.com/cli/) configured with appropriate credentials
- [AWS CDK CLI](https://docs.aws.amazon.com/cdk/v2/guide/getting_started.html)
- [Node.js](https://nodejs.org/) (for AWS CDK)

### 1. Build Lambda Function

First, build the Lambda function that will be deployed as part of the infrastructure:

```bash
# Navigate to Lambda function directory
cd lambda/Draugesac.Processor/src/Draugesac.Processor

# Build and publish for deployment
dotnet publish -c Release -o ./bin/Release/net8.0/publish
```

### 2. Deploy AWS Infrastructure

Deploy the required AWS resources using CDK:

```bash
# Navigate to infrastructure directory
cd infra

# Install dependencies and build
dotnet build src

# Bootstrap CDK (first time only)
cdk bootstrap

# Deploy infrastructure
cdk deploy
```

This creates:
- S3 bucket for document storage
- DynamoDB table for document metadata
- SQS queue for processing jobs
- Lambda function for document redaction
- IAM roles with least-privilege permissions

### 3. Configure Environment Variables

Set the following environment variables using the values from your CDK deployment output:

```bash
export AWS_REGION=us-east-1
export AWS_S3_BUCKET_NAME=<your-bucket-name-from-cdk-output>
export AWS_DYNAMODB_TABLE_NAME=<your-table-name-from-cdk-output>
export AWS_SQS_QUEUE_URL=<your-queue-url-from-cdk-output>
```

**Getting the values**: After running `cdk deploy`, use the output values:
- `DocumentsBucketName` for `AWS_S3_BUCKET_NAME`
- `DocumentsTableName` for `AWS_DYNAMODB_TABLE_NAME`  
- `JobQueueUrl` for `AWS_SQS_QUEUE_URL`

### 4. Run the Backend API

```bash
# Navigate to project root
cd ../../../..

# Start the API service using Docker Compose
docker compose up --build
```

The API will be available at `http://localhost:8080`

### 5. Run the Frontend UI

```bash
# Navigate to UI project
cd src/Draugesac.UI

# Run the Blazor application
dotnet run
```

The UI will be available at `https://localhost:7156` (or the port shown in console)

## 🧪 Testing

### API Testing

Use the included HTTP file for API testing:

```bash
# Test API endpoints
curl -X GET http://localhost:8080/api/documents
```

### Upload and Redaction Workflow

1. **Upload Document**: Use the UI or API to upload a PDF, DOC, DOCX, or TXT file
2. **Request Redaction**: Specify phrases to redact from the document
3. **Monitor Status**: Check processing status via UI or API
4. **Download Result**: Download the redacted document when processing completes

## 🔧 Development

### Project Structure

```
draugesac/
├── src/
│   ├── Draugesac.Api/          # Web API for document management
│   ├── Draugesac.Application/   # Business logic and interfaces
│   ├── Draugesac.Domain/        # Domain entities and enums
│   ├── Draugesac.Infrastructure/ # AWS service implementations
│   └── Draugesac.UI/           # Blazor Server frontend
├── lambda/
│   └── Draugesac.Processor/    # AWS Lambda function for processing
├── infra/                      # AWS CDK infrastructure code
└── docker-compose.yml          # Local development setup
```

### Running Individual Services

#### API Only
```bash
cd src/Draugesac.Api
dotnet run
```

#### UI Only
```bash
cd src/Draugesac.UI
dotnet run
```

#### Lambda Function Testing
```bash
cd lambda/Draugesac.Processor/src/Draugesac.Processor
dotnet test
```

## 🔒 Security

- **Environment Variables**: All secrets are externalized to environment variables
- **IAM Least Privilege**: AWS roles follow principle of least privilege
- **Secure Defaults**: S3 buckets block public access, DynamoDB tables use encryption
- **Input Validation**: File type and size validation on uploads
- **Presigned URLs**: Secure, temporary access to stored documents

## 🌍 Environment Configuration

### Development
- Uses local Docker environment for API
- Connects to AWS services for processing and storage
- Detailed logging for debugging

### Production
- Deploy API to AWS ECS or EC2 using provided IAM role
- All services run in AWS for optimal performance
- CloudWatch logging and monitoring

## 📚 Documentation

- **API Documentation**: Available at `/swagger` when running the API
- **Architecture Decisions**: See `docs/` directory (if created)
- **Code Documentation**: Comprehensive XML documentation in source code

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🚨 Troubleshooting

### Common Issues

1. **AWS Credentials**: Ensure AWS CLI is configured with proper credentials
2. **Docker Issues**: Verify Docker Desktop is running and you have sufficient resources
3. **Port Conflicts**: Check if ports 8080 or 7156 are already in use
4. **Lambda Timeout**: Increase timeout in CDK configuration for large documents

### Getting Help

- Check application logs in Docker Compose output
- Review AWS CloudWatch logs for Lambda function
- Verify environment variables are set correctly
- Ensure AWS resources are deployed successfully with `aws s3 ls` and similar commands
