// ==============================================================================
// MainWindow.xaml.cs - HomestayDesktopApp (.NET 8 WPF)
// ==============================================================================
// Code-behind orchestrating document selection, edge server inference polling,
// verification binding, and Chromium WebView2 DOM automation.
// Base Server: https://api.theasymptotebrief.com via Cloudflare Tunnel
// ==============================================================================

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using HomestayDesktopApp.Models;
using HomestayDesktopApp.Services;

namespace HomestayDesktopApp
{
    public partial class MainWindow : Window
    {
        // Network client communicating with Dell server backend
        private readonly PassportApiClient _apiClient;

        // Automation engine executing DOM scripts inside WebView2
        private FormCDomInjector? _domInjector;

        // Current active verified record model
        private GuestFormCRecord _currentRecord = new GuestFormCRecord();

        // Cancellation token source for background server polling
        private CancellationTokenSource? _pollingCts;

        /// <summary>
        /// Initializes the main application window and backing services.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Initialize client targeting Dell server over Cloudflare Tunnel
            _apiClient = new PassportApiClient();

            // Hook WebView2 initialization event
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Ensures Microsoft WebView2 core environment is initialized.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize WebView2 core engine
                await PortalWebView.EnsureCoreWebView2Async(null);

                // Instantiate DOM injector once WebView2 is ready
                _domInjector = new FormCDomInjector(PortalWebView);

                // Listen for navigation state updates
                PortalWebView.CoreWebView2.SourceChanged += (s, args) =>
                {
                    TxtCurrentUrl.Text = PortalWebView.Source?.ToString() ?? string.Empty;
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize Microsoft WebView2:\n{ex.Message}\n\nEnsure WebView2 Runtime is installed.",
                    "WebView2 Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // --------------------------------------------------------------------------
        // Document File Pickers
        // --------------------------------------------------------------------------

        private void BtnBrowsePassport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All Files (*.*)|*.*",
                Title = "Select Passport Bio-Data Page Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtPassportPath.Text = openFileDialog.FileName;
            }
        }

        private void BtnBrowseVisa_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All Files (*.*)|*.*",
                Title = "Select Visa / OCI Page Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtVisaPath.Text = openFileDialog.FileName;
            }
        }

        private void BtnBrowseStamp_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All Files (*.*)|*.*",
                Title = "Select Entry Stamp Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtStampPath.Text = openFileDialog.FileName;
            }
        }

        private void BtnBrowseWelcome_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All Files (*.*)|*.*",
                Title = "Select Handwritten Welcome Form (Form-C Rule 14) Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtWelcomePath.Text = openFileDialog.FileName;
            }
        }

        // --------------------------------------------------------------------------
        // Edge Server Job Submission and Status Polling
        // --------------------------------------------------------------------------

        private async void BtnProcessDocuments_Click(object sender, RoutedEventArgs e)
        {
            string passportPath = TxtPassportPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(passportPath) || !File.Exists(passportPath))
            {
                MessageBox.Show("Please select a valid Passport Bio-Data image first.", "Document Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lock UI and show progress indicator
            BtnProcessDocuments.IsEnabled = false;
            PbExtraction.Visibility = Visibility.Visible;
            PbExtraction.IsIndeterminate = true;
            TxtStatus.Text = "Status: Uploading documents to Dell server...";

            _pollingCts = new CancellationTokenSource();

            try
            {
                // 1. Submit documents
                var submitResponse = await _apiClient.SubmitDocumentsAsync(
                    passportBioPath: passportPath,
                    visaPath: TxtVisaPath.Text.Trim(),
                    entryStampPath: TxtStampPath.Text.Trim(),
                    welcomeFormPath: TxtWelcomePath.Text.Trim(),
                    cancellationToken: _pollingCts.Token
                );

                string jobId = submitResponse.JobId;
                TxtStatus.Text = $"Status: Job queued on server (ID: {jobId[..8]}...). Running OCR & VLM inference...";

                // 2. Poll job status until COMPLETED or FAILED
                while (!_pollingCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(2500, _pollingCts.Token);

                    var pollResponse = await _apiClient.PollJobStatusAsync(jobId, _pollingCts.Token);
                    if (pollResponse.Status == "COMPLETED")
                    {
                        TxtStatus.Text = "Status: Extraction complete! Review fields below.";
                        if (pollResponse.Result != null)
                        {
                            _currentRecord = pollResponse.Result;
                            PopulateFormUi(_currentRecord);
                        }
                        break;
                    }
                    else if (pollResponse.Status == "FAILED")
                    {
                        TxtStatus.Text = $"Status: Inference failed ({pollResponse.ErrorMessage ?? "Unknown error"}).";
                        MessageBox.Show($"Server processing failed:\n{pollResponse.ErrorMessage}", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
                    else
                    {
                        TxtStatus.Text = $"Status: Server processing ({pollResponse.Status})...";
                    }
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Status: Network or server error occurred.";
                MessageBox.Show($"Error during document processing:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnProcessDocuments.IsEnabled = true;
                PbExtraction.Visibility = Visibility.Collapsed;
                PbExtraction.IsIndeterminate = false;
            }
        }

        /// <summary>
        /// Populates the desktop review text boxes with the extracted fields.
        /// </summary>
        /// <param name="record">The extracted GuestFormCRecord model.</param>
        private void PopulateFormUi(GuestFormCRecord record)
        {
            TxtSurname.Text = record.Surname?.Value ?? string.Empty;
            TxtGivenNames.Text = record.GivenNames?.Value ?? string.Empty;
            TxtSex.Text = record.Sex?.Value ?? string.Empty;
            TxtDob.Text = record.DateOfBirth?.Value ?? string.Empty;
            TxtPassportNumber.Text = record.PassportNumber?.Value ?? string.Empty;
            TxtNationality.Text = record.Nationality?.Value ?? string.Empty;
            TxtVisaNumber.Text = record.VisaNumber?.Value ?? string.Empty;
            TxtVisaType.Text = record.VisaType?.Value ?? string.Empty;
            TxtArrivalDate.Text = record.ArrivalDateIndia?.Value ?? string.Empty;
            TxtArrivalPort.Text = record.ArrivalPortIndia?.Value ?? string.Empty;
            TxtArrivedFrom.Text = record.ArrivedFrom?.Value ?? string.Empty;
            TxtProceedingTo.Text = record.ProceedingTo?.Value ?? string.Empty;
            TxtPurposeOfVisit.Text = string.IsNullOrWhiteSpace(record.PurposeOfVisit?.Value) ? "Tourism" : record.PurposeOfVisit.Value;
            TxtEmail.Text = record.ContactEmail?.Value ?? string.Empty;
            TxtPhone.Text = record.ContactPhone?.Value ?? string.Empty;
        }

        /// <summary>
        /// Collects values from the editable form text boxes back into the current model.
        /// </summary>
        private void SyncFormUiToRecord()
        {
            _currentRecord.Surname.Value = TxtSurname.Text.Trim();
            _currentRecord.GivenNames.Value = TxtGivenNames.Text.Trim();
            _currentRecord.Sex.Value = TxtSex.Text.Trim();
            _currentRecord.DateOfBirth.Value = TxtDob.Text.Trim();
            _currentRecord.PassportNumber.Value = TxtPassportNumber.Text.Trim();
            _currentRecord.Nationality.Value = TxtNationality.Text.Trim();
            _currentRecord.VisaNumber.Value = TxtVisaNumber.Text.Trim();
            _currentRecord.VisaType.Value = TxtVisaType.Text.Trim();
            _currentRecord.ArrivalDateIndia.Value = TxtArrivalDate.Text.Trim();
            _currentRecord.ArrivalPortIndia.Value = TxtArrivalPort.Text.Trim();
            _currentRecord.ArrivedFrom.Value = TxtArrivedFrom.Text.Trim();
            _currentRecord.ProceedingTo.Value = TxtProceedingTo.Text.Trim();
            _currentRecord.PurposeOfVisit.Value = TxtPurposeOfVisit.Text.Trim();
            _currentRecord.ContactEmail.Value = TxtEmail.Text.Trim();
            _currentRecord.ContactPhone.Value = TxtPhone.Text.Trim();
        }

        // --------------------------------------------------------------------------
        // WebView2 Automation Action Buttons
        // --------------------------------------------------------------------------

        private async void BtnAutofillLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_domInjector == null)
            {
                MessageBox.Show("WebView2 is not yet initialized.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prompt for credentials or use stored credentials
            // For convenience, opens prompt or injects directly
            string portalUser = Environment.GetEnvironmentVariable("FORMC_PORTAL_USER") ?? "";
            string portalPass = Environment.GetEnvironmentVariable("FORMC_PORTAL_PASS") ?? "";

            if (string.IsNullOrWhiteSpace(portalUser) || string.IsNullOrWhiteSpace(portalPass))
            {
                // In production, these can be saved to Windows Credential Manager or local app settings
                portalUser = Microsoft.VisualBasic.Interaction.InputBox("Enter Form-C Portal User ID:", "Portal Credentials", portalUser);
                if (string.IsNullOrWhiteSpace(portalUser)) return;

                portalPass = Microsoft.VisualBasic.Interaction.InputBox("Enter Form-C Portal Password:", "Portal Credentials", portalPass);
                if (string.IsNullOrWhiteSpace(portalPass)) return;
            }

            bool success = await _domInjector.InjectLoginCredentialsAsync(portalUser, portalPass);
            if (success)
            {
                MessageBox.Show("Credentials injected! Please enter CAPTCHA and sign in.", "Autofill Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Could not find login fields on active page. Make sure you are on the login screen.", "Autofill Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnAutofillFields_Click(object sender, RoutedEventArgs e)
        {
            if (_domInjector == null)
            {
                MessageBox.Show("WebView2 is not yet initialized.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Sync user edits from text boxes before injection
            SyncFormUiToRecord();

            string resultJson = await _domInjector.InjectGuestDetailsAsync(_currentRecord);
            MessageBox.Show($"Guest fields injected into portal:\n{resultJson}\n\nPlease review on page before saving.", "Autofill Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnReloadWeb_Click(object sender, RoutedEventArgs e)
        {
            PortalWebView?.Reload();
        }
    }
}