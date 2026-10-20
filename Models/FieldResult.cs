// ==============================================================================
// FieldResult.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Represents a single extracted field value paired with its normalized 
// confidence score (0.0 to 1.0) and source attribution (e.g., 'tesseract_mrz',
// 'qwen2_vl', or 'welcome_form').
// Directly mirrors FieldResult defined in PassportReadAPI schemas/job.py.
// ==============================================================================

using System.Text.Json.Serialization;

namespace HomestayDesktopApp.Models
{
    public class FieldResult
    {
        /// <summary>
        /// Extracted text content for the field (or null if unextracted).
        /// </summary>
        [JsonPropertyName("value")]
        public string? Value { get; set; } = null;

        /// <summary>
        /// Normalized confidence score between 0.0 (unreliable) and 1.0 (verified).
        /// </summary>
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 0.0;

        /// <summary>
        /// The pipeline stage or engine that produced this value:
        /// 'tesseract_mrz', 'qwen2_vl', 'welcome_form', or 'unassigned'.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = "unassigned";

        /// <summary>
        /// Parameterless constructor required for System.Text.Json deserialization.
        /// </summary>
        public FieldResult() { }

        /// <summary>
        /// Convenience constructor for initializing a field with an explicit value.
        /// </summary>
        /// <param name="value">The text string extracted from the document.</param>
        /// <param name="confidence">Confidence rating between 0.0 and 1.0.</param>
        /// <param name="source">Source engine attribution string.</param>
        public FieldResult(string? value, double confidence = 1.0, string source = "manual_override")
        {
            Value = value;
            Confidence = confidence;
            Source = source;
        }

        /// <summary>
        /// Helper string representation returning the raw value or empty string.
        /// </summary>
        public override string ToString() => Value ?? string.Empty;
    }
}