// ==============================================================================
// FormCDomInjector.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Automation script generator and execution bridge for Microsoft WebView2.
// Injects sanitized credentials and guest data into the Form-C portal.
// Features:
// - Recursive iframe scanning to reach nested portal frames.
// - Date format normalization (converts DD-MM-YYYY or YYYY-MM-DD to DD/MM/YYYY).
// - Prototype value setters to fire native change and input events.
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
        // Reference to the WPF WebView2 control hosting the Form-C portal
        private readonly WebView2 _webView;

        /// <summary>
        /// Initializes a new instance of FormCDomInjector bound to an active WebView2 instance.
        /// </summary>
        /// <param name="webView">The instantiated WPF WebView2 control from MainWindow.</param>
        public FormCDomInjector(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        // --------------------------------------------------------------------------
        // Sanitization, Date Normalization & Anti-Hallucination Guardrails
        // --------------------------------------------------------------------------

        /// <summary>
        /// Sanitizes and escapes a candidate string value for safe JavaScript insertion.
        /// Rejects known mock/placeholder tokens and returns an empty string for null values.
        /// </summary>
        private static string CleanValue(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return string.Empty;

            string trimmed = candidate.Trim();
            string lower = trimmed.ToLowerInvariant();

            if (lower == "null" || lower == "none" || lower == "n/a" || lower == "unknown") return string.Empty;

            if (lower.Contains("parthasarthy") || 
                lower.Contains("parthasarathy") || 
                lower.Contains("example@domain.com") || 
                lower.Contains("09876543210") ||
                lower.Contains("dummy") ||
                lower.Contains("lorem ipsum"))
            {
                return string.Empty;
            }

            return JavaScriptEncoder.Default.Encode(trimmed);
        }

        /// <summary>
        /// Normalizes date strings (DD-MM-YYYY or YYYY-MM-DD) into official Form-C portal format DD/MM/YYYY.
        /// </summary>
        private static string NormalizeDateForPortal(string? dateCandidate)
        {
            string clean = CleanValue(dateCandidate);
            if (string.IsNullOrWhiteSpace(clean)) return string.Empty;

            // If already DD-MM-YYYY, swap hyphens for slashes
            if (System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\d{2}-\d{2}-\d{4}$"))
            {
                return clean.Replace("-", "/");
            }

            // Attempt DateTime parse across standard ISO and culture formats
            if (DateTime.TryParse(clean, out DateTime parsed))
            {
                return parsed.ToString("dd/MM/yyyy");
            }

            return clean;
        }

        // --------------------------------------------------------------------------
        // Script Synthesis Routines (Multi-Frame / Iframe Aware)
        // --------------------------------------------------------------------------

        /// <summary>
        /// Synthesizes JavaScript to autofill portal login credentials across the main document
        /// and any nested iframes.
        /// </summary>
        public string GenerateLoginInjectionScript(string username, string password)
        {
            string cleanUser = JavaScriptEncoder.Default.Encode(username ?? string.Empty);
            string cleanPass = JavaScriptEncoder.Default.Encode(password ?? string.Empty);

            var sb = new StringBuilder();
            sb.AppendLine("(function() {");
            sb.AppendLine("    console.log('[FormCInjector] Attempting automated credential injection across frames...');");
            sb.AppendLine("    function findAndFill(doc) {");
            sb.AppendLine("        if (!doc) return false;");
            sb.AppendLine("        function setVal(sel, val) {");
            sb.AppendLine("            const el = doc.querySelector(sel);");
            sb.AppendLine("            if (el) {");
            sb.AppendLine("                el.focus();");
            sb.AppendLine("                el.value = val;");
            sb.AppendLine("                el.dispatchEvent(new Event('input', { bubbles: true }));");
            sb.AppendLine("                el.dispatchEvent(new Event('change', { bubbles: true }));");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine($"        const uFound = setVal('input[name=\"u_name\"], input[name=\"username\"], #u_name, input[name=\"u_id\"], #u_id, #txt_uid, input[type=\"text\"]', '{cleanUser}');");
            sb.AppendLine($"        const pFound = setVal('input[name=\"pass\"], input[name=\"password\"], #pass, input[name=\"upass\"], #upass, #txt_pwd, input[type=\"password\"]', '{cleanPass}');");
            sb.AppendLine("        const captcha = doc.querySelector('input[name=\"cap\"], input[name=\"captcha\"], #cap, input[name=\"txt_cap\"], #txt_cap, input[name=\"u_captcha\"]');");
            sb.AppendLine("        if (captcha) { captcha.focus(); }");
            sb.AppendLine("        return uFound || pFound;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    // 1. Try top-level document");
            sb.AppendLine("    let success = findAndFill(document);");
            sb.AppendLine();
            sb.AppendLine("    // 2. Try all nested iframes if not found");
            sb.AppendLine("    if (!success) {");
            sb.AppendLine("        const frames = document.querySelectorAll('iframe');");
            sb.AppendLine("        for (let i = 0; i < frames.length; i++) {");
            sb.AppendLine("            try {");
            sb.AppendLine("                const frameDoc = frames[i].contentDocument || (frames[i].contentWindow ? frames[i].contentWindow.document : null);");
            sb.AppendLine("                if (frameDoc && findAndFill(frameDoc)) {");
            sb.AppendLine("                    success = true;");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
            sb.AppendLine("            } catch (frameErr) {");
            sb.AppendLine("                console.warn('[FormCInjector] Iframe access restricted:', frameErr);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    return JSON.stringify({ success: success });");
            sb.AppendLine("})();");

            return sb.ToString();
        }

        /// <summary>
        /// Synthesizes JavaScript to populate guest bio-data and Form-C fields across main document and iframes.
        /// </summary>
        public string GenerateFormFillScript(GuestFormCRecord record)
        {
            if (record == null)
            {
                return "JSON.stringify({ success: false, message: 'No guest record supplied.' });";
            }

            string surname = CleanValue(record.Surname?.Value);
            string givenNames = CleanValue(record.GivenNames?.Value);
            string sex = CleanValue(record.Sex?.Value);
            string dob = NormalizeDateForPortal(record.DateOfBirth?.Value);
            string nationality = CleanValue(record.Nationality?.Value);
            string passportNum = CleanValue(record.PassportNumber?.Value);
            string passportExp = NormalizeDateForPortal(record.PassportExpiryDate?.Value);

            string visaNum = CleanValue(record.VisaNumber?.Value);
            string visaType = CleanValue(record.VisaType?.Value);
            string visaIssueDate = NormalizeDateForPortal(record.VisaIssueDate?.Value);
            string visaExpDate = NormalizeDateForPortal(record.VisaExpiryDate?.Value);
            string visaPlace = CleanValue(record.VisaPlaceOfIssue?.Value);

            string arrivalDate = NormalizeDateForPortal(record.ArrivalDateIndia?.Value);
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
            sb.AppendLine("    console.log('[FormCInjector] Populating Form-C guest fields across frames...');");
            sb.AppendLine("    let filledCount = 0;");
            sb.AppendLine();
            sb.AppendLine("    function assignField(doc, names, val) {");
            sb.AppendLine("        if (!doc || !val) return false;");
            sb.AppendLine("        for (let i = 0; i < names.length; i++) {");
            sb.AppendLine("            const el = doc.querySelector(`input[name=\"${names[i]}\"], select[name=\"${names[i]}\"], textarea[name=\"${names[i]}\"], #${names[i]}`);");
            sb.AppendLine("            if (el) {");
            sb.AppendLine("                el.focus();");
            sb.AppendLine("                el.value = val;");
            sb.AppendLine("                el.dispatchEvent(new Event('input', { bubbles: true }));");
            sb.AppendLine("                el.dispatchEvent(new Event('change', { bubbles: true }));");
            sb.AppendLine("                el.dispatchEvent(new Event('blur', { bubbles: true }));");
            sb.AppendLine("                filledCount++;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    function processTargetDoc(doc) {");
            sb.AppendLine("        if (!doc) return;");
            sb.AppendLine($"        assignField(doc, ['splname', 'sur_name', 'surname', 'txtSurname'], '{surname}');");
            sb.AppendLine($"        assignField(doc, ['givname', 'given_name', 'txtGivenNames'], '{givenNames}');");
            sb.AppendLine($"        assignField(doc, ['sex', 'gender', 'ddlGender'], '{sex}');");
            sb.AppendLine($"        assignField(doc, ['dob', 'birth_date', 'txtDob'], '{dob}');");
            sb.AppendLine($"        assignField(doc, ['passnum', 'pass_num', 'passno', 'txtPassNum'], '{passportNum}');");
            sb.AppendLine($"        assignField(doc, ['passexpdt', 'pass_exp_date', 'pass_expiry', 'txtPassExp'], '{passportExp}');");
            sb.AppendLine($"        assignField(doc, ['natcode', 'nati', 'nationality', 'ddlNat'], '{nationality}');");
            sb.AppendLine($"        assignField(doc, ['visano', 'visa_num', 'visa_number', 'txtVisaNum'], '{visaNum}');");
            sb.AppendLine($"        assignField(doc, ['visatype', 'visa_type', 'ddlVisaType'], '{visaType}');");
            sb.AppendLine($"        assignField(doc, ['visaidt', 'visa_issue_date', 'txtVisaIssue'], '{visaIssueDate}');");
            sb.AppendLine($"        assignField(doc, ['visaexpdt', 'visa_exp_date', 'visa_expiry', 'txtVisaExp'], '{visaExpDate}');");
            sb.AppendLine($"        assignField(doc, ['visapoi', 'visa_place', 'txtVisaPlace'], '{visaPlace}');");
            sb.AppendLine($"        assignField(doc, ['arvdateind', 'arri_date', 'arrival_date_india', 'txtArrDateInd'], '{arrivalDate}');");
            sb.AppendLine($"        assignField(doc, ['arvplaceind', 'arri_port', 'arrival_port_india', 'txtArrPortInd'], '{arrivalPort}');");
            sb.AppendLine($"        assignField(doc, ['addr', 'address', 'txtAddress'], '{address}');");
            sb.AppendLine($"        assignField(doc, ['city', 'txtCity'], '{city}');");
            sb.AppendLine($"        assignField(doc, ['country', 'coun', 'txtCountry'], '{country}');");
            sb.AppendLine($"        assignField(doc, ['email', 'email_id', 'txtEmail'], '{email}');");
            sb.AppendLine($"        assignField(doc, ['phone', 'phone_num', 'mno', 'txtMobile'], '{phone}');");
            sb.AppendLine($"        assignField(doc, ['arvhotel', 'datetime_arrival_homestay', 'txtArrHomestay'], '{arrivalHomestay}');");
            sb.AppendLine($"        assignField(doc, ['arvfrom', 'arri_from', 'arrived_from', 'txtArvFrom'], '{arrivedFrom}');");
            sb.AppendLine($"        assignField(doc, ['procto', 'proc_to', 'proceeding_to', 'txtProcTo'], '{proceedingTo}');");
            sb.AppendLine($"        assignField(doc, ['duration', 'stay_duration', 'proposed_duration', 'txtDuration'], '{stayDuration}');");
            sb.AppendLine($"        assignField(doc, ['purpose', 'purpose_of_visit', 'ddlPurpose'], '{purpose}');");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    // 1. Process root document");
            sb.AppendLine("    processTargetDoc(document);");
            sb.AppendLine();
            sb.AppendLine("    // 2. Process all child iframes");
            sb.AppendLine("    const frames = document.querySelectorAll('iframe');");
            sb.AppendLine("    for (let i = 0; i < frames.length; i++) {");
            sb.AppendLine("        try {");
            sb.AppendLine("            const frameDoc = frames[i].contentDocument || (frames[i].contentWindow ? frames[i].contentWindow.document : null);");
            sb.AppendLine("            if (frameDoc) { processTargetDoc(frameDoc); }");
            sb.AppendLine("        } catch (frameErr) {");
            sb.AppendLine("            console.warn('[FormCInjector] Iframe access restricted:', frameErr);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
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

                using var doc = JsonDocument.Parse(rawResult.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\"));
                var root = doc.RootElement;
                bool success = root.TryGetProperty("success", out var s) && s.GetBoolean();

                if (success)
                {
                    return (true, "Credentials populated successfully. Enter visual CAPTCHA to continue.");
                }
                else
                {
                    return (false, "Could not find login fields on active page or frames. Ensure you are on the Form-C login page.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Login autofill exception: {ex.Message}");
            }
        }

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

                using var doc = JsonDocument.Parse(rawResult.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\"));
                var root = doc.RootElement;
                bool success = root.TryGetProperty("success", out var s) && s.GetBoolean();
                int count = root.TryGetProperty("count", out var cVal) ? cVal.GetInt32() : 0;

                if (success)
                {
                    return (true, $"{count} field(s) populated into the active Form-C portal page.");
                }
                else
                {
                    return (false, "No matching Form-C input elements found on this page. Navigate to the guest registration form.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Field autofill exception: {ex.Message}");
            }
        }
    }
}