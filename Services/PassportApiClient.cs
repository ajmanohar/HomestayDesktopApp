// ==============================================================================
// PassportApiClient.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Handles asynchronous multipart document uploads, status polling, and batch
// synchronization against the live Dell Micro edge server over Cloudflare Tunnel.
// Production Domain: https://api.theasymptotebrief.com/api/v1/
// ==============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HomestayDesktopApp.Models;

namespace HomestayDesktopApp.Services
{
    public class PassportApiClient
    {
        // --------------------------------------------------------------------------
        // Configuration: Explicit production endpoint with trailing slash
        // --------------------------------------------------------------------------
        private const string DefaultBaseUrl = "https://api.theasymptotebrief.com/api/v1/";

        // Reusable HttpClient instance following .NET socket management standards
        private readonly HttpClient _httpClient;

        // Public Base URL property for runtime inspection or local mesh overrides
        public string BaseUrl { get; set; }

        /// <summary>
        /// Initializes the HTTP client configured with a 120-second timeout to handle
        /// CPU inference latency on Qwen2-VL-2B and Tesseract OCR models.
        /// </summary>
        /// <param name="baseUrl">Optional custom API endpoint URL.</param>
        public PassportApiClient(string? baseUrl = null)
        {
            // Ensure base URL ends with a trailing slash so relative paths resolve cleanly
            BaseUrl = baseUrl ?? DefaultBaseUrl;
            if (!BaseUrl.EndsWith("/"))
            {
                BaseUrl += "/";
            }

            // Initialize HttpClient with explicit BaseAddress and headers
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };

            // Set default headers to expect application/json responses
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        /// <summary>
        /// Fetches the recent completed and processing jobs from the Dell server backend.
        /// Maps to GET /api/v1/jobs on PassportReadAPI.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for aborting the request.</param>
        /// <returns>List of JobDetailResponse objects retrieved directly from SQLite.</returns>
        public async Task<List<JobDetailResponse>> GetRecentJobsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Explicit relative path resolving against BaseAddress (https://api.theasymptotebrief.com/api/v1/)
                var response = await _httpClient.GetAsync("jobs?limit=50", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new List<JobDetailResponse>();
                }

                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                var jobs = JsonSerializer.Deserialize<List<JobDetailResponse>>(jsonString);
                return jobs ?? new List<JobDetailResponse>();
            }
            catch
            {
                return new List<JobDetailResponse>();
            }
        }

        /// <summary>
        /// Submits captured document images to the backend extraction queue.
        /// Maps to POST /api/v1/jobs/submit on PassportReadAPI.
        /// </summary>
        /// <param name="passportBioPath">Absolute local path to passport bio image (required).</param>
        /// <param name="visaPath">Absolute local path to visa / OCI image (optional).</param>
        /// <param name="entryStampPath">Absolute local path to entry stamp image (optional).</param>
        /// <param name="welcomeFormPath">Absolute local path to handwritten Form-C slip (optional).</param>
        /// <param name="cancellationToken">Cancellation token for aborting the upload request.</param>
        /// <returns>JobDetailResponse containing the assigned job_id and initial QUEUED status.</returns>
        public async Task<JobDetailResponse> SubmitDocumentsAsync(
            string passportBioPath,
            string? visaPath = null,
            string? entryStampPath = null,
            string? welcomeFormPath = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(passportBioPath) || !File.Exists(passportBioPath))
            {
                throw new FileNotFoundException("Passport bio page image is required and must exist.", passportBioPath);
            }

            // Construct multipart/form-data payload
            using var form = new MultipartFormDataContent();

            // 1. Attach primary passport bio image (mandatory)
            var bioStream = File.OpenRead(passportBioPath);
            var bioContent = new StreamContent(bioStream);
            bioContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(bioContent, "passport_bio", Path.GetFileName(passportBioPath));

            // 2. Attach visa page if available
            FileStream? visaStream = null;
            if (!string.IsNullOrWhiteSpace(visaPath) && File.Exists(visaPath))
            {
                visaStream = File.OpenRead(visaPath);
                var visaContent = new StreamContent(visaStream);
                visaContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(visaContent, "visa_page", Path.GetFileName(visaPath));
            }

            // 3. Attach entry stamp image if available
            FileStream? stampStream = null;
            if (!string.IsNullOrWhiteSpace(entryStampPath) && File.Exists(entryStampPath))
            {
                stampStream = File.OpenRead(entryStampPath);
                var stampContent = new StreamContent(stampStream);
                stampContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(stampContent, "entry_stamp", Path.GetFileName(entryStampPath));
            }

            // 4. Attach handwritten welcome form (Form-C Rule 14) if available
            FileStream? welcomeStream = null;
            if (!string.IsNullOrWhiteSpace(welcomeFormPath) && File.Exists(welcomeFormPath))
            {
                welcomeStream = File.OpenRead(welcomeFormPath);
                var welcomeContent = new StreamContent(welcomeStream);
                welcomeContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(welcomeContent, "welcome_form", Path.GetFileName(welcomeFormPath));
            }

            try
            {
                // Send multipart POST request to jobs/submit
                var response = await _httpClient.PostAsync("jobs/submit", form, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Read and deserialize backend response payload
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var jobResponse = JsonSerializer.Deserialize<JobDetailResponse>(responseJson);

                if (jobResponse == null)
                {
                    throw new InvalidOperationException("Failed to parse backend job submission response.");
                }

                return jobResponse;
            }
            finally
            {
                // Ensure all underlying file streams are cleanly disposed
                bioStream.Dispose();
                visaStream?.Dispose();
                stampStream?.Dispose();
                welcomeStream?.Dispose();
            }
        }

        /// <summary>
        /// Polls the current processing status and extracted results of a specific job.
        /// Maps to GET /api/v1/jobs/{job_id} on PassportReadAPI.
        /// </summary>
        /// <param name="jobId">Unique job identifier returned from submission.</param>
        /// <param name="cancellationToken">Cancellation token for polling requests.</param>
        /// <returns>JobDetailResponse with current status and partial or final GuestFormCRecord.</returns>
        public async Task<JobDetailResponse> PollJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            // Send GET request to jobs/{jobId}
            var response = await _httpClient.GetAsync($"jobs/{jobId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            // Deserialize response content
            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JobDetailResponse>(jsonString);

            if (result == null)
            {
                throw new InvalidOperationException($"Unable to parse job status for ID: {jobId}");
            }

            return result;
        }
    }
}