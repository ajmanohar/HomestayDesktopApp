// ==============================================================================
// FormCDomInjector.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Automation script generator and execution bridge for Microsoft WebView2.
// Injects sanitized guest data into the official Indian Bureau of Immigration
// Form-C web portal (https://indianfrro.gov.in/frro/FormC/).
//
// Critical Anti-Hallucination & Architecture Principles:
// - Accepts Microsoft.Web.WebView2.Wpf.WebView2 in the constructor.
// - Exposes InjectLoginCredentialsAsync and InjectGuestDetailsAsync returning Task<(bool Success, string Message)>.
// - Sanitizes all extracted strings (strips dummy tokens like "Parthasarthy Nagar").
// - If any field value is null or empty, leaves the input blank ("").
// - Dispatches input, change, and blur DOM events to trigger validation listeners.
// ==============================================================================

using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using HomestayDesktopApp.Models;

namespace HomestayDesktopApp.Services
{
    /// <summary>
    /// Service responsible for synthesizing and executing JavaScript routines directly
    /// within the embedded Microsoft WebView2 Chromium environment.
    /// </summary>
    public class FormCDomInjector
    {
        // --------------------------------------------------------------------------
        // Private Fields
        // --------------------------------------------------------------------------

        /// <summary>
        /// Reference to the WPF WebView2 control hosting the Form-C portal.
        /// </summary>
        private readonly WebView2 _webView;

        // --------------------------------------------------------------------------
        // Constructor
        // --------------------------------------------------------------------------

        /// <summary>
        /// Initializes a new instance of FormCDomInjector bound to an active WebView2 instance.
        /// </summary>
        /// <param name="webView">The instantiated WPF WebView2 control from MainWindow.</param>
        public FormCDomInjector(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        // --------------------------------------------------------------------------
        // Sanitization & Anti-Hallucination Guardrails
        // --------------------------------------------------------------------------

        /// <summary>
        /// Sanitizes and escapes a candidate string value for safe JavaScript insertion.
        /// Rejects known mock/placeholder tokens and returns an empty string for null values.
        /// </summary>
        /// <param name="candidate">The raw extracted string value.</param>
        /// <returns>A clean, JavaScript-safe string.</returns>
        private static string CleanValue(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            string trimmed = candidate.Trim();
            string lower = trimmed.ToLowerInvariant();

            // Guard against common placeholder or null tokens
            if (lower == "null" || lower == "none" || lower == "n/a" || lower == "unknown")
            {
                return string.Empty;
            }

            // Reject hallucination remnants
            if (lower.Contains("parthasarthy") || 
                lower.Contains("parthasarathy") || 
                lower.Contains("example@domain.com") || 
                lower.Contains("09876543210") ||
                lower.Contains("dummy") ||
                lower.Contains("lorem ipsum"))
            {
                return string.Empty;
            }

            // Safely encode for JavaScript insertion
            return JavaScriptEncoder.Default.Encode(trimmed);
        }

        // --------------------------------------------------------------------------
        // Script Synthesis Routines
        // --------------------------------------------------------------------------

        /// <summary>
        /// Synthesizes the JavaScript snippet to autofill portal login credentials securely.
        /// Focuses on the visual CAPTCHA input element after populating username and password.
        /// </summary>
        /// <param name="username">Decrypted portal username.</param>
        /// <param name="password">Decrypted portal password.</param>
        /// <returns>JavaScript string for WebView2 execution.</returns>
        public string GenerateLoginInjectionScript(string username, string password)
        {
            string cleanUser = JavaScriptEncoder.Default.Encode(username ?? string.Empty);
            string cleanPass = JavaScriptEncoder.Default.Encode(password ?? string.Empty);

            var sb = new StringBuilder();
            sb.AppendLine("(function() {");
            sb.AppendLine("    console.log('[FormCInjector] Attempting automated credential injection...');");
            sb.AppendLine("    function setVal(selector, val) {");
            sb.AppendLine("        const el = document.querySelector(selector);");
            sb.AppendLine("        if (el) {");
            sb.AppendLine("            el.focus();");
            sb.AppendLine("            el.value = val;");
            sb.AppendLine("            el.dispatchEvent(new Event('input', { bubbles: true }));");
            sb.AppendLine("            el.dispatchEvent(new Event('change', { bubbles: true }));");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    const uFound = setVal('input[name=\"u_name\"], input[name=\"username\"], #u_name, input[name=\"u_id\"], #u_id, #txt_uid', '{cleanUser}');");
            sb.AppendLine($"    const pFound = setVal('input[name=\"pass\"], input[name=\"password\"], #pass, input[name=\"upass\"], #upass, #txt_pwd', '{cleanPass}');");
            sb.AppendLine();
            sb.AppendLine("    // Focus visual CAPTCHA input for rapid manual operator completion");
            sb.AppendLine("    const captcha = document.querySelector('input[name=\"cap\"], input[name=\"captcha\"], #cap, input[name=\"txt_cap\"], #txt_cap');");
            sb.AppendLine("    if (captcha) {");
            sb.AppendLine("        captcha.focus();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    return JSON.stringify({");
            sb.AppendLine("        success: uFound || pFound,");
            sb.AppendLine("        userFound: uFound,");
            sb.AppendLine("        passFound: pFound");
            sb.AppendLine("    });");
            sb.AppendLine("})();");

            return sb.ToString();
        }

        /// <summary>
        /// Synthesizes the JavaScript snippet to populate guest bio-data and Form-C fields.
        /// Operates strictly on authentic extracted data, outputting blank inputs for missing fields.
        /// </summary>
        /// <param name="record">The verified guest record containing extracted bio-data.</param>
        /// <returns>JavaScript string for WebView2 execution.</returns>
        public string GenerateFormFillScript(GuestFormCRecord record)
        {
            if (record == null)
            {
                return "JSON.stringify({ success: false, message: 'No guest record supplied.' });";
            }

            // Extract and sanitize individual field values
            string surname = CleanValue(record.Surname?.Value);
            string givenNames = CleanValue(record.GivenNames?.Value);
            string sex = CleanValue(record.Sex?.Value);
            string dob = CleanValue(record.DateOfBirth?.Value);
            string nationality = CleanValue(record.Nationality?.Value);
            string passportNum = CleanValue(record.PassportNumber?.Value);
            string passportExp = CleanValue(record.PassportExpiryDate?.Value);

            string visaNum = CleanValue(record.VisaNumber?.Value);
            string visaType = CleanValue(record.VisaType?.Value);
            string visaIssueDate = CleanValue(record.VisaIssueDate?.Value);
            string visaExpDate = CleanValue(record.VisaExpiryDate?.Value);
            string visaPlace = CleanValue(record.VisaPlaceOfIssue?.Value);

            string arrivalDate = CleanValue(record.ArrivalDateIndia?.Value);
            string arrivalPort = CleanValue(record.ArrivalPortIndia?.Value);

            string address = CleanValue(record.PermanentAddress?.Value);
            string city = CleanValue(record.City?.Value);
            string country = CleanValue(record.Country?.Value);
            string email = CleanValue(record.ContactEmail?.Value);
            string phone = CleanValue(record.ContactPhone?.Value);
            string arrivalHomestay = CleanValue(record.ArrivalDatetimeHomestay?.Value);
            string arrivedFrom = CleanValue(record.ArrivedFrom?.Value);
            string proceedingTo = CleanValue(record.ProceedingTo?.Value);
            string stayDuration = CleanValue(record.StayDurationIndia?.Value);
            string purpose = CleanValue(record.PurposeOfVisit?.Value);

            var sb = new StringBuilder();
            sb.AppendLine("(function() {");
            sb.AppendLine("    console.log('[FormCInjector] Populating Form-C guest fields...');");
            sb.AppendLine("    let filledCount = 0;");
            sb.AppendLine();
            sb.AppendLine("    function assignField(names, val) {");
            sb.AppendLine("        if (!val) return false;");
            sb.AppendLine("        for (let i = 0; i < names.length; i++) {");
            sb.AppendLine("            const el = document.querySelector(`input[name=\"${names[i]}\"], select[name=\"${names[i]}\"], textarea[name=\"${names[i]}\"], #${names[i]}`);");
            sb.AppendLine("            if (el) {");
            sb.AppendLine("                el.focus();");
            sb.AppendLine("                el.value = val;");
            sb.AppendLine("                el.dispatchEvent(new Event('input', { bubbles: true }));");
            sb.AppendLine("                el.dispatchEvent(new Event('change', { bubbles: true }));");
            sb.AppendLine("                el.dispatchEvent(new Event('blur', { bubbles: true }));");
            sb.AppendLine("                console.log(`[FormCInjector] Assigned '${names[i]}' = '${val}'`);");
            sb.AppendLine("                filledCount++;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    // 1. Passport & Identity Details");
            sb.AppendLine($"    assignField(['splname', 'surname', 'txtSurname'], '{surname}');");
            sb.AppendLine($"    assignField(['givname', 'given_name', 'txtGivenNames'], '{givenNames}');");
            sb.AppendLine($"    assignField(['sex', 'gender', 'ddlGender'], '{sex}');");
            sb.AppendLine($"    assignField(['dob', 'birth_date', 'txtDob'], '{dob}');");
            sb.AppendLine($"    assignField(['passnum', 'passno', 'txtPassNum'], '{passportNum}');");
            sb.AppendLine($"    assignField(['passexpdt', 'pass_expiry', 'txtPassExp'], '{passportExp}');");
            sb.AppendLine($"    assignField(['natcode', 'nationality', 'ddlNat'], '{nationality}');");
            sb.AppendLine();
            sb.AppendLine("    // 2. Visa Details");
            sb.AppendLine($"    assignField(['visano', 'visa_number', 'txtVisaNum'], '{visaNum}');");
            sb.AppendLine($"    assignField(['visatype', 'ddlVisaType'], '{visaType}');");
            sb.AppendLine($"    assignField(['visaidt', 'visa_issue_date', 'txtVisaIssue'], '{visaIssueDate}');");
            sb.AppendLine($"    assignField(['visaexpdt', 'visa_expiry', 'txtVisaExp'], '{visaExpDate}');");
            sb.AppendLine($"    assignField(['visapoi', 'visa_place', 'txtVisaPlace'], '{visaPlace}');");
            sb.AppendLine();
            sb.AppendLine("    // 3. Port & Arrival Details");
            sb.AppendLine($"    assignField(['arvdateind', 'arrival_date_india', 'txtArrDateInd'], '{arrivalDate}');");
            sb.AppendLine($"    assignField(['arvplaceind', 'arrival_port_india', 'txtArrPortInd'], '{arrivalPort}');");
            sb.AppendLine();
            sb.AppendLine("    // 4. Stay & Contact Details (Form-C Rule 14)");
            sb.AppendLine($"    assignField(['addr', 'address', 'txtAddress'], '{address}');");
            sb.AppendLine($"    assignField(['city', 'txtCity'], '{city}');");
            sb.AppendLine($"    assignField(['country', 'txtCountry'], '{country}');");
            sb.AppendLine($"    assignField(['email', 'txtEmail'], '{email}');");
            sb.AppendLine($"    assignField(['phone', 'mno', 'txtMobile'], '{phone}');");
            sb.AppendLine($"    assignField(['arvhotel', 'datetime_arrival_homestay', 'txtArrHomestay'], '{arrivalHomestay}');");
            sb.AppendLine($"    assignField(['arvfrom', 'arrived_from', 'txtArvFrom'], '{arrivedFrom}');");
            sb.AppendLine($"    assignField(['procto', 'proceeding_to', 'txtProcTo'], '{proceedingTo}');");
            sb.AppendLine($"    assignField(['duration', 'proposed_duration', 'txtDuration'], '{stayDuration}');");
            sb.AppendLine($"    assignField(['purpose', 'purpose_of_visit', 'ddlPurpose'], '{purpose}');");
            sb.AppendLine();
            sb.AppendLine("    return JSON.stringify({");
            sb.AppendLine("        success: filledCount > 0,");
            sb.AppendLine("        count: filledCount");
            sb.AppendLine("    });");
            sb.AppendLine("})();");

            return sb.ToString();
        }

        // --------------------------------------------------------------------------
        // WebView2 Execution Bridge Methods
        // --------------------------------------------------------------------------

        /// <summary>
        /// Executes credential injection asynchronously inside the active WebView2 runtime.
        /// </summary>
        /// <param name="username">Portal username/User ID.</param>
        /// <param name="password">Portal password.</param>
        /// <returns>Tuple containing success indicator and user-facing status message.</returns>
        public async Task<(bool Success, string Message)> InjectLoginCredentialsAsync(string username, string password)
        {
            if (_webView.CoreWebView2 == null)
            {
                return (false, "WebView2 core engine is not initialized yet.");
            }

            try
            {
                string script = GenerateLoginInjectionScript(username, password);
                string rawResult = await _webView.CoreWebView2.ExecuteScriptAsync(script);

                if (string.IsNullOrWhiteSpace(rawResult) || rawResult == "null")
                {
                    return (false, "Autofill script executed, but login elements were not found on the active page.");
                }

                // Parse returned JSON from the execution script
                using var doc = JsonDocument.Parse(rawResult.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\"));
                var root = doc.RootElement;
                bool success = root.GetProperty("success").GetBoolean();
                bool userFound = root.GetProperty("userFound").GetBoolean();
                bool passFound = root.GetProperty("passFound").GetBoolean();

                if (success)
                {
                    return (true, $"Credentials populated successfully (User: {userFound}, Password: {passFound}). Enter CAPTCHA to continue.");
                }
                else
                {
                    return (false, "Could not find login fields on the active page. Ensure you are on the Form-C login page.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Login autofill exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes guest Form-C field autofill asynchronously inside the active WebView2 runtime.
        /// </summary>
        /// <param name="record">Extracted guest bio-data and arrival information.</param>
        /// <returns>Tuple containing success indicator and user-facing status message.</returns>
        public async Task<(bool Success, string Message)> InjectGuestDetailsAsync(GuestFormCRecord record)
        {
            if (_webView.CoreWebView2 == null)
            {
                return (false, "WebView2 core engine is not initialized yet.");
            }

            if (record == null)
            {
                return (false, "No active guest record selected for injection.");
            }

            try
            {
                string script = GenerateFormFillScript(record);
                string rawResult = await _webView.CoreWebView2.ExecuteScriptAsync(script);

                if (string.IsNullOrWhiteSpace(rawResult) || rawResult == "null")
                {
                    return (false, "Form fill script executed, but target input fields were not detected.");
                }

                // Parse returned JSON from the execution script
                using var doc = JsonDocument.Parse(rawResult.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\"));
                var root = doc.RootElement;
                bool success = root.GetProperty("success").GetBoolean();
                int count = root.TryGetProperty("count", out var cVal) ? cVal.GetInt32() : 0;

                if (success)
                {
                    return (true, $"{count} field(s) populated into the active Form-C portal page.");
                }
                else
                {
                    return (false, "No matching Form-C input elements found on this page. Navigate to the guest entry form.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Field autofill exception: {ex.Message}");
            }
        }
    }
}