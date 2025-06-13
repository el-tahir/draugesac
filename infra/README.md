# Draugesac Infrastructure

**AWS CDK infrastructure as code for the Draugesac document redaction system.**

This project defines all AWS resources required to run the Draugesac application using AWS CDK with C#. It creates a complete serverless architecture with proper security, monitoring, and cost optimization.

## 🏗️ Infrastructure Components

### Core Services
- **S3 Bucket**: Document storage with encryption and lifecycle policies
- **DynamoDB Table**: Document metadata with on-demand billing
- **SQS FIFO Queue**: Redaction job processing with deduplication
- **Lambda Function**: Serverless document processing

### Security & Access
- **IAM Roles**: Least-privilege access for Lambda and API services
- **Encryption**: Server-side encryption for S3 and DynamoDB
- **Network Security**: Private subnet deployment options

### Monitoring & Operations
- **CloudWatch Logs**: Centralized logging for all services
- **CloudFormation Outputs**: Easy access to resource identifiers
- **Tags**: Consistent resource tagging for cost allocation

## 🚀 Deployment

### Prerequisites

- [AWS CLI](https://aws.amazon.com/cli/) configured with appropriate permissions
- [AWS CDK CLI v2](https://docs.aws.amazon.com/cdk/v2/guide/getting_started.html)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) (for CDK CLI)

### Required AWS Permissions

Your AWS credentials need the following permissions:
- CloudFormation stack management
- S3 bucket creation and management
- DynamoDB table creation and management
- SQS queue creation and management
- Lambda function deployment
- IAM role and policy management
- CloudWatch logs management

### Deployment Steps

1. **Install CDK CLI** (if not already installed):
   ```bash
   npm install -g aws-cdk@latest
   ```

2. **Build the CDK Application**:
   ```bash
   dotnet build src
   ```

3. **Bootstrap CDK** (first time only per account/region):
   ```bash
   cdk bootstrap
   ```

4. **Deploy the Infrastructure**:
   ```bash
   cdk deploy
   ```

5. **Verify Deployment**:
   ```bash
   cdk list
   aws s3 ls | grep draugesac
   aws dynamodb list-tables | grep Draugesac
   ```

## 🔧 Configuration

### Environment Variables

The CDK stack uses the following environment variables (optional):

```bash
export CDK_DEFAULT_ACCOUNT=123456789012  # Your AWS account ID
export CDK_DEFAULT_REGION=us-east-1      # Your preferred region
```

### Customization

You can customize the deployment by modifying `InfraStack.cs`:

#### S3 Bucket Configuration
```csharp
var documentsBucket = new Bucket(this, "DraugesacDocumentsBucket", new BucketProps
{
    BucketName = $"draugesac-documents-{Account}-{Region}",
    Versioned = true,                    // Enable/disable versioning
    RemovalPolicy = RemovalPolicy.RETAIN, // Change for production
    LifecycleRules = new[]
    {
        new LifecycleRule
        {
            Expiration = Duration.Days(365), // Adjust retention period
        }
    }
});
```

#### Lambda Configuration
```csharp
var processorLambda = new Function(this, "DraugesacProcessorFunction", new FunctionProps
{
    Runtime = Runtime.DOTNET_8,
    MemorySize = 2048,                    // Adjust based on document size
    Timeout = Duration.Minutes(5),        // Adjust based on processing time
});
```

## 📊 Monitoring and Costs

### CloudWatch Dashboards

Create custom dashboards to monitor:
- Lambda invocation metrics
- SQS queue depth and processing time
- S3 storage usage and request patterns
- DynamoDB read/write capacity usage

### Cost Optimization

The infrastructure is designed for cost efficiency:
- **DynamoDB**: On-demand billing scales with usage
- **Lambda**: Pay-per-invocation with no idle costs
- **S3**: Lifecycle policies automatically archive old documents
- **SQS**: FIFO queue with efficient message batching

### Estimated Monthly Costs

For moderate usage (1000 documents/month):
- **Lambda**: ~$5-10
- **DynamoDB**: ~$1-5
- **S3**: ~$1-3
- **SQS**: ~$0.50
- **Total**: ~$10-20/month

## 🔒 Security Best Practices

### IAM Roles
- Lambda role has minimal permissions (S3 read/write, DynamoDB read/write, SQS consume)
- API role has specific permissions (S3 write, DynamoDB read/write, SQS send)
- No wildcard permissions or overly broad access

### Data Protection
- S3 bucket blocks all public access
- Server-side encryption enabled for all data at rest
- In-transit encryption for all API communications
- Presigned URLs for secure document access

### Network Security
- VPC deployment ready (uncomment VPC sections)
- Private subnet deployment for Lambda
- NAT Gateway for outbound internet access

## 🛠️ Development Commands

### Build and Test
```bash
# Compile the CDK application
dotnet build src

# Run unit tests (if available)
dotnet test src

# Validate CDK synthesis
cdk synth
```

### Deployment Management
```bash
# Show differences between deployed and local stacks
cdk diff

# Deploy with approval prompts for security changes
cdk deploy --require-approval=any-change

# Destroy all resources (careful!)
cdk destroy
```

### Debugging
```bash
# View synthesized CloudFormation template
cdk synth > template.json

# List all stacks in the app
cdk list

# Show metadata about the stack
cdk metadata
```

## 🚨 Troubleshooting

### Common Issues

1. **Bootstrap Required**: If you see bootstrap errors, run `cdk bootstrap`
2. **Permissions Denied**: Ensure your AWS credentials have sufficient permissions
3. **Resource Conflicts**: Check for existing resources with similar names
4. **Region Mismatch**: Verify your AWS CLI region matches CDK region

### Cleanup

To completely remove all resources:
```bash
# Destroy the CDK stack
cdk destroy

# Verify no resources remain
aws s3 ls | grep draugesac
aws dynamodb list-tables | grep Draugesac
aws sqs list-queues | grep draugesac
```

## 📚 Additional Resources

- [AWS CDK Developer Guide](https://docs.aws.amazon.com/cdk/v2/guide/)
- [CDK API Reference](https://docs.aws.amazon.com/cdk/api/v2/)
- [AWS Well-Architected Framework](https://aws.amazon.com/architecture/well-architected/)
- [CloudFormation Resource Reference](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/)