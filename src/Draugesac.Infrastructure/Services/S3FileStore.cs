using Amazon.S3;
using Amazon.S3.Model;
using Draugesac.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Draugesac.Infrastructure.Services;

/// <summary>
/// AWS S3 implementation of the file storage interface.
/// Handles document upload and secure access via presigned URLs.
/// </summary>
public class S3FileStore : IFileStore
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3FileStore> _logger;

    /// <summary>
    /// Initializes a new instance of the S3FileStore.
    /// </summary>
    /// <param name="s3Client">The AWS S3 client for storage operations</param>
    /// <param name="bucketName">The S3 bucket name for document storage</param>
    /// <param name="logger">Logger for tracking operations and errors</param>
    public S3FileStore(IAmazonS3 s3Client, string bucketName, ILogger<S3FileStore> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = !string.IsNullOrWhiteSpace(bucketName)
            ? bucketName
            : throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Uploads a file stream to S3 storage with proper metadata and security settings.
    /// </summary>
    /// <param name="stream">The file stream to upload</param>
    /// <param name="fileName">The original filename for metadata</param>
    /// <param name="contentType">The MIME content type of the file</param>
    /// <returns>The S3 key (unique identifier) for the uploaded file</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    /// <exception cref="InvalidOperationException">Thrown when S3 upload fails</exception>
    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
    {
        // Input validation
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be null or empty", nameof(contentType));
        }

        // Generate unique S3 key with timestamp and GUID
        string timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd");
        string uniqueId = Guid.NewGuid().ToString("N");
        string sanitizedFileName = SanitizeFileName(fileName);
        string s3Key = $"documents/{timestamp}/{uniqueId}-{sanitizedFileName}";

        _logger.LogInformation("Uploading file {FileName} to S3 with key {S3Key}", fileName, s3Key);

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = stream,
                ContentType = contentType,

                // Security and metadata settings
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                Metadata =
                {
                    ["original-filename"] = fileName,
                    ["upload-timestamp"] = DateTime.UtcNow.ToString("O"),
                    ["content-type"] = contentType
                }
            };

            PutObjectResponse response = await _s3Client.PutObjectAsync(request);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully uploaded file {FileName} to S3 with key {S3Key}", fileName, s3Key);
                return s3Key;
            }

            throw new InvalidOperationException($"S3 upload failed with status code: {response.HttpStatusCode}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error uploading file {FileName}: {ErrorMessage}", fileName, ex.Message);
            throw new InvalidOperationException($"Failed to upload file to S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading file {FileName}: {ErrorMessage}", fileName, ex.Message);
            throw new InvalidOperationException($"Unexpected error during file upload: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Downloads a file from S3 storage as a stream.
    /// </summary>
    /// <param name="key">The S3 key (identifier) of the file to download</param>
    /// <returns>A stream containing the file data</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when S3 download fails</exception>
    public async Task<Stream> DownloadAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("S3 key cannot be null or empty", nameof(key));
        }

        _logger.LogInformation("Downloading file from S3 with key {S3Key}", key);

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            GetObjectResponse response = await _s3Client.GetObjectAsync(request);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully downloaded file from S3 with key {S3Key}", key);
                return response.ResponseStream;
            }

            throw new InvalidOperationException($"S3 download failed with status code: {response.HttpStatusCode}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error downloading file with key {S3Key}: {ErrorMessage}", key, ex.Message);
            throw new InvalidOperationException($"Failed to download file from S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading file with key {S3Key}: {ErrorMessage}", key, ex.Message);
            throw new InvalidOperationException($"Unexpected error during file download: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a presigned URL for secure, temporary access to a stored file.
    /// </summary>
    /// <param name="key">The S3 key (identifier) of the file</param>
    /// <returns>A presigned URL that allows temporary access to the file</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when URL generation fails</exception>
    public async Task<string> GetPresignedUrlAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("S3 key cannot be null or empty", nameof(key));
        }

        _logger.LogInformation("Generating presigned URL for S3 key {S3Key}", key);

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(1), // URL valid for 1 hour
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = $"attachment; filename=\"{ExtractOriginalFileName(key)}\""
                }
            };

            string presignedUrl = await _s3Client.GetPreSignedURLAsync(request);

            _logger.LogInformation("Successfully generated presigned URL for S3 key {S3Key}", key);
            return presignedUrl;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error generating presigned URL for key {S3Key}: {ErrorMessage}", key, ex.Message);
            throw new InvalidOperationException($"Failed to generate presigned URL: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating presigned URL for key {S3Key}: {ErrorMessage}", key, ex.Message);
            throw new InvalidOperationException($"Unexpected error during presigned URL generation: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sanitizes a filename to ensure it's safe for S3 storage.
    /// </summary>
    /// <param name="fileName">The original filename</param>
    /// <returns>A sanitized filename safe for S3</returns>
    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid characters and replace with underscores
        IEnumerable<char> invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '(', ')', '[', ']', '{', '}' });
        string sanitized = fileName;

        foreach (char invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        // Ensure filename doesn't exceed reasonable length
        if (sanitized.Length > 100)
        {
            string extension = Path.GetExtension(sanitized);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = nameWithoutExtension[..Math.Min(nameWithoutExtension.Length, 100 - extension.Length)] + extension;
        }

        return sanitized;
    }

    /// <summary>
    /// Extracts the original filename from an S3 key for download purposes.
    /// </summary>
    /// <param name="s3Key">The S3 key containing the filename</param>
    /// <returns>The original filename or a generic name if extraction fails</returns>
    private static string ExtractOriginalFileName(string s3Key)
    {
        try
        {
            // Extract filename from pattern: documents/yyyy/MM/dd/guid-originalname.ext
            string[] parts = s3Key.Split('/');
            if (parts.Length > 0)
            {
                string lastPart = parts[^1]; // Last part contains guid-filename
                int dashIndex = lastPart.IndexOf('-');
                if (dashIndex >= 0 && dashIndex < lastPart.Length - 1)
                {
                    return lastPart[(dashIndex + 1)..]; // Return part after first dash
                }
            }
        }
        catch
        {
            // Fallback to generic name if extraction fails
        }

        return "document";
    }

    /// <summary>
    /// Deletes a file from S3.
    /// </summary>
    /// <param name="s3Key">The key of the file in S3 to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(string s3Key)
    {
        if (string.IsNullOrEmpty(s3Key))
        {
            _logger.LogWarning("Attempted to delete file with null or empty S3 key.");
            throw new ArgumentNullException(nameof(s3Key), "S3 key cannot be null or empty.");
        }

        try
        {
            _logger.LogInformation("Deleting file {S3Key} from S3 bucket {BucketName}", s3Key, _bucketName);

            var deleteObjectRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            var response = await _s3Client.DeleteObjectAsync(deleteObjectRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent)
            {
                _logger.LogInformation("Successfully deleted file {S3Key} from S3 bucket {BucketName}", s3Key, _bucketName);
            }
            else
            {
                _logger.LogWarning("Failed to delete file {S3Key} from S3. Status code: {StatusCode}", s3Key, response.HttpStatusCode);
                // Consider throwing a more specific exception or handling based on status code
                throw new InvalidOperationException($"Failed to delete file {s3Key} from S3. Status code: {response.HttpStatusCode}");
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error deleting file {S3Key}: {ErrorMessage}", s3Key, ex.Message);
            throw; // Re-throw to allow controller to handle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting file {S3Key} from S3: {ErrorMessage}", s3Key, ex.Message);
            throw; // Re-throw to allow controller to handle
        }
    }
}
