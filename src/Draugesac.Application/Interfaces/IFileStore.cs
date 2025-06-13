namespace Draugesac.Application.Interfaces;

/// <summary>
/// Interface for file storage operations, typically implemented by cloud storage providers like AWS S3.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Uploads a file stream to the storage provider.
    /// </summary>
    /// <param name="stream">The file stream to upload</param>
    /// <param name="fileName">The original filename</param>
    /// <param name="contentType">The MIME content type of the file</param>
    /// <returns>The storage key or identifier for the uploaded file</returns>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Downloads a file from the storage provider as a stream.
    /// </summary>
    /// <param name="key">The storage key or identifier of the file</param>
    /// <returns>A stream containing the file data</returns>
    Task<Stream> DownloadAsync(string key);

    /// <summary>
    /// Generates a presigned URL for accessing a stored file.
    /// </summary>
    /// <param name="key">The storage key or identifier of the file</param>
    /// <returns>A presigned URL that allows temporary access to the file</returns>
    Task<string> GetPresignedUrlAsync(string key);

    /// <summary>
    /// Deletes a file from the store.
    /// </summary>
    /// <param name="s3Key">The key of the file in S3 to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(string s3Key);
}
