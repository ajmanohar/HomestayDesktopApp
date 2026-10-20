// ==============================================================================
// PassportApiClient.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Handles asynchronous querying and polling against the live Dell Micro edge server
// over Cloudflare Tunnel with Bearer Token Authorization.
// Dedicated strictly to reading server-ingested submissions and job results.
// Production Domain: https://api.theasymptotebrief.com/api/v1/
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HomestayDesktopApp.Models;

namespace HomestayDesktopApp.Services
{
    /// <summary>
    /// Communicates with the Dell Micro edge server (PassportReadAPI).
    /// Provides methods to synchronize recent submissions and poll job details
    /// using pre-shared Bearer token authentication.
    /// </summary>
    public class PassportApiClient
    {
        // --------------------------------------------------------------------------
        // Configuration: Production endpoint and pre-shared Bearer Token
        // --------------------------------------------------------------------------

        // Default production API endpoint on the Dell edge server via Cloudflare Tunnel
        private const string DefaultBaseUrl = "https://api.theasymptotebrief.com/api/v1/";

        // Pre-shared Bearer API key configured in /opt/PassportReadAPI/.env on the server
        private const string DefaultBearerToken = "388f64ef3bea954970952b97a477e9822aaf4c2fad7d3b046caf8df82a224f88";

        // Reusable HttpClient instance following .NET socket management standards
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Public Base URL property for runtime inspection or local network overrides.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Initializes the HTTP client configured with a 120-second timeout to handle
        /// inference latency on Qwen2-VL-2B and Tesseract OCR models.
        /// Injects Bearer token authorization headers on all outgoing requests.
        /// </summary>
        /// <param name="baseUrl">Optional custom API endpoint URL.</param>
        /// <param name="bearerToken">Optional custom Bearer token.</param>
        public PassportApiClient(string? baseUrl = null, string? bearerToken = null)
        {
            // Ensure base URL ends with a trailing slash so relative paths resolve cleanly
            BaseUrl = baseUrl ?? DefaultBaseUrl;
            if (!BaseUrl.EndsWith("/"))
            {
                BaseUrl += "/";
            }

            string tokenToUse = bearerToken ?? DefaultBearerToken;

            // Initialize HttpClient with explicit BaseAddress and connection timeout
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };

            // Configure default request headers to expect application/json responses
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            // Inject static Bearer authorization header for API access control
            if (!string.IsNullOrWhiteSpace(tokenToUse))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokenToUse.Trim());
            }
        }

        /// <summary>
        /// Fetches the recent completed and processing jobs from the Dell server backend.
        /// Maps to GET /api/v1/jobs on PassportReadAPI with Authorization Bearer header.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for aborting the request.</param>
        /// <returns>List of JobDetailResponse objects retrieved directly from SQLite.</returns>
        public async Task<List<JobDetailResponse>> GetRecentJobsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Explicit relative path resolving against BaseAddress (https://api.theasymptotebrief.com/api/v1/)
                var response = await _httpClient.GetAsync("jobs?limit=50", cancellationToken);

                // If non-200 status code is returned (e.g. 401 Unauthorized), return empty list safely
                if (!response.IsSuccessStatusCode)
                {
                    return new List<JobDetailResponse>();
                }

                // Read and deserialize JSON payload from server
                var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                var jobs = JsonSerializer.Deserialize<List<JobDetailResponse>>(jsonString);

                return jobs ?? new List<JobDetailResponse>();
            }
            catch
            {
                // Return an empty list upon connection failure to prevent unhandled UI crashes
                return new List<JobDetailResponse>();
            }
        }

        /// <summary>
        /// Polls the current processing status and extracted results of a specific job.
        /// Maps to GET /api/v1/jobs/{job_id} on PassportReadAPI with Authorization Bearer header.
        /// </summary>
        /// <param name="jobId">Unique job identifier returned from submission.</param>
        /// <param name="cancellationToken">Cancellation token for polling requests.</param>
        /// <returns>JobDetailResponse with current status and partial or final GuestFormCRecord.</returns>
        public async Task<JobDetailResponse> PollJobStatusAsync(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            // Validate input job identifier parameter
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            // Send GET request to jobs/{jobId}
            var response = await _httpClient.GetAsync($"jobs/{jobId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            // Deserialize response content into typed model
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