using Draugesac.Application.Interfaces;
using Draugesac.Domain.Entities;
using Draugesac.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Draugesac.Api.Controllers;

/// <summary>
/// Data Transfer Object for redaction requests.
/// </summary>
/// <param name="PhrasesToRedact">List of phrases/terms that should be redacted from the document</param>
public record RedactionRequest(List<string> PhrasesToRedact);

/// <summary>
/// Helper class for building consistent API responses for document operations.
/// </summary>
public static class DocumentResponseBuilder
{
    /// <summary>
    /// Builds a standardized document response object.
    /// </summary>
    /// <param name="document">The document entity to convert to response format</param>
    /// <returns>Anonymous object containing standardized document information</returns>
    public static object BuildDocumentResponse(Document document)
    {
        return new
        {
            Id = document.Id,
            OriginalFileName = document.OriginalFileName,
            Status = document.Status.ToString(),
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            HasOriginal = !string.IsNullOrEmpty(document.OriginalS3Key),
            HasRedacted = !string.IsNullOrEmpty(document.RedactedS3Key),
            OriginalS3Key = document.OriginalS3Key,
            RedactedS3Key = document.RedactedS3Key
        };
    }
    
    /// <summary>
    /// Builds a download URL response object.
    /// </summary>
    /// <param name="documentId">The document identifier</param>
    /// <param name="fileName">The filename for download</param>
    /// <param name="downloadUrl">The presigned download URL</param>
    /// <param name="isRedacted">Whether this is for a redacted document</param>
    /// <returns>Anonymous object containing download information</returns>
    public static object BuildDownloadResponse(Guid documentId, string fileName, string downloadUrl, bool isRedacted = false)
    {
        var response = new
        {
            DocumentId = documentId,
            FileName = fileName,
            DownloadUrl = downloadUrl,
            ExpiresIn = "1 hour",
            GeneratedAt = DateTime.UtcNow
        };

        if (isRedacted)
        {
            return new
            {
                response.DocumentId,
                response.FileName,
                OriginalFileName = fileName.StartsWith("redacted_") ? fileName[9..] : fileName,
                response.DownloadUrl,
                response.ExpiresIn,
                response.GeneratedAt
            };
        }

        return response;
    }
}

/// <summary>
/// Helper class for validating file uploads and request data.
/// </summary>
public static class DocumentValidator
{
    private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx", ".txt" };
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Validates an uploaded file for size and type restrictions.
    /// </summary>
    /// <param name="file">The uploaded file to validate</param>
    /// <returns>Validation result with error message if invalid, null if valid</returns>
    public static string? ValidateUploadedFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return "No file provided or file is empty";
        }

        if (file.Length > MaxFileSize)
        {
            return "File size exceeds 10MB limit";
        }

        string? fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(fileExtension) || !AllowedExtensions.Contains(fileExtension))
        {
            return "Unsupported file type. Allowed types: PDF, DOC, DOCX, TXT";
        }

        return null; // Valid file
    }

    /// <summary>
    /// Validates and cleans phrases for redaction requests.
    /// </summary>
    /// <param name="phrasesToRedact">The list of phrases to validate</param>
    /// <returns>Cleaned list of valid phrases, or null if no valid phrases found</returns>
    public static List<string>? ValidateAndCleanPhrases(List<string>? phrasesToRedact)
    {
        if (phrasesToRedact == null || !phrasesToRedact.Any())
        {
            return null;
        }

        var validPhrases = phrasesToRedact
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Select(phrase => phrase.Trim())
            .Distinct()
            .ToList();

        return validPhrases.Any() ? validPhrases : null;
    }
}

/// <summary>
/// Controller for managing document operations including upload, redaction, and status tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStore _fileStore;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DocumentsController> _logger;

    /// <summary>
    /// Initializes a new instance of the DocumentsController class.
    /// </summary>
    /// <param name="documentRepository">The document repository dependency</param>
    /// <param name="fileStore">The file storage dependency</param>
    /// <param name="messagePublisher">The message publisher dependency</param>
    /// <param name="logger">The logger dependency</param>
    public DocumentsController(
        IDocumentRepository documentRepository,
        IFileStore fileStore,
        IMessagePublisher messagePublisher,
        ILogger<DocumentsController> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _messagePublisher = messagePublisher ?? throw new ArgumentNullException(nameof(messagePublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all documents in the system.
    /// </summary>
    /// <returns>A list of all documents with their status and information</returns>
    /// <response code="200">Documents retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllDocuments()
    {
        try
        {
            _logger.LogInformation("Retrieving all documents");

            var documents = await _documentRepository.GetAllAsync();
            var response = documents.Select(DocumentResponseBuilder.BuildDocumentResponse).ToList();

            _logger.LogInformation("Successfully retrieved {DocumentCount} documents", documents.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all documents: {ErrorMessage}", ex.Message);
            return StatusCode(500, new { Error = "An error occurred while retrieving documents" });
        }
    }

    /// <summary>
    /// Uploads a document to the system.
    /// </summary>
    /// <param name="file">The file to upload</param>
    /// <returns>The created document information</returns>
    /// <response code="201">Document uploaded successfully</response>
    /// <response code="400">Bad request - file validation failed</response>
    /// <response code="409">Conflict - document already exists</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        try
        {
            _logger.LogInformation("Starting document upload for file: {FileName}", file?.FileName);

            // Validate uploaded file using centralized validator
            string? validationError = DocumentValidator.ValidateUploadedFile(file);
            if (validationError != null)
            {
                _logger.LogWarning("Upload validation failed: {ValidationError}", validationError);
                return BadRequest(new { Error = validationError });
            }

            // Generate unique document entity
            var document = new Document
            {
                Id = Guid.NewGuid(),
                OriginalFileName = file.FileName,
                Status = DocumentStatus.Uploaded,
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Generated document entity with ID: {DocumentId}", document.Id);

            // Upload file to S3
            using Stream stream = file.OpenReadStream();
            string s3Key = await _fileStore.UploadAsync(stream, file.FileName, file.ContentType);

            // Update document with S3 key
            document.OriginalS3Key = s3Key;

            _logger.LogInformation("File uploaded to S3 with key: {S3Key}", s3Key);

            // Save document metadata to repository
            await _documentRepository.AddAsync(document);

            _logger.LogInformation("Document metadata saved successfully with ID: {DocumentId}", document.Id);

            // Prepare response using response builder
            var response = DocumentResponseBuilder.BuildDocumentResponse(document);

            _logger.LogInformation("Document upload completed successfully for file: {FileName} with ID: {DocumentId}",
                file.FileName, document.Id);

            return CreatedAtAction(
                nameof(GetDocumentStatus),
                new { id = document.Id },
                response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument during document upload: {FileName}", file?.FileName);
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operation failed during document upload: {FileName}", file?.FileName);
            return Conflict(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading document: {FileName}", file?.FileName);
            return StatusCode(500, new { Error = "An unexpected error occurred while uploading the document" });
        }
    }

    /// <summary>
    /// Starts a redaction job for the specified document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <param name="request">The redaction request containing phrases to redact</param>
    /// <returns>Result of the redaction job initiation</returns>
    /// <response code="202">Redaction job accepted and queued for processing</response>
    /// <response code="400">Bad request - invalid input</response>
    /// <response code="404">Document not found</response>
    /// <response code="409">Conflict - document not in valid state for redaction</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{id}/redactions")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartRedaction(Guid id, [FromBody] RedactionRequest request)
    {
        try
        {
            _logger.LogInformation("Starting redaction job for document: {DocumentId}", id);

            // Validate and clean redaction phrases using centralized validator
            var validPhrases = DocumentValidator.ValidateAndCleanPhrases(request?.PhrasesToRedact);
            if (validPhrases == null)
            {
                _logger.LogWarning("Redaction request for document {DocumentId} has no valid phrases", id);
                return BadRequest(new { Error = "No valid phrases to redact provided" });
            }

            // Get document from repository
            Document? document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", id);
                return NotFound(new { Error = "Document not found" });
            }

            // Check if document is in a valid state for redaction
            if (document.Status != DocumentStatus.Uploaded && document.Status != DocumentStatus.Completed)
            {
                _logger.LogWarning("Document {DocumentId} is in invalid state for redaction: {Status}", id, document.Status);
                return Conflict(new { Error = $"Document is not in a valid state for redaction. Current status: {document.Status}" });
            }

            // Update document status to Processing
            document.Status = DocumentStatus.Processing;
            document.UpdatedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document);

            _logger.LogInformation("Document {DocumentId} status updated to Processing", id);

            // Create and publish redaction job payload
            var payload = new RedactionJobPayload(id, validPhrases);
            await _messagePublisher.PublishRedactionJobAsync(payload);

            _logger.LogInformation("Redaction job published successfully for document: {DocumentId} with {PhrasesCount} phrases",
                id, validPhrases.Count);

            // Return 202 Accepted with job details
            var response = new
            {
                DocumentId = id,
                Status = DocumentStatus.Processing.ToString(),
                PhrasesToRedact = validPhrases,
                Message = "Redaction job accepted and queued for processing",
                QueuedAt = DateTime.UtcNow
            };

            return Accepted(response);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Null argument error starting redaction for document: {DocumentId}", id);
            return BadRequest(new { Error = "Invalid request data" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operation error starting redaction for document: {DocumentId}", id);
            return StatusCode(500, new { Error = $"Failed to queue redaction job: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error starting redaction for document: {DocumentId}", id);
            return StatusCode(500, new { Error = "An unexpected error occurred while starting the redaction job" });
        }
    }

    /// <summary>
    /// Gets the status and details of a document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>The document status and information</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocumentStatus(Guid id)
    {
        try
        {
            _logger.LogInformation("Retrieving status for document: {DocumentId}", id);

            Document? document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", id);
                return NotFound(new { Error = "Document not found" });
            }

            var response = DocumentResponseBuilder.BuildDocumentResponse(document);

            _logger.LogInformation("Document status retrieved successfully for: {DocumentId}", id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document status: {DocumentId}", id);
            return StatusCode(500, new { Error = "An error occurred while retrieving the document status" });
        }
    }

    /// <summary>
    /// Gets a presigned download URL for the original document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>A presigned URL for downloading the original document</returns>
    /// <response code="200">Download URL generated successfully</response>
    /// <response code="404">Document not found or no original file available</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOriginalDocumentDownloadUrl(Guid id)
    {
        try
        {
            _logger.LogInformation("Generating download URL for original document: {DocumentId}", id);

            Document? document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", id);
                return NotFound(new { Error = "Document not found" });
            }

            if (string.IsNullOrEmpty(document.OriginalS3Key))
            {
                _logger.LogWarning("No original file available for document: {DocumentId}", id);
                return NotFound(new { Error = "No original file available for this document" });
            }

            string downloadUrl = await _fileStore.GetPresignedUrlAsync(document.OriginalS3Key);
            var response = DocumentResponseBuilder.BuildDownloadResponse(id, document.OriginalFileName, downloadUrl);

            _logger.LogInformation("Download URL generated successfully for document: {DocumentId}", id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download URL for document: {DocumentId}", id);
            return StatusCode(500, new { Error = "An error occurred while generating the download URL" });
        }
    }

    /// <summary>
    /// Gets a presigned download URL for the redacted document.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>A presigned URL for downloading the redacted document</returns>
    /// <response code="200">Download URL generated successfully</response>
    /// <response code="404">Document not found or no redacted file available</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id}/download/redacted")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRedactedDocumentDownloadUrl(Guid id)
    {
        try
        {
            _logger.LogInformation("Generating download URL for redacted document: {DocumentId}", id);

            Document? document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", id);
                return NotFound(new { Error = "Document not found" });
            }

            if (string.IsNullOrEmpty(document.RedactedS3Key))
            {
                _logger.LogWarning("No redacted file available for document: {DocumentId}", id);
                return NotFound(new { Error = "No redacted file available for this document. Document may not have been processed yet." });
            }

            string downloadUrl = await _fileStore.GetPresignedUrlAsync(document.RedactedS3Key);
            string redactedFileName = $"redacted_{document.OriginalFileName}";
            var response = DocumentResponseBuilder.BuildDownloadResponse(id, redactedFileName, downloadUrl, isRedacted: true);

            _logger.LogInformation("Redacted download URL generated successfully for document: {DocumentId}", id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating redacted download URL for document: {DocumentId}", id);
            return StatusCode(500, new { Error = "An error occurred while generating the download URL" });
        }
    }

    /// <summary>
    /// Deletes a document from the system, including its S3 objects and DynamoDB record.
    /// </summary>
    /// <param name="id">The document identifier</param>
    /// <returns>Result of the delete operation</returns>
    /// <response code="204">Document deleted successfully</response>
    /// <response code="404">Document not found</response>
    /// <response code="500">Internal server error</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        _logger.LogInformation("Attempting to delete document with ID: {DocumentId}", id);

        try
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                _logger.LogWarning("Document with ID: {DocumentId} not found for deletion.", id);
                return NotFound(new { Error = "Document not found" });
            }

            _logger.LogInformation("Document {DocumentId} found. Proceeding with deletion of associated files and metadata.", id);

            // Attempt to delete S3 objects first
            try
            {
                if (!string.IsNullOrEmpty(document.OriginalS3Key))
                {
                    _logger.LogInformation("Deleting original S3 object with key: {S3Key} for document {DocumentId}", document.OriginalS3Key, id);
                    await _fileStore.DeleteAsync(document.OriginalS3Key);
                    _logger.LogInformation("Successfully deleted original S3 object with key: {S3Key}", document.OriginalS3Key);
                }
                if (!string.IsNullOrEmpty(document.RedactedS3Key))
                {
                    _logger.LogInformation("Deleting redacted S3 object with key: {S3Key} for document {DocumentId}", document.RedactedS3Key, id);
                    await _fileStore.DeleteAsync(document.RedactedS3Key);
                    _logger.LogInformation("Successfully deleted redacted S3 object with key: {S3Key}", document.RedactedS3Key);
                }
            }
            catch (Exception ex)
            {
                // Log S3 deletion errors but proceed to delete the metadata entry
                _logger.LogError(ex, "Error deleting S3 object(s) for document {DocumentId}. Metadata deletion will still be attempted. Error: {ErrorMessage}", id, ex.Message);
            }

            // Delete document metadata from repository
            _logger.LogInformation("Deleting document metadata for ID: {DocumentId}", id);
            await _documentRepository.DeleteAsync(id);
            _logger.LogInformation("Successfully deleted document metadata for ID: {DocumentId}", id);

            return NoContent();
        }
        catch (InvalidOperationException ex) // Catch specific exceptions from repository/filestore if they are critical for the operation
        {
            _logger.LogError(ex, "A critical operation failed during document deletion for ID: {DocumentId}. Error: {ErrorMessage}", id, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = $"A critical error occurred while deleting the document: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting document with ID: {DocumentId}. Error: {ErrorMessage}", id, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "An unexpected error occurred while deleting the document" });
        }
    }
}
