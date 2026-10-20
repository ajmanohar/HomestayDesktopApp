// ==============================================================================
// MainWindow.xaml.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Orchestrates multi-screen navigation:
// 1. Dashboard Landing (Review & Retrieve Submissions vs Ingest New)
// 2. Submissions List & Batch Ingestion
// 3. Multi-Guest Split-Screen Review (Document Preview + Form-C Fields)
// 4. Form-C Portal Runner with Microsoft WebView2 Chromium Automation
// Base Server: https://api.theasymptotebrief.com via Cloudflare Tunnel
// ==============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        // Current Active Submission & Guest Navigation
        private List<SubmissionSummary> _submissionsList = new List<SubmissionSummary>();
        private SubmissionSummary? _selectedSubmission;
        private int _currentGuestIndex = 0;

        /// <summary>
        /// Constructor: Initializes client targeting Dell server and hooks WebView2.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Initialize API Client communicating with Dell Edge Server
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
            // Prompt user to select local document images for extraction
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
                    // Submit to Dell edge server
                    var jobResp = await _apiClient.SubmitDocumentsAsync(passportFile);
                    MessageBox.Show($"Documents dispatched to Dell server! Assigned Job ID: {jobResp.JobId}", "Extraction Dispatched", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Switch to submissions list and reload
                    ShowSubmissionsList();
                    await LoadSubmissionsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Upload failed:\n{ex.Message}", "Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --------------------------------------------------------------------------
        // Screen 2: Submissions Retrieval & Selection
        // --------------------------------------------------------------------------

        private async Task LoadSubmissionsAsync()
        {
            try
            {
                var jobs = await _apiClient.GetRecentJobsAsync();
                _submissionsList = new List<SubmissionSummary>();

                foreach (var job in jobs)
                {
                    var summary = new SubmissionSummary
                    {
                        Id = job.JobId,
                        Reference = $"Submission #{job.JobId[..Math.Min(8, job.JobId.Length)]}",
                        Status = job.Status,
                        CreatedAt = job.CreatedAt,
                        GuestCount = 1,
                        Guests = new List<GuestItem>
                        {
                            new GuestItem
                            {
                                GuestIndex = 0,
                                GuestName = job.Result != null && !string.IsNullOrWhiteSpace(job.Result.GivenNames?.Value)
                                    ? $"{job.Result.GivenNames.Value} {job.Result.Surname?.Value}".Trim()
                                    : "Guest 1",
                                ExtractedRecord = job.Result ?? new GuestFormCRecord()
                            }
                        }
                    };
                    _submissionsList.Add(summary);
                }

                // If no jobs returned from server yet, create placeholder entry for testing
                if (_submissionsList.Count == 0)
                {
                    _submissionsList.Add(new SubmissionSummary
                    {
                        Id = "local_demo_1",
                        Reference = "Demo Homestay Check-In (2 Guests)",
                        Status = "COMPLETED",
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        GuestCount = 2,
                        Guests = new List<GuestItem>
                        {
                            new GuestItem
                            {
                                GuestIndex = 0,
                                GuestName = "BAUER DOE",
                                ExtractedRecord = new GuestFormCRecord
                                {
                                    Surname = new FieldResult("DOE"),
                                    GivenNames = new FieldResult("BAUER"),
                                    Sex = new FieldResult("M"),
                                    DateOfBirth = new FieldResult("1990-10-20"),
                                    PassportNumber = new FieldResult("PAD640874"),
                                    Nationality = new FieldResult("Panamanian"),
                                    VisaNumber = new FieldResult("1041548"),
                                    VisaType = new FieldResult("TOURIST VISA"),
                                    ArrivalDateIndia = new FieldResult("15/08/2026"),
                                    ArrivalPortIndia = new FieldResult("COCHIN INSEAPORT"),
                                    PurposeOfVisit = new FieldResult("Tourism")
                                }
                            },
                            new GuestItem
                            {
                                GuestIndex = 1,
                                GuestName = "JANE DOE",
                                ExtractedRecord = new GuestFormCRecord
                                {
                                    Surname = new FieldResult("DOE"),
                                    GivenNames = new FieldResult("JANE"),
                                    Sex = new FieldResult("F"),
                                    DateOfBirth = new FieldResult("1992-04-15"),
                                    PassportNumber = new FieldResult("PAD991823"),
                                    Nationality = new FieldResult("Panamanian"),
                                    VisaNumber = new FieldResult("1041549"),
                                    VisaType = new FieldResult("TOURIST VISA"),
                                    ArrivalDateIndia = new FieldResult("15/08/2026"),
                                    ArrivalPortIndia = new FieldResult("COCHIN INSEAPORT"),
                                    PurposeOfVisit = new FieldResult("Tourism")
                                }
                            }
                        }
                    });
                }

                LstSubmissions.ItemsSource = _submissionsList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch submissions:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void LstSubmissions_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LstSubmissions.SelectedItem is SubmissionSummary submission)
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

        private void LoadCurrentGuestDetails()
        {
            if (_selectedSubmission == null || _selectedSubmission.Guests.Count == 0) return;

            int totalGuests = _selectedSubmission.Guests.Count;
            var guest = _selectedSubmission.Guests[_currentGuestIndex];

            TxtGuestCounter.Text = $"Guest {_currentGuestIndex + 1} of {totalGuests}: {guest.GuestName}";

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
            // Switch document preview placeholder or image if available
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
                _currentGuestIndex = 0; // Wrap around to first guest
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

            string portalUser = Environment.GetEnvironmentVariable("FORMC_PORTAL_USER") ?? "";
            string portalPass = Environment.GetEnvironmentVariable("FORMC_PORTAL_PASS") ?? "";

            if (string.IsNullOrWhiteSpace(portalUser) || string.IsNullOrWhiteSpace(portalPass))
            {
                portalUser = Microsoft.VisualBasic.Interaction.InputBox("Enter Form-C Portal User ID:", "Portal Login", portalUser);
                if (string.IsNullOrWhiteSpace(portalUser)) return;

                portalPass = Microsoft.VisualBasic.Interaction.InputBox("Enter Form-C Portal Password:", "Portal Login", portalPass);
                if (string.IsNullOrWhiteSpace(portalPass)) return;
            }

            // Fixed tuple deconstruction resolving error CS0029
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

            // Fixed tuple deconstruction resolving error CS0029
            (bool success, string message) = await _domInjector.InjectGuestDetailsAsync(guest.ExtractedRecord);

            MessageBox.Show(message, success ? "Autofill Complete" : "Autofill Status", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void BtnReloadWeb_Click(object sender, RoutedEventArgs e)
        {
            PortalWebView?.Reload();
        }
    }
}