using AuthPilot.Models;

namespace AuthPilot.Services;

/// <summary>
/// Interface for Azure Document Intelligence operations
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Analyzes a fax document and extracts authorization data
    /// </summary>
    /// <param name="blobStream">The blob stream containing the document</param>
    /// <param name="modelId">The custom model ID to use for analysis</param>
    /// <returns>Extracted authorization data</returns>
    Task<ExtractedAuthorizationData> AnalyzeFaxDocumentAsync(Stream blobStream, string modelId);
}
