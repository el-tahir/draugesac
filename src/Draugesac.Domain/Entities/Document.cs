namespace Draugesac.Domain.Entities;

using Draugesac.Domain.Enums;

/// <summary>
/// Represents a document entity in the system that can be uploaded, processed, and stored.
/// </summary>
public class Document
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
    /// Gets or sets the timestamp when the document was last updated. Null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the S3 key for the original document. Null if not stored in S3.
    /// </summary>
    public string? OriginalS3Key { get; set; }

    /// <summary>
    /// Gets or sets the S3 key for the redacted version of the document. Null if not redacted or not stored in S3.
    /// </summary>
    public string? RedactedS3Key { get; set; }
}
