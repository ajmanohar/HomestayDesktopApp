// ==============================================================================
// MainWindow.xaml.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Strictly operates against real live submissions from the Dell Edge Server
// Features: Default whole-image fitting, interactive zoom/pan controls,
// and guest portrait photo streaming.
// Domain: https://api.theasymptotebrief.com via Cloudflare Tunnel
// ==============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using HomestayDesktopApp.Models;
using HomestayDesktopApp.Services;

namespace HomestayDesktopApp
{
    public partial class MainWindow : Window
    {
        // --------------------------------------------------------------------------
        // State & Backing Services
        // --------------------------------------------------------------------------
        private readonly PassportApiClient _apiClient;
        private FormCDomInjector? _domInjector;

        // Current Active Submissions & Selected Guest Navigation
        private List<SubmissionSummary> _submissionsList = new List<SubmissionSummary>();
        private SubmissionSummary? _selectedSubmission;
        private int _currentGuestIndex = 0;

        // Zoom state tracking
        private double _currentZoomScale = 1.0;

        /// <summary>
        /// Constructor: Initializes client targeting Dell server and hooks WebView2.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Initialize API Client communicating with live Dell Edge Server
            _apiClient = new PassportApiClient();

            // Ensure WebView2 is initialized
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Asynchronously initializes Microsoft WebView2 control.
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

        private async void TileNewCapture_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "Select Passport Bio-Data Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string passportFile = openFileDialog.FileName;

                try
                {
                    var jobResp = await _apiClient.SubmitDocumentsAsync(passportFile);
                    MessageBox.Show($"Documents dispatched to Dell server! Assigned Job ID: {jobResp.JobId}", "Extraction Dispatched", MessageBoxButton.OK, MessageBoxImage.Information);

                    ShowSubmissionsList();
                    await LoadSubmissionsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Upload failed:\n{ex.Message}", "Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
        // Screen 2: Submissions Retrieval & Tabular DataGrid (Live Server Only)
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

                DgSubmissions.ItemsSource = _submissionsList;
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
        // Screen 3: Multi-Guest Carousel & Review (Strictly Scoped to Submission)
        // --------------------------------------------------------------------------

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

            UpdateDocumentImagePreview();
        }

        private void UpdateDocumentImagePreview()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];

            // Build image endpoint matching active tab
            string serverBase = "https://api.theasymptotebrief.com/api/v1/jobs/" + _selectedSubmission.Id + "/image/";
            string? imageUrl = null;

            if (TabPassport.IsChecked == true) imageUrl = serverBase + "passport_bio";
            else if (TabVisa.IsChecked == true) imageUrl = serverBase + "visa_page";
            else if (TabStamp.IsChecked == true) imageUrl = serverBase + "entry_stamp";
            else if (TabWelcome.IsChecked == true) imageUrl = serverBase + "welcome_form";
            else if (TabPhoto.IsChecked == true) imageUrl = serverBase + "guest_photo";

            ResetImageZoom();

            try
            {
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
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

        // --------------------------------------------------------------------------
        // Zoom and Image Pan Controls
        // --------------------------------------------------------------------------

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
                TxtZoomLevel.Text = _currentZoomScale == 1.0 ? "Zoom: Fit to Window (100%)" : $"Zoom: {pct}%";
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
            // Zoom in or out with Ctrl + MouseWheel
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
            UpdateDocumentImagePreview();
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
                MessageBox.Show("No saved Form-C credentials found. Please configure them once via Tile 3 (Portal Credentials).", "Credentials Needed", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtModalUserId.Text = "";
                TxtModalPassword.Password = "";
                ModalCredentials.Visibility = Visibility.Visible;
                return;
            }

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

            MessageBox.Show(message, success ? "Autofill Complete" : "Autofill Status", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void BtnReloadWeb_Click(object sender, RoutedEventArgs e)
        {
            PortalWebView?.Reload();
        }
    }
}