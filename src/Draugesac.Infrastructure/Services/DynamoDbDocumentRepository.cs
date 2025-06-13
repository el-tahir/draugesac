using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Draugesac.Application.Interfaces;
using Draugesac.Domain.Entities;
using Draugesac.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Draugesac.Infrastructure.Services;

/// <summary>
/// AWS DynamoDB implementation of the document repository interface.
/// Handles document storage and retrieval operations in DynamoDB.
/// </summary>
public class DynamoDbDocumentRepository : IDocumentRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ILogger<DynamoDbDocumentRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the DynamoDbDocumentRepository.
    /// </summary>
    /// <param name="dynamoDb">The AWS DynamoDB client for database operations</param>
    /// <param name="tableName">The DynamoDB table name for document storage</param>
    /// <param name="logger">Logger for tracking operations and errors</param>
    public DynamoDbDocumentRepository(IAmazonDynamoDB dynamoDb, string tableName, ILogger<DynamoDbDocumentRepository> logger)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = !string.IsNullOrWhiteSpace(tableName)
            ? tableName
            : throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a document by its unique identifier from DynamoDB.
    /// </summary>
    /// <param name="id">The unique identifier of the document</param>
    /// <returns>The document if found, otherwise null</returns>
    /// <exception cref="InvalidOperationException">Thrown when DynamoDB operation fails</exception>
    public async Task<Document?> GetByIdAsync(Guid id)
    {
        _logger.LogInformation("Retrieving document with ID {DocumentId} from DynamoDB", id);

        try
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = id.ToString() }
                }
            };

            GetItemResponse response = await _dynamoDb.GetItemAsync(request);

            if (!response.Item.Any())
            {
                _logger.LogInformation("Document with ID {DocumentId} not found in DynamoDB", id);
                return null;
            }

            Document document = MapFromDynamoDb(response.Item);
            _logger.LogInformation("Successfully retrieved document with ID {DocumentId} from DynamoDB", id);
            return document;
        }
        catch (AmazonDynamoDBException ex)
        {
            _logger.LogError(ex, "AWS DynamoDB error retrieving document {DocumentId}: {ErrorMessage}", id, ex.Message);
            throw new InvalidOperationException($"Failed to retrieve document from DynamoDB: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving document {DocumentId}: {ErrorMessage}", id, ex.Message);
            throw new InvalidOperationException($"Unexpected error during document retrieval: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves all documents from DynamoDB.
    /// </summary>
    /// <returns>A list of all documents</returns>
    /// <exception cref="InvalidOperationException">Thrown when DynamoDB operation fails</exception>
    public async Task<List<Document>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all documents from DynamoDB");

        try
        {
            var documents = new List<Document>();
            string? lastEvaluatedKey = null;

            do
            {
                var request = new ScanRequest
                {
                    TableName = _tableName,
                    Limit = 100 // Process in batches to handle large datasets
                };

                // Add pagination token if available
                if (!string.IsNullOrEmpty(lastEvaluatedKey))
                {
                    request.ExclusiveStartKey = new Dictionary<string, AttributeValue>
                    {
                        ["Id"] = new AttributeValue { S = lastEvaluatedKey }
                    };
                }

                ScanResponse response = await _dynamoDb.ScanAsync(request);

                // Convert DynamoDB items to Document entities
                foreach (var item in response.Items)
                {
                    documents.Add(MapFromDynamoDb(item));
                }

                // Get pagination token for next batch
                lastEvaluatedKey = response.LastEvaluatedKey?.ContainsKey("Id") == true 
                    ? response.LastEvaluatedKey["Id"].S 
                    : null;

            } while (!string.IsNullOrEmpty(lastEvaluatedKey));

            _logger.LogInformation("Successfully retrieved {DocumentCount} documents from DynamoDB", documents.Count);
            return documents;
        }
        catch (AmazonDynamoDBException ex)
        {
            _logger.LogError(ex, "AWS DynamoDB error retrieving all documents: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Failed to retrieve documents from DynamoDB: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving all documents: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Unexpected error during document retrieval: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves documents by their status from DynamoDB.
    /// </summary>
    /// <param name="status">The status to filter documents by.</param>
    /// <returns>A list of documents matching the specified status.</returns>
    public async Task<List<Document>> GetByStatusAsync(DocumentStatus status)
    {
        _logger.LogInformation("Retrieving documents with status {Status} from DynamoDB table {TableName}", status, _tableName);
        var documents = new List<Document>();
        try
        {
            var request = new ScanRequest
            {
                TableName = _tableName,
                FilterExpression = "#status_attr = :status_val", // Ensure #status_attr is defined if 'Status' is a reserved keyword
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status_attr", "Status" } 
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":status_val", new AttributeValue { S = status.ToString() } }
                }
            };

            ScanResponse response;
            do
            {
                response = await _dynamoDb.ScanAsync(request);
                foreach (var item in response.Items)
                {
                    documents.Add(MapFromDynamoDb(item));
                }
                request.ExclusiveStartKey = response.LastEvaluatedKey;
            } while (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0);

            _logger.LogInformation("Successfully retrieved {Count} documents with status {Status}", documents.Count, status);
            return documents;
        }
        catch (AmazonDynamoDBException ex)
        {
            _logger.LogError(ex, "AWS DynamoDB error retrieving documents by status {Status}: {ErrorMessage}", status, ex.Message);
            throw new InvalidOperationException($"Failed to retrieve documents by status from DynamoDB: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving documents by status {Status}: {ErrorMessage}", status, ex.Message);
            throw new InvalidOperationException($"Unexpected error during document retrieval by status: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Adds a new document to DynamoDB.
    /// </summary>
    /// <param name="document">The document to add</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when document is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when DynamoDB operation fails</exception>
    public async Task AddAsync(Document document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _logger.LogInformation("Adding new document with ID {DocumentId} to DynamoDB", document.Id);

        try
        {
            Dictionary<string, AttributeValue> item = MapToDynamoDb(document);
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item,
                // Ensure the document doesn't already exist
                ConditionExpression = "attribute_not_exists(Id)"
            };

            await _dynamoDb.PutItemAsync(request);
            _logger.LogInformation("Successfully added document with ID {DocumentId} to DynamoDB", document.Id);
        }
        catch (ConditionalCheckFailedException ex)
        {
            _logger.LogError(ex, "Document with ID {DocumentId} already exists in DynamoDB", document.Id);
            throw new InvalidOperationException($"Document with ID {document.Id} already exists", ex);
        }
        catch (AmazonDynamoDBException ex)
        {
            _logger.LogError(ex, "AWS DynamoDB error adding document {DocumentId}: {ErrorMessage}", document.Id, ex.Message);
            throw new InvalidOperationException($"Failed to add document to DynamoDB: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding document {DocumentId}: {ErrorMessage}", document.Id, ex.Message);
            throw new InvalidOperationException($"Unexpected error during document addition: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Updates an existing document in DynamoDB.
    /// </summary>
    /// <param name="document">The document to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when document is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when DynamoDB operation fails</exception>
    public async Task UpdateAsync(Document document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _logger.LogInformation("Updating document with ID {DocumentId} in DynamoDB", document.Id);

        try
        {
            var request = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = document.Id.ToString() }
                },
                UpdateExpression = "SET #status = :status, #updatedAt = :updatedAt, #originalFileName = :originalFileName, #originalS3Key = :originalS3Key, #redactedS3Key = :redactedS3Key",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "Status",
                    ["#updatedAt"] = "UpdatedAt",
                    ["#originalFileName"] = "OriginalFileName",
                    ["#originalS3Key"] = "OriginalS3Key",
                    ["#redactedS3Key"] = "RedactedS3Key"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = document.Status.ToString() },
                    [":updatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") },
                    [":originalFileName"] = new AttributeValue { S = document.OriginalFileName },
                    [":originalS3Key"] = document.OriginalS3Key != null
                        ? new AttributeValue { S = document.OriginalS3Key }
                        : new AttributeValue { NULL = true },
                    [":redactedS3Key"] = document.RedactedS3Key != null
                        ? new AttributeValue { S = document.RedactedS3Key }
                        : new AttributeValue { NULL = true }
                },
                // Ensure the document exists before updating
                ConditionExpression = "attribute_exists(Id)"
            };

            await _dynamoDb.UpdateItemAsync(request);
            _logger.LogInformation("Successfully updated document with ID {DocumentId} in DynamoDB", document.Id);
        }
        catch (ConditionalCheckFailedException ex)
        {
            _logger.LogError(ex, "Document with ID {DocumentId} not found for update in DynamoDB", document.Id);
            throw new InvalidOperationException($"Document with ID {document.Id} not found for update", ex);
        }
        catch (AmazonDynamoDBException ex)
        {
            _logger.LogError(ex, "AWS DynamoDB error updating document {DocumentId}: {ErrorMessage}", document.Id, ex.Message);
            throw new InvalidOperationException($"Failed to update document in DynamoDB: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating document {DocumentId}: {ErrorMessage}", document.Id, ex.Message);
            throw new InvalidOperationException($"Unexpected error during document update: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Maps a Document entity to DynamoDB attribute format.
    /// </summary>
    /// <param name="document">The document entity to map</param>
    /// <returns>Dictionary of DynamoDB attributes</returns>
    private static Dictionary<string, AttributeValue> MapToDynamoDb(Document document)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new AttributeValue { S = document.Id.ToString() },
            ["OriginalFileName"] = new AttributeValue { S = document.OriginalFileName },
            ["Status"] = new AttributeValue { S = document.Status.ToString() },
            ["CreatedAt"] = new AttributeValue { S = document.CreatedAt.ToString("O") }
        };

        // Handle nullable UpdatedAt
        if (document.UpdatedAt.HasValue)
        {
            item["UpdatedAt"] = new AttributeValue { S = document.UpdatedAt.Value.ToString("O") };
        }

        // Handle nullable S3 keys
        if (!string.IsNullOrEmpty(document.OriginalS3Key))
        {
            item["OriginalS3Key"] = new AttributeValue { S = document.OriginalS3Key };
        }

        if (!string.IsNullOrEmpty(document.RedactedS3Key))
        {
            item["RedactedS3Key"] = new AttributeValue { S = document.RedactedS3Key };
        }

        return item;
    }

    /// <summary>
    /// Maps DynamoDB attributes to a Document entity.
    /// </summary>
    /// <param name="item">The DynamoDB item attributes</param>
    /// <returns>The mapped Document entity</returns>
    private static Document MapFromDynamoDb(Dictionary<string, AttributeValue> item)
    {
        var document = new Document
        {
            Id = Guid.Parse(item["Id"].S),
            OriginalFileName = item["OriginalFileName"].S,
            Status = Enum.Parse<DocumentStatus>(item["Status"].S),
            CreatedAt = DateTime.Parse(item["CreatedAt"].S)
        };

        // Handle nullable UpdatedAt
        if (item.ContainsKey("UpdatedAt") && item["UpdatedAt"] != null && !string.IsNullOrEmpty(item["UpdatedAt"].S))
        {
            document.UpdatedAt = DateTime.Parse(item["UpdatedAt"].S);
        }

        // Handle nullable S3 keys
        if (item.ContainsKey("OriginalS3Key") && item["OriginalS3Key"] != null && !string.IsNullOrEmpty(item["OriginalS3Key"].S))
        {
            document.OriginalS3Key = item["OriginalS3Key"].S;
        }

        if (item.ContainsKey("RedactedS3Key") && item["RedactedS3Key"] != null && !string.IsNullOrEmpty(item["RedactedS3Key"].S))
        {
            document.RedactedS3Key = item["RedactedS3Key"].S;
        }

        return document;
    }

    /// <summary>
    /// Deletes a document by its ID from DynamoDB.
    /// </summary>
    /// <param name="id">The ID of the document to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(Guid id)
    {
        _logger.LogInformation("Deleting document with ID {DocumentId} from DynamoDB table {TableName}", id, _tableName);
        try
        {
            var request = new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = id.ToString() } }
                }
            };

            var response = await _dynamoDb.DeleteItemAsync(request);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully deleted document with ID {DocumentId} from DynamoDB", id);
            }
            else
            {
                // This case might not be hit often if the item doesn't exist, as DeleteItem is idempotent.
                // However, other errors could result in a non-OK status.
                _logger.LogWarning("Failed to delete document with ID {DocumentId} from DynamoDB. Status code: {StatusCode}", id, response.HttpStatusCode);
                throw new InvalidOperationException($"Failed to delete document {id} from DynamoDB. Status code: {response.HttpStatusCode}");
            }
        }
        catch (AmazonDynamoDBException ex)
        {
            _logger.LogError(ex, "AWS DynamoDB error deleting document {DocumentId}: {ErrorMessage}", id, ex.Message);
            throw; // Re-throw to allow controller to handle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting document {DocumentId} from DynamoDB: {ErrorMessage}", id, ex.Message);
            throw; // Re-throw to allow controller to handle
        }
    }
}
