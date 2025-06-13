namespace Draugesac.Application.Interfaces;

/// <summary>
/// Represents the payload for a document redaction job.
/// </summary>
/// <param name="DocumentId">The unique identifier of the document to redact</param>
/// <param name="PhrasesToRedact">List of phrases/terms that should be redacted from the document</param>
public record RedactionJobPayload(Guid DocumentId, List<string> PhrasesToRedact);

/// <summary>
/// Interface for publishing messages to a message queue or event bus.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a redaction job message to the message queue for asynchronous processing.
    /// </summary>
    /// <param name="payload">The redaction job payload containing document ID and phrases to redact</param>
    /// <returns>A task representing the asynchronous publish operation</returns>
    Task PublishRedactionJobAsync(RedactionJobPayload payload);
}
