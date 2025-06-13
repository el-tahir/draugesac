namespace Draugesac.Application.Interfaces;

using Draugesac.Domain.Entities;
using Draugesac.Domain.Enums;

/// <summary>
/// Repository interface for managing Document entities in the data layer.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Retrieves a document by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the document</param>
    /// <returns>The document if found, otherwise null</returns>
    Task<Document?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieves all documents from the repository.
    /// </summary>
    /// <returns>A list of all documents</returns>
    Task<List<Document>> GetAllAsync();

    /// <summary>
    /// Retrieves documents by their status.
    /// </summary>
    /// <param name="status">The status of the documents</param>
    /// <returns>A list of documents with the specified status</returns>
    Task<List<Document>> GetByStatusAsync(DocumentStatus status);

    /// <summary>
    /// Adds a new document to the repository.
    /// </summary>
    /// <param name="document">The document to add</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task AddAsync(Document document);

    /// <summary>
    /// Updates an existing document in the repository.
    /// </summary>
    /// <param name="document">The document to update</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpdateAsync(Document document);

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="id">The ID of the document to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id);
}
