using AuthPilot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AuthPilot.Services;

/// <summary>
/// MongoDB service for managing authorization documents
/// </summary>
public class MongoDbService : IMongoDbService
{
    private readonly IMongoCollection<AuthorizationDocument> _collection;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration["MongoDBConnectionString"] 
            ?? throw new InvalidOperationException("MongoDBConnectionString not configured");
        var databaseName = configuration["MongoDBDatabase"] 
            ?? throw new InvalidOperationException("MongoDBDatabase not configured");
        var collectionName = configuration["MongoDBCollection"] 
            ?? throw new InvalidOperationException("MongoDBCollection not configured");

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<AuthorizationDocument>(collectionName);
        
        _logger.LogInformation("MongoDB service initialized: {Database}/{Collection}", databaseName, collectionName);
    }

    public async Task<string> CreateAuthorizationDocumentAsync(string blobName, string fileName, DateTime uploadedAt)
    {
        ArgumentNullException.ThrowIfNull(blobName, nameof(blobName));
        ArgumentNullException.ThrowIfNull(fileName, nameof(fileName));
        
        try
        {
            var document = new AuthorizationDocument
            {
                BlobName = blobName,
                FileName = fileName,
                UploadedAt = uploadedAt,
                Status = "processing"
            };

            await _collection.InsertOneAsync(document);
            _logger.LogInformation("Created authorization document with ID: {Id}, Status: processing", document.Id);
            
            return document.Id!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create authorization document for blob: {BlobName}", blobName);
            throw;
        }
    }

    public async Task UpdateAuthorizationWithExtractedDataAsync(string documentId, ExtractedAuthorizationData extractedData)
    {
        try
        {
            var filter = Builders<AuthorizationDocument>.Filter.Eq(d => d.Id, documentId);
            var update = Builders<AuthorizationDocument>.Update
                .Set(d => d.ExtractedData, extractedData)
                .Set(d => d.Status, "completed")
                .Set(d => d.ProcessedAt, DateTime.UtcNow);

            await _collection.UpdateOneAsync(filter, update);
            _logger.LogInformation("Updated authorization document {Id} with extracted data, Status: completed", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update authorization document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task MarkAuthorizationAsFailedAsync(string documentId, string errorMessage)
    {
        try
        {
            var filter = Builders<AuthorizationDocument>.Filter.Eq(d => d.Id, documentId);
            var update = Builders<AuthorizationDocument>.Update
                .Set(d => d.Status, "failed")
                .Set(d => d.ErrorMessage, errorMessage)
                .Set(d => d.ProcessedAt, DateTime.UtcNow);

            await _collection.UpdateOneAsync(filter, update);
            _logger.LogWarning("Marked authorization document {Id} as failed: {ErrorMessage}", documentId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark authorization document as failed: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<AuthorizationDocument?> GetAuthorizationByIdAsync(string documentId)
    {
        try
        {
            var filter = Builders<AuthorizationDocument>.Filter.Eq(d => d.Id, documentId);
            var document = await _collection.Find(filter).FirstOrDefaultAsync();
            
            _logger.LogInformation("Retrieved authorization document: {Id}", documentId);
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve authorization document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> IsBlobAlreadyProcessedAsync(string blobName)
    {
        try
        {
            var filter = Builders<AuthorizationDocument>.Filter.Eq(d => d.BlobName, blobName);
            var existingDoc = await _collection.Find(filter).FirstOrDefaultAsync();
            
            if (existingDoc != null)
            {
                _logger.LogInformation("Blob {BlobName} already processed (Document ID: {Id})", blobName, existingDoc.Id);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if blob already processed: {BlobName}", blobName);
            throw;
        }
    }
}
