using MongoDB.Bson.Serialization.Attributes;

namespace AuthPilot.Models;

/// <summary>
/// Data extracted from a prior authorization fax document
/// </summary>
public class ExtractedAuthorizationData
{
    // ===== Patient Information =====
    
    /// <summary>
    /// Full name of patient
    /// </summary>
    [BsonElement("patientName")]
    public string? PatientName { get; set; }

    /// <summary>
    /// Patient date of birth
    /// </summary>
    [BsonElement("dateOfBirth")]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Insurance member ID
    /// </summary>
    [BsonElement("memberId")]
    public string? MemberId { get; set; }

    /// <summary>
    /// Insurance policy number
    /// </summary>
    [BsonElement("policyNumber")]
    public string? PolicyNumber { get; set; }

    // ===== Provider Information =====
    
    /// <summary>
    /// Treating physician name
    /// </summary>
    [BsonElement("providerName")]
    public string? ProviderName { get; set; }

    /// <summary>
    /// National Provider Identifier
    /// </summary>
    [BsonElement("npiNumber")]
    public string? NpiNumber { get; set; }

    /// <summary>
    /// Provider phone number
    /// </summary>
    [BsonElement("providerContact")]
    public string? ProviderContact { get; set; }

    /// <summary>
    /// Medical facility name
    /// </summary>
    [BsonElement("facilityName")]
    public string? FacilityName { get; set; }

    /// <summary>
    /// Full facility address
    /// </summary>
    [BsonElement("facilityAddress")]
    public string? FacilityAddress { get; set; }

    /// <summary>
    /// Referring physician name
    /// </summary>
    [BsonElement("referringProvider")]
    public string? ReferringProvider { get; set; }

    // ===== Service Information =====
    
    /// <summary>
    /// Type of service requested
    /// </summary>
    [BsonElement("serviceType")]
    public string? ServiceType { get; set; }

    /// <summary>
    /// CPT procedure codes
    /// </summary>
    [BsonElement("cptCodes")]
    public List<string>? CptCodes { get; set; }

    /// <summary>
    /// ICD-10 diagnosis codes
    /// </summary>
    [BsonElement("icd10Codes")]
    public List<string>? Icd10Codes { get; set; }

    /// <summary>
    /// Requested service start date
    /// </summary>
    [BsonElement("serviceStartDate")]
    public DateTime? ServiceStartDate { get; set; }

    /// <summary>
    /// Requested service end date
    /// </summary>
    [BsonElement("serviceEndDate")]
    public DateTime? ServiceEndDate { get; set; }

    /// <summary>
    /// Number of units/visits requested
    /// </summary>
    [BsonElement("unitsRequested")]
    public string? UnitsRequested { get; set; }

    /// <summary>
    /// Urgency level: "Urgent" or "Standard"
    /// </summary>
    [BsonElement("urgencyLevel")]
    public string? UrgencyLevel { get; set; }

    // ===== Clinical Information =====
    
    /// <summary>
    /// Clinical justification notes
    /// </summary>
    [BsonElement("clinicalNotes")]
    public string? ClinicalNotes { get; set; }

    // ===== Administrative Information =====
    
    /// <summary>
    /// Date fax was received
    /// </summary>
    [BsonElement("faxReceivedDate")]
    public DateTime? FaxReceivedDate { get; set; }

    /// <summary>
    /// Number of pages in fax
    /// </summary>
    [BsonElement("pageCount")]
    public int? PageCount { get; set; }

    // ===== Fax Header Information =====
    
    /// <summary>
    /// Date on fax transmission
    /// </summary>
    [BsonElement("faxDate")]
    public DateTime? FaxDate { get; set; }

    /// <summary>
    /// Recipient insurance company name
    /// </summary>
    [BsonElement("insuranceCompany")]
    public string? InsuranceCompany { get; set; }

    /// <summary>
    /// Insurance company fax number
    /// </summary>
    [BsonElement("insuranceFaxNumber")]
    public string? InsuranceFaxNumber { get; set; }

    /// <summary>
    /// Sender's fax number
    /// </summary>
    [BsonElement("senderFaxNumber")]
    public string? SenderFaxNumber { get; set; }

    /// <summary>
    /// Sender's name/organization (FROM field)
    /// </summary>
    [BsonElement("senderName")]
    public string? SenderName { get; set; }
}
