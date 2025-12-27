using AuthPilot.Models;

namespace AuthPilot.Tests.Fixtures;

/// <summary>
/// Provides sample test data for unit tests
/// </summary>
public static class TestData
{
    /// <summary>
    /// Generates a sample AuthorizationDocument for testing
    /// </summary>
    public static AuthorizationDocument CreateSampleAuthorizationDocument(
        string? id = null,
        string? status = "processing")
    {
        return new AuthorizationDocument
        {
            Id = id ?? "507f1f77bcf86cd799439011",
            BlobName = "test-fax.pdf",
            FileName = "test-fax.pdf",
            UploadedAt = DateTime.UtcNow,
            Status = status ?? "processing",
            ExtractedData = null,
            ProcessedAt = null,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Generates a sample ExtractedAuthorizationData with all fields populated
    /// </summary>
    public static ExtractedAuthorizationData CreateSampleExtractedData()
    {
        return new ExtractedAuthorizationData
        {
            // Patient fields
            PatientName = "John Doe",
            DateOfBirth = new DateTime(1980, 5, 15),
            MemberId = "MEM123456",
            PolicyNumber = "POL789012",

            // Provider fields
            ProviderName = "Dr. Jane Smith",
            NpiNumber = "1234567890",
            ProviderContact = "(555) 123-4567",
            FacilityName = "General Hospital",
            FacilityAddress = "123 Main St, City, ST 12345",
            ReferringProvider = "Dr. Bob Johnson",

            // Service fields
            ServiceType = "Physical Therapy",
            CptCodes = new List<string> { "97110", "97140" },
            Icd10Codes = new List<string> { "M54.5", "M25.561" },
            ServiceStartDate = DateTime.UtcNow.AddDays(7),
            ServiceEndDate = DateTime.UtcNow.AddDays(37),
            UnitsRequested = "12",
            UrgencyLevel = "Standard",

            // Clinical fields
            ClinicalNotes = "Patient requires physical therapy following knee surgery.",

            // Administrative fields
            FaxReceivedDate = DateTime.UtcNow,
            PageCount = 3,

            // Fax Header fields
            FaxDate = DateTime.UtcNow,
            InsuranceCompany = "Blue Cross Blue Shield",
            InsuranceFaxNumber = "(555) 987-6543",
            SenderFaxNumber = "(555) 123-4567"
        };
    }

    /// <summary>
    /// Generates a sample ExtractedAuthorizationData with minimal/nullable fields
    /// </summary>
    public static ExtractedAuthorizationData CreateMinimalExtractedData()
    {
        return new ExtractedAuthorizationData
        {
            PatientName = "John Doe",
            MemberId = "MEM123456",
            ProviderName = "Dr. Jane Smith",
            ServiceType = "Physical Therapy",
            CptCodes = new List<string>(),
            Icd10Codes = new List<string>(),
            
            // Nullable fields left as null
            DateOfBirth = null,
            PolicyNumber = null,
            NpiNumber = null,
            ProviderContact = null,
            FacilityName = null,
            FacilityAddress = null,
            ReferringProvider = null,
            ServiceStartDate = null,
            ServiceEndDate = null,
            UnitsRequested = null,
            UrgencyLevel = null,
            ClinicalNotes = null,
            FaxReceivedDate = null,
            PageCount = null,
            FaxDate = null,
            InsuranceCompany = null,
            InsuranceFaxNumber = null,
            SenderFaxNumber = null
        };
    }

    /// <summary>
    /// Creates a sample memory stream with test content
    /// </summary>
    public static MemoryStream CreateSampleBlobStream()
    {
        var content = "Sample fax document content for testing";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }
}
