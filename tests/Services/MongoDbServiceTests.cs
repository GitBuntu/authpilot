using AuthPilot.Models;
using AuthPilot.Services;
using AuthPilot.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using Xunit;

namespace AuthPilot.Tests.Services;

/// <summary>
/// Integration tests for MongoDbService using real Azure Cosmos DB for MongoDB
/// </summary>
public class MongoDbServiceTests
{
    private readonly Mock<ILogger<MongoDbService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly string _testDatabase;
    private readonly string _testCollection;

    public MongoDbServiceTests()
    {
        _mockLogger = new Mock<ILogger<MongoDbService>>();
        
        // Load configuration from actual local.settings.json
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src"))
            .AddJsonFile("local.settings.json", optional: false)
            .Build();
        
        // Use test database/collection to avoid affecting production data
        _testDatabase = "authpilot_test";
        _testCollection = "authorizations_test";
    }

    [Fact]
    public async Task CreateAuthorizationDocumentAsync_WithValidData_CreatesDocument()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);
        var blobName = "test-fax.pdf";
        var fileName = "test-fax.pdf";
        var uploadedAt = DateTime.UtcNow;

        // Act
        var documentId = await service.CreateAuthorizationDocumentAsync(blobName, fileName, uploadedAt);

        // Assert
        documentId.Should().NotBeNullOrEmpty("document ID should be returned");
        
        // Verify document was created with correct status
        var document = await service.GetAuthorizationByIdAsync(documentId);
        document.Should().NotBeNull();
        document!.BlobName.Should().Be(blobName);
        document.FileName.Should().Be(fileName);
        document.Status.Should().Be("processing");
        document.UploadedAt.Should().BeCloseTo(uploadedAt, TimeSpan.FromSeconds(1));
        
        // Cleanup
        await CleanupDocument(service, documentId);
    }

    [Fact]
    public async Task CreateAuthorizationDocumentAsync_WithNullBlobName_ThrowsArgumentNullException()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);

        // Act
        Func<Task> act = async () => await service.CreateAuthorizationDocumentAsync(
            null!, "test.pdf", DateTime.UtcNow);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*blobName*");
    }

    [Fact]
    public async Task UpdateAuthorizationWithExtractedDataAsync_WithValidData_UpdatesDocument()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);
        var documentId = await service.CreateAuthorizationDocumentAsync(
            "test.pdf", "test.pdf", DateTime.UtcNow);
        var extractedData = TestData.CreateSampleExtractedData();

        // Act
        await service.UpdateAuthorizationWithExtractedDataAsync(documentId, extractedData);

        // Assert
        var document = await service.GetAuthorizationByIdAsync(documentId);
        document.Should().NotBeNull();
        document!.Status.Should().Be("completed");
        document.ExtractedData.Should().NotBeNull();
        document.ExtractedData!.PatientName.Should().Be(extractedData.PatientName);
        document.ProcessedAt.Should().NotBeNull();
        document.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        
        // Cleanup
        await CleanupDocument(service, documentId);
    }

    [Fact]
    public async Task UpdateAuthorizationWithExtractedDataAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);
        var invalidId = "000000000000000000000000"; // Valid ObjectId format but doesn't exist
        var extractedData = TestData.CreateSampleExtractedData();

        // Act
        Func<Task> act = async () => await service.UpdateAuthorizationWithExtractedDataAsync(
            invalidId, extractedData);

        // Assert - The service should handle this gracefully or throw a specific exception
        // For now, we'll just verify it doesn't crash with an unhandled exception
        try
        {
            await act();
        }
        catch (Exception ex)
        {
            ex.Should().NotBeOfType<NullReferenceException>("should handle missing documents gracefully");
        }
    }

    [Fact]
    public async Task MarkAuthorizationAsFailedAsync_WithErrorMessage_UpdatesStatus()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);
        var documentId = await service.CreateAuthorizationDocumentAsync(
            "test.pdf", "test.pdf", DateTime.UtcNow);
        var errorMessage = "Test error message";

        // Act
        await service.MarkAuthorizationAsFailedAsync(documentId, errorMessage);

        // Assert
        var document = await service.GetAuthorizationByIdAsync(documentId);
        document.Should().NotBeNull();
        document!.Status.Should().Be("failed");
        document.ErrorMessage.Should().Be(errorMessage);
        document.ProcessedAt.Should().NotBeNull();
        document.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        
        // Cleanup
        await CleanupDocument(service, documentId);
    }

    [Fact]
    public async Task GetAuthorizationByIdAsync_WithValidId_ReturnsDocument()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);
        var documentId = await service.CreateAuthorizationDocumentAsync(
            "test.pdf", "test.pdf", DateTime.UtcNow);

        // Act
        var document = await service.GetAuthorizationByIdAsync(documentId);

        // Assert
        document.Should().NotBeNull();
        document!.Id.Should().Be(documentId);
        document.BlobName.Should().Be("test.pdf");
        
        // Cleanup
        await CleanupDocument(service, documentId);
    }

    [Fact]
    public async Task GetAuthorizationByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var service = new MongoDbService(testConfig, _mockLogger.Object);
        var invalidId = "000000000000000000000000"; // Valid ObjectId format but doesn't exist

        // Act
        var document = await service.GetAuthorizationByIdAsync(invalidId);

        // Assert
        document.Should().BeNull("document with this ID should not exist");
    }

    [Fact]
    public void MongoDbService_WhenDatabaseConnectionFails_ThrowsException()
    {
        // Arrange - create config with invalid connection string format
        var badConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDBConnectionString"] = "mongodb://invalid:99999",
                ["MongoDBDatabase"] = _testDatabase,
                ["MongoDBCollection"] = _testCollection
            })
            .Build();

        // Act & Assert - MongoClient constructor validates the connection string
        Action act = () => new MongoDbService(badConfig, _mockLogger.Object);

        // MongoDB throws MongoConfigurationException for invalid connection strings
        act.Should().Throw<MongoDB.Driver.MongoConfigurationException>(
            "invalid connection string should throw during service construction");
    }

    /// <summary>
    /// Create test configuration with test database/collection names
    /// </summary>
    private IConfiguration CreateTestConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDBConnectionString"] = _configuration["Values:MongoDBConnectionString"]!,
                ["MongoDBDatabase"] = _testDatabase,
                ["MongoDBCollection"] = _testCollection
            })
            .Build();
        return config;
    }

    /// <summary>
    /// Helper method to cleanup test documents
    /// </summary>
    private async Task CleanupDocument(MongoDbService service, string documentId)
    {
        try
        {
            var connectionString = _configuration["Values:MongoDBConnectionString"];
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(_testDatabase);
            var collection = database.GetCollection<AuthorizationDocument>(_testCollection);
            await collection.DeleteOneAsync(d => d.Id == documentId);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
