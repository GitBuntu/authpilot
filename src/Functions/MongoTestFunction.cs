using AuthPilot.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AuthPilot.Functions;

/// <summary>
/// Test function to verify MongoDB connectivity
/// </summary>
public class MongoTestFunction
{
    private readonly IMongoDbService _mongoDbService;
    private readonly ILogger<MongoTestFunction> _logger;

    public MongoTestFunction(IMongoDbService mongoDbService, ILogger<MongoTestFunction> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    [Function("MongoTest")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test/mongo")] HttpRequestData req)
    {
        _logger.LogInformation("Testing MongoDB connection...");

        try
        {
            // Test 1: Create a test document
            var testDocId = await _mongoDbService.CreateAuthorizationDocumentAsync(
                blobName: "test-connection.txt",
                fileName: "test-connection.txt",
                uploadedAt: DateTime.UtcNow
            );

            _logger.LogInformation("✓ Successfully created test document with ID: {DocumentId}", testDocId);

            // Test 2: Retrieve the document we just created
            var retrievedDoc = await _mongoDbService.GetAuthorizationByIdAsync(testDocId);
            
            if (retrievedDoc == null)
            {
                throw new Exception("Failed to retrieve test document");
            }

            _logger.LogInformation("✓ Successfully retrieved test document");

            // Test 3: Check idempotency
            var alreadyProcessed = await _mongoDbService.IsBlobAlreadyProcessedAsync("test-connection.txt");
            _logger.LogInformation("✓ Idempotency check returned: {Result}", alreadyProcessed);

            // Test 4: Mark as completed
            await _mongoDbService.MarkAuthorizationAsFailedAsync(testDocId, "Test cleanup - marking as failed");
            _logger.LogInformation("✓ Successfully updated test document status");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($@"✅ MongoDB Connection Test PASSED

Test Results:
✓ Connection established successfully
✓ Document created with ID: {testDocId}
✓ Document retrieved successfully
✓ Idempotency check working
✓ Document update working

Database: authpilot
Collection: authorizations
Status: ✅ All operations successful

Note: Test document created and marked as failed for cleanup.
You can delete it manually or leave it for reference.
");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MongoDB connection test FAILED");

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($@"❌ MongoDB Connection Test FAILED

Error: {ex.Message}

Stack Trace:
{ex.StackTrace}

Troubleshooting:
1. Verify the password in MongoDBConnectionString (local.settings.json)
2. Check network connectivity to: mongodb-authpilot-dev.mongocluster.cosmos.azure.com
3. Ensure the database 'authpilot' exists
4. Verify the user 'dbadmin' has read/write permissions
5. Check firewall rules in Azure Cosmos DB
");

            return response;
        }
    }
}
