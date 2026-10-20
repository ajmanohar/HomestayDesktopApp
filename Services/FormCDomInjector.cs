// ==============================================================================
// FormCDomInjector.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Generates JavaScript automation payloads and executes them against the
// Bureau of Immigration Form-C portal inside Microsoft WebView2.
// Implements recursive frame / iframe traversal, prototype property setters,
// and DOM event dispatching to eliminate "element not found" errors.
// ==============================================================================

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using HomestayDesktopApp.Models;

namespace HomestayDesktopApp.Services
{
    public class FormCDomInjector
    {
        // Reference to the active Microsoft WebView2 instance
        private readonly WebView2 _webView;

        /// <summary>
        /// Initializes the DOM injector with the active WebView2 control.
        /// </summary>
        /// <param name="webView">Active WebView2 instance.</param>
        public FormCDomInjector(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        /// <summary>
        /// Injects homestay operator credentials into the portal login form.
        /// Searches across the main document and all accessible child frames/iframes.
        /// Sets focus on the visual CAPTCHA input for manual operator entry.
        /// </summary>
        /// <param name="userId">Portal User ID / Username.</param>
        /// <param name="password">Portal Password.</param>
        /// <returns>A tuple with success status and detailed diagnostic message.</returns>
        public async Task<(bool Success, string Message)> InjectLoginCredentialsAsync(string userId, string password)
        {
            if (_webView.CoreWebView2 == null)
            {
                return (false, "WebView2 core engine is not yet initialized.");
            }

            // Safely serialize user credentials for JavaScript injection
            string safeUser = JsonSerializer.Serialize(userId);
            string safePass = JsonSerializer.Serialize(password);

            string script = $$"""
            (function() {
                try {
                    const targetUser = {{safeUser}};
                    const targetPass = {{safePass}};

                    // Helper to deeply set an input value and trigger browser framework events
                    function applyInputValue(inputEl, val) {
                        if (!inputEl) return;
                        inputEl.focus();
                        
                        // Use native prototype setter to bypass event suppression
                        const nativeSetter = Object.getOwnPropertyDescriptor(
                            window.HTMLInputElement.prototype, 'value'
                        );
                        if (nativeSetter && nativeSetter.set) {
                            nativeSetter.set.call(inputEl, val);
                        } else {
                            inputEl.value = val;
                        }

                        // Dispatch standard events for portal event listeners
                        inputEl.dispatchEvent(new Event('input', { bubbles: true }));
                        inputEl.dispatchEvent(new Event('change', { bubbles: true }));
                        inputEl.dispatchEvent(new Event('blur', { bubbles: true }));
                    }

                    // Collect all contexts: main document plus any accessible frames/iframes
                    const contexts = [document];
                    const iframes = document.querySelectorAll('iframe, frame');
                    for (let i = 0; i < iframes.length; i++) {
                        try {
                            if (iframes[i].contentDocument) {
                                contexts.push(iframes[i].contentDocument);
                            }
                        } catch(e) {
                            // Suppress cross-origin frame access errors
                        }
                    }

                    let userInput = null;
                    let passInput = null;
                    let captchaInput = null;
                    const detectedInputs = [];

                    for (let c = 0; c < contexts.length; c++) {
                        const ctx = contexts[c];

                        // 1. Check known Bureau of Immigration selectors
                        if (!userInput) {
                            userInput = ctx.querySelector('input[name="u_id"]') ||
                                        ctx.querySelector('#u_id') ||
                                        ctx.querySelector('input[name="txt_uid"]') ||
                                        ctx.querySelector('#txt_uid') ||
                                        ctx.querySelector('input[name*="user" i]') ||
                                        ctx.querySelector('input[name*="uid" i]') ||
                                        ctx.querySelector('input[name*="login" i]');
                        }

                        if (!passInput) {
                            passInput = ctx.querySelector('input[name="u_pwd"]') ||
                                        ctx.querySelector('#u_pwd') ||
                                        ctx.querySelector('input[name="txt_pwd"]') ||
                                        ctx.querySelector('#txt_pwd') ||
                                        ctx.querySelector('input[type="password"]');
                        }

                        if (!captchaInput) {
                            captchaInput = ctx.querySelector('input[name="u_captcha"]') ||
                                           ctx.querySelector('#u_captcha') ||
                                           ctx.querySelector('input[name*="captcha" i]');
                        }

                        // Record detected input attributes for diagnostic troubleshooting
                        const allInputs = ctx.querySelectorAll('input');
                        for (let k = 0; k < allInputs.length; k++) {
                            const inp = allInputs[k];
                            detectedInputs.push(
                                (inp.name ? 'name=' + inp.name : '') + 
                                (inp.id ? ' id=' + inp.id : '') + 
                                (inp.type ? ' type=' + inp.type : '')
                            );
                        }

                        // 2. Fallback heuristic: text field immediately preceding password
                        if (passInput && !userInput) {
                            const ctxInputs = Array.from(ctx.querySelectorAll('input'));
                            const passIndex = ctxInputs.indexOf(passInput);
                            for (let j = passIndex - 1; j >= 0; j--) {
                                if (ctxInputs[j].type === 'text' || ctxInputs[j].type === '') {
                                    userInput = ctxInputs[j];
                                    break;
                                }
                            }
                        }

                        if (userInput && passInput) break;
                    }

                    if (userInput && passInput) {
                        applyInputValue(userInput, targetUser);
                        applyInputValue(passInput, targetPass);

                        // Focus CAPTCHA field so user can type immediately
                        if (captchaInput) {
                            captchaInput.focus();
                        }

                        return JSON.stringify({
                            success: true,
                            message: 'Login credentials populated successfully! Please enter CAPTCHA and sign in.'
                        });
                    } else {
                        return JSON.stringify({
                            success: false,
                            message: 'Unable to locate login input fields.',
                            detected: detectedInputs.slice(0, 15).join(' | ')
                        });
                    }
                } catch (err) {
                    return JSON.stringify({
                        success: false,
                        message: 'DOM injection exception: ' + err.toString()
                    });
                }
            })();
            """;

            string rawResult = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            
            // WebView2 returns JSON string of the returned JavaScript value
            try
            {
                // Unescape JSON string returned by ExecuteScriptAsync
                string unescaped = JsonSerializer.Deserialize<string>(rawResult) ?? rawResult;
                using var doc = JsonDocument.Parse(unescaped);
                bool success = doc.RootElement.GetProperty("success").GetBoolean();
                string msg = doc.RootElement.GetProperty("message").GetString() ?? "";

                if (!success && doc.RootElement.TryGetProperty("detected", out var detectedProp))
                {
                    msg += $"\n\nDetected Page Inputs:\n{detectedProp.GetString()}";
                }

                return (success, msg);
            }
            catch
            {
                return (false, $"Raw Execution Output: {rawResult}"); } }          /// <summary> /// Injects verified guest fields into the active Form-C Arrival Registration page. /// Searches text inputs, textareas, and select dropdowns across frames. /// </summary> /// <param name="record">The verified GuestFormCRecord model for the current guest.</param> /// <returns>A tuple with success status and summary of populated fields.</returns> public async Task<(bool Success, string Message)> InjectGuestDetailsAsync(GuestFormCRecord record) { if (_webView.CoreWebView2 == null) { return (false, "WebView2 core engine is not yet initialized."); }              if (record == null) { return (false, "No guest record provided for injection."); }              // Serialize guest fields safely into JSON string payloadJson = JsonSerializer.Serialize(new { surname = record.Surname?.Value ?? "", given_name = record.GivenNames?.Value ?? "", sex = record.Sex?.Value ?? "", dob = record.DateOfBirth?.Value ?? "", nationality = record.Nationality?.Value ?? "", passport_num = record.PassportNumber?.Value ?? "", passport_expiry = record.PassportExpiryDate?.Value ?? "", address = record.PermanentAddress?.Value ?? "", city = record.City?.Value ?? "", country = record.Country?.Value ?? "", email = record.ContactEmail?.Value ?? "", phone = record.ContactPhone?.Value ?? "", visa_num = record.VisaNumber?.Value ?? "", visa_type = record.VisaType?.Value ?? "", visa_issue = record.VisaIssueDate?.Value ?? "", visa_expiry = record.VisaExpiryDate?.Value ?? "", visa_place = record.VisaPlaceOfIssue?.Value ?? "", arrival_date = record.ArrivalDateIndia?.Value ?? "", arrival_port = record.ArrivalPortIndia?.Value ?? "", arrival_homestay = record.ArrivalDatetimeHomestay?.Value ?? "", arrived_from = record.ArrivedFrom?.Value ?? "", proceeding_to = record.ProceedingTo?.Value ?? "", stay_duration = record.StayDurationIndia?.Value ?? "", purpose_of_visit = record.PurposeOfVisit?.Value ?? "Tourism" });              string script = $$"""
            (function() {
                try {
                    const data = {{payloadJson}};
                    let fieldsUpdated = 0;
                    const populated = [];

                    // Collect all frames/contexts
                    const contexts = [document];
                    const iframes = document.querySelectorAll('iframe, frame');
                    for (let i = 0; i < iframes.length; i++) {
                        try {
                            if (iframes[i].contentDocument) contexts.push(iframes[i].contentDocument);
                        } catch(e) {}
                    }

                    // Setter helper for inputs and textareas
                    function setInputValue(selectors, val) {
                        if (!val || val.trim() === '') return false;
                        for (const ctx of contexts) {
                            for (const sel of selectors) {
                                const el = ctx.querySelector(sel);
                                if (el) {
                                    el.focus();
                                    const proto = el.tagName === 'TEXTAREA' 
                                        ? window.HTMLTextAreaElement.prototype 
                                        : window.HTMLInputElement.prototype;
                                    const setter = Object.getOwnPropertyDescriptor(proto, 'value');
                                    if (setter && setter.set) {
                                        setter.set.call(el, val);
                                    } else {
                                        el.value = val;
                                    }
                                    el.dispatchEvent(new Event('input', { bubbles: true }));
                                    el.dispatchEvent(new Event('change', { bubbles: true }));
                                    el.dispatchEvent(new Event('blur', { bubbles: true }));
                                    return true;
                                }
                            }
                        }
                        return false;
                    }

                    // Setter helper for dropdown selects
                    function setSelectValue(selectors, val) {
                        if (!val || val.trim() === '') return false;
                        const targetLower = val.toLowerCase().trim();
                        for (const ctx of contexts) {
                            for (const sel of selectors) {
                                const selectEl = ctx.querySelector(sel);
                                if (selectEl && selectEl.options) {
                                    for (let i = 0; i < selectEl.options.length; i++) {
                                        const opt = selectEl.options[i];
                                        if (opt.text.toLowerCase().includes(targetLower) || 
                                            opt.value.toLowerCase() === targetLower) {
                                            selectEl.selectedIndex = i;
                                            selectEl.dispatchEvent(new Event('change', { bubbles: true }));
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                        return false;
                    }

                    // Bureau of Immigration Form-C field mapping definitions
                    const textMappings = {
                        surname: ['input[name="sur_name"]', '#sur_name', 'input[name="surname"]'],
                        given_name: ['input[name="given_name"]', '#given_name', 'input[name="first_name"]'],
                        dob: ['input[name="dob"]', '#dob', 'input[name="date_of_birth"]'],
                        passport_num: ['input[name="pass_num"]', '#pass_num', 'input[name="passport_no"]'],
                        passport_expiry: ['input[name="pass_exp_date"]', '#pass_exp_date'],
                        address: ['textarea[name="addr"]', '#addr', 'input[name="addr"]'],
                        city: ['input[name="city"]', '#city'],
                        email: ['input[name="email_id"]', '#email_id'],
                        phone: ['input[name="phone_num"]', '#phone_num', 'input[name="mobile_num"]'],
                        visa_num: ['input[name="visa_num"]', '#visa_num', 'input[name="visa_number"]'],
                        visa_issue: ['input[name="visa_issue_date"]', '#visa_issue_date'],
                        visa_expiry: ['input[name="visa_exp_date"]', '#visa_exp_date'],
                        visa_place: ['input[name="visa_place"]', '#visa_place'],
                        arrival_date: ['input[name="arri_date"]', '#arri_date'],
                        arrived_from: ['input[name="arri_from"]', '#arri_from', 'input[name="arrived_from"]'],
                        proceeding_to: ['input[name="proc_to"]', '#proc_to', 'input[name="proceeding_to"]']
                    };

                    const selectMappings = {
                        sex: ['select[name="gender"]', '#gender', 'select[name="sex"]'],
                        nationality: ['select[name="nati"]', '#nati', 'select[name="spl_category"]', 'select[name="nationality"]'],
                        visa_type: ['select[name="visa_type"]', '#visa_type'],
                        arrival_port: ['select[name="arri_port"]', '#arri_port', 'select[name="port_of_arrival"]'],
                        country: ['select[name="coun"]', '#coun', 'select[name="country"]'],
                        purpose_of_visit: ['select[name="purpose_of_visit"]', '#purpose_of_visit', 'select[name="purpose"]']
                    };

                    for (const [key, selectors] of Object.entries(textMappings)) {
                        if (setInputValue(selectors, data[key])) {
                            fieldsUpdated++;
                            populated.push(key);
                        }
                    }

                    for (const [key, selectors] of Object.entries(selectMappings)) {
                        if (setSelectValue(selectors, data[key])) {
                            fieldsUpdated++;
                            populated.push(key);
                        }
                    }

                    return JSON.stringify({
                        success: fieldsUpdated > 0,
                        count: fieldsUpdated,
                        fields: populated,
                        message: `Successfully populated ${fieldsUpdated} fields into the active Form-C page.`
                    });
                } catch (err) {
                    return JSON.stringify({
                        success: false,
                        message: 'Error during Form-C field population: ' + err.toString()
                    });
                }
            })();
            """;

            string rawResult = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            try
            {
                string unescaped = JsonSerializer.Deserialize<string>(rawResult) ?? rawResult;
                using var doc = JsonDocument.Parse(unescaped);
                bool success = doc.RootElement.GetProperty("success").GetBoolean();
                string msg = doc.RootElement.GetProperty("message").GetString() ?? "";
                return (success, msg);
            }
            catch
            {
                return (true, $"Executed script. Output: {rawResult}");
            }
        }
    }
}