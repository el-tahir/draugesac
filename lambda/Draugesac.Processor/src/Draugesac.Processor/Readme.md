# Draugesac Document Processor

**AWS Lambda function for serverless document redaction processing.**

This Lambda function processes document redaction jobs triggered by SQS messages. It downloads documents from S3, performs text redaction based on specified phrases, and uploads the redacted documents back to S3.

## 🏗️ Architecture

The Lambda function integrates with several AWS services:
- **SQS**: Receives redaction job messages
- **S3**: Downloads original documents and uploads redacted versions
- **DynamoDB**: Updates document status and metadata
- **CloudWatch**: Logs processing activity and errors

## ⚡ Function Overview

### Core Functionality
- **Message Processing**: Handles SQS events with redaction job payloads
- **Document Download**: Retrieves original documents from S3
- **Text Redaction**: Replaces sensitive phrases with asterisks using regex patterns
- **Document Upload**: Stores redacted documents back to S3
- **Status Updates**: Tracks processing status in DynamoDB

### Performance Features
- **Memory Optimization**: Uses StringBuilder for efficient string manipulation
- **Stream Processing**: Minimal memory footprint for large documents
- **Timeout Handling**: Cancellation token support for long-running operations
- **Duplicate Prevention**: HashSet-based phrase deduplication
- **Regex Optimization**: Word boundary matching with timeout protection

## 🚀 Deployment

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [AWS CLI](https://aws.amazon.com/cli/) configured with appropriate permissions
- [AWS Lambda Tools](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools)

### Install Lambda Tools

```bash
# Install AWS Lambda Tools globally
dotnet tool install -g Amazon.Lambda.Tools

# Update to latest version
dotnet tool update -g Amazon.Lambda.Tools
```

### Build and Package

```bash
# Navigate to the function directory
cd lambda/Draugesac.Processor/src/Draugesac.Processor

# Build the project
dotnet build -c Release

# Create deployment package
dotnet publish -c Release -o ./bin/Release/net8.0/publish
```

### Deploy via CDK (Recommended)

The function is deployed automatically as part of the CDK infrastructure stack:

```bash
# From project root
cd infra
cdk deploy
```

### Manual Deployment (Alternative)

```bash
# Deploy function directly
dotnet lambda deploy-function --function-name DraugesacProcessor

# Update function code only
dotnet lambda update-function-code --function-name DraugesacProcessor
```

## 🔧 Configuration

### Environment Variables

The function expects these environment variables (set by CDK):

```bash
AWS__S3BucketName=draugesac-documents-{account-id}-{region}
AWS__DynamoDbTableName=DraugesacDocuments-CDK
```

### Function Settings

Configured via CDK in `InfraStack.cs`:
- **Runtime**: .NET 8
- **Memory**: 2048 MB (adjustable based on document size)
- **Timeout**: 5 minutes (adjustable based on processing time)
- **Architecture**: x86_64

### Retry Configuration

- **SQS Visibility Timeout**: 6 minutes (> Lambda timeout)
- **SQS Message Retention**: 4 days
- **Error Handling**: Failed messages go to DLQ for investigation

## 🧪 Testing

### Local Testing

```bash
# Run unit tests (if available)
cd lambda/Draugesac.Processor/test/Draugesac.Processor.Tests
dotnet test

# Test locally with sample event
dotnet lambda invoke-function --payload sample-sqs-event.json
```

### Integration Testing

1. **Upload Test Document**: Use the API to upload a test document
2. **Trigger Redaction**: Request redaction with test phrases
3. **Monitor Logs**: Check CloudWatch logs for processing activity
4. **Verify Results**: Download and verify the redacted document

### Sample SQS Event

```json
{
  "Records": [
    {
      "messageId": "test-message-id",
      "body": "{\"documentId\":\"550e8400-e29b-41d4-a716-446655440000\",\"phrasesToRedact\":[\"sensitive\",\"confidential\"]}"
    }
  ]
}
```

## 📊 Monitoring

### CloudWatch Metrics

Monitor these key metrics:
- **Invocations**: Function execution count
- **Duration**: Processing time per document
- **Errors**: Failed redaction attempts
- **Throttles**: Concurrent execution limits

### Logging

The function provides detailed logging:
```csharp
_logger.LogInformation("🎯 Lambda invocation started. Processing {Count} messages", evnt.Records.Count);
_logger.LogInformation("✅ Document {DocumentId} processed successfully", documentId);
_logger.LogError("❌ Error processing document {DocumentId}: {Error}", documentId, ex.Message);
```

### Alerts

Set up CloudWatch alarms for:
- Error rate > 5%
- Duration > 4 minutes (approaching timeout)
- Memory usage > 1.8GB

## 🔒 Security

### IAM Permissions

The Lambda execution role has minimal permissions:
- **S3**: Read/write access to documents bucket only
- **DynamoDB**: Read/write access to documents table only
- **SQS**: Consume messages from redaction queue only
- **CloudWatch**: Write logs to function log group only

### Data Protection

- **In-Transit**: HTTPS for all AWS service communications
- **At-Rest**: Server-side encryption for S3 and DynamoDB
- **Processing**: Documents processed in memory only, no disk storage
- **Cleanup**: Automatic memory cleanup after each invocation

## ⚠️ Error Handling

### Exception Management

```csharp
try
{
    // Process document
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    _logger.LogError("⏰ Processing timed out");
    throw new TimeoutException("Processing timeout");
}
catch (AmazonS3Exception ex)
{
    _logger.LogError("S3 error: {Message}", ex.Message);
    await UpdateDocumentStatus(document, DocumentStatus.Failed);
    throw;
}
```

### Retry Strategy

- **SQS Retries**: Automatic retry with exponential backoff
- **AWS Service Retries**: Built-in SDK retry logic
- **Timeout Protection**: Cancellation tokens prevent indefinite processing
- **Dead Letter Queue**: Failed messages preserved for investigation

## 📈 Performance Optimization

### Memory Management

- **StringBuilder**: Efficient string manipulation for large documents
- **Stream Processing**: Minimal memory allocation for file operations
- **Resource Disposal**: Proper using statements for all disposable resources
- **Garbage Collection**: Optimized object lifecycle management

### Processing Efficiency

- **Regex Caching**: Compiled patterns for repeated use
- **Duplicate Removal**: HashSet prevents processing duplicate phrases
- **Word Boundaries**: Accurate matching prevents partial word redaction
- **Batch Processing**: Single SQS message per invocation for simplicity

## 🚨 Troubleshooting

### Common Issues

1. **Timeout Errors**: Increase Lambda timeout or reduce document size limits
2. **Memory Errors**: Increase memory allocation or optimize processing
3. **S3 Access Denied**: Verify IAM permissions and bucket policies
4. **DynamoDB Throttling**: Check table capacity or use on-demand billing

### Debugging

```bash
# View recent logs
aws logs tail /aws/lambda/DraugesacProcessor-CDK --follow

# Check function configuration
aws lambda get-function --function-name DraugesacProcessor-CDK

# Monitor SQS queue
aws sqs get-queue-attributes --queue-url $SQS_QUEUE_URL --attribute-names All
```

### Performance Tuning

- **Memory**: Start with 2048MB, adjust based on document size
- **Timeout**: Start with 5 minutes, increase for large documents
- **Concurrency**: Monitor and adjust reserved concurrency if needed
- **Batch Size**: Keep at 1 for simple error handling

## 🔗 Related Documentation

- [AWS Lambda Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/)
- [.NET on AWS Lambda](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html)
- [Lambda Best Practices](https://docs.aws.amazon.com/lambda/latest/dg/best-practices.html)
- [SQS Event Sources](https://docs.aws.amazon.com/lambda/latest/dg/with-sqs.html)
