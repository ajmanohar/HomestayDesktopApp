// ==============================================================================
// FormCDomInjector.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Generates JavaScript automation payloads and executes them against the
// Bureau of Immigration Form-C portal inside Microsoft WebView2.
// Target Portal: https://indianfrro.gov.in/frro/FormC/login.jsp
// ==============================================================================

using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using HomestayDesktopApp.Models;

namespace HomestayDesktopApp.Services
{
    public class FormCDomInjector
    {
        private readonly WebView2 _webView;

        /// <summary>
        /// Initializes the DOM injector with a reference to the active WebView2 control.
        /// </summary>
        /// <param name="webView">The active Microsoft.Web.WebView2 instance on the MainWindow.</param>
        public FormCDomInjector(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        }

        /// <summary>
        /// Injects stored homestay credentials into the portal login form.
        /// Leaves the CAPTCHA field untouched and focused for human entry.
        /// </summary>
        /// <param name="userId">Portal User ID / Username.</param>
        /// <param name="password">Portal Password.</param>
        /// <returns>True if script executed successfully, otherwise false.</returns>
        public async Task<bool> InjectLoginCredentialsAsync(string userId, string password)
        {
            if (_webView.CoreWebView2 == null)
            {
                throw new InvalidOperationException("WebView2 Core engine is not yet initialized.");
            }

            // Escape strings for safe JavaScript string literal injection
            string safeUser = JsonSerializer.Serialize(userId);
            string safePass = JsonSerializer.Serialize(password);

            string script = $$"""
            (function() {
                try {
                    // Helper to set value and trigger framework input events
                    function setInputValue(selector, val) {
                        const el = document.querySelector(selector);
                        if (!el) return false;
                        
                        // Use prototype setter to bypass framework property wrappers
                        const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
                            window.HTMLInputElement.prototype, 'value'
                        ).set;
                        nativeInputValueSetter.call(el, val);

                        // Dispatch standard DOM events for change listeners
                        el.dispatchEvent(new Event('input', { bubbles: true }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                        return true;
                    }

                    // Locate and populate user ID and password fields
                    const userFilled = setInputValue('input[name="u_id"]', {{safeUser}}) || 
                                       setInputValue('#u_id', {{safeUser}});
                                       
                    const passFilled = setInputValue('input[name="u_pwd"]', {{safePass}}) || 
                                       setInputValue('#u_pwd', {{safePass}});

                    // Locate CAPTCHA field and place focus for the operator
                    const captchaInput = document.querySelector('input[name="u_captcha"]') || 
                                         document.querySelector('#u_captcha');
                    if (captchaInput) {
                        captchaInput.focus();
                    }

                    return JSON.stringify({
                        success: userFilled && passFilled,
                        userFilled: userFilled,
                        passFilled: passFilled,
                        captchaFocused: !!captchaInput
                    });
                } catch (err) {
                    return JSON.stringify({ success: false, error: err.toString() });
                }
            })();
            """;

            string resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            return !string.IsNullOrEmpty(resultJson) && resultJson.Contains("\"success\":true");
        }

        /// <summary>
        /// Injects verified guest fields into the active Form-C Arrival Registration page.
        /// Maps standard Form-C fields matching BoI Rule 14.
        /// </summary>
        /// <param name="record">The verified GuestFormCRecord model.</param>
        /// <returns>Execution summary string detailing filled and unfound fields.</returns>
        public async Task<string> InjectGuestDetailsAsync(GuestFormCRecord record)
        {
            if (_webView.CoreWebView2 == null)
            {
                throw new InvalidOperationException("WebView2 Core engine is not yet initialized.");
            }

            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            // Serialize record values for safe evaluation in JavaScript
            string payloadJson = JsonSerializer.Serialize(new
            {
                surname = record.Surname?.Value ?? "",
                given_name = record.GivenNames?.Value ?? "",
                sex = record.Sex?.Value ?? "",
                dob = record.DateOfBirth?.Value ?? "",
                nationality = record.Nationality?.Value ?? "",
                passport_num = record.PassportNumber?.Value ?? "",
                passport_expiry = record.PassportExpiryDate?.Value ?? "",
                address = record.PermanentAddress?.Value ?? "",
                city = record.City?.Value ?? "",
                country = record.Country?.Value ?? "",
                email = record.ContactEmail?.Value ?? "",
                phone = record.ContactPhone?.Value ?? "",
                visa_num = record.VisaNumber?.Value ?? "",
                visa_type = record.VisaType?.Value ?? "",
                visa_issue = record.VisaIssueDate?.Value ?? "",
                visa_expiry = record.VisaExpiryDate?.Value ?? "",
                visa_place = record.VisaPlaceOfIssue?.Value ?? "",
                arrival_date = record.ArrivalDateIndia?.Value ?? "",
                arrival_port = record.ArrivalPortIndia?.Value ?? "",
                arrival_homestay = record.ArrivalDatetimeHomestay?.Value ?? "",
                arrived_from = record.ArrivedFrom?.Value ?? "",
                proceeding_to = record.ProceedingTo?.Value ?? "",
                stay_duration = record.StayDurationIndia?.Value ?? "",
                purpose_of_visit = record.PurposeOfVisit?.Value ?? ""
            });

            string script = $$"""
            (function() {
                const data = {{payloadJson}};
                const results = { filled: 0, fields: {} };

                function setValue(selectors, val) {
                    if (!val || val.trim() === '') return false;
                    for (const sel of selectors) {
                        const el = document.querySelector(sel);
                        if (el) {
                            if (el.tagName === 'SELECT') {
                                // Match dropdown option by text or value
                                let matched = false;
                                for (let opt of el.options) {
                                    if (opt.text.toLowerCase().includes(val.toLowerCase()) || 
                                        opt.value.toLowerCase() === val.toLowerCase()) {
                                        el.value = opt.value;
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched) el.value = val;
                            } else {
                                // Standard input / textarea prototype setter
                                const setter = Object.getOwnPropertyDescriptor(
                                    el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype,
                                    'value'
                                ).set;
                                setter.call(el, val);
                            }

                            el.dispatchEvent(new Event('input', { bubbles: true }));
                            el.dispatchEvent(new Event('change', { bubbles: true }));
                            return true;
                        }
                    }
                    return false;
                }

                // Field mappings for BoI Form-C inputs
                const mappings = {
                    surname: ['input[name="sur_name"]', '#sur_name'],
                    given_name: ['input[name="given_name"]', '#given_name'],
                    sex: ['select[name="gender"]', '#gender', 'input[name="gender"]'],
                    dob: ['input[name="dob"]', '#dob'],
                    nationality: ['select[name="nati"]', '#nati', 'input[name="nati"]'],
                    passport_num: ['input[name="pass_num"]', '#pass_num'],
                    passport_expiry: ['input[name="pass_exp_date"]', '#pass_exp_date'],
                    address: ['textarea[name="addr"]', '#addr', 'input[name="addr"]'],
                    city: ['input[name="city"]', '#city'],
                    country: ['select[name="coun"]', '#coun', 'input[name="coun"]'],
                    email: ['input[name="email_id"]', '#email_id'],
                    phone: ['input[name="phone_num"]', '#phone_num', 'input[name="mobile_num"]'],
                    visa_num: ['input[name="visa_num"]', '#visa_num'],
                    visa_type: ['select[name="visa_type"]', '#visa_type', 'input[name="visa_type"]'],
                    visa_issue: ['input[name="visa_issue_date"]', '#visa_issue_date'],
                    visa_expiry: ['input[name="visa_exp_date"]', '#visa_exp_date'],
                    visa_place: ['input[name="visa_place"]', '#visa_place'],
                    arrival_date: ['input[name="arri_date"]', '#arri_date'],
                    arrival_port: ['select[name="arri_port"]', '#arri_port', 'input[name="arri_port"]'],
                    arrived_from: ['input[name="arri_from"]', '#arri_from'],
                    proceeding_to: ['input[name="proc_to"]', '#proc_to'],
                    purpose_of_visit: ['select[name="purpose_of_visit"]', '#purpose_of_visit', 'input[name="purpose_of_visit"]']
                };

                for (const [key, selectors] of Object.entries(mappings)) {
                    const value = data[key];
                    const success = setValue(selectors, value);
                    results.fields[key] = success;
                    if (success) results.filled++;
                }

                return JSON.stringify(results);
            })();
            """;

            return await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }
}