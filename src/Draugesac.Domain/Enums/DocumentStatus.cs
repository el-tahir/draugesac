namespace Draugesac.Domain.Enums;

/// <summary>
/// Represents the processing status of a document in the system.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// Document has been uploaded to the system but not yet processed.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Document is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// Document processing has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Document processing has failed.
    /// </summary>
    Failed
}
