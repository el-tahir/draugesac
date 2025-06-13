using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Draugesac.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Draugesac.Infrastructure.Services;

/// <summary>
/// AWS SQS implementation of the message publisher interface.
/// Handles publishing redaction job messages to an SQS queue for Lambda processing.
/// </summary>
public class SqsMessagePublisher : IMessagePublisher
{
    private readonly IAmazonSQS _sqsClient;
    private readonly string _queueUrl;
    private readonly ILogger<SqsMessagePublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the SqsMessagePublisher.
    /// </summary>
    /// <param name="sqsClient">The AWS SQS client for message operations</param>
    /// <param name="queueUrl">The SQS queue URL for publishing messages</param>
    /// <param name="logger">Logger for tracking operations and errors</param>
    public SqsMessagePublisher(IAmazonSQS sqsClient, string queueUrl, ILogger<SqsMessagePublisher> logger)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _queueUrl = !string.IsNullOrWhiteSpace(queueUrl)
            ? queueUrl
            : throw new ArgumentException("Queue URL cannot be null or empty", nameof(queueUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publishes a redaction job message to the SQS queue for asynchronous processing.
    /// </summary>
    /// <param name="payload">The redaction job payload containing document ID and phrases to redact</param>
    /// <returns>A task representing the asynchronous publish operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when payload is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when SQS message publishing fails</exception>
    public async Task PublishRedactionJobAsync(RedactionJobPayload payload)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.DocumentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID cannot be empty", nameof(payload));
        }

        if (payload.PhrasesToRedact == null || !payload.PhrasesToRedact.Any())
        {
            _logger.LogWarning("Publishing redaction job for document {DocumentId} with no phrases to redact", payload.DocumentId);
        }

        _logger.LogInformation("Publishing redaction job for document {DocumentId} with {PhrasesCount} phrases",
            payload.DocumentId, payload.PhrasesToRedact?.Count ?? 0);

        try
        {
            // Serialize the payload to JSON
            string messageBody = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            _logger.LogDebug("Serialized message body: {MessageBody}", messageBody);

            // Create SQS message request
            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = messageBody,
                MessageGroupId = "redaction-jobs",
                MessageDeduplicationId = payload.DocumentId.ToString(),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["DocumentId"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = payload.DocumentId.ToString()
                    },
                    ["PhrasesCount"] = new MessageAttributeValue
                    {
                        DataType = "Number",
                        StringValue = (payload.PhrasesToRedact?.Count ?? 0).ToString()
                    },
                    ["Timestamp"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = DateTime.UtcNow.ToString("O")
                    }
                }
            };

            // Send message to SQS
            SendMessageResponse response = await _sqsClient.SendMessageAsync(sendMessageRequest);

            if (!string.IsNullOrEmpty(response.MessageId))
            {
                _logger.LogInformation("Successfully published redaction job message for document {DocumentId}. SQS MessageId: {MessageId}",
                    payload.DocumentId, response.MessageId);
            }
            else
            {
                throw new InvalidOperationException("SQS message publishing failed - no message ID returned");
            }
        }
        catch (Amazon.SQS.AmazonSQSException ex)
        {
            _logger.LogError(ex, "AWS SQS error publishing redaction job for document {DocumentId}: {ErrorMessage}",
                payload.DocumentId, ex.Message);
            throw new InvalidOperationException($"Failed to publish message to SQS: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization error for redaction job payload {DocumentId}: {ErrorMessage}",
                payload.DocumentId, ex.Message);
            throw new InvalidOperationException($"Failed to serialize redaction job payload: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing redaction job for document {DocumentId}: {ErrorMessage}",
                payload.DocumentId, ex.Message);
            throw new InvalidOperationException($"Unexpected error during message publishing: {ex.Message}", ex);
        }
    }
}
