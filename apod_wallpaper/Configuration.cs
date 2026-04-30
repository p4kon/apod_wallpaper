using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace apod_wallpaper
{
    internal partial class ConfigurationForm : Form
    {
        private const string NasaApiKeyUrl = "https://api.nasa.gov/";
        private const MonthRefreshMode CalendarWarmupMode = MonthRefreshMode.Balanced;
        private static readonly WallpaperStyle[] WallpaperStyleDisplayOrder =
        {
            WallpaperStyle.Smart,
            WallpaperStyle.Fill,
            WallpaperStyle.Fit,
            WallpaperStyle.Stretch,
            WallpaperStyle.Tile,
            WallpaperStyle.Center,
            WallpaperStyle.Span,
        };
        private readonly IApplicationSettingsFacade settingsFacade;
        private readonly IApplicationStorageFacade storageFacade;
        private readonly IApodWorkflowFacade workflowFacade;
        private readonly IApodCalendarFacade calendarFacade;
        private readonly IApplicationSessionFacade sessionFacade;
        private IEventSubscription wallpaperAppliedSubscription;
        private bool suppressSettingsSync = true;
        private bool not_found = false;
        private bool nonImageMedia = false;
        private bool loading_picture = false;
        private bool download_only = false;
        private bool applyingWallpaperStyleChange = false;
        private int previewRequestVersion;
        private ApodEntry currentEntry;
        private ApplicationSettingsSnapshot currentSettings;
        private ToolTip toolTip = new ToolTip();

        internal ConfigurationForm(IApplicationBackendFacade backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            settingsFacade = backend;
            storageFacade = backend;
            workflowFacade = backend;
            calendarFacade = backend;
            sessionFacade = backend;
            InitializeComponent();
            pictureDayDateTimePicker.MaxDate = DateTime.Today;
            pictureDayDateTimePicker.Value = DateTime.Today;
            wallpaperStyleComboBox.DataSource = WallpaperStyleDisplayOrder;
            pictureDayDateTimePicker.DropDown += pictureDayDateTimePicker_DropDown;
            imagesFolderTextBox.TextChanged += imagesFolderTextBox_TextChanged;
            downloadSetCheckBox.CheckedChanged += settingsControl_Changed;
            startWithWindowsCheckBox.CheckedChanged += settingsControl_Changed;
            everyTimeCheckBox.CheckedChanged += settingsControl_Changed;
            wallpaperStyleComboBox.SelectedIndexChanged += settingsControl_Changed;
            apiKeyTextBox.TextChanged += settingsControl_Changed;
            ConfigureToolTips();
        }

        private async void LoadSettings(object sender, EventArgs e)
        {
            try
            {
                suppressSettingsSync = true;
                if (wallpaperAppliedSubscription == null)
                {
                    wallpaperAppliedSubscription = await GetValueOrThrowAsync(
                        sessionFacade.SubscribeWallpaperAppliedAsync(controller_WallpaperApplied),
                        "Unable to subscribe to wallpaper applied events.").ConfigureAwait(true);
                }

                var initialState = await GetValueOrThrowAsync(settingsFacade.GetInitialStateAsync(), "Unable to load the initial application state.").ConfigureAwait(true);
                currentSettings = initialState.Settings;
                downloadSetCheckBox.Checked = currentSettings.TrayDoubleClickAction;
                wallpaperStyleComboBox.SelectedItem = initialState.SelectedWallpaperStyle;
                everyTimeCheckBox.Checked = currentSettings.AutoRefreshEnabled;
                startWithWindowsCheckBox.Checked = currentSettings.StartWithWindows;
                apiKeyTextBox.Text = currentSettings.NasaApiKey;
                imagesFolderTextBox.Text = initialState.StoragePaths.ImagesDirectory;
                var preferredDate = initialState.PreferredDisplayDate;
                if (preferredDate > pictureDayDateTimePicker.MaxDate)
                    preferredDate = pictureDayDateTimePicker.MaxDate.Date;
                pictureDayDateTimePicker.Value = preferredDate;
                UpdateApiKeyValidationIndicator(initialState.ApiKeyValidationState);
                suppressSettingsSync = false;

                await WarmUpCalendarMonthAsync(pictureDayDateTimePicker.Value.Date, true);
            }
            catch (Exception ex)
            {
                suppressSettingsSync = false;
                MessageBox.Show(GetUserFacingExceptionMessage(ex),
                                "APOD error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private async void SaveSettings(object sender, FormClosingEventArgs e)
        {
            await PersistSettingsAsync().ConfigureAwait(true);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            wallpaperAppliedSubscription?.Dispose();
            base.OnFormClosed(e);
        }

        public async Task DownloadWallpaperAsync()
        {
            var selectedDate = pictureDayDateTimePicker.Value.Date;
            download_only = Control.ModifierKeys == Keys.Shift;

            OperationResult<ApodWorkflowResult> operationResult;
            if (download_only)
            {
                operationResult = await workflowFacade.DownloadDayAsync(selectedDate).ConfigureAwait(true);
            }
            else
            {
                operationResult = await workflowFacade.ApplyDayAsync(selectedDate, GetSelectedWallpaperStyle()).ConfigureAwait(true);
            }
            currentSettings = await GetValueOrThrowAsync(settingsFacade.GetSettingsAsync(), "Unable to reload application settings.").ConfigureAwait(true);
            UpdateApiKeyValidationIndicator(await GetValueOrThrowAsync(settingsFacade.GetApiKeyValidationStateAsync(), "Unable to read API key validation state.").ConfigureAwait(true));

            if (!operationResult.Succeeded)
                throw new InvalidOperationException(GetOperationErrorMessage(operationResult, "Unable to complete the requested wallpaper operation."));

            var workflowResult = operationResult.Value;
            currentEntry = workflowResult.Entry;
            if (!workflowResult.IsSuccess)
            {
                ShowUnavailableEntryState();
                return;
            }

            nonImageMedia = false;
            UpdatePreviewImage(workflowResult.ImagePath);
            MaybeDisableAutoRefreshForManualSelection(selectedDate);
        }

        private async void PictureDayDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            not_found = false;
            nonImageMedia = false;
            downloadButton.Enabled = false;
            await PictureShowPreviewAsync();
        }

        private async Task PictureShowPreviewAsync()
        {
            var selectedDate = pictureDayDateTimePicker.Value.Date;
            var requestVersion = Interlocked.Increment(ref previewRequestVersion);
            currentEntry = null;

            try
            {
                PreviewPictureBox.ImageLocation = null;
                PreviewPictureBox.Image = ApodResources.loading_image_progress;
                loading_picture = true;

                var operationResult = await workflowFacade.LoadDayAsync(selectedDate).ConfigureAwait(true);
                currentSettings = await GetValueOrThrowAsync(settingsFacade.GetSettingsAsync(), "Unable to reload application settings.").ConfigureAwait(true);
                UpdateApiKeyValidationIndicator(await GetValueOrThrowAsync(settingsFacade.GetApiKeyValidationStateAsync(), "Unable to read API key validation state.").ConfigureAwait(true));
                if (requestVersion != previewRequestVersion)
                    return;

                if (!operationResult.Succeeded)
                {
                    ShowUnavailableEntryState();
                    return;
                }

                var workflowResult = operationResult.Value;
                currentEntry = workflowResult.Entry;

                if (!workflowResult.IsSuccess || string.IsNullOrWhiteSpace(workflowResult.PreviewLocation))
                {
                    ShowUnavailableEntryState();
                    return;
                }

                nonImageMedia = false;
                UpdatePreviewImage(workflowResult.PreviewLocation);
            }
            catch
            {
                if (requestVersion != previewRequestVersion)
                    return;

                ShowUnavailableEntryState();
            }
        }

        private void everyTimeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _ = PersistSettingsAsync();
        }

        private async void pictureDayDateTimePicker_DropDown(object sender, EventArgs e)
        {
            await WarmUpCalendarMonthAsync(pictureDayDateTimePicker.Value.Date, true);
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            _ = HandleDownloadButtonClickAsync();
            PreviewPictureBox.Focus();
        }

        private async Task HandleDownloadButtonClickAsync()
        {
            try
            {
                await DownloadWallpaperAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ShowUnavailableEntryState();
                MessageBox.Show(GetUserFacingExceptionMessage(ex),
                                "APOD error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private void browseImagesFolderButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = imagesFolderTextBox.Text;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    imagesFolderTextBox.Text = dialog.SelectedPath;
                    _ = ApplyPendingImagesDirectoryAsync();
                }
            }
        }

        private async void openImagesFolderButton_Click(object sender, EventArgs e)
        {
            var path = string.IsNullOrWhiteSpace(imagesFolderTextBox.Text)
                ? (await GetValueOrThrowAsync(storageFacade.EnsureStorageLayoutAsync(), "Unable to prepare the images directory.").ConfigureAwait(true)).ImagesDirectory
                : await GetValueOrThrowAsync(settingsFacade.UpdateSessionImagesDirectoryAsync(imagesFolderTextBox.Text.Trim()), "Unable to resolve the images directory.").ConfigureAwait(true);

            await GetValueOrThrowAsync(storageFacade.EnsureStorageLayoutAsync(), "Unable to prepare the images directory.").ConfigureAwait(true);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void imagesFolderTextBox_TextChanged(object sender, EventArgs e)
        {
            _ = ApplyPendingImagesDirectoryAsync();
            _ = PersistSettingsAsync();
        }

        private async void PreviewPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (!not_found && !loading_picture)
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    Process.Start(new ProcessStartInfo(await GetValueOrThrowAsync(workflowFacade.GetPostUrlAsync(pictureDayDateTimePicker.Value.Date), "Unable to resolve the NASA APOD page URL.").ConfigureAwait(true))
                    {
                        UseShellExecute = true
                    });
                }
                else
                {
                    var fullScreenFrom = new Form();
                    var fullScreenPictureBox = new PictureBox();
                    fullScreenPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    fullScreenPictureBox.Image = PreviewPictureBox.Image;
                    fullScreenPictureBox.Dock = DockStyle.Fill;
                    fullScreenFrom.Controls.Add(fullScreenPictureBox);
                    fullScreenFrom.Icon = ApodResources.apod_icon;
                    fullScreenFrom.WindowState = FormWindowState.Maximized;
                    fullScreenFrom.KeyPreview = true;
                    fullScreenFrom.KeyDown += new KeyEventHandler(FullScreenFrom_KeyDown);
                    fullScreenFrom.ShowDialog();
                }
            }
            else if (not_found && Control.ModifierKeys == Keys.Shift)
            {
                Process.Start(new ProcessStartInfo(await GetValueOrThrowAsync(workflowFacade.GetPostUrlAsync(pictureDayDateTimePicker.Value.Date), "Unable to resolve the NASA APOD page URL.").ConfigureAwait(true))
                {
                    UseShellExecute = true
                });
            }
        }

        private void FullScreenFrom_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                var activeForm = Form.ActiveForm;
                if (activeForm != null)
                    activeForm.Close();
            }
        }

        private void PreviewPictureBox_MouseHover(object sender, EventArgs e)
        {
            if (!not_found && !loading_picture)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Hand;
                toolTip.SetToolTip(this.PreviewPictureBox,
                                   "Click: open fullsize preview" + Environment.NewLine + "Shift+Click: open NASA APOD page");
            }
            else if (!not_found && loading_picture)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Default;
                toolTip.SetToolTip(this.PreviewPictureBox, "Loading may take some time");
            }
            else if (nonImageMedia)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Hand;
                toolTip.SetToolTip(this.PreviewPictureBox,
                                   "This APOD entry is not an image." + Environment.NewLine + "Shift+Click: open NASA APOD page");
            }
            else if (not_found)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Hand;
                toolTip.SetToolTip(this.PreviewPictureBox, "Shift+Click: open NASA APOD page");
            }
        }

        private void PreviewPictureBox_LoadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            pictureDayDateTimePicker.Enabled = true;
            downloadButton.Enabled = !not_found;
            loading_picture = false;
        }

        private void wallpaperStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var activeForm = ActiveForm;
            if (activeForm != null)
                activeForm.Focus();

            if (!suppressSettingsSync)
                _ = ReapplySelectedWallpaperStyleAsync();
        }

        private void downloadButton_MouseHover(object sender, EventArgs e)
        {
            toolTip.Active = true;
            toolTip.SetToolTip(this.downloadButton, "Shift+Click for only download picture");
        }

        private void getApiKeyButton_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(NasaApiKeyUrl);
            }
            catch
            {
            }

            try
            {
                Process.Start(new ProcessStartInfo(NasaApiKeyUrl)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
            }

            MessageBox.Show(
                "NASA API key page link has been copied to the clipboard." + Environment.NewLine +
                "1. Open the page in your browser (VPN may be required in some regions)." + Environment.NewLine +
                "2. Enter your email and generate a free key." + Environment.NewLine +
                "3. Paste the key into the field above." + Environment.NewLine +
                "DEMO_KEY is fine for testing, but your own key gives much higher limits.",
                "NASA API key",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private WallpaperStyle GetSelectedWallpaperStyle()
        {
            if (InvokeRequired)
            {
                return (WallpaperStyle)wallpaperStyleComboBox.Invoke(new Func<WallpaperStyle>(GetSelectedWallpaperStyle));
            }

            return wallpaperStyleComboBox.SelectedItem != null
                ? (WallpaperStyle)wallpaperStyleComboBox.SelectedItem
                : (WallpaperStyle)currentSettings.WallpaperStyleIndex;
        }

        private void ShowUnavailableEntryState()
        {
            nonImageMedia = currentEntry != null && !currentEntry.HasImage;

            if (InvokeRequired)
            {
                Invoke((MethodInvoker)ShowUnavailableEntryState);
                return;
            }

            PreviewPictureBox.ImageLocation = null;
            PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewPictureBox.Image = ApodResources.image_not_found;
            not_found = true;
            downloadButton.Enabled = false;
            loading_picture = false;
        }

        private async Task ApplyPendingImagesDirectoryAsync()
        {
            var configuredImagesPath = imagesFolderTextBox.Text.Trim();
            var updateResult = await settingsFacade.UpdateSessionImagesDirectoryAsync(configuredImagesPath).ConfigureAwait(true);
            if (!updateResult.Succeeded)
                return;

            _ = settingsFacade.RefreshLocalImageIndexAsync();
        }

        private void settingsControl_Changed(object sender, EventArgs e)
        {
            _ = PersistSettingsAsync();
        }

        private async Task PersistSettingsAsync()
        {
            if (suppressSettingsSync)
                return;

            try
            {
                currentSettings = new ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = downloadSetCheckBox.Checked,
                    WallpaperStyleIndex = (int)GetSelectedWallpaperStyle(),
                    AutoRefreshEnabled = everyTimeCheckBox.Checked,
                    StartWithWindows = startWithWindowsCheckBox.Checked,
                    ImagesDirectoryPath = imagesFolderTextBox.Text.Trim(),
                    NasaApiKey = apiKeyTextBox.Text.Trim(),
                    NasaApiKeyValidationState = currentSettings != null ? currentSettings.NasaApiKeyValidationState : ApiKeyValidationState.Unknown.ToString(),
                    LastAutoRefreshRunDate = currentSettings != null ? currentSettings.LastAutoRefreshRunDate : string.Empty,
                    LastAutoRefreshAppliedDate = currentSettings != null ? currentSettings.LastAutoRefreshAppliedDate : string.Empty,
                };

                var saveResult = await settingsFacade.SaveSettingsAsync(currentSettings).ConfigureAwait(true);
                if (!saveResult.Succeeded)
                    throw new InvalidOperationException(GetOperationErrorMessage(saveResult, "Unable to persist application settings."));

                currentSettings = saveResult.Value;
                UpdateApiKeyValidationIndicator(await GetValueOrThrowAsync(settingsFacade.GetApiKeyValidationStateAsync(), "Unable to read API key validation state.").ConfigureAwait(true));
            }
            catch
            {
            }
        }

        private void MaybeDisableAutoRefreshForManualSelection(DateTime selectedDate)
        {
            if (download_only)
                return;

            if (selectedDate.Date == DateTime.Today)
                return;

            if (!everyTimeCheckBox.Checked)
                return;

            everyTimeCheckBox.Checked = false;
        }

        private void UpdatePreviewImage(string imageLocation)
        {
            if (InvokeRequired)
            {
                PreviewPictureBox.Invoke((MethodInvoker)delegate { UpdatePreviewImage(imageLocation); });
                return;
            }

            PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            PreviewPictureBox.Image = null;
            PreviewPictureBox.ImageLocation = imageLocation;
            not_found = false;
            downloadButton.Enabled = true;
            loading_picture = false;
        }

        private async Task WarmUpCalendarMonthAsync(DateTime month, bool refreshMissingDates)
        {
            try
            {
                var monthStateResult = await calendarFacade.GetCalendarMonthStateAsync(month, refreshMissingDates, CalendarWarmupMode).ConfigureAwait(true);
                if (!monthStateResult.Succeeded)
                    throw new InvalidOperationException(GetOperationErrorMessage(monthStateResult, "Unable to warm up APOD calendar state."));
            }
            catch
            {
            }
        }

        private void ConfigureToolTips()
        {
            toolTip.SetToolTip(downloadSetCheckBox, "When enabled, double-clicking the tray icon applies the latest available APOD. When disabled, double-click opens this window. Shift+DoubleClick on the tray icon always applies the latest APOD.");
            toolTip.SetToolTip(startWithWindowsCheckBox, "Start APOD Wallpaper automatically when you sign in to Windows.");
            toolTip.SetToolTip(pictureDayDateTimePicker, "Choose the APOD date to preview, download or apply as wallpaper.");
            toolTip.SetToolTip(downloadButton, "Download the selected APOD image and apply it as wallpaper. Hold Shift to only download the image.");
            toolTip.SetToolTip(everyTimeCheckBox, "Check automatically for the latest APOD image. DEMO_KEY checks about once per hour; a personal NASA API key checks about every 30 minutes until an image is available.");
            toolTip.SetToolTip(wallpaperStyleComboBox, "Choose how the wallpaper should be placed on your screen. Smart mode adapts to the current monitor ratio and changing the mode reapplies the selected wallpaper automatically.");
            toolTip.SetToolTip(apiKeyTextBox, "Paste your personal NASA APOD API key here. Changes are saved automatically.");
            toolTip.SetToolTip(getApiKeyButton, "Open NASA API key page and copy the link. VPN may be needed in some regions.");
            toolTip.SetToolTip(imagesFolderTextBox, "Folder where downloaded APOD images are stored. Changes are saved automatically.");
            toolTip.SetToolTip(browseImagesFolderButton, "Choose another folder for downloaded APOD images.");
            toolTip.SetToolTip(openImagesFolderButton, "Open the folder where APOD images are currently stored.");
        }

        private void controller_WallpaperApplied(object sender, WallpaperAppliedEventArgs e)
        {
            if (e == null || e.Result == null || !e.Automatic || !e.Result.ResolvedDate.HasValue)
                return;

            var resolvedDate = e.Result.ResolvedDate.Value.Date;
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { SyncAutoAppliedDate(resolvedDate); });
                return;
            }

            SyncAutoAppliedDate(resolvedDate);
        }

        private void SyncAutoAppliedDate(DateTime resolvedDate)
        {
            if (pictureDayDateTimePicker.Value.Date == resolvedDate)
                return;

            pictureDayDateTimePicker.Value = resolvedDate;
        }

        internal void SyncDisplayedDate(DateTime preferredDate)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { SyncDisplayedDate(preferredDate); });
                return;
            }

            pictureDayDateTimePicker.MaxDate = DateTime.Today;
            var targetDate = preferredDate.Date;
            if (targetDate > pictureDayDateTimePicker.MaxDate.Date)
                targetDate = pictureDayDateTimePicker.MaxDate.Date;
            if (targetDate < pictureDayDateTimePicker.MinDate.Date)
                targetDate = pictureDayDateTimePicker.MinDate.Date;

            if (pictureDayDateTimePicker.Value.Date != targetDate)
                pictureDayDateTimePicker.Value = targetDate;
        }

        private static T GetValueOrThrow<T>(OperationResult<T> result, string fallbackMessage)
        {
            if (result == null)
                throw new InvalidOperationException(fallbackMessage);

            if (!result.Succeeded)
                throw new InvalidOperationException(GetOperationErrorMessage(result, fallbackMessage));

            return result.Value;
        }

        private static string GetOperationErrorMessage(OperationResult result, string fallbackMessage)
        {
            return result != null && result.Error != null && !string.IsNullOrWhiteSpace(result.Error.Message)
                ? result.Error.Message
                : fallbackMessage;
        }

        private void UpdateApiKeyValidationIndicator(ApiKeyValidationState validationState)
        {
            switch (validationState)
            {
                case ApiKeyValidationState.Invalid:
                    apiKeyStatusLabel.Text = "The current NASA API key looks invalid. DEMO_KEY fallback is active.";
                    break;
                default:
                    apiKeyStatusLabel.Text = string.Empty;
                    break;
            }
        }

        private async Task ReapplySelectedWallpaperStyleAsync()
        {
            if (applyingWallpaperStyleChange || suppressSettingsSync || loading_picture || not_found || currentEntry == null || !currentEntry.HasImage)
                return;

            applyingWallpaperStyleChange = true;
            try
            {
                var selectedDate = pictureDayDateTimePicker.Value.Date;
                var operationResult = await workflowFacade.ApplyDayAsync(selectedDate, GetSelectedWallpaperStyle()).ConfigureAwait(true);
                UpdateApiKeyValidationIndicator(await GetValueOrThrowAsync(settingsFacade.GetApiKeyValidationStateAsync(), "Unable to read API key validation state.").ConfigureAwait(true));
                if (!operationResult.Succeeded)
                    return;

                var workflowResult = operationResult.Value;
                if (!workflowResult.IsSuccess)
                    return;

                currentEntry = workflowResult.Entry;
                UpdatePreviewImage(workflowResult.ImagePath);
                MaybeDisableAutoRefreshForManualSelection(selectedDate);
            }
            catch
            {
            }
            finally
            {
                applyingWallpaperStyleChange = false;
            }
        }

        private static string GetUserFacingExceptionMessage(Exception exception, string fallbackMessage = "Something went wrong while processing the APOD request.")
        {
            if (exception == null)
                return fallbackMessage;

            return string.IsNullOrWhiteSpace(exception.Message)
                ? fallbackMessage
                : exception.Message;
        }

        private static async Task<T> GetValueOrThrowAsync<T>(Task<OperationResult<T>> resultTask, string fallbackMessage)
        {
            return GetValueOrThrow(await resultTask.ConfigureAwait(true), fallbackMessage);
        }
    }
}
