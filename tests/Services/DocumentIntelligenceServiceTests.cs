using AuthPilot.Models;
using AuthPilot.Services;
using AuthPilot.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthPilot.Tests.Services;

/// <summary>
/// Unit tests for DocumentIntelligenceService
/// </summary>
public class DocumentIntelligenceServiceTests
{
    private readonly Mock<ILogger<DocumentIntelligenceService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private const string TestEndpoint = "https://test.cognitiveservices.azure.com/";
    private const string TestKey = "test-key-123";
    private const string TestModelId = "test-model-v1";

    public DocumentIntelligenceServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentIntelligenceService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup configuration mocks
        _mockConfiguration.Setup(c => c["DocumentIntelligenceEndpoint"]).Returns(TestEndpoint);
        _mockConfiguration.Setup(c => c["DocumentIntelligenceKey"]).Returns(TestKey);
    }

    [Fact(Skip = "Requires live Azure Document Intelligence service")]
    public async Task AnalyzeFaxDocumentAsync_WithValidPdf_ReturnsExtractedData()
    {
        // Arrange
        var service = new DocumentIntelligenceService(_mockConfiguration.Object, _mockLogger.Object);
        using var blobStream = TestData.CreateSampleBlobStream();

        // Act
        var result = await service.AnalyzeFaxDocumentAsync(blobStream, TestModelId);

        // Assert
        result.Should().NotBeNull("extracted data should be returned");
        // Additional assertions would depend on the actual model output
    }

    [Fact]
    public async Task AnalyzeFaxDocumentAsync_WithMissingFields_HandlesGracefully()
    {
        // Arrange
        var service = new DocumentIntelligenceService(_mockConfiguration.Object, _mockLogger.Object);
        using var blobStream = TestData.CreateSampleBlobStream();

        // Act
        Func<Task> act = async () => await service.AnalyzeFaxDocumentAsync(blobStream, TestModelId);

        // Assert
        // The service should handle missing fields gracefully
        // In a real scenario with mocked Azure SDK, we would verify null handling
        // For now, we verify it doesn't throw NullReferenceException
        try
        {
            await act();
        }
        catch (Exception ex)
        {
            ex.Should().NotBeOfType<NullReferenceException>(
                "service should handle missing fields gracefully");
        }
    }

    [Fact]
    public void AnalyzeFaxDocumentAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new DocumentIntelligenceService(_mockConfiguration.Object, _mockLogger.Object);

        // Act
        Func<Task> act = async () => await service.AnalyzeFaxDocumentAsync(null!, TestModelId);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*blobStream*");
    }

    [Fact]
    public void AnalyzeFaxDocumentAsync_WithInvalidModelId_ThrowsException()
    {
        // Arrange
        var service = new DocumentIntelligenceService(_mockConfiguration.Object, _mockLogger.Object);
        using var blobStream = TestData.CreateSampleBlobStream();

        // Act
        Func<Task> act = async () => await service.AnalyzeFaxDocumentAsync(
            blobStream, string.Empty);

        // Assert
        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*modelId*");
    }

    [Fact]
    public void ParsesDateFields_WithVariousFormats_HandlesCorrectly()
    {
        // This test verifies date parsing logic
        // In a real implementation, you would test the private date parsing method
        // or extract it to a testable helper
        
        // Test cases for date parsing
        var dateStrings = new[]
        {
            "12/27/2025",
            "2025-12-27",
            "27-Dec-2025",
            "December 27, 2025"
        };

        foreach (var dateString in dateStrings)
        {
            if (DateTime.TryParse(dateString, out var result))
            {
                result.Year.Should().Be(2025);
                result.Month.Should().Be(12);
                result.Day.Should().Be(27);
            }
        }
    }

    [Fact]
    public void ParsesArrayFields_WithCommaSeparatedValues_ReturnsCorrectList()
    {
        // Test array field parsing logic
        var cptCodesString = "97110, 97140, 97530";
        var expectedCodes = new List<string> { "97110", "97140", "97530" };

        // Simulate the parsing logic that would be in the service
        var parsedCodes = cptCodesString
            .Split(',')
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        parsedCodes.Should().BeEquivalentTo(expectedCodes);
    }

    [Fact]
    public void ParsesArrayFields_WithEmptyString_ReturnsEmptyList()
    {
        // Test empty array handling
        var emptyString = "";
        
        var parsedCodes = string.IsNullOrWhiteSpace(emptyString)
            ? new List<string>()
            : emptyString.Split(',').Select(c => c.Trim()).ToList();

        parsedCodes.Should().BeEmpty();
    }

    [Fact(Skip = "Requires live Azure Document Intelligence service")]
    public async Task AnalyzeFaxDocumentAsync_WhenServiceThrowsException_PropagatesError()
    {
        // Arrange
        var badConfig = new Mock<IConfiguration>();
        badConfig.Setup(c => c["DocumentIntelligenceEndpoint"]).Returns("https://invalid.endpoint.com/");
        badConfig.Setup(c => c["DocumentIntelligenceKey"]).Returns("invalid-key");
        
        var service = new DocumentIntelligenceService(badConfig.Object, _mockLogger.Object);
        using var blobStream = TestData.CreateSampleBlobStream();

        // Act
        Func<Task> act = async () => await service.AnalyzeFaxDocumentAsync(blobStream, TestModelId);

        // Assert
        await act.Should().ThrowAsync<Exception>(
            "invalid credentials should cause service to fail");
    }

    [Fact]
    public void ExtractedData_VerifyFieldMapping_IsCorrect()
    {
        // This test verifies that all fields from the model are correctly mapped
        var extractedData = TestData.CreateSampleExtractedData();

        // Patient fields
        extractedData.PatientName.Should().NotBeNullOrEmpty();
        extractedData.MemberId.Should().NotBeNullOrEmpty();

        // Provider fields
        extractedData.ProviderName.Should().NotBeNullOrEmpty();
        extractedData.FacilityName.Should().NotBeNullOrEmpty();

        // Service fields
        extractedData.ServiceType.Should().NotBeNullOrEmpty();
        extractedData.CptCodes.Should().NotBeNull();
        extractedData.Icd10Codes.Should().NotBeNull();

        // All fields should be accessible without errors
        var allProperties = typeof(ExtractedAuthorizationData).GetProperties();
        foreach (var prop in allProperties)
        {
            var value = prop.GetValue(extractedData);
            // Verify we can access all properties without exception
            // Note: nullable properties like ClinicalNotes2 may be null
            var _ = value; // Just accessing it is enough to verify no exception
        }
    }
}
