using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace apod_wallpaper
{
    internal partial class ConfigurationForm : Form
    {
        private const string NasaApiKeyUrl = "https://api.nasa.gov/";
        private readonly ApplicationController controller;
        private bool suppressSettingsSync = true;
        private bool not_found = false;
        private bool nonImageMedia = false;
        private bool loading_picture = false;
        private bool download_only = false;
        private int previewRequestVersion;
        private ApodEntry currentEntry;
        private ApplicationSettingsSnapshot currentSettings;
        private ToolTip toolTip = new ToolTip();

        internal ConfigurationForm(ApplicationController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            InitializeComponent();
            pictureDayDateTimePicker.Value = DateTime.UtcNow;
            wallpaperStyleComboBox.DataSource = Enum.GetValues(typeof(WallpaperStyle));
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
            suppressSettingsSync = true;
            currentSettings = controller.GetSettings();
            downloadSetCheckBox.Checked = currentSettings.TrayDoubleClickAction;
            wallpaperStyleComboBox.SelectedIndex = currentSettings.WallpaperStyleIndex;
            everyTimeCheckBox.Checked = currentSettings.AutoRefreshEnabled;
            startWithWindowsCheckBox.Checked = currentSettings.StartWithWindows;
            apiKeyTextBox.Text = currentSettings.NasaApiKey;
            imagesFolderTextBox.Text = FileStorage.ImagesDirectory;
            controller.UpdateSessionImagesDirectory(imagesFolderTextBox.Text);
            suppressSettingsSync = false;

            await controller.RefreshLocalImageIndexAsync().ConfigureAwait(true);
            await WarmUpCalendarMonthAsync(pictureDayDateTimePicker.Value.Date, true);
        }

        private void SaveSettings(object sender, FormClosingEventArgs e)
        {
            PersistSettings();
        }

        public void DownloadWallpaper()
        {
            var selectedDate = pictureDayDateTimePicker.Value.Date;
            download_only = Control.ModifierKeys == Keys.Shift;

            try
            {
                ApodWorkflowResult workflowResult;
                if (download_only)
                {
                    workflowResult = controller.DownloadDay(selectedDate);
                }
                else
                {
                    workflowResult = controller.ApplyDay(selectedDate, GetSelectedWallpaperStyle());
                }

                currentEntry = workflowResult.Entry;
                if (!workflowResult.IsSuccess)
                {
                    ShowUnavailableEntryState();
                    if (workflowResult.Status == ApodWorkflowStatus.Failed)
                    {
                        MessageBox.Show(workflowResult.Message,
                                        "APOD error",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                    }

                    return;
                }

                nonImageMedia = false;
                UpdatePreviewImage(workflowResult.ImagePath);
            }
            catch (Exception ex)
            {
                ShowUnavailableEntryState();
                MessageBox.Show(ApodErrorTranslator.ToUserMessage(ex),
                                "APOD error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        public async Task DownloadWallpaperAsync()
        {
            var selectedDate = pictureDayDateTimePicker.Value.Date;
            download_only = Control.ModifierKeys == Keys.Shift;

            ApodWorkflowResult workflowResult;
            if (download_only)
            {
                workflowResult = await controller.DownloadDayAsync(selectedDate).ConfigureAwait(true);
            }
            else
            {
                workflowResult = await controller.ApplyDayAsync(selectedDate, GetSelectedWallpaperStyle()).ConfigureAwait(true);
            }

            currentEntry = workflowResult.Entry;
            if (!workflowResult.IsSuccess)
            {
                ShowUnavailableEntryState();
                if (workflowResult.Status == ApodWorkflowStatus.Failed)
                {
                    MessageBox.Show(workflowResult.Message,
                                    "APOD error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }

                return;
            }

            nonImageMedia = false;
            UpdatePreviewImage(workflowResult.ImagePath);
        }

        private async void PictureDayDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            not_found = false;
            nonImageMedia = false;
            pictureDayDateTimePicker.Enabled = false;
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

                var workflowResult = await controller.LoadDayAsync(selectedDate).ConfigureAwait(true);
                if (requestVersion != previewRequestVersion)
                    return;

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
            PersistSettings();
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
                MessageBox.Show(ApodErrorTranslator.ToUserMessage(ex),
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
                    ApplyPendingImagesDirectory();
                }
            }
        }

        private void openImagesFolderButton_Click(object sender, EventArgs e)
        {
            var path = string.IsNullOrWhiteSpace(imagesFolderTextBox.Text) ? FileStorage.ImagesDirectory : imagesFolderTextBox.Text.Trim();
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void imagesFolderTextBox_TextChanged(object sender, EventArgs e)
        {
            ApplyPendingImagesDirectory();
            PersistSettings();
        }

        private void PreviewPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (!not_found && !loading_picture)
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    Process.Start(new ProcessStartInfo(controller.GetPostUrl(pictureDayDateTimePicker.Value.Date))
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
                Process.Start(new ProcessStartInfo(controller.GetPostUrl(pictureDayDateTimePicker.Value.Date))
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
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to open NASA API key page.", ex);
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
            pictureDayDateTimePicker.Enabled = true;
            downloadButton.Enabled = false;
            loading_picture = false;
        }

        private void ApplyPendingImagesDirectory()
        {
            var configuredImagesPath = imagesFolderTextBox.Text.Trim();
            controller.UpdateSessionImagesDirectory(configuredImagesPath);
            _ = controller.RefreshLocalImageIndexAsync();
        }

        private void settingsControl_Changed(object sender, EventArgs e)
        {
            PersistSettings();
        }

        private void PersistSettings()
        {
            if (suppressSettingsSync)
                return;

            try
            {
                currentSettings = new ApplicationSettingsSnapshot
                {
                    TrayDoubleClickAction = downloadSetCheckBox.Checked,
                    WallpaperStyleIndex = wallpaperStyleComboBox.SelectedIndex,
                    AutoRefreshEnabled = everyTimeCheckBox.Checked,
                    StartWithWindows = startWithWindowsCheckBox.Checked,
                    ImagesDirectoryPath = imagesFolderTextBox.Text.Trim(),
                    NasaApiKey = apiKeyTextBox.Text.Trim(),
                    LastAutoRefreshRunDate = currentSettings != null ? currentSettings.LastAutoRefreshRunDate : string.Empty,
                };

                controller.SaveSettings(currentSettings);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to persist settings immediately.", ex);
            }
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
            pictureDayDateTimePicker.Enabled = true;
            downloadButton.Enabled = true;
            loading_picture = false;
        }

        private async Task WarmUpCalendarMonthAsync(DateTime month, bool refreshMissingDates)
        {
            try
            {
                await controller.GetCalendarMonthStateAsync(month, refreshMissingDates).ConfigureAwait(true);
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
            toolTip.SetToolTip(everyTimeCheckBox, "Check automatically for today's APOD. DEMO_KEY checks about once per hour; a personal NASA API key checks about every 5 minutes until today's image becomes available.");
            toolTip.SetToolTip(wallpaperStyleComboBox, "Choose how the wallpaper should be placed on your screen. Smart mode adapts vertical images automatically.");
            toolTip.SetToolTip(apiKeyTextBox, "Paste your personal NASA APOD API key here. Changes are saved automatically.");
            toolTip.SetToolTip(getApiKeyButton, "Open NASA API key page and copy the link. VPN may be needed in some regions.");
            toolTip.SetToolTip(imagesFolderTextBox, "Folder where downloaded APOD images are stored. Changes are saved automatically.");
            toolTip.SetToolTip(browseImagesFolderButton, "Choose another folder for downloaded APOD images.");
            toolTip.SetToolTip(openImagesFolderButton, "Open the folder where APOD images are currently stored.");
        }
    }
}
