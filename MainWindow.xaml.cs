// ==============================================================================
// MainWindow.xaml.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Orchestrates the desktop workflow strictly for:
// 1. Retrieving live mobile-captured submissions from the Dell Edge Server.
// 2. Dual-pane inspection of documents (with auto-fit and zoom controls).
// 3. Automated Form-C portal filing with DPAPI-encrypted credential storage.
// (All desktop ingestion features have been removed; ingestion is mobile-only).
// Production Edge Server: https://api.theasymptotebrief.com via Cloudflare Tunnel
// ==============================================================================

using System;
using System.Collections.Generic;
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

        // API client configured for communication with the Dell Micro edge server
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
            // Initialize XAML component layout and controls
            InitializeComponent();

            // Initialize HTTP client targeting the Dell Micro edge server endpoint
            _apiClient = new PassportApiClient();

            // Ensure Microsoft WebView2 runtime is initialized when window loads
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Asynchronously initializes the Microsoft WebView2 Chromium engine.
        /// Instantiates the DOM injector once CoreWebView2 is ready.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize the underlying Chromium browser environment
                await PortalWebView.EnsureCoreWebView2Async(null);

                // Instantiate DOM injector bound to this WebView2 instance
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
        // View Switching Helpers
        // --------------------------------------------------------------------------

        /// <summary>
        /// Displays View 1: Home Dashboard with two primary action tiles.
        /// </summary>
        private void ShowDashboard()
        {
            ViewDashboard.Visibility = Visibility.Visible;
            ViewSubmissionsList.Visibility = Visibility.Collapsed;
            ViewGuestReview.Visibility = Visibility.Collapsed;
            ViewPortalRunner.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Displays View 2: Recent Submissions Tabular DataGrid.
        /// </summary>
        private void ShowSubmissionsList()
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSubmissionsList.Visibility = Visibility.Visible;
            ViewGuestReview.Visibility = Visibility.Collapsed;
            ViewPortalRunner.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Displays View 3: Split-Screen Guest Review and Document Inspection.
        /// </summary>
        private void ShowGuestReview()
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewSubmissionsList.Visibility = Visibility.Collapsed;
            ViewGuestReview.Visibility = Visibility.Visible;
            ViewPortalRunner.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Displays View 4: Full-Screen Form-C Portal Runner Workspace.
        /// </summary>
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

        /// <summary>
        /// Handles click on Tile 1 (Review & Retrieve Submissions).
        /// Navigates to the submissions table and triggers an asynchronous server fetch.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private async void TileRetrieveSubmissions_Click(object sender, RoutedEventArgs e)
        {
            ShowSubmissionsList();
            await LoadSubmissionsAsync();
        }

        /// <summary>
        /// Handles click on Tile 2 (Portal Credentials).
        /// Loads currently stored DPAPI credentials and displays the modal configuration dialog.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void TileCredentials_Click(object sender, RoutedEventArgs e)
        {
            // Decrypt stored credentials if available
            var creds = CredentialStorageService.LoadCredentials();

            // Populate modal dialog input controls
            TxtModalUserId.Text = creds?.UserId ?? string.Empty;
            TxtModalPassword.Password = creds?.Password ?? string.Empty;

            // Display modal dialog overlay
            ModalCredentials.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Closes the credentials modal without saving changes.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnCloseCredsModal_Click(object sender, RoutedEventArgs e)
        {
            ModalCredentials.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Encrypts and persists entered Form-C credentials using Windows DPAPI.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnSaveCredsModal_Click(object sender, RoutedEventArgs e)
        {
            string user = TxtModalUserId.Text.Trim();
            string pass = TxtModalPassword.Password.Trim();

            // Validate that required fields are non-empty
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show(
                    "Please enter both User ID and Password.",
                    "Validation Notice",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Save encrypted credentials to %LOCALAPPDATA%\HomestayDesktopApp\credentials.dat
            CredentialStorageService.SaveCredentials(user, pass);

            // Close dialog overlay
            ModalCredentials.Visibility = Visibility.Collapsed;

            MessageBox.Show(
                "Form-C portal credentials securely encrypted and saved via Windows DPAPI.",
                "Credentials Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        // --------------------------------------------------------------------------
        // Screen 2: Submissions Retrieval & Tabular DataGrid (Live Server Only)
        // --------------------------------------------------------------------------

        /// <summary>
        /// Queries live jobs from the Dell Micro edge server and populates the DataGrid.
        /// Strictly binds live production data without any mock fallback.
        /// </summary>
        private async Task LoadSubmissionsAsync()
        {
            try
            {
                // Query live Dell Edge Server for processed submissions (GET /api/v1/jobs?limit=50)
                var jobs = await _apiClient.GetRecentJobsAsync();
                _submissionsList = new List<SubmissionSummary>();

                foreach (var job in jobs)
                {
                    // Derive lead guest name from extracted given names and surname
                    string leadGuestName = job.Result != null && !string.IsNullOrWhiteSpace(job.Result.GivenNames?.Value)
                        ? $"{job.Result.GivenNames.Value} {job.Result.Surname?.Value}".Trim()
                        : "Lead Guest";

                    // Base URL for document artifacts served by the Dell backend
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
                                VisaImageUrl = serverBase + "visa_page",
                                StampImageUrl = serverBase + "entry_stamp",
                                WelcomeFormImageUrl = serverBase + "welcome_form",
                                ExtractedRecord = job.Result ?? new GuestFormCRecord()
                            }
                        }
                    };
                    _submissionsList.Add(summary);
                }

                // Bind strictly live server submissions to the DataGrid
                DgSubmissions.ItemsSource = _submissionsList;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to fetch submissions from Dell server:\n{ex.Message}",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        /// <summary>
        /// Navigates back from the submissions list to the Home Dashboard.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnBackToDashboard_Click(object sender, RoutedEventArgs e) => ShowDashboard();

        /// <summary>
        /// Refreshes the submissions table by querying the edge server again.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private async void BtnRefreshSubmissions_Click(object sender, RoutedEventArgs e) => await LoadSubmissionsAsync();

        /// <summary>
        /// Handles click on the "Review Guests →" button within a DataGrid row.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnSelectSubmission_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SubmissionSummary submission)
            {
                OpenSubmissionReview(submission);
            }
        }

        /// <summary>
        /// Handles double-click on a row in the DataGrid to open review.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Mouse button event arguments.</param>
        private void DgSubmissions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgSubmissions.SelectedItem is SubmissionSummary submission)
            {
                OpenSubmissionReview(submission);
            }
        }

        /// <summary>
        /// Opens the review screen strictly scoped to the selected submission.
        /// Resets the guest navigation carousel to the first guest.
        /// </summary>
        /// <param name="submission">Selected SubmissionSummary object.</param>
        private void OpenSubmissionReview(SubmissionSummary submission)
        {
            // Strict isolation: guest carousel is scoped solely to this submission
            _selectedSubmission = submission;
            _currentGuestIndex = 0;
            ShowGuestReview();
            LoadCurrentGuestDetails();
        }

        // --------------------------------------------------------------------------
        // Screen 3: Multi-Guest Carousel & Review (Strictly Scoped to Submission)
        // --------------------------------------------------------------------------

        /// <summary>
        /// Populates the Form-C text boxes and document preview for the active guest.
        /// Enforces boundary states on Prev/Next guest buttons.
        /// </summary>
        private void LoadCurrentGuestDetails()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;

            int totalGuests = _selectedSubmission.Guests.Count;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];

            // Update header counter and navigation boundary controls
            TxtGuestCounter.Text = $"Guest {_currentGuestIndex + 1} of {totalGuests}: {guest.GuestName}";
            BtnPrevGuest.IsEnabled = _currentGuestIndex > 0;
            BtnNextGuest.IsEnabled = _currentGuestIndex < totalGuests - 1;

            // Map extracted Form-C bio-data fields into text boxes
            var rec = guest.ExtractedRecord;
            TxtReviewSurname.Text = rec.Surname?.Value ?? "";
            TxtReviewGivenNames.Text = rec.GivenNames?.Value ?? "";
            TxtReviewSex.Text = rec.Sex?.Value ?? "";
            TxtReviewDob.Text = rec.DateOfBirth?.Value ?? "";
            TxtReviewPassportNumber.Text = rec.PassportNumber?.Value ?? "";
            TxtReviewNationality.Text = rec.Nationality?.Value ?? "";
            TxtReviewVisaNumber.Text = rec.VisaNumber?.Value ?? "";
            TxtReviewVisaType.Text = rec.VisaType?.Value ?? "";
            TxtReviewArrivalDate.Text = rec.ArrivalDateIndia?.Value ?? "";
            TxtReviewArrivalPort.Text = rec.ArrivalPortIndia?.Value ?? "";
            TxtReviewArrivedFrom.Text = rec.ArrivedFrom?.Value ?? "";
            TxtReviewProceedingTo.Text = rec.ProceedingTo?.Value ?? "";
            TxtReviewPurposeOfVisit.Text = string.IsNullOrWhiteSpace(rec.PurposeOfVisit?.Value) ? "Tourism" : rec.PurposeOfVisit.Value;
            TxtReviewAddress.Text = rec.PermanentAddress?.Value ?? "";
            TxtReviewEmail.Text = rec.ContactEmail?.Value ?? "";
            TxtReviewPhone.Text = rec.ContactPhone?.Value ?? "";

            // Refresh document preview image based on the active tab
            UpdateDocumentImagePreview();
        }

        /// <summary>
        /// Updates the preview image in the left inspection pane according to the selected tab.
        /// Constrains the image to the visible viewport by default (100% fit).
        /// Falls back to passport_bio if a standalone guest_photo was not uploaded.
        /// </summary>
        private void UpdateDocumentImagePreview()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];

            // Base URL on the Dell Micro edge server
            string serverBase = "https://api.theasymptotebrief.com/api/v1/jobs/" + _selectedSubmission.Id + "/image/";
            string targetUrl = serverBase + "passport_bio";

            if (TabPassport.IsChecked == true)
            {
                targetUrl = serverBase + "passport_bio";
            }
            else if (TabVisa.IsChecked == true)
            {
                targetUrl = serverBase + "visa_page";
            }
            else if (TabStamp.IsChecked == true)
            {
                targetUrl = serverBase + "entry_stamp";
            }
            else if (TabWelcome.IsChecked == true)
            {
                targetUrl = serverBase + "welcome_form";
            }
            else if (TabPhoto.IsChecked == true)
            {
                // Attempt to load standalone portrait photo; falls back to passport_bio if absent on server
                targetUrl = serverBase + "guest_photo";
            }

            // Reset zoom scale so the whole image fits entirely inside the frame
            ResetImageZoom();

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(targetUrl, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                // If loading guest_photo fails (404), fall back to passport_bio
                bitmap.DownloadFailed += (s, e) =>
                {
                    if (TabPhoto.IsChecked == true)
                    {
                        var fallbackBitmap = new BitmapImage();
                        fallbackBitmap.BeginInit();
                        fallbackBitmap.UriSource = new Uri(serverBase + "passport_bio", UriKind.Absolute);
                        fallbackBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        fallbackBitmap.EndInit();
                        ImgDocumentPreview.Source = fallbackBitmap;
                    }
                    else
                    {
                        ImgDocumentPreview.Source = null;
                    }
                };

                bitmap.EndInit();
                ImgDocumentPreview.Source = bitmap;
            }
            catch
            {
                ImgDocumentPreview.Source = null;
            }
        }

        // --------------------------------------------------------------------------
        // Zoom and Image Pan Controls
        // --------------------------------------------------------------------------

        /// <summary>
        /// Resets the scale transform to 1.0 (fitting the entire document inside the visible viewport).
        /// </summary>
        private void ResetImageZoom()
        {
            _currentZoomScale = 1.0;
            ApplyZoom();
        }

        /// <summary>
        /// Applies the current zoom scale transform and updates the status indicator.
        /// </summary>
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

        /// <summary>
        /// Increases zoom scale up to a maximum of 400%.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZoomScale < 4.0)
            {
                _currentZoomScale += 0.25;
                ApplyZoom();
            }
        }

        /// <summary>
        /// Decreases zoom scale down to a minimum of 50%.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZoomScale > 0.5)
            {
                _currentZoomScale -= 0.25;
                ApplyZoom();
            }
        }

        /// <summary>
        /// Resets zoom back to 100% (frame fit).
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetImageZoom();
        }

        /// <summary>
        /// Handles mouse wheel events with Ctrl pressed to zoom smoothly in or out.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Mouse wheel event arguments.</param>
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
        /// Saves any operator text edits from UI controls back into the in-memory GuestFormCRecord model.
        /// </summary>
        private void SaveCurrentGuestEdits()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;

            var rec = _selectedSubmission.Guests[_currentGuestIndex].ExtractedRecord;
            rec.Surname.Value = TxtReviewSurname.Text.Trim();
            rec.GivenNames.Value = TxtReviewGivenNames.Text.Trim();
            rec.Sex.Value = TxtReviewSex.Text.Trim();
            rec.DateOfBirth.Value = TxtReviewDob.Text.Trim();
            rec.PassportNumber.Value = TxtReviewPassportNumber.Text.Trim();
            rec.Nationality.Value = TxtReviewNationality.Text.Trim();
            rec.VisaNumber.Value = TxtReviewVisaNumber.Text.Trim();
            rec.VisaType.Value = TxtReviewVisaType.Text.Trim();
            rec.ArrivalDateIndia.Value = TxtReviewArrivalDate.Text.Trim();
            rec.ArrivalPortIndia.Value = TxtReviewArrivalPort.Text.Trim();
            rec.ArrivedFrom.Value = TxtReviewArrivedFrom.Text.Trim();
            rec.ProceedingTo.Value = TxtReviewProceedingTo.Text.Trim();
            rec.PurposeOfVisit.Value = TxtReviewPurposeOfVisit.Text.Trim();
            rec.PermanentAddress.Value = TxtReviewAddress.Text.Trim();
            rec.ContactEmail.Value = TxtReviewEmail.Text.Trim();
            rec.ContactPhone.Value = TxtReviewPhone.Text.Trim();
        }

        /// <summary>
        /// Navigates to the previous guest within the active submission.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
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

        /// <summary>
        /// Navigates to the next guest within the active submission.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
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

        /// <summary>
        /// Returns from guest review back to the submissions DataGrid table.
        /// Persists current edits into memory.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnBackToSubmissions_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentGuestEdits();
            ShowSubmissionsList();
        }

        /// <summary>
        /// Handles switching of document preview tabs (Passport, Visa, Stamp, Form-C, Photo).
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void DocTab_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDocumentImagePreview();
        }

        /// <summary>
        /// Saves edits and transitions from review to the embedded Form-C Portal Runner.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnLaunchPortal_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentGuestEdits();
            ShowPortalRunner();
            UpdatePortalHeader();
        }

        // --------------------------------------------------------------------------
        // Screen 4: Portal Runner & WebView2 DOM Automation
        // --------------------------------------------------------------------------

        /// <summary>
        /// Updates the top runner banner with the active guest name and submission index.
        /// </summary>
        private void UpdatePortalHeader()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];
            TxtPortalActiveGuest.Text = $"Active Guest: {guest.GuestName} ({_currentGuestIndex + 1} of {_selectedSubmission.Guests.Count})";
        }

        /// <summary>
        /// Returns from the portal runner back to the guest review screen.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnBackToGuestReview_Click(object sender, RoutedEventArgs e) => ShowGuestReview();

        /// <summary>
        /// Switches to the next guest within the active submission while inside the portal runner.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
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

        /// <summary>
        /// Injects DPAPI-decrypted credentials into the Form-C login form.
        /// Automatically focuses the visual CAPTCHA input for operator entry.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private async void BtnAutofillLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_domInjector == null)
            {
                MessageBox.Show("WebView2 is not yet initialized.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Read encrypted credentials from disk
            var stored = CredentialStorageService.LoadCredentials();
            string portalUser = stored?.UserId ?? "";
            string portalPass = stored?.Password ?? "";

            // Prompt user via settings modal if not yet configured
            if (string.IsNullOrWhiteSpace(portalUser) || string.IsNullOrWhiteSpace(portalPass))
            {
                MessageBox.Show(
                    "No saved Form-C credentials found. Please configure them once via Tile 2 (Portal Credentials).",
                    "Credentials Needed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                TxtModalUserId.Text = "";
                TxtModalPassword.Password = "";
                ModalCredentials.Visibility = Visibility.Visible;
                return;
            }

            // Injects credentials into login inputs across all frames/iframes
            (bool success, string message) = await _domInjector.InjectLoginCredentialsAsync(portalUser, portalPass);

            if (success)
            {
                MessageBox.Show(message, "Autofill Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(message, "Autofill Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Injects the active guest's verified bio-data fields into the active Form-C registration page.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
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

        /// <summary>
        /// Reloads the embedded WebView2 portal web page.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Routed event arguments.</param>
        private void BtnReloadWeb_Click(object sender, RoutedEventArgs e)
        {
            PortalWebView?.Reload();
        }
    }
}