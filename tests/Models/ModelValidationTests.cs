using AuthPilot.Models;
using AuthPilot.Tests.Fixtures;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace AuthPilot.Tests.Models;

/// <summary>
/// Unit tests for data models and their serialization
/// </summary>
public class ModelValidationTests
{
    [Fact]
    public void AuthorizationDocument_SerializesToBson_Correctly()
    {
        // Arrange
        var document = TestData.CreateSampleAuthorizationDocument();
        document.ExtractedData = TestData.CreateSampleExtractedData();

        // Act
        var bsonDocument = document.ToBsonDocument();

        // Assert
        bsonDocument.Should().NotBeNull();
        bsonDocument.Contains("_id").Should().BeTrue("should have _id field");
        bsonDocument.Contains("blobName").Should().BeTrue("should have blobName field");
        bsonDocument.Contains("fileName").Should().BeTrue("should have fileName field");
        bsonDocument.Contains("uploadedAt").Should().BeTrue("should have uploadedAt field");
        bsonDocument.Contains("status").Should().BeTrue("should have status field");
        bsonDocument.Contains("extractedData").Should().BeTrue("should have extractedData field");
        
        // Verify field values
        bsonDocument["blobName"].AsString.Should().Be(document.BlobName);
        bsonDocument["status"].AsString.Should().Be(document.Status);
    }

    [Fact]
    public void AuthorizationDocument_DeserializesFromBson_Correctly()
    {
        // Arrange
        var originalDocument = TestData.CreateSampleAuthorizationDocument();
        originalDocument.ExtractedData = TestData.CreateSampleExtractedData();
        
        var bsonDocument = originalDocument.ToBsonDocument();

        // Act
        var deserializedDocument = BsonSerializer.Deserialize<AuthorizationDocument>(bsonDocument);

        // Assert
        deserializedDocument.Should().NotBeNull();
        deserializedDocument.Id.Should().Be(originalDocument.Id);
        deserializedDocument.BlobName.Should().Be(originalDocument.BlobName);
        deserializedDocument.FileName.Should().Be(originalDocument.FileName);
        deserializedDocument.Status.Should().Be(originalDocument.Status);
        deserializedDocument.ExtractedData.Should().NotBeNull();
        deserializedDocument.ExtractedData!.PatientName.Should().Be(originalDocument.ExtractedData!.PatientName);
    }

    [Fact]
    public void ExtractedAuthorizationData_WithAllFields_SerializesCorrectly()
    {
        // Arrange
        var extractedData = TestData.CreateSampleExtractedData();

        // Act
        var bsonDocument = extractedData.ToBsonDocument();

        // Assert
        bsonDocument.Should().NotBeNull();
        
        // Verify patient fields
        bsonDocument.Contains("patientName").Should().BeTrue();
        bsonDocument.Contains("memberId").Should().BeTrue();
        bsonDocument["patientName"].AsString.Should().Be(extractedData.PatientName);
        
        // Verify provider fields
        bsonDocument.Contains("providerName").Should().BeTrue();
        bsonDocument["providerName"].AsString.Should().Be(extractedData.ProviderName);
        
        // Verify service fields
        bsonDocument.Contains("serviceType").Should().BeTrue();
        bsonDocument.Contains("cptCodes").Should().BeTrue();
        bsonDocument.Contains("icd10Codes").Should().BeTrue();
        
        // Verify array fields
        var cptCodes = bsonDocument["cptCodes"].AsBsonArray;
        cptCodes.Should().HaveCount(extractedData.CptCodes.Count);
    }

    [Fact]
    public void ExtractedAuthorizationData_WithNullableFields_HandlesNulls()
    {
        // Arrange
        var extractedData = TestData.CreateMinimalExtractedData();

        // Act
        var bsonDocument = extractedData.ToBsonDocument();

        // Assert
        bsonDocument.Should().NotBeNull();
        
        // Required fields should be present
        bsonDocument.Contains("patientName").Should().BeTrue();
        bsonDocument.Contains("memberId").Should().BeTrue();
        
        // Nullable fields should handle null values
        // BSON omits null values by default, or includes them as BsonNull
        if (bsonDocument.Contains("dateOfBirth"))
        {
            bsonDocument["dateOfBirth"].IsBsonNull.Should().BeTrue();
        }
        
        if (bsonDocument.Contains("policyNumber"))
        {
            bsonDocument["policyNumber"].IsBsonNull.Should().BeTrue();
        }
    }

    [Fact]
    public void ExtractedAuthorizationData_CptCodesList_HandlesEmptyList()
    {
        // Arrange
        var extractedData = TestData.CreateMinimalExtractedData();
        extractedData.CptCodes = new List<string>();

        // Act
        var bsonDocument = extractedData.ToBsonDocument();

        // Assert
        bsonDocument.Should().NotBeNull();
        bsonDocument.Contains("cptCodes").Should().BeTrue();
        
        var cptCodes = bsonDocument["cptCodes"].AsBsonArray;
        cptCodes.Should().BeEmpty("empty list should serialize to empty BSON array");
    }

    [Fact]
    public void ExtractedAuthorizationData_Icd10CodesList_HandlesEmptyList()
    {
        // Arrange
        var extractedData = TestData.CreateMinimalExtractedData();
        extractedData.Icd10Codes = new List<string>();

        // Act
        var bsonDocument = extractedData.ToBsonDocument();

        // Assert
        bsonDocument.Should().NotBeNull();
        bsonDocument.Contains("icd10Codes").Should().BeTrue();
        
        var icd10Codes = bsonDocument["icd10Codes"].AsBsonArray;
        icd10Codes.Should().BeEmpty("empty list should serialize to empty BSON array");
    }

    [Fact]
    public void AuthorizationDocument_WithProcessingStatus_IsValid()
    {
        // Arrange & Act
        var document = TestData.CreateSampleAuthorizationDocument(status: "processing");

        // Assert
        document.Status.Should().Be("processing");
        document.ProcessedAt.Should().BeNull("processing documents should not have processedAt");
        document.ErrorMessage.Should().BeNull("processing documents should not have errors");
        document.ExtractedData.Should().BeNull("processing documents should not have extracted data yet");
    }

    [Fact]
    public void AuthorizationDocument_WithCompletedStatus_IsValid()
    {
        // Arrange & Act
        var document = TestData.CreateSampleAuthorizationDocument(status: "completed");
        document.ProcessedAt = DateTime.UtcNow;
        document.ExtractedData = TestData.CreateSampleExtractedData();

        // Assert
        document.Status.Should().Be("completed");
        document.ProcessedAt.Should().NotBeNull("completed documents should have processedAt");
        document.ExtractedData.Should().NotBeNull("completed documents should have extracted data");
        document.ErrorMessage.Should().BeNull("completed documents should not have errors");
    }

    [Fact]
    public void AuthorizationDocument_WithFailedStatus_IsValid()
    {
        // Arrange & Act
        var document = TestData.CreateSampleAuthorizationDocument(status: "failed");
        document.ProcessedAt = DateTime.UtcNow;
        document.ErrorMessage = "Test error message";

        // Assert
        document.Status.Should().Be("failed");
        document.ProcessedAt.Should().NotBeNull("failed documents should have processedAt");
        document.ErrorMessage.Should().NotBeNullOrEmpty("failed documents should have error message");
    }

    [Fact]
    public void ExtractedAuthorizationData_AllPropertiesAccessible_WithoutException()
    {
        // Arrange
        var extractedData = TestData.CreateSampleExtractedData();

        // Act & Assert - Verify all properties can be accessed
        Action act = () =>
        {
            _ = extractedData.PatientName;
            _ = extractedData.DateOfBirth;
            _ = extractedData.MemberId;
            _ = extractedData.PolicyNumber;
            _ = extractedData.ProviderName;
            _ = extractedData.NpiNumber;
            _ = extractedData.ProviderContact;
            _ = extractedData.FacilityName;
            _ = extractedData.FacilityAddress;
            _ = extractedData.ReferringProvider;
            _ = extractedData.ServiceType;
            _ = extractedData.CptCodes;
            _ = extractedData.Icd10Codes;
            _ = extractedData.ServiceStartDate;
            _ = extractedData.ServiceEndDate;
            _ = extractedData.UnitsRequested;
            _ = extractedData.UrgencyLevel;
            _ = extractedData.ClinicalNotes;
            _ = extractedData.FaxReceivedDate;
            _ = extractedData.PageCount;
            _ = extractedData.FaxDate;
            _ = extractedData.InsuranceCompany;
            _ = extractedData.InsuranceFaxNumber;
            _ = extractedData.SenderFaxNumber;
        };

        act.Should().NotThrow("all properties should be accessible without exception");
    }

    [Fact]
    public void AuthorizationDocument_RoundTrip_PreservesData()
    {
        // Arrange
        var originalDocument = TestData.CreateSampleAuthorizationDocument();
        originalDocument.ExtractedData = TestData.CreateSampleExtractedData();
        originalDocument.ProcessedAt = DateTime.UtcNow;

        // Act - Serialize to BSON and back
        var bsonDocument = originalDocument.ToBsonDocument();
        var deserializedDocument = BsonSerializer.Deserialize<AuthorizationDocument>(bsonDocument);

        // Assert - Verify data integrity
        deserializedDocument.Id.Should().Be(originalDocument.Id);
        deserializedDocument.BlobName.Should().Be(originalDocument.BlobName);
        deserializedDocument.FileName.Should().Be(originalDocument.FileName);
        deserializedDocument.Status.Should().Be(originalDocument.Status);
        deserializedDocument.ExtractedData.Should().NotBeNull();
        deserializedDocument.ExtractedData!.PatientName.Should().Be(originalDocument.ExtractedData!.PatientName);
        deserializedDocument.ExtractedData!.CptCodes.Should().BeEquivalentTo(originalDocument.ExtractedData!.CptCodes);
    }
}
