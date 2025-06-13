using System.Collections.Generic; // For Dictionary
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Draugesac.Infra
{
    public class DraugesacInfraStack : Stack
    {
        internal DraugesacInfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // **1. S3 Bucket for Documents**
            // Ensure a globally unique bucket name, possibly by incorporating account/region or a random suffix.
            // For simplicity here, we'll use a fixed name, but in practice, make it unique.
            var documentsBucket = new Bucket(this, "DraugesacDocumentsBucket", new BucketProps
            {
                BucketName = $"draugesac-documents-{Account}-{Region}", // Makes it unique
                Versioned = true, // Recommended for data safety
                RemovalPolicy = RemovalPolicy.DESTROY, // DESTROY for dev/test, RETAIN for prod
                AutoDeleteObjects = true, // DESTROY for dev/test, false for prod
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                LifecycleRules = new[]
                {
                    new LifecycleRule
                    {
                        Id = "ExpireOldObjects",
                        Expiration = Duration.Days(365), // Example: delete objects older than 1 year
                        NoncurrentVersionExpiration = Duration.Days(30) // Clean up old versions
                    }
                }
            });
            new CfnOutput(this, "DocumentsBucketName", new CfnOutputProps { Value = documentsBucket.BucketName });

            // **2. DynamoDB Table for Document Metadata**
            var documentsTable = new Table(this, "DraugesacDocumentsTable", new TableProps
            {
                TableName = "DraugesacDocuments-CDK", // Different name to avoid conflict
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "Id", Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST, // Cost-effective for variable workloads
                RemovalPolicy = RemovalPolicy.DESTROY // DESTROY for dev/test, RETAIN for prod
            });
            new CfnOutput(this, "DocumentsTableName", new CfnOutputProps { Value = documentsTable.TableName });
            new CfnOutput(this, "DocumentsTableArn", new CfnOutputProps { Value = documentsTable.TableArn });

            // **3. SQS Queue for Redaction Jobs**
            var jobQueue = new Queue(this, "DraugesacJobQueue", new QueueProps
            {
                QueueName = "DraugesacJobQueue-CDK.fifo", // Different name to avoid conflict
                Fifo = true, // If using FIFO
                ContentBasedDeduplication = true, // If using FIFO and want auto-deduplication
                VisibilityTimeout = Duration.Minutes(6), // Should be >= Lambda timeout
                RetentionPeriod = Duration.Days(4)      // How long messages stay in queue if not processed
            });
            new CfnOutput(this, "JobQueueName", new CfnOutputProps { Value = jobQueue.QueueName });
            new CfnOutput(this, "JobQueueUrl", new CfnOutputProps { Value = jobQueue.QueueUrl });
            new CfnOutput(this, "JobQueueArn", new CfnOutputProps { Value = jobQueue.QueueArn });

            // **4. IAM Role for the Lambda Function**
            var lambdaRole = new Role(this, "DraugesacProcessorLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = new[]
                {
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                }
            });

            // Grant least-privilege permissions to the Lambda role
            // S3 permissions: Only read/write access to specific bucket
            documentsBucket.GrantRead(lambdaRole);
            documentsBucket.GrantWrite(lambdaRole);
            
            // DynamoDB permissions: Only read/write data, not table structure changes
            documentsTable.GrantReadWriteData(lambdaRole);
            
            // SQS permissions: Only consume messages from the specific queue
            jobQueue.GrantConsumeMessages(lambdaRole);

            // **5. AWS Lambda Function for Processing**
            var processorLambda = new Function(this, "DraugesacProcessorFunction", new FunctionProps
            {
                FunctionName = "DraugesacProcessor-CDK", // Different name to avoid conflict
                Runtime = Runtime.DOTNET_8,
                // Ensure this path correctly points to your published Lambda artifacts
                Code = Code.FromAsset("../lambda/Draugesac.Processor/src/Draugesac.Processor/bin/Release/net8.0/publish"),
                Handler = "Draugesac.Processor::Draugesac.Processor.Function::FunctionHandler",
                Role = lambdaRole,
                MemorySize = 2048, // As specified in your TESTING.md
                Timeout = Duration.Minutes(5), // As specified in your TESTING.md (300 seconds)
                Environment = new Dictionary<string, string>
                {
                    { "AWS__S3BucketName", documentsBucket.BucketName },            // Primary format expected by Lambda
                    { "AWS_S3_BUCKET_NAME", documentsBucket.BucketName },           // Fallback format
                    { "AWS__DynamoDbTableName", documentsTable.TableName },        // Primary format expected by Lambda  
                    { "AWS_DYNAMODB_TABLE_NAME", documentsTable.TableName },       // Fallback format
                    { "AWS__SQSQueueUrl", jobQueue.QueueUrl },                     // SQS Queue URL
                    { "AWS_SQS_QUEUE_URL", jobQueue.QueueUrl },                   // Fallback SQS Queue URL
                    { "ASPNETCORE_ENVIRONMENT", "Production" } // Or your desired environment
                    // The Lambda function code already tries variations like AWS__S3BucketName or S3_BUCKET_NAME
                }
            });
            new CfnOutput(this, "ProcessorLambdaName", new CfnOutputProps { Value = processorLambda.FunctionName });

            // **6. Event Source Mapping (SQS to Lambda)**
            processorLambda.AddEventSource(new SqsEventSource(jobQueue, new SqsEventSourceProps
            {
                BatchSize = 1 // Process one message at a time, adjust as needed
            }));

            // **6. API Role (Conceptual for ECS/EC2 deployment)**
            // This role demonstrates least-privilege permissions for the API service
            // For local Docker testing, the API uses credentials from your local AWS CLI setup
            var apiRole = new Role(this, "DraugesacApiRole", new RoleProps
            {
                AssumedBy = new CompositePrincipal(
                    new ServicePrincipal("ec2.amazonaws.com"),
                    new ServicePrincipal("ecs-tasks.amazonaws.com")
                ),
                Description = "IAM role for Draugesac API service with least-privilege permissions"
            });

            // API only needs specific S3 permissions
            documentsBucket.GrantPut(apiRole);              // Upload documents
            documentsBucket.GrantReadWrite(apiRole);        // Generate presigned URLs and delete
            
            // API needs full DynamoDB data access but not table management
            documentsTable.GrantReadWriteData(apiRole);
            
            // API only needs to send messages to SQS, not consume
            jobQueue.GrantSendMessages(apiRole);
            
            // Output this role ARN for potential ECS/EC2 deployment
            new CfnOutput(this, "ApiRoleArn", new CfnOutputProps { 
                Value = apiRole.RoleArn,
                Description = "IAM Role ARN for Draugesac API service"
            });
        }
    }
}
