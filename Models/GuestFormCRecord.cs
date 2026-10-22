// ==============================================================================
// GuestFormCRecord.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Unified bio-data model merging MRZ, VLM bio, Visa, Stamp, and Welcome Form data.
// Maps all 12 items of the Bureau of Immigration Form-C Arrival Report (Rule 14).
// Directly mirrors GuestFormCRecord defined in PassportReadAPI schemas/job.py.
//
// Key Features:
// - Task 45: Includes DocumentExtractionContainer for isolated per-document data
//   (PassportBioRecord, VisaDocumentRecord, VisaStampRecord, EntryStampRecord,
//   FormCReportRecord) alongside the fused composite record.
// ==============================================================================

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HomestayDesktopApp.Models
{
    /// <summary>
    /// Isolated extraction fields originating strictly from the Passport Bio page.
    /// </summary>
    public class PassportBioRecord
    {
        [JsonPropertyName("surname")]
        public FieldResult Surname { get; set; } = new FieldResult();

        [JsonPropertyName("given_names")]
        public FieldResult GivenNames { get; set; } = new FieldResult();

        [JsonPropertyName("sex")]
        public FieldResult Sex { get; set; } = new FieldResult();

        [JsonPropertyName("date_of_birth")]
        public FieldResult DateOfBirth { get; set; } = new FieldResult();

        [JsonPropertyName("nationality")]
        public FieldResult Nationality { get; set; } = new FieldResult();

        [JsonPropertyName("passport_number")]
        public FieldResult PassportNumber { get; set; } = new FieldResult();

        [JsonPropertyName("passport_expiry_date")]
        public FieldResult PassportExpiryDate { get; set; } = new FieldResult();

        [JsonPropertyName("valid_mrz")]
        public bool ValidMrz { get; set; } = false;
    }

    /// <summary>
    /// Isolated extraction fields originating strictly from an e-Visa paper/PDF document.
    /// </summary>
    public class VisaDocumentRecord
    {
        [JsonPropertyName("visa_number")]
        public FieldResult VisaNumber { get; set; } = new FieldResult();

        [JsonPropertyName("visa_type")]
        public FieldResult VisaType { get; set; } = new FieldResult();

        [JsonPropertyName("visa_issue_date")]
        public FieldResult VisaIssueDate { get; set; } = new FieldResult();

        [JsonPropertyName("visa_expiry_date")]
        public FieldResult VisaExpiryDate { get; set; } = new FieldResult();

        [JsonPropertyName("visa_place_of_issue")]
        public FieldResult VisaPlaceOfIssue { get; set; } = new FieldResult();
    }

    /// <summary>
    /// Isolated extraction fields originating strictly from a passport booklet visa stamp.
    /// </summary>
    public class VisaStampRecord
    {
        [JsonPropertyName("visa_number")]
        public FieldResult VisaNumber { get; set; } = new FieldResult();

        [JsonPropertyName("visa_type")]
        public FieldResult VisaType { get; set; } = new FieldResult();

        [JsonPropertyName("visa_issue_date")]
        public FieldResult VisaIssueDate { get; set; } = new FieldResult();

        [JsonPropertyName("visa_expiry_date")]
        public FieldResult VisaExpiryDate { get; set; } = new FieldResult();

        [JsonPropertyName("visa_place_of_issue")]
        public FieldResult VisaPlaceOfIssue { get; set; } = new FieldResult();
    }

    /// <summary>
    /// Isolated extraction fields originating strictly from an immigration entry stamp.
    /// </summary>
    public class EntryStampRecord
    {
        [JsonPropertyName("arrival_date_india")]
        public FieldResult ArrivalDateIndia { get; set; } = new FieldResult();

        [JsonPropertyName("arrival_port_india")]
        public FieldResult ArrivalPortIndia { get; set; } = new FieldResult();
    }

    /// <summary>
    /// Isolated extraction fields originating strictly from the Form-C arrival report (Rows 1-12).
    /// </summary>
    public class FormCReportRecord
    {
        [JsonPropertyName("name_of_foreigner")]
        public FieldResult NameOfForeigner { get; set; } = new FieldResult();

        [JsonPropertyName("permanent_address")]
        public FieldResult PermanentAddress { get; set; } = new FieldResult();

        [JsonPropertyName("city")]
        public FieldResult City { get; set; } = new FieldResult();

        [JsonPropertyName("country")]
        public FieldResult Country { get; set; } = new FieldResult();

        [JsonPropertyName("contact_email")]
        public FieldResult ContactEmail { get; set; } = new FieldResult();

        [JsonPropertyName("contact_phone")]
        public FieldResult ContactPhone { get; set; } = new FieldResult();

        [JsonPropertyName("nationality")]
        public FieldResult Nationality { get; set; } = new FieldResult();

        [JsonPropertyName("place_of_issue_of_visa")]
        public FieldResult PlaceOfIssueOfVisa { get; set; } = new FieldResult();

        [JsonPropertyName("date_of_arrival_in_india")]
        public FieldResult DateOfArrivalInIndia { get; set; } = new FieldResult();

        [JsonPropertyName("arrival_datetime_homestay")]
        public FieldResult ArrivalDatetimeHomestay { get; set; } = new FieldResult();

        [JsonPropertyName("arrived_from")]
        public FieldResult ArrivedFrom { get; set; } = new FieldResult();

        [JsonPropertyName("proceeding_to")]
        public FieldResult ProceedingTo { get; set; } = new FieldResult();

        [JsonPropertyName("stay_duration_india")]
        public FieldResult StayDurationIndia { get; set; } = new FieldResult();

        [JsonPropertyName("purpose_of_visit")]
        public FieldResult PurposeOfVisit { get; set; } = new FieldResult();

        [JsonPropertyName("expected_departure_date")]
        public FieldResult ExpectedDepartureDate { get; set; } = new FieldResult();

        [JsonPropertyName("arrived_country_city_india")]
        public FieldResult ArrivedCountryCityIndia { get; set; } = new FieldResult();
    }

    /// <summary>
    /// Container holding per-document isolated extractions (Task 45).
    /// </summary>
    public class DocumentExtractionContainer
    {
        [JsonPropertyName("passport_bio")]
        public PassportBioRecord? PassportBio { get; set; }

        [JsonPropertyName("visa_document")]
        public VisaDocumentRecord? VisaDocument { get; set; }

        [JsonPropertyName("visa_stamp")]
        public VisaStampRecord? VisaStamp { get; set; }

        [JsonPropertyName("entry_stamp")]
        public EntryStampRecord? EntryStamp { get; set; }

        [JsonPropertyName("welcome_form")]
        public FormCReportRecord? WelcomeForm { get; set; }
    }

    /// <summary>
    /// Composite fused record matching schemas/job.py on the Dell edge server.
    /// </summary>
    public class GuestFormCRecord
    {
        // --- Item 1 & Passport Bio-Data ---
        [JsonPropertyName("surname")]
        public FieldResult Surname { get; set; } = new FieldResult();

        [JsonPropertyName("given_names")]
        public FieldResult GivenNames { get; set; } = new FieldResult();

        [JsonPropertyName("sex")]
        public FieldResult Sex { get; set; } = new FieldResult();

        [JsonPropertyName("date_of_birth")]
        public FieldResult DateOfBirth { get; set; } = new FieldResult();

        [JsonPropertyName("nationality")]
        public FieldResult Nationality { get; set; } = new FieldResult();

        [JsonPropertyName("passport_number")]
        public FieldResult PassportNumber { get; set; } = new FieldResult();

        [JsonPropertyName("passport_expiry_date")]
        public FieldResult PassportExpiryDate { get; set; } = new FieldResult();

        [JsonPropertyName("valid_mrz")]
        public bool ValidMrz { get; set; } = false;

        // --- Item 2: Guest Contact & Address Details ---
        [JsonPropertyName("permanent_address")]
        public FieldResult PermanentAddress { get; set; } = new FieldResult();

        [JsonPropertyName("city")]
        public FieldResult City { get; set; } = new FieldResult();

        [JsonPropertyName("country")]
        public FieldResult Country { get; set; } = new FieldResult();

        [JsonPropertyName("contact_email")]
        public FieldResult ContactEmail { get; set; } = new FieldResult();

        [JsonPropertyName("contact_phone")]
        public FieldResult ContactPhone { get; set; } = new FieldResult();

        // --- Items 3 & 4: Visa / OCI Details ---
        [JsonPropertyName("visa_number")]
        public FieldResult VisaNumber { get; set; } = new FieldResult();

        [JsonPropertyName("visa_type")]
        public FieldResult VisaType { get; set; } = new FieldResult();

        [JsonPropertyName("visa_issue_date")]
        public FieldResult VisaIssueDate { get; set; } = new FieldResult();

        [JsonPropertyName("visa_expiry_date")]
        public FieldResult VisaExpiryDate { get; set; } = new FieldResult();

        [JsonPropertyName("visa_place_of_issue")]
        public FieldResult VisaPlaceOfIssue { get; set; } = new FieldResult();

        // --- Item 5: Immigration Entry Stamp ---
        [JsonPropertyName("arrival_date_india")]
        public FieldResult ArrivalDateIndia { get; set; } = new FieldResult();

        [JsonPropertyName("arrival_port_india")]
        public FieldResult ArrivalPortIndia { get; set; } = new FieldResult();

        // --- Items 6 - 12: Homestay Arrival & Movement (Form-C Rule 14) ---
        [JsonPropertyName("arrival_datetime_homestay")]
        public FieldResult ArrivalDatetimeHomestay { get; set; } = new FieldResult();

        [JsonPropertyName("arrived_from")]
        public FieldResult ArrivedFrom { get; set; } = new FieldResult();

        [JsonPropertyName("proceeding_to")]
        public FieldResult ProceedingTo { get; set; } = new FieldResult();

        [JsonPropertyName("stay_duration_india")]
        public FieldResult StayDurationIndia { get; set; } = new FieldResult();

        [JsonPropertyName("purpose_of_visit")]
        public FieldResult PurposeOfVisit { get; set; } = new FieldResult();

        [JsonPropertyName("expected_departure_date")]
        public FieldResult ExpectedDepartureDate { get; set; } = new FieldResult();

        [JsonPropertyName("arrived_country_city_india")]
        public FieldResult ArrivedCountryCityIndia { get; set; } = new FieldResult();

        // --- Task 45: Isolated Document Extraction Persistence ---
        [JsonPropertyName("documents")]
        public DocumentExtractionContainer Documents { get; set; } = new DocumentExtractionContainer();

        // --- Pipeline Diagnostics ---
        [JsonPropertyName("processing_notes")]
        public List<string> ProcessingNotes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Response schema returned upon polling /api/v1/jobs/{job_id}.
    /// </summary>
    public class JobDetailResponse
    {
        [JsonPropertyName("job_id")]
        public string JobId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "QUEUED"; // QUEUED, PROCESSING, COMPLETED, FAILED

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("result")]
        public GuestFormCRecord? Result { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }
}