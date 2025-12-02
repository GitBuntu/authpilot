using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthPilot.Models;

/// <summary>
/// MongoDB document representing a prior authorization request
/// </summary>
public class AuthorizationDocument
{
    /// <summary>
    /// MongoDB document ID
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Name of the blob in Azure Storage
    /// </summary>
    [BsonElement("blobName")]
    public string BlobName { get; set; } = string.Empty;

    /// <summary>
    /// Original file name
    /// </summary>
    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the file was uploaded
    /// </summary>
    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Processing status: "processing", "completed", or "failed"
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "processing";

    /// <summary>
    /// Extracted authorization data from the fax
    /// </summary>
    [BsonElement("extractedData")]
    public ExtractedAuthorizationData? ExtractedData { get; set; }

    /// <summary>
    /// Timestamp when processing completed
    /// </summary>
    [BsonElement("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
