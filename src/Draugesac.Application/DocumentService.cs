using Draugesac.Application.Interfaces;
using Draugesac.Domain.Entities;
using Draugesac.Domain.Enums;

namespace Draugesac.Application;

/// <summary>
/// Application service for managing document operations.
/// Demonstrates dependency injection with repository, file storage, and message publishing patterns.
/// </summary>
public class DocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStore _fileStore;
    private readonly IMessagePublisher _messagePublisher;

    /// <summary>
    /// Initializes a new instance of the DocumentService class.
    /// </summary>
    /// <param name="documentRepository">The document repository dependency</param>
    /// <param name="fileStore">The file storage dependency</param>
    /// <param name="messagePublisher">The message publisher dependency</param>
    public DocumentService(
        IDocumentRepository documentRepository,
        IFileStore fileStore,
        IMessagePublisher messagePublisher)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _messagePublisher = messagePublisher ?? throw new ArgumentNullException(nameof(messagePublisher));
    }

    /// <summary>
    /// Uploads a file and creates a new document record.
    /// </summary>
    /// <param name="stream">The file stream to upload</param>
    /// <param name="fileName">The original file name</param>
    /// <param name="contentType">The MIME content type</param>
    /// <returns>The created document entity</returns>
    public async Task<Document> UploadDocumentAsync(Stream stream, string fileName, string contentType)
    {
        // Upload file to storage
        string s3Key = await _fileStore.UploadAsync(stream, fileName, contentType);

        // Create document record
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = fileName,
            Status = DocumentStatus.Uploaded,
            CreatedAt = DateTime.UtcNow,
            OriginalS3Key = s3Key
        };

        await _documentRepository.AddAsync(document);
        return document;
    }

    /// <summary>
    /// Initiates a redaction job for a document with specified phrases to redact.
    /// </summary>
    /// <param name="documentId">The document identifier</param>
    /// <param name="phrasesToRedact">List of phrases to redact from the document</param>
    /// <returns>True if the redaction job was initiated successfully, false if document not found</returns>
    public async Task<bool> StartRedactionJobAsync(Guid documentId, List<string> phrasesToRedact)
    {
        Document? document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            return false;
        }

        // Update document status to processing
        document.Status = DocumentStatus.Processing;
        document.UpdatedAt = DateTime.UtcNow;
        await _documentRepository.UpdateAsync(document);

        // Publish redaction job message
        var payload = new RedactionJobPayload(documentId, phrasesToRedact);
        await _messagePublisher.PublishRedactionJobAsync(payload);

        return true;
    }

    /// <summary>
    /// Gets a presigned URL for downloading a document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>The presigned URL if document exists, otherwise null</returns>
    public async Task<string?> GetDocumentDownloadUrlAsync(Guid id)
    {
        Document? document = await _documentRepository.GetByIdAsync(id);
        if (document?.OriginalS3Key == null)
        {
            return null;
        }

        return await _fileStore.GetPresignedUrlAsync(document.OriginalS3Key);
    }

    /// <summary>
    /// Gets a presigned URL for downloading the redacted version of a document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>The presigned URL for the redacted document if it exists, otherwise null</returns>
    public async Task<string?> GetRedactedDocumentDownloadUrlAsync(Guid id)
    {
        Document? document = await _documentRepository.GetByIdAsync(id);
        if (document?.RedactedS3Key == null)
        {
            return null;
        }

        return await _fileStore.GetPresignedUrlAsync(document.RedactedS3Key);
    }

    /// <summary>
    /// Creates and persists a new document.
    /// </summary>
    /// <param name="fileName">The original file name</param>
    /// <returns>The created document entity</returns>
    public async Task<Document> CreateDocumentAsync(string fileName)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = fileName,
            Status = DocumentStatus.Uploaded,
            CreatedAt = DateTime.UtcNow
        };

        await _documentRepository.AddAsync(document);
        return document;
    }

    /// <summary>
    /// Retrieves a document by its unique identifier.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>The document if found, otherwise null</returns>
    public async Task<Document?> GetDocumentAsync(Guid id)
    {
        return await _documentRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Retrieves all documents from the repository.
    /// </summary>
    /// <returns>A list of all documents</returns>
    public async Task<List<Document>> GetAllDocumentsAsync()
    {
        return await _documentRepository.GetAllAsync();
    }

    /// <summary>
    /// Updates the status of an existing document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <param name="status">The new status</param>
    /// <returns>True if the document was updated, false if not found</returns>
    public async Task<bool> UpdateDocumentStatusAsync(Guid id, DocumentStatus status)
    {
        Document? document = await _documentRepository.GetByIdAsync(id);
        if (document == null)
        {
            return false;
        }

        document.Status = status;
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document);
        return true;
    }
} 