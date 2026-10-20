// ==============================================================================
// SubmissionItem.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Represents a submission record and its associated guest documents / extractions.
// Mirrors the multi-guest hierarchy stored on the Dell edge server and mobile DB.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HomestayDesktopApp.Models
{
    /// <summary>
    /// Represents an individual guest within a submission batch.
    /// Holds document image paths or preview URLs, plus the extracted Form-C record.
    /// </summary>
    public class GuestItem
    {
        [JsonPropertyName("guest_index")]
        public int GuestIndex { get; set; } = 0;

        [JsonPropertyName("guest_name")]
        public string GuestName { get; set; } = "Guest 1";

        [JsonPropertyName("passport_image_url")]
        public string? PassportImageUrl { get; set; }

        [JsonPropertyName("visa_image_url")]
        public string? VisaImageUrl { get; set; }

        [JsonPropertyName("stamp_image_url")]
        public string? StampImageUrl { get; set; }

        [JsonPropertyName("welcome_form_image_url")]
        public string? WelcomeFormImageUrl { get; set; }

        [JsonPropertyName("extracted_record")]
        public GuestFormCRecord ExtractedRecord { get; set; } = new GuestFormCRecord();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "PENDING_REVIEW";
    }

    /// <summary>
    /// Represents a complete submission container containing one or more guests.
    /// </summary>
    public class SubmissionSummary
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }

        [JsonPropertyName("guest_count")]
        public int GuestCount { get; set; } = 1;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "PENDING_REVIEW";

        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("guests")]
        public List<GuestItem> Guests { get; set; } = new List<GuestItem>();

        /// <summary>
        /// Display title formatting reference or ID.
        /// </summary>
        public string DisplayTitle => string.IsNullOrWhiteSpace(Reference) ? $"Submission #{Id[..Math.Min(8, Id.Length)]}" : Reference;
    }
}