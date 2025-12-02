using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AuthPilot.Services;

namespace AuthPilot.Functions;

/// <summary>
/// Azure Function that processes fax documents uploaded to blob storage
/// </summary>
public class FaxProcessorFunction
{
    private readonly IMongoDbService _mongoDbService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly ILogger<FaxProcessorFunction> _logger;
    private readonly IConfiguration _configuration;

    public FaxProcessorFunction(
        IMongoDbService mongoDbService,
        IDocumentIntelligenceService documentIntelligenceService,
        ILogger<FaxProcessorFunction> logger,
        IConfiguration configuration)
    {
        _mongoDbService = mongoDbService;
        _documentIntelligenceService = documentIntelligenceService;
        _logger = logger;
        _configuration = configuration;
    }

    [Function("ProcessFax")]
    public async Task Run(
        [BlobTrigger("faxes/{name}", Connection = "BlobStorageConnection")] Stream blobStream,
        string name,
        FunctionContext context)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("ProcessFax triggered for blob: {BlobName}", name);

        string? documentId = null;

        try
        {
            // Step 1: Initialize
            var fileName = Path.GetFileName(name);
            var uploadedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Processing file: {FileName}, Size: {Size} bytes", fileName, blobStream.Length);

            // Step 2: Create MongoDB document with "processing" status
            documentId = await _mongoDbService.CreateAuthorizationDocumentAsync(name, fileName, uploadedAt);
            _logger.LogInformation("Created MongoDB document: {DocumentId}, Status: processing", documentId);

            // Step 3: Analyze document with Document Intelligence
            var modelId = _configuration["DocumentIntelligenceModelId"] 
                ?? throw new InvalidOperationException("DocumentIntelligenceModelId not configured");
            
            _logger.LogInformation("Analyzing document with model: {ModelId}", modelId);
            var extractedData = await _documentIntelligenceService.AnalyzeFaxDocumentAsync(blobStream, modelId);
            
            _logger.LogInformation("Document analysis completed. Patient: {PatientName}, MemberId: {MemberId}", 
                extractedData.PatientName ?? "N/A", 
                extractedData.MemberId ?? "N/A");

            // Step 4: Update MongoDB with extracted data
            await _mongoDbService.UpdateAuthorizationWithExtractedDataAsync(documentId, extractedData);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("ProcessFax completed successfully for {BlobName} in {Duration}ms", 
                name, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessFax failed for blob: {BlobName}. Error: {ErrorMessage}", name, ex.Message);

            // Mark document as failed if we have a document ID
            if (documentId != null)
            {
                try
                {
                    await _mongoDbService.MarkAuthorizationAsFailedAsync(documentId, ex.Message);
                    _logger.LogInformation("Marked document {DocumentId} as failed", documentId);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update document status to failed: {DocumentId}", documentId);
                }
            }

            // Don't rethrow - we've handled the error by marking the document as failed
        }
    }
}
