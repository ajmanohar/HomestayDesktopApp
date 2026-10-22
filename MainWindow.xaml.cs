// ==============================================================================
// MainWindow.xaml.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Orchestrates desktop Form-C operations:
// 1. Retrieving live mobile-captured submissions from Dell Edge Server with cache-busting.
// 2. Dual-pane inspection of documents via Bearer-authenticated MemoryStream image loading.
// 3. User-facing date conversion (DD-MM-YYYY display vs YYYY-MM-DD ISO storage).
// 4. Automated Form-C portal filing with DPAPI-encrypted credentials and iframe traversal.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HomestayDesktopApp.Models;
using HomestayDesktopApp.Services;

namespace HomestayDesktopApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// Manages view transitions across Dashboard, Submissions DataGrid, Review, and Portal Runner.
    /// </summary>
    public partial class MainWindow : Window
    {
        // --------------------------------------------------------------------------
        // State & Backing Services
        // --------------------------------------------------------------------------

        // API client targeting the Dell Micro edge server over Cloudflare Tunnel
        private readonly PassportApiClient _apiClient;

        // Recursive DOM automation engine for WebView2 Form-C portal interaction
        private FormCDomInjector? _domInjector;

        // Cached list of submissions retrieved from the server
        private List<SubmissionSummary> _submissionsList = new List<SubmissionSummary>();

        // Currently selected submission record for review and filing
        private SubmissionSummary? _selectedSubmission;

        // Active guest index within the selected submission (0-based)
        private int _currentGuestIndex = 0;

        // Zoom scale tracking for document inspection (1.0 = 100% fit to container frame)
        private double _currentZoomScale = 1.0;

        /// <summary>
        /// Initializes the MainWindow component and instantiates the backend API client.
        /// Hooks into the Window Loaded event to initialize Microsoft WebView2.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Initialize HTTP client targeting the Dell Micro edge server endpoint
            _apiClient = new PassportApiClient();

            // Ensure Microsoft WebView2 runtime is initialized when window loads
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Asynchronously initializes the Microsoft WebView2 Chromium engine.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await PortalWebView.EnsureCoreWebView2Async(null);
                _domInjector = new FormCDomInjector(PortalWebView);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Microsoft WebView2:\n{ex.Message}\n\nVerify Microsoft Edge WebView2 runtime is installed.",
                    "WebView2 Engine Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // --------------------------------------------------------------------------
        // Date Formatting Helpers (DD-MM-YYYY Display vs YYYY-MM-DD ISO Storage)
        // --------------------------------------------------------------------------

        /// <summary>
        /// Converts an ISO date string (YYYY-MM-DD) into user-facing display format (DD-MM-YYYY).
        /// </summary>
        /// <param name="rawIsoDate">Raw ISO date string from database or server.</param>
        /// <returns>Formatted DD-MM-YYYY date string, or empty if null/empty.</returns>
        private static string FormatIsoToDisplay(string? rawIsoDate)
        {
            if (string.IsNullOrWhiteSpace(rawIsoDate)) return string.Empty;
            string clean = rawIsoDate.Trim();

            // If already in DD-MM-YYYY format, return directly
            if (DateTime.TryParseExact(clean, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return clean;
            }

            // Parse standard ISO date and convert to DD-MM-YYYY
            if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return dt.ToString("dd-MM-yyyy");
            }

            return clean;
        }

        /// <summary>
        /// Converts a user-entered display date string (DD-MM-YYYY) back into ISO format (YYYY-MM-DD).
        /// </summary>
        /// <param name="displayDate">Display date entered by operator.</param>
        /// <returns>Standard ISO YYYY-MM-DD string.</returns>
        private static string FormatDisplayToIso(string? displayDate)
        {
            if (string.IsNullOrWhiteSpace(displayDate)) return string.Empty;
            string clean = displayDate.Trim();

            // If already in YYYY-MM-DD format, return directly
            if (DateTime.TryParseExact(clean, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return clean;
            }

            // Parse DD-MM-YYYY or DD/MM/YYYY and format to ISO
            if (DateTime.TryParseExact(clean, new[] { "dd-MM-yyyy", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return dt.ToString("yyyy-MM-dd");
            }

            // Fallback generic parse
            if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fallbackDt))
            {
                return fallbackDt.ToString("yyyy-MM-dd");
            }

            return clean;
        }

        // --------------------------------------------------------------------------
        // View Switching Helpers
        // --------------------------------------------------------------------------

        private void ShowDashboard()
        {
            ViewDashboard.Visibility = Visibility.Visible;
            ViewSubmissionsList.Visibility = Visibility.Collapsed;
            ViewGuestReview.Visibility = Visibility.Collapsed;
            ViewPortalRunner.Visibility = Visibility.Collapsed;
        }

        private void ShowSubmissionsList()
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSubmissionsList.Visibility = Visibility.Visible;
            ViewGuestReview.Visibility = Visibility.Collapsed;
            ViewPortalRunner.Visibility = Visibility.Collapsed;
        }

        private void ShowGuestReview()
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSubmissionsList.Visibility = Visibility.Collapsed;
            ViewGuestReview.Visibility = Visibility.Visible;
            ViewPortalRunner.Visibility = Visibility.Collapsed;
        }

        private void ShowPortalRunner()
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSubmissionsList.Visibility = Visibility.Collapsed;
            ViewGuestReview.Visibility = Visibility.Collapsed;
            ViewPortalRunner.Visibility = Visibility.Visible;
        }

        // --------------------------------------------------------------------------
        // Screen 1: Dashboard Tile Click Handlers
        // --------------------------------------------------------------------------

        private async void TileRetrieveSubmissions_Click(object sender, RoutedEventArgs e)
        {
            ShowSubmissionsList();
            await LoadSubmissionsAsync();
        }

        private void TileCredentials_Click(object sender, RoutedEventArgs e)
        {
            var creds = CredentialStorageService.LoadCredentials();
            TxtModalUserId.Text = creds?.UserId ?? string.Empty;
            TxtModalPassword.Password = creds?.Password ?? string.Empty;
            ModalCredentials.Visibility = Visibility.Visible;
        }

        private void BtnCloseCredsModal_Click(object sender, RoutedEventArgs e)
        {
            ModalCredentials.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveCredsModal_Click(object sender, RoutedEventArgs e)
        {
            string user = TxtModalUserId.Text.Trim();
            string pass = TxtModalPassword.Password.Trim();

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Please enter both User ID and Password.", "Validation Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CredentialStorageService.SaveCredentials(user, pass);
            ModalCredentials.Visibility = Visibility.Collapsed;

            MessageBox.Show("Form-C portal credentials securely encrypted and saved via Windows DPAPI.", "Credentials Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --------------------------------------------------------------------------
        // Screen 2: Submissions Retrieval & Tabular DataGrid
        // --------------------------------------------------------------------------

        private async Task LoadSubmissionsAsync()
        {
            try
            {
                var jobs = await _apiClient.GetRecentJobsAsync();
                _submissionsList = new List<SubmissionSummary>();

                foreach (var job in jobs)
                {
                    string leadGuestName = job.Result != null && !string.IsNullOrWhiteSpace(job.Result.GivenNames?.Value)
                        ? $"{job.Result.GivenNames.Value} {job.Result.Surname?.Value}".Trim()
                        : "Lead Guest";

                    string serverBase = "https://api.theasymptotebrief.com/api/v1/jobs/" + job.JobId + "/image/";

                    var summary = new SubmissionSummary
                    {
                        Id = job.JobId,
                        Reference = leadGuestName + " (Job #" + job.JobId[..Math.Min(6, job.JobId.Length)] + ")",
                        Status = job.Status,
                        CreatedAt = job.CreatedAt,
                        GuestCount = 1,
                        Guests = new List<GuestItem>
                        {
                            new GuestItem
                            {
                                GuestIndex = 0,
                                GuestName = leadGuestName,
                                PassportImageUrl = serverBase + "passport_bio",
                                VisaImageUrl = serverBase + "visa_document",
                                StampImageUrl = serverBase + "entry_stamp",
                                WelcomeFormImageUrl = serverBase + "welcome_form",
                                ExtractedRecord = job.Result ?? new GuestFormCRecord()
                            }
                        }
                    };
                    _submissionsList.Add(summary);
                }

                // Explicitly clear ItemsSource before re-binding to force visual tree refresh
                DgSubmissions.ItemsSource = null;
                DgSubmissions.ItemsSource = _submissionsList;
                DgSubmissions.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch submissions from Dell server:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnBackToDashboard_Click(object sender, RoutedEventArgs e) => ShowDashboard();

        private async void BtnRefreshSubmissions_Click(object sender, RoutedEventArgs e) => await LoadSubmissionsAsync();

        private void BtnSelectSubmission_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SubmissionSummary submission)
            {
                OpenSubmissionReview(submission);
            }
        }

        private void DgSubmissions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgSubmissions.SelectedItem is SubmissionSummary submission)
            {
                OpenSubmissionReview(submission);
            }
        }

        private void OpenSubmissionReview(SubmissionSummary submission)
        {
            _selectedSubmission = submission;
            _currentGuestIndex = 0;
            ShowGuestReview();
            LoadCurrentGuestDetails();
        }

        // --------------------------------------------------------------------------
        // Screen 3: Multi-Guest Carousel & Review
        // --------------------------------------------------------------------------

        /// <summary>
        /// Populates Form-C input controls with formatted display dates.
        /// </summary>
        private void LoadCurrentGuestDetails()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;

            int totalGuests = _selectedSubmission.Guests.Count;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];

            TxtGuestCounter.Text = $"Guest {_currentGuestIndex + 1} of {totalGuests}: {guest.GuestName}";
            BtnPrevGuest.IsEnabled = _currentGuestIndex > 0;
            BtnNextGuest.IsEnabled = _currentGuestIndex < totalGuests - 1;

            var rec = guest.ExtractedRecord;

            TxtReviewSurname.Text = rec.Surname?.Value ?? "";
            TxtReviewGivenNames.Text = rec.GivenNames?.Value ?? "";
            TxtReviewSex.Text = rec.Sex?.Value ?? "";

            // Format dates to DD-MM-YYYY for display
            TxtReviewDob.Text = FormatIsoToDisplay(rec.DateOfBirth?.Value);
            TxtReviewPassportNumber.Text = rec.PassportNumber?.Value ?? "";
            TxtReviewPassportExpiry.Text = FormatIsoToDisplay(rec.PassportExpiryDate?.Value);
            TxtReviewNationality.Text = rec.Nationality?.Value ?? "";

            TxtReviewVisaNumber.Text = rec.VisaNumber?.Value ?? "";
            TxtReviewVisaType.Text = rec.VisaType?.Value ?? "";
            TxtReviewVisaIssueDate.Text = FormatIsoToDisplay(rec.VisaIssueDate?.Value);
            TxtReviewVisaExpiryDate.Text = FormatIsoToDisplay(rec.VisaExpiryDate?.Value);
            TxtReviewVisaPlaceOfIssue.Text = rec.VisaPlaceOfIssue?.Value ?? "";

            TxtReviewArrivalDate.Text = FormatIsoToDisplay(rec.ArrivalDateIndia?.Value);
            TxtReviewArrivalPort.Text = rec.ArrivalPortIndia?.Value ?? "";
            TxtReviewArrivedFrom.Text = rec.ArrivedFrom?.Value ?? "";
            TxtReviewProceedingTo.Text = rec.ProceedingTo?.Value ?? "";
            TxtReviewPurposeOfVisit.Text = string.IsNullOrWhiteSpace(rec.PurposeOfVisit?.Value) ? "Tourism" : rec.PurposeOfVisit.Value;
            TxtReviewAddress.Text = rec.PermanentAddress?.Value ?? "";
            TxtReviewEmail.Text = rec.ContactEmail?.Value ?? "";
            TxtReviewPhone.Text = rec.ContactPhone?.Value ?? "";

            // Trigger authenticated image preview update
            _ = UpdateDocumentImagePreviewAsync();
        }

        /// <summary>
        /// Fetches binary image bytes using Bearer authentication and decodes into BitmapImage.
        /// StreamSource is safely initialized to prevent WPF premature stream disposal.
        /// </summary>
        private async Task UpdateDocumentImagePreviewAsync()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;

            string docType = "passport_bio";
            if (TabPassport.IsChecked == true) docType = "passport_bio";
            else if (TabVisaDoc.IsChecked == true) docType = "visa_document";
            else if (TabVisa.IsChecked == true) docType = "visa_stamp";
            else if (TabStamp.IsChecked == true) docType = "entry_stamp";
            else if (TabWelcome.IsChecked == true) docType = "welcome_form";
            else if (TabPhoto.IsChecked == true) docType = "guest_photo";

            ResetImageZoom();

            try
            {
                // Authenticated binary download from Dell edge server via Bearer header
                byte[]? imageBytes = await _apiClient.GetDocumentImageBytesAsync(_selectedSubmission.Id, docType);

                // Fallback for visa document / stamp aliases
                if (imageBytes == null && (docType == "visa_document" || docType == "visa_stamp"))
                {
                    imageBytes = await _apiClient.GetDocumentImageBytesAsync(_selectedSubmission.Id, "visa_page");
                }
                // Fallback for standalone portrait photo
                else if (imageBytes == null && docType == "guest_photo")
                {
                    imageBytes = await _apiClient.GetDocumentImageBytesAsync(_selectedSubmission.Id, "passport_bio");
                }

                if (imageBytes != null && imageBytes.Length > 0)
                {
                    // MemoryStream is preserved in memory for BitmapImage decoding with OnLoad cache option
                    var ms = new MemoryStream(imageBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Enables cross-thread and asynchronous rendering safety

                    ImgDocumentPreview.Source = bitmap;
                }
                else
                {
                    ImgDocumentPreview.Source = null;
                }
            }
            catch
            {
                ImgDocumentPreview.Source = null;
            }
        }

        private void ResetImageZoom()
        {
            _currentZoomScale = 1.0;
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (ImageScaleTransform != null)
            {
                ImageScaleTransform.ScaleX = _currentZoomScale;
                ImageScaleTransform.ScaleY = _currentZoomScale;
            }
            if (TxtZoomLevel != null)
            {
                int pct = (int)Math.Round(_currentZoomScale * 100);
                TxtZoomLevel.Text = _currentZoomScale == 1.0 ? "Zoom: Fit to Frame (100%)" : $"Zoom: {pct}%";
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZoomScale < 4.0)
            {
                _currentZoomScale += 0.25;
                ApplyZoom();
            }
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZoomScale > 0.5)
            {
                _currentZoomScale -= 0.25;
                ApplyZoom();
            }
        }

        private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetImageZoom();
        }

        private void ImgScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0 && _currentZoomScale < 4.0)
                {
                    _currentZoomScale += 0.15;
                    ApplyZoom();
                    e.Handled = true;
                }
                else if (e.Delta < 0 && _currentZoomScale > 0.5)
                {
                    _currentZoomScale -= 0.15;
                    ApplyZoom();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Saves operator text edits into memory, converting DD-MM-YYYY back into standard ISO YYYY-MM-DD.
        /// </summary>
        private void SaveCurrentGuestEdits()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;

            var rec = _selectedSubmission.Guests[_currentGuestIndex].ExtractedRecord;

            rec.Surname.Value = TxtReviewSurname.Text.Trim();
            rec.GivenNames.Value = TxtReviewGivenNames.Text.Trim();
            rec.Sex.Value = TxtReviewSex.Text.Trim();

            // Convert DD-MM-YYYY back to ISO YYYY-MM-DD
            rec.DateOfBirth.Value = FormatDisplayToIso(TxtReviewDob.Text);
            rec.PassportNumber.Value = TxtReviewPassportNumber.Text.Trim();
            rec.PassportExpiryDate.Value = FormatDisplayToIso(TxtReviewPassportExpiry.Text);
            rec.Nationality.Value = TxtReviewNationality.Text.Trim();

            rec.VisaNumber.Value = TxtReviewVisaNumber.Text.Trim();
            rec.VisaType.Value = TxtReviewVisaType.Text.Trim();
            rec.VisaIssueDate.Value = FormatDisplayToIso(TxtReviewVisaIssueDate.Text);
            rec.VisaExpiryDate.Value = FormatDisplayToIso(TxtReviewVisaExpiryDate.Text);
            rec.VisaPlaceOfIssue.Value = TxtReviewVisaPlaceOfIssue.Text.Trim();

            rec.ArrivalDateIndia.Value = FormatDisplayToIso(TxtReviewArrivalDate.Text);
            rec.ArrivalPortIndia.Value = TxtReviewArrivalPort.Text.Trim();
            rec.ArrivedFrom.Value = TxtReviewArrivedFrom.Text.Trim();
            rec.ProceedingTo.Value = TxtReviewProceedingTo.Text.Trim();
            rec.PurposeOfVisit.Value = TxtReviewPurposeOfVisit.Text.Trim();
            rec.PermanentAddress.Value = TxtReviewAddress.Text.Trim();
            rec.ContactEmail.Value = TxtReviewEmail.Text.Trim();
            rec.ContactPhone.Value = TxtReviewPhone.Text.Trim();
        }

        private void BtnSaveEdits_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentGuestEdits();
            MessageBox.Show("Operator edits saved into memory and will be used during Form-C filing.", "Edits Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPrevGuest_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSubmission == null) return;
            SaveCurrentGuestEdits();
            if (_currentGuestIndex > 0)
            {
                _currentGuestIndex--;
                LoadCurrentGuestDetails();
            }
        }

        private void BtnNextGuest_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSubmission == null) return;
            SaveCurrentGuestEdits();
            if (_currentGuestIndex < _selectedSubmission.Guests.Count - 1)
            {
                _currentGuestIndex++;
                LoadCurrentGuestDetails();
            }
        }

        private void BtnBackToSubmissions_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentGuestEdits();
            ShowSubmissionsList();
        }

        private void DocTab_Checked(object sender, RoutedEventArgs e)
        {
            _ = UpdateDocumentImagePreviewAsync();
        }

        private void BtnLaunchPortal_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentGuestEdits();
            ShowPortalRunner();
            UpdatePortalHeader();
        }

        // --------------------------------------------------------------------------
        // Screen 4: Portal Runner & WebView2 DOM Automation
        // --------------------------------------------------------------------------

        private void UpdatePortalHeader()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];
            TxtPortalActiveGuest.Text = $"Active Guest: {guest.GuestName} ({_currentGuestIndex + 1} of {_selectedSubmission.Guests.Count})";
        }

        private void BtnBackToGuestReview_Click(object sender, RoutedEventArgs e) => ShowGuestReview();

        private void BtnPortalNextGuest_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSubmission == null) return;
            if (_currentGuestIndex < _selectedSubmission.Guests.Count - 1)
            {
                _currentGuestIndex++;
            }
            else
            {
                _currentGuestIndex = 0;
            }
            UpdatePortalHeader();
            LoadCurrentGuestDetails();
        }

        private async void BtnAutofillLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_domInjector == null)
            {
                MessageBox.Show("WebView2 is not yet initialized.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var stored = CredentialStorageService.LoadCredentials();
            string portalUser = stored?.UserId ?? "";
            string portalPass = stored?.Password ?? "";

            if (string.IsNullOrWhiteSpace(portalUser) || string.IsNullOrWhiteSpace(portalPass))
            {
                MessageBox.Show("No saved Form-C credentials found. Configure them via Portal Credentials on Dashboard.", "Credentials Needed", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtModalUserId.Text = "";
                TxtModalPassword.Password = "";
                ModalCredentials.Visibility = Visibility.Visible;
                return;
            }

            (bool success, string message) = await _domInjector.InjectLoginCredentialsAsync(portalUser, portalPass);

            MessageBox.Show(message, success ? "Autofill Success" : "Autofill Notice", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private async void BtnAutofillFields_Click(object sender, RoutedEventArgs e)
        {
            if (_domInjector == null)
            {
                MessageBox.Show("WebView2 is not yet initialized.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0)
            {
                MessageBox.Show("No active guest record selected.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var guest = _selectedSubmission.Guests[_currentGuestIndex];
            (bool success, string message) = await _domInjector.InjectGuestDetailsAsync(guest.ExtractedRecord);

            MessageBox.Show(
                message,
                success ? "Autofill Complete" : "Autofill Status",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning
            );
        }

        private void BtnReloadWeb_Click(object sender, RoutedEventArgs e)
        {
            PortalWebView?.Reload();
        }
    }
}