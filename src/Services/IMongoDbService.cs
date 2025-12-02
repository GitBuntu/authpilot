using AuthPilot.Models;

namespace AuthPilot.Services;

/// <summary>
/// Interface for MongoDB database operations
/// </summary>
public interface IMongoDbService
{
    /// <summary>
    /// Creates a new authorization document with "processing" status
    /// </summary>
    Task<string> CreateAuthorizationDocumentAsync(string blobName, string fileName, DateTime uploadedAt);
    
    /// <summary>
    /// Updates an authorization document with extracted data and sets status to "completed"
    /// </summary>
    Task UpdateAuthorizationWithExtractedDataAsync(string documentId, ExtractedAuthorizationData extractedData);
    
    /// <summary>
    /// Marks an authorization document as failed with an error message
    /// </summary>
    Task MarkAuthorizationAsFailedAsync(string documentId, string errorMessage);
    
    /// <summary>
    /// Retrieves an authorization document by its ID
    /// </summary>
    Task<AuthorizationDocument?> GetAuthorizationByIdAsync(string documentId);
    
    /// <summary>
    /// Checks if a blob has already been processed (idempotency check)
    /// </summary>
    Task<bool> IsBlobAlreadyProcessedAsync(string blobName);
}
