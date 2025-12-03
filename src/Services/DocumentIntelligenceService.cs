using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using AuthPilot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthPilot.Services;

/// <summary>
/// Service for analyzing fax documents using Azure Document Intelligence
/// </summary>
public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<DocumentIntelligenceService> _logger;

    public DocumentIntelligenceService(IConfiguration configuration, ILogger<DocumentIntelligenceService> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["DocumentIntelligenceEndpoint"] 
            ?? throw new InvalidOperationException("DocumentIntelligenceEndpoint not configured");
        var apiKey = configuration["DocumentIntelligenceKey"] 
            ?? throw new InvalidOperationException("DocumentIntelligenceKey not configured");

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        
        _logger.LogInformation("Document Intelligence service initialized with endpoint: {Endpoint}", endpoint);
    }

    public async Task<ExtractedAuthorizationData> AnalyzeFaxDocumentAsync(Stream blobStream, string modelId)
    {
        _logger.LogInformation("Starting document analysis with model: {ModelId}", modelId);

        try
        {
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, blobStream);
            var result = operation.Value;

            _logger.LogInformation("Document analysis completed. Documents found: {Count}", result.Documents.Count);

            if (result.Documents.Count == 0)
            {
                _logger.LogWarning("No documents found in analysis result");
                return new ExtractedAuthorizationData();
            }

            var extractedData = MapToExtractedData(result.Documents[0]);
            
            _logger.LogInformation("Field extraction completed. Patient: {PatientName}", extractedData.PatientName ?? "N/A");
            
            return extractedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document analysis failed for model: {ModelId}", modelId);
            throw;
        }
    }

    private ExtractedAuthorizationData MapToExtractedData(AnalyzedDocument document)
    {
        var data = new ExtractedAuthorizationData();
        var fields = document.Fields;

        // Patient fields
        data.PatientName = GetFieldValue(fields, "PatientName");
        data.DateOfBirth = GetFieldDateValue(fields, "DateOfBirth");
        data.MemberId = GetFieldValue(fields, "MemberId");
        data.PolicyNumber = GetFieldValue(fields, "PolicyNumber");

        // Provider fields
        data.ProviderName = GetFieldValue(fields, "ProviderName");
        data.NpiNumber = GetFieldValue(fields, "NpiNumber");
        data.ProviderContact = GetFieldValue(fields, "ProviderContact");
        data.FacilityName = GetFieldValue(fields, "FacilityName");
        data.FacilityAddress = GetFieldValue(fields, "FacilityAddress");
        data.ReferringProvider = GetFieldValue(fields, "ReferringProvider");

        // Service fields
        data.ServiceType = GetFieldValue(fields, "ServiceType");
        data.CptCodes = GetFieldListValue(fields, "CptCodes");
        data.Icd10Codes = GetFieldListValue(fields, "Icd10Codes");
        data.ServiceStartDate = GetFieldDateValue(fields, "ServiceStartDate");
        data.ServiceEndDate = GetFieldDateValue(fields, "ServiceEndDate");
        data.UnitsRequested = GetFieldValue(fields, "UnitsRequested");
        data.UrgencyLevel = GetFieldValue(fields, "UrgencyLevel");

        // Clinical fields
        data.ClinicalNotes = GetFieldValue(fields, "ClinicalNotes");
        data.ClinicalNotes2 = GetFieldValue(fields, "ClinicalNotes2");

        // Administrative fields
        data.FaxReceivedDate = GetFieldDateValue(fields, "FaxReceivedDate");
        data.PageCount = GetFieldIntValue(fields, "PageCount");

        // Fax Header fields
        data.FaxDate = GetFieldDateValue(fields, "FaxDate");
        data.InsuranceCompany = GetFieldValue(fields, "InsuranceCompany");
        data.InsuranceFaxNumber = GetFieldValue(fields, "InsuranceFaxNumber");
        data.SenderFaxNumber = GetFieldValue(fields, "SenderFaxNumber");
        data.SenderName = GetFieldValue(fields, "SenderName");

        LogExtractedFields(data);

        return data;
    }

    private string? GetFieldValue(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field) && field.FieldType == DocumentFieldType.String)
        {
            return field.Value.AsString();
        }
        
        _logger.LogDebug("Field not found or not string type: {FieldName}", fieldName);
        return null;
    }

    private DateTime? GetFieldDateValue(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field))
        {
            if (field.FieldType == DocumentFieldType.Date)
            {
                return field.Value.AsDate().DateTime;
            }
            else if (field.FieldType == DocumentFieldType.String)
            {
                if (DateTime.TryParse(field.Value.AsString(), out var parsedDate))
                {
                    return parsedDate;
                }
            }
        }
        
        _logger.LogDebug("Date field not found or invalid: {FieldName}", fieldName);
        return null;
    }

    private int? GetFieldIntValue(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field))
        {
            if (field.FieldType == DocumentFieldType.Int64)
            {
                return (int)field.Value.AsInt64();
            }
            else if (field.FieldType == DocumentFieldType.String)
            {
                if (int.TryParse(field.Value.AsString(), out var parsedInt))
                {
                    return parsedInt;
                }
            }
        }
        
        _logger.LogDebug("Int field not found or invalid: {FieldName}", fieldName);
        return null;
    }

    private List<string>? GetFieldListValue(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field))
        {
            if (field.FieldType == DocumentFieldType.List)
            {
                return field.Value.AsList()
                    .Where(f => f.FieldType == DocumentFieldType.String)
                    .Select(f => f.Value.AsString())
                    .ToList();
            }
            else if (field.FieldType == DocumentFieldType.String)
            {
                // Handle comma-separated values
                var value = field.Value.AsString();
                return value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
        }
        
        _logger.LogDebug("List field not found or invalid: {FieldName}", fieldName);
        return null;
    }

    private void LogExtractedFields(ExtractedAuthorizationData data)
    {
        var fieldCount = 0;
        if (!string.IsNullOrEmpty(data.PatientName)) fieldCount++;
        if (data.DateOfBirth.HasValue) fieldCount++;
        if (!string.IsNullOrEmpty(data.MemberId)) fieldCount++;
        if (!string.IsNullOrEmpty(data.PolicyNumber)) fieldCount++;
        if (!string.IsNullOrEmpty(data.ProviderName)) fieldCount++;
        if (!string.IsNullOrEmpty(data.NpiNumber)) fieldCount++;
        if (!string.IsNullOrEmpty(data.ServiceType)) fieldCount++;
        if (data.CptCodes?.Count > 0) fieldCount++;
        if (data.Icd10Codes?.Count > 0) fieldCount++;
        
        _logger.LogInformation("Extracted {FieldCount} key fields from document", fieldCount);
    }
}
