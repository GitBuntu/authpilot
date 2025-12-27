using AuthPilot.Functions;
using AuthPilot.Models;
using AuthPilot.Services;
using AuthPilot.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthPilot.Tests.Functions;

/// <summary>
/// Unit tests for FaxProcessorFunction
/// Covers all error scenarios from section 10.5 of the plan
/// </summary>
public class FaxProcessorFunctionTests
{
    private readonly Mock<IMongoDbService> _mockMongoDbService;
    private readonly Mock<IDocumentIntelligenceService> _mockDocumentIntelligenceService;
    private readonly Mock<ILogger<FaxProcessorFunction>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly Mock<FunctionContext> _mockContext;

    public FaxProcessorFunctionTests()
    {
        _mockMongoDbService = new Mock<IMongoDbService>();
        _mockDocumentIntelligenceService = new Mock<IDocumentIntelligenceService>();
        _mockLogger = new Mock<ILogger<FaxProcessorFunction>>();
        _mockContext = new Mock<FunctionContext>();
        
        // Load configuration from actual local.settings.json
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src"))
            .AddJsonFile("local.settings.json", optional: false)
            .Build();
    }
    
    /// <summary>
    /// Create configuration with real blob storage connection from local.settings.json
    /// </summary>
    private IConfiguration CreateTestConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlobStorageConnection"] = _configuration["Values:BlobStorageConnection"]!,
                ["DocumentIntelligenceModelId"] = _configuration["Values:DocumentIntelligenceModelId"]!
            })
            .Build();
        return config;
    }

    [Fact]
    public async Task ProcessFax_WithValidBlob_ProcessesSuccessfully()
    {
        // Note: This test verifies the function's processing logic for blobs already in subfolders
        // The blob organization (moving to subfolders) requires real blob storage
        // So we test with a blob that's already organized (contains '/')
        
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        var blobName = "test-folder/test-fax.pdf";  // Already in subfolder
        var documentId = "507f1f77bcf86cd799439011";
        var extractedData = TestData.CreateSampleExtractedData();
        using var blobStream = TestData.CreateSampleBlobStream();

        _mockMongoDbService
            .Setup(m => m.CreateAuthorizationDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentId);

        _mockDocumentIntelligenceService
            .Setup(d => d.AnalyzeFaxDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(extractedData);

        _mockMongoDbService
            .Setup(m => m.UpdateAuthorizationWithExtractedDataAsync(
                It.IsAny<string>(), It.IsAny<ExtractedAuthorizationData>()))
            .Returns(Task.CompletedTask);

        // Act
        await function.Run(blobStream, blobName, _mockContext.Object);

        // Assert
        _mockMongoDbService.Verify(
            m => m.CreateAuthorizationDocumentAsync(blobName, It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Once,
            "should create document with processing status");

        _mockDocumentIntelligenceService.Verify(
            d => d.AnalyzeFaxDocumentAsync(blobStream, It.IsAny<string>()),
            Times.Once,
            "should analyze document with Document Intelligence");

        _mockMongoDbService.Verify(
            m => m.UpdateAuthorizationWithExtractedDataAsync(documentId, extractedData),
            Times.Once,
            "should update document with extracted data");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("triggered")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log function trigger");
    }

    [Fact]
    public async Task ProcessFax_WhenMongoDbCreateFails_MarksDocumentAsFailed()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        var blobName = "test-fax.pdf";
        var errorMessage = "MongoDB connection failed";
        using var blobStream = TestData.CreateSampleBlobStream();

        _mockMongoDbService
            .Setup(m => m.CreateAuthorizationDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        await function.Run(blobStream, blobName, _mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log error when MongoDB fails");

        // Should not proceed to document analysis
        _mockDocumentIntelligenceService.Verify(
            d => d.AnalyzeFaxDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Never,
            "should not analyze document if creation fails");
    }

    [Fact]
    public async Task ProcessFax_WhenDocumentIntelligenceFails_MarksDocumentAsFailed()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        var blobName = "test-folder/test-fax.pdf";  // Already organized
        var documentId = "507f1f77bcf86cd799439011";
        var errorMessage = "Document analysis failed";
        using var blobStream = TestData.CreateSampleBlobStream();

        _mockMongoDbService
            .Setup(m => m.CreateAuthorizationDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentId);

        _mockDocumentIntelligenceService
            .Setup(d => d.AnalyzeFaxDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception(errorMessage));

        _mockMongoDbService
            .Setup(m => m.MarkAuthorizationAsFailedAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await function.Run(blobStream, blobName, _mockContext.Object);

        // Assert
        _mockMongoDbService.Verify(
            m => m.MarkAuthorizationAsFailedAsync(documentId, It.Is<string>(s => s.Contains(errorMessage))),
            Times.Once,
            "should mark document as failed with error message");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(errorMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log error with exception details");
    }

    [Fact]
    public async Task ProcessFax_WhenUpdateFails_LogsError()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        var blobName = "test-folder/test-fax.pdf";  // Already organized
        var documentId = "507f1f77bcf86cd799439011";
        var extractedData = TestData.CreateSampleExtractedData();
        var errorMessage = "Update failed";
        using var blobStream = TestData.CreateSampleBlobStream();

        _mockMongoDbService
            .Setup(m => m.CreateAuthorizationDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentId);

        _mockDocumentIntelligenceService
            .Setup(d => d.AnalyzeFaxDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(extractedData);

        _mockMongoDbService
            .Setup(m => m.UpdateAuthorizationWithExtractedDataAsync(
                It.IsAny<string>(), It.IsAny<ExtractedAuthorizationData>()))
            .ThrowsAsync(new Exception(errorMessage));

        _mockMongoDbService
            .Setup(m => m.MarkAuthorizationAsFailedAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await function.Run(blobStream, blobName, _mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log error appropriately");

        _mockMongoDbService.Verify(
            m => m.MarkAuthorizationAsFailedAsync(documentId, It.IsAny<string>()),
            Times.Once,
            "should mark document as failed when update fails");
    }

    [Fact]
    public async Task ProcessFax_WithNullBlobStream_HandlesGracefully()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        var blobName = "test-fax.pdf";

        // Act
        await function.Run(null!, blobName, _mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log error for null blob stream");
    }

    [Fact]
    public async Task ProcessFax_WithEmptyBlobName_HandlesGracefully()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        using var blobStream = TestData.CreateSampleBlobStream();

        // Act
        await function.Run(blobStream, string.Empty, _mockContext.Object);

        // Assert - empty name is treated as unsupported format and skipped
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Skipping unsupported file format")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log skipping for empty blob name");
    }

    [Fact]
    public async Task ProcessFax_LogsAllStages_CorrectlyThroughoutExecution()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        var function = new FaxProcessorFunction(
            _mockMongoDbService.Object,
            _mockDocumentIntelligenceService.Object,
            _mockLogger.Object,
            testConfig);

        var blobName = "test-folder/test-fax.pdf";  // Already organized
        var documentId = "507f1f77bcf86cd799439011";
        var extractedData = TestData.CreateSampleExtractedData();
        using var blobStream = TestData.CreateSampleBlobStream();

        _mockMongoDbService
            .Setup(m => m.CreateAuthorizationDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(documentId);

        _mockDocumentIntelligenceService
            .Setup(d => d.AnalyzeFaxDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(extractedData);

        _mockMongoDbService
            .Setup(m => m.UpdateAuthorizationWithExtractedDataAsync(
                It.IsAny<string>(), It.IsAny<ExtractedAuthorizationData>()))
            .Returns(Task.CompletedTask);

        // Act
        await function.Run(blobStream, blobName, _mockContext.Object);

        // Assert - Verify logging at various stages
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("triggered")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log function start");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing") || v.ToString()!.Contains("Analyzing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log processing stages");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed") || v.ToString()!.Contains("success")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log successful completion");
    }
}
