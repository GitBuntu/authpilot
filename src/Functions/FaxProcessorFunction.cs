using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AuthPilot.Services;

namespace AuthPilot.Functions;

/// <summary>
/// Azure Function that processes fax documents uploaded to blob storage
/// Organizes files into folders and processes with Document Intelligence
/// </summary>
public class FaxProcessorFunction
{
    private readonly IMongoDbService _mongoDbService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly ILogger<FaxProcessorFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly BlobServiceClient _blobServiceClient;

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
        
        var connectionString = configuration["BlobStorageConnection"] 
            ?? throw new InvalidOperationException("BlobStorageConnection not configured");
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    [Function("ProcessFax")]
    public async Task Run(
        [BlobTrigger("faxes/{name}", Connection = "BlobStorageConnection")] Stream blobStream,
        string name,
        FunctionContext context)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("ProcessFax triggered for blob: {BlobName}", name);

        // Skip if this is already in a subfolder (already organized)
        if (name.Contains('/'))
        {
            _logger.LogInformation("Blob {BlobName} is already in a subfolder, checking if it needs processing.", name);
            await ProcessOrganizedFax(name, blobStream, startTime);
            return;
        }

        // Only process supported fax formats
        if (!IsSupportedFaxFormat(name))
        {
            _logger.LogInformation("Skipping unsupported file format: {BlobName}", name);
            return;
        }

        // Move file to its own folder and process
        var folderName = Path.GetFileNameWithoutExtension(name);
        var newBlobPath = $"{folderName}/{name}";
        
        _logger.LogInformation("Organizing fax: moving {OldPath} to {NewPath}", name, newBlobPath);

        try
        {
            // Copy to new location
            var containerClient = _blobServiceClient.GetBlobContainerClient("faxes");
            var sourceBlob = containerClient.GetBlobClient(name);
            var destBlob = containerClient.GetBlobClient(newBlobPath);

            // Copy the blob to the new location
            await destBlob.StartCopyFromUriAsync(sourceBlob.Uri);
            
            // Wait for copy to complete
            var destProperties = await destBlob.GetPropertiesAsync();
            while (destProperties.Value.CopyStatus == Azure.Storage.Blobs.Models.CopyStatus.Pending)
            {
                await Task.Delay(100);
                destProperties = await destBlob.GetPropertiesAsync();
            }

            if (destProperties.Value.CopyStatus != Azure.Storage.Blobs.Models.CopyStatus.Success)
            {
                throw new InvalidOperationException($"Blob copy failed with status: {destProperties.Value.CopyStatus}");
            }

            // Delete the original blob
            await sourceBlob.DeleteIfExistsAsync();
            _logger.LogInformation("Successfully moved blob to {NewPath}", newBlobPath);

            // The new blob will trigger this function again (in the subfolder path)
            // So we don't process here - let the re-trigger handle it
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to organize fax {BlobName}: {Error}", name, ex.Message);
        }
    }

    private async Task ProcessOrganizedFax(string blobPath, Stream blobStream, DateTime startTime)
    {
        var fileName = Path.GetFileName(blobPath);

        // Only process supported fax formats
        if (!IsSupportedFaxFormat(fileName))
        {
            _logger.LogInformation("Skipping unsupported file format: {BlobName}", blobPath);
            return;
        }

        // Idempotency check - skip if already processed
        if (await _mongoDbService.IsBlobAlreadyProcessedAsync(blobPath))
        {
            _logger.LogInformation("Blob {BlobName} already processed, skipping.", blobPath);
            return;
        }

        string? documentId = null;

        try
        {
            var uploadedAt = DateTime.UtcNow;
            _logger.LogInformation("Processing file: {FileName}, Size: {Size} bytes", fileName, blobStream.Length);

            // Create MongoDB document with "processing" status
            documentId = await _mongoDbService.CreateAuthorizationDocumentAsync(blobPath, fileName, uploadedAt);
            _logger.LogInformation("Created MongoDB document: {DocumentId}, Status: processing", documentId);

            // Analyze document with Document Intelligence
            var modelId = _configuration["DocumentIntelligenceModelId"] 
                ?? throw new InvalidOperationException("DocumentIntelligenceModelId not configured");
            
            _logger.LogInformation("Analyzing document with model: {ModelId}", modelId);
            var extractedData = await _documentIntelligenceService.AnalyzeFaxDocumentAsync(blobStream, modelId);
            
            _logger.LogInformation("Document analysis completed. Patient: {PatientName}, MemberId: {MemberId}", 
                extractedData.PatientName ?? "N/A", 
                extractedData.MemberId ?? "N/A");

            // Update MongoDB with extracted data
            await _mongoDbService.UpdateAuthorizationWithExtractedDataAsync(documentId, extractedData);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("ProcessFax completed successfully for {BlobName} in {Duration}ms", 
                blobPath, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessFax failed for blob: {BlobName}. Error: {ErrorMessage}", blobPath, ex.Message);

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
        }
    }

    private static bool IsSupportedFaxFormat(string fileName)
    {
        return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".tif", StringComparison.OrdinalIgnoreCase);
    }
}
