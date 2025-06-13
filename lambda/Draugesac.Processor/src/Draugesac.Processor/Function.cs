using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Draugesac.Application.Interfaces;
using Draugesac.Domain.Entities;
using Draugesac.Domain.Enums;
using Draugesac.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Draugesac.Processor;

public class Function
{
    private readonly Lazy<IServiceProvider> _serviceProvider;
    private readonly ILogger<Function> _logger;

    public Function()
    {
        // Create minimal logger first for initialization logging
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
        _logger = loggerFactory.CreateLogger<Function>();

        _logger.LogInformation("🚀 Lambda Function constructor starting...");

        // Use lazy initialization to defer AWS service creation until actually needed
        _serviceProvider = new Lazy<IServiceProvider>(() =>
        {
            _logger.LogInformation("🔧 Initializing AWS services...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider provider = services.BuildServiceProvider();
            _logger.LogInformation("✅ AWS services initialized successfully");
            return provider;
        });

        _logger.LogInformation("✅ Lambda Function constructor completed");
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        // Add null checks for event and records
        if (evnt?.Records == null)
        {
            _logger.LogWarning("⚠️ SQS event or records is null - no messages to process");
            return;
        }

        _logger.LogInformation($"🎯 Lambda invocation started. Processing {evnt.Records.Count} SQS messages. Remaining time: {context.RemainingTime.TotalSeconds:F1}s");

        // Add timeout monitoring with safe calculation for test environments
        double remainingMilliseconds = context.RemainingTime.TotalMilliseconds;
        double timeoutMilliseconds = remainingMilliseconds > 10000 ? remainingMilliseconds - 5000 : 300000; // Use 5 minutes default for test tool
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMilliseconds));

        _logger.LogInformation($"⏰ Timeout set to {timeoutMilliseconds / 1000:F1}s (remaining: {remainingMilliseconds / 1000:F1}s)");

        try
        {
            // Process messages with timeout monitoring
            IEnumerable<Task> tasks = evnt.Records.Select(message => ProcessMessageAsync(message, context, timeoutCts.Token));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            _logger.LogInformation($"✅ All messages processed successfully. Remaining time: {context.RemainingTime.TotalSeconds:F1}s");
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger.LogError("⏰ Lambda function timed out during execution");
            throw new TimeoutException("Lambda function execution timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Fatal error in Lambda function handler");
            throw;
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context, CancellationToken cancellationToken)
    {
        DateTime startTime = DateTime.UtcNow;
        _logger.LogInformation($"📝 Processing message: {message.MessageId}");

        try
        {
            // Early cancellation check
            cancellationToken.ThrowIfCancellationRequested();

            // Validate message body
            if (string.IsNullOrWhiteSpace(message.Body))
            {
                _logger.LogError("Message body is null or empty");
                return;
            }

            _logger.LogInformation($"Message body: {message.Body}");

            // Deserialize SQS message body into RedactionJobPayload
            RedactionJobPayload? payload = JsonSerializer.Deserialize<RedactionJobPayload>(message.Body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            if (payload == null)
            {
                _logger.LogError($"Failed to deserialize message body: {message.Body}");
                return;
            }

            // Validate payload properties
            if (payload.DocumentId == Guid.Empty)
            {
                _logger.LogError("Payload contains empty DocumentId");
                return;
            }

            List<string> phrasesToRedact = payload.PhrasesToRedact ?? new List<string>();
            _logger.LogInformation($"Processing redaction job for document {payload.DocumentId} with {phrasesToRedact.Count} phrases");

            // Initialize services with timeout monitoring
            IDocumentRepository documentRepository;
            IFileStore fileStore;

            try
            {
                _logger.LogInformation("🔧 Getting AWS services...");
                IServiceProvider services = _serviceProvider.Value;
                documentRepository = services.GetRequiredService<IDocumentRepository>();
                fileStore = services.GetRequiredService<IFileStore>();
                _logger.LogInformation("✅ AWS services obtained successfully");
            }
            catch (Exception serviceEx)
            {
                _logger.LogError(serviceEx, "❌ Failed to initialize AWS services");
                throw new InvalidOperationException("AWS service initialization failed", serviceEx);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Fetch document from repository with timeout
            _logger.LogInformation($"🔍 Fetching document {payload.DocumentId} from DynamoDB...");
            Document? document = await documentRepository.GetByIdAsync(payload.DocumentId).ConfigureAwait(false);
            if (document == null)
            {
                _logger.LogError($"Document {payload.DocumentId} not found");
                return;
            }
            _logger.LogInformation($"✅ Document {payload.DocumentId} fetched successfully");

            if (string.IsNullOrEmpty(document.OriginalS3Key))
            {
                _logger.LogError($"Document {payload.DocumentId} has no original S3 key");
                await UpdateDocumentStatus(documentRepository, document, DocumentStatus.Failed, cancellationToken).ConfigureAwait(false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Update document status to processing
            _logger.LogInformation($"📝 Updating document {payload.DocumentId} status to Processing...");
            await UpdateDocumentStatus(documentRepository, document, DocumentStatus.Processing, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Download original file from S3 with timeout and memory-efficient processing
            _logger.LogInformation($"⬇️ Downloading original file from S3: {document.OriginalS3Key}");
            string originalContent;
            using (Stream originalFileStream = await fileStore.DownloadAsync(document.OriginalS3Key).ConfigureAwait(false))
            {
                originalContent = await ReadStreamAsTextAsync(originalFileStream, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation($"✅ Downloaded original document from S3. Content length: {originalContent.Length} characters");

            cancellationToken.ThrowIfCancellationRequested();

            // Perform redaction logic with timeout monitoring
            _logger.LogInformation($"✂️ Starting redaction process...");
            string redactedContent = PerformRedaction(originalContent, phrasesToRedact, cancellationToken);

            _logger.LogInformation($"✅ Redaction completed for document {payload.DocumentId}. Original length: {originalContent.Length}, Redacted length: {redactedContent.Length}");

            cancellationToken.ThrowIfCancellationRequested();

            // Upload redacted file to S3 with memory-efficient stream handling
            string redactedFileName = $"redacted_{document.OriginalFileName}";
            byte[] redactedBytes = Encoding.UTF8.GetBytes(redactedContent);
            
            _logger.LogInformation($"⬆️ Uploading redacted document to S3. Content size: {redactedBytes.Length} bytes");

            string redactedS3Key;
            try
            {
                using var redactedStream = new MemoryStream(redactedBytes);
                redactedS3Key = await fileStore.UploadAsync(redactedStream, redactedFileName, "text/plain").ConfigureAwait(false);
                _logger.LogInformation($"✅ Successfully uploaded redacted document to S3 with key: {redactedS3Key}");
            }
            catch (Exception uploadEx)
            {
                _logger.LogError(uploadEx, $"❌ Failed to upload redacted document to S3 for document {payload.DocumentId}");
                await UpdateDocumentStatus(documentRepository, document, DocumentStatus.Failed, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"S3 upload failed for redacted document: {uploadEx.Message}", uploadEx);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Update document with redacted file info
            try
            {
                _logger.LogInformation($"📝 Updating document {payload.DocumentId} with redacted S3 key...");
                document.RedactedS3Key = redactedS3Key;
                document.Status = DocumentStatus.Completed;
                document.UpdatedAt = DateTime.UtcNow;
                await documentRepository.UpdateAsync(document).ConfigureAwait(false);

                _logger.LogInformation($"✅ Successfully updated document {document.Id} status to Completed with redacted S3 key: {redactedS3Key}");
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, $"❌ Failed to update document {payload.DocumentId} status after successful S3 upload");
                // Note: S3 upload succeeded, but DB update failed - this is a partial success
                throw new InvalidOperationException($"Database update failed after successful S3 upload: {dbEx.Message}", dbEx);
            }

            TimeSpan processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation($"🎉 Successfully processed redaction job for document {payload.DocumentId} in {processingTime.TotalSeconds:F1}s. Remaining time: {context.RemainingTime.TotalSeconds:F1}s");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogError($"⏰ Message processing timed out for {message.MessageId}");
            throw;
        }
        catch (Exception ex)
        {
            TimeSpan processingTime = DateTime.UtcNow - startTime;
            _logger.LogError(ex, $"❌ Error processing message {message.MessageId} after {processingTime.TotalSeconds:F1}s: {ex.Message}");

            // Try to update document status to failed if we can parse the message
            try
            {
                if (!string.IsNullOrWhiteSpace(message.Body))
                {
                    RedactionJobPayload? payload = JsonSerializer.Deserialize<RedactionJobPayload>(message.Body, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (payload != null && payload.DocumentId != Guid.Empty)
                    {
                        IServiceProvider services = _serviceProvider.Value;
                        IDocumentRepository documentRepository = services.GetRequiredService<IDocumentRepository>();
                        Document? document = await documentRepository.GetByIdAsync(payload.DocumentId).ConfigureAwait(false);
                        if (document != null)
                        {
                            await UpdateDocumentStatus(documentRepository, document, DocumentStatus.Failed, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, $"❌ Failed to update document status to failed: {innerEx.Message}");
            }

            throw; // Re-throw to trigger SQS retry/DLQ behavior
        }
    }

    private async Task<string> ReadStreamAsTextAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs text redaction using optimized StringBuilder for better memory efficiency.
    /// Uses regex patterns for case-insensitive matching and whole word boundaries.
    /// </summary>
    /// <param name="content">The original text content to redact</param>
    /// <param name="phrasesToRedact">List of phrases to be redacted from the content</param>
    /// <param name="cancellationToken">Cancellation token for timeout monitoring</param>
    /// <returns>The redacted content with sensitive phrases replaced by asterisks</returns>
    private string PerformRedaction(string content, List<string> phrasesToRedact, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content) || !phrasesToRedact.Any())
        {
            return content;
        }

        // Use StringBuilder for memory-efficient string manipulation
        var result = new StringBuilder(content);
        var processedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < phrasesToRedact.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string phrase = phrasesToRedact[i]?.Trim();
            if (string.IsNullOrWhiteSpace(phrase) || processedPhrases.Contains(phrase))
            {
                continue; // Skip duplicates and empty phrases
            }

            processedPhrases.Add(phrase);

            // Use regex for more accurate case-insensitive matching with word boundaries
            string escapedPhrase = Regex.Escape(phrase);
            string pattern = $@"\b{escapedPhrase}\b";
            string redaction = new string('*', phrase.Length);

            try
            {
                // Replace all occurrences with timeout protection
                string currentContent = result.ToString();
                string redactedContent = Regex.Replace(currentContent, pattern, redaction, 
                    RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                
                result.Clear();
                result.Append(redactedContent);
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning($"Regex timeout occurred for phrase: {phrase}. Falling back to simple string replacement.");
                // Fallback to simple string replacement if regex times out
                string simpleRedaction = result.ToString().Replace(phrase, redaction, StringComparison.OrdinalIgnoreCase);
                result.Clear();
                result.Append(simpleRedaction);
            }

            // Log progress for long lists with reduced frequency
            if (i % 25 == 0 && phrasesToRedact.Count > 50)
            {
                _logger.LogInformation($"Redaction progress: {i + 1}/{phrasesToRedact.Count} phrases processed");
            }
        }

        return result.ToString();
    }

    private async Task UpdateDocumentStatus(IDocumentRepository documentRepository, Document document, DocumentStatus status, CancellationToken cancellationToken = default)
    {
        document.Status = status;
        document.UpdatedAt = DateTime.UtcNow;
        await documentRepository.UpdateAsync(document).ConfigureAwait(false);
        _logger.LogInformation($"📝 Updated document {document.Id} status to {status}");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        _logger.LogInformation("🔧 Configuring AWS services...");

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // Add AWS services with timeout configuration
        services.AddSingleton<IAmazonS3>(provider =>
        {
            _logger.LogInformation("🔧 Creating S3 client...");
            var config = new Amazon.S3.AmazonS3Config
            {
                Timeout = TimeSpan.FromSeconds(30),
                RetryMode = Amazon.Runtime.RequestRetryMode.Standard,
                MaxErrorRetry = 3
            };
            var client = new AmazonS3Client(config);
            _logger.LogInformation("✅ S3 client created successfully");
            return client;
        });

        services.AddSingleton<IAmazonDynamoDB>(provider =>
        {
            _logger.LogInformation("🔧 Creating DynamoDB client...");
            var config = new Amazon.DynamoDBv2.AmazonDynamoDBConfig
            {
                Timeout = TimeSpan.FromSeconds(30),
                RetryMode = Amazon.Runtime.RequestRetryMode.Standard,
                MaxErrorRetry = 3
            };
            var client = new AmazonDynamoDBClient(config);
            _logger.LogInformation("✅ DynamoDB client created successfully");
            return client;
        });

        // Add application services with proper dependency injection
        services.AddSingleton<IDocumentRepository>(provider =>
        {
            IAmazonDynamoDB dynamoDbClient = provider.GetRequiredService<IAmazonDynamoDB>();
            ILogger<DynamoDbDocumentRepository> logger = provider.GetRequiredService<ILogger<DynamoDbDocumentRepository>>();

            // Try both Lambda environment variable formats
            string tableName = Environment.GetEnvironmentVariable("AWS__DynamoDbTableName")
                         ?? Environment.GetEnvironmentVariable("AWS_DynamoDbTableName")
                         ?? Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME")
                         ?? "DraugesacDocuments";

            logger.LogInformation($"🔧 Using DynamoDB table name: {tableName}");
            return new DynamoDbDocumentRepository(dynamoDbClient, tableName, logger);
        });

        services.AddSingleton<IFileStore>(provider =>
        {
            IAmazonS3 s3Client = provider.GetRequiredService<IAmazonS3>();
            ILogger<S3FileStore> logger = provider.GetRequiredService<ILogger<S3FileStore>>();

            // Try both Lambda environment variable formats
            string bucketName = Environment.GetEnvironmentVariable("AWS__S3BucketName")
                          ?? Environment.GetEnvironmentVariable("AWS_S3BucketName")
                          ?? Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
                          ?? "draugesac-documents";

            logger.LogInformation($"🔧 Using S3 bucket name: {bucketName}");
            return new S3FileStore(s3Client, bucketName, logger);
        });

        _logger.LogInformation("✅ AWS services configuration completed");
    }
}
