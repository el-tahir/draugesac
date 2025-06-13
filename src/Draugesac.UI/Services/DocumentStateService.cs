using Draugesac.Domain.Enums;
using System.Text.Json;
using System.Net.Http.Json;

namespace Draugesac.UI.Services;

/// <summary>
/// Client-side service for managing document state across the application.
/// Provides a centralized store for document information that persists during the session.
/// </summary>
public class DocumentStateService
{
    private readonly List<ClientDocumentInfo> _documents = new();
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Event triggered when the document list changes.
    /// </summary>
    public event Action? DocumentsChanged;

    /// <summary>
    /// Constructor - starts with clean state and requires HttpClient for API calls
    /// </summary>
    public DocumentStateService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        // Start with empty document list - documents will be loaded from API on demand
    }

    /// <summary>
    /// Gets a read-only copy of all documents in the current session.
    /// </summary>
    /// <returns>List of documents with their current state</returns>
    public List<ClientDocumentInfo> GetDocuments()
    {
        return _documents.ToList(); // Return a copy to prevent external modification
    }

    /// <summary>
    /// Adds a new document to the client-side state.
    /// Thread-safe implementation to prevent race conditions during concurrent additions.
    /// </summary>
    /// <param name="document">The document information to add</param>
    /// <exception cref="ArgumentNullException">Thrown when document is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when document already exists</exception>
    public void AddDocument(ClientDocumentInfo document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        lock (_documents) // Thread safety for concurrent access
        {
            // Check if document already exists (prevent duplicates)
            if (_documents.Any(d => d.Id == document.Id))
            {
                throw new InvalidOperationException($"Document with ID {document.Id} already exists in the state");
            }

            _documents.Add(document);
        }

        NotifyDocumentsChanged();
    }

    /// <summary>
    /// Updates an existing document's status and metadata.
    /// Thread-safe implementation to prevent race conditions during concurrent updates.
    /// </summary>
    /// <param name="documentId">The ID of the document to update</param>
    /// <param name="status">The new status</param>
    /// <param name="redactedS3Key">Optional redacted file S3 key</param>
    /// <exception cref="ArgumentException">Thrown when document with specified ID is not found</exception>
    public void UpdateDocumentStatus(Guid documentId, DocumentStatus status, string? redactedS3Key = null)
    {
        lock (_documents) // Thread safety for concurrent access
        {
            ClientDocumentInfo? document = _documents.FirstOrDefault(d => d.Id == documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found in state", nameof(documentId));
            }

            document.Status = status;
            document.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(redactedS3Key))
            {
                document.RedactedS3Key = redactedS3Key;
            }
        }

        NotifyDocumentsChanged();
    }

    /// <summary>
    /// Gets a specific document by its ID.
    /// </summary>
    /// <param name="documentId">The document ID to find</param>
    /// <returns>The document if found, null otherwise</returns>
    public ClientDocumentInfo? GetDocumentById(Guid documentId)
    {
        return _documents.FirstOrDefault(d => d.Id == documentId);
    }

    /// <summary>
    /// Removes a document from the client-side state.
    /// </summary>
    /// <param name="documentId">The ID of the document to remove</param>
    /// <returns>True if the document was found and removed, false otherwise</returns>
    public bool RemoveDocument(Guid documentId)
    {
        ClientDocumentInfo? document = _documents.FirstOrDefault(d => d.Id == documentId);
        if (document == null)
        {
            return false;
        }

        _documents.Remove(document);
        NotifyDocumentsChanged();
        return true;
    }

    /// <summary>
    /// Gets documents filtered by status.
    /// </summary>
    /// <param name="status">The status to filter by</param>
    /// <returns>List of documents with the specified status</returns>
    public List<ClientDocumentInfo> GetDocumentsByStatus(DocumentStatus status)
    {
        return _documents.Where(d => d.Status == status).ToList();
    }

    /// <summary>
    /// Gets document statistics for dashboard display.
    /// </summary>
    /// <returns>Statistics about document counts by status</returns>
    public DocumentStatistics GetStatistics()
    {
        return new DocumentStatistics
        {
            TotalDocuments = _documents.Count,
            UploadedCount = _documents.Count(d => d.Status == DocumentStatus.Uploaded),
            ProcessingCount = _documents.Count(d => d.Status == DocumentStatus.Processing),
            CompletedCount = _documents.Count(d => d.Status == DocumentStatus.Completed),
            FailedCount = _documents.Count(d => d.Status == DocumentStatus.Failed)
        };
    }

    /// <summary>
    /// Clears all documents from the client-side state.
    /// Useful for testing or when starting a new session.
    /// </summary>
    public void ClearDocuments()
    {
        _documents.Clear();
        NotifyDocumentsChanged();
    }

    /// <summary>
    /// Loads initial mock data for demonstration purposes.
    /// This can be removed once the real API integration is complete.
    /// </summary>
    public void LoadMockData()
    {
        if (_documents.Any())
        {
            return; // Don't load mock data if documents already exist
        }

        var mockDocuments = new List<ClientDocumentInfo>
        {
            new ClientDocumentInfo
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "Contract_2024.pdf",
                Status = DocumentStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3).AddHours(2),
                RedactedS3Key = "redacted/contract-123",
                OriginalS3Key = "original/contract-123"
            },
            new ClientDocumentInfo
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "Employee_Records.docx",
                Status = DocumentStatus.Processing,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                OriginalS3Key = "original/employee-records-456"
            },
            new ClientDocumentInfo
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "Financial_Report.txt",
                Status = DocumentStatus.Uploaded,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                OriginalS3Key = "original/financial-report-789"
            },
            new ClientDocumentInfo
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "Customer_Data.pdf",
                Status = DocumentStatus.Failed,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(5),
                OriginalS3Key = "original/customer-data-101"
            }
        };

        foreach (ClientDocumentInfo document in mockDocuments)
        {
            _documents.Add(document);
        }

        NotifyDocumentsChanged();
    }

    /// <summary>
    /// Loads documents from the API server and updates the local state.
    /// This method should be called on application startup or when refreshing the document list.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <exception cref="HttpRequestException">Thrown when the API call fails</exception>
    public async Task LoadDocumentsFromApiAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("Documents");
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiDocuments = JsonSerializer.Deserialize<List<ApiDocumentResponse>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiDocuments != null)
                {
                    lock (_documents) // Thread safety during bulk update
                    {
                        // Clear existing documents and load from API
                        _documents.Clear();
                        
                        foreach (var apiDoc in apiDocuments)
                        {
                            if (Enum.TryParse<DocumentStatus>(apiDoc.Status, out var status))
                            {
                                var clientDoc = new ClientDocumentInfo
                                {
                                    Id = apiDoc.Id,
                                    OriginalFileName = apiDoc.OriginalFileName,
                                    Status = status,
                                    CreatedAt = apiDoc.CreatedAt,
                                    UpdatedAt = apiDoc.UpdatedAt,
                                    OriginalS3Key = apiDoc.OriginalS3Key,
                                    RedactedS3Key = apiDoc.RedactedS3Key
                                };
                                _documents.Add(clientDoc);
                            }
                        }
                    }

                    NotifyDocumentsChanged();
                }
            }
            else
            {
                throw new HttpRequestException($"Failed to load documents from API: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse API response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Notifies subscribers that the document list has changed.
    /// </summary>
    private void NotifyDocumentsChanged()
    {
        DocumentsChanged?.Invoke();
    }
}

/// <summary>
/// Client-side document information model.
/// Represents document metadata tracked during the current session.
/// </summary>
public class ClientDocumentInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the document.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the original filename of the uploaded document.
    /// </summary>
    public required string OriginalFileName { get; set; }

    /// <summary>
    /// Gets or sets the current processing status of the document.
    /// </summary>
    public DocumentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the document was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the document was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the S3 key for the original document.
    /// </summary>
    public string? OriginalS3Key { get; set; }

    /// <summary>
    /// Gets or sets the S3 key for the redacted version of the document.
    /// </summary>
    public string? RedactedS3Key { get; set; }
}

/// <summary>
/// Statistics about documents in the current session.
/// </summary>
public class DocumentStatistics
{
    public int TotalDocuments { get; set; }
    public int UploadedCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// API response model for document data from the server.
/// </summary>
public class ApiDocumentResponse
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? OriginalS3Key { get; set; }
    public string? RedactedS3Key { get; set; }
}
