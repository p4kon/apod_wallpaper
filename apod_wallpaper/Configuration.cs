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
            setRefreshDateTimePicker.ValueChanged += settingsControl_Changed;
            apiKeyTextBox.TextChanged += settingsControl_Changed;
        }

        private async void LoadSettings(object sender, EventArgs e)
        {
            suppressSettingsSync = true;
            currentSettings = controller.GetSettings();
            downloadSetCheckBox.Checked = currentSettings.TrayDoubleClickAction;
            wallpaperStyleComboBox.SelectedIndex = currentSettings.WallpaperStyleIndex;
            setRefreshDateTimePicker.Value = currentSettings.RefreshTime;
            everyTimeCheckBox.Checked = currentSettings.AutoRefreshEnabled;
            startWithWindowsCheckBox.Checked = currentSettings.StartWithWindows;
            apiKeyTextBox.Text = currentSettings.NasaApiKey;
            imagesFolderTextBox.Text = FileStorage.ImagesDirectory;
            controller.UpdateSessionImagesDirectory(imagesFolderTextBox.Text);
            await controller.RefreshLocalImageIndexAsync().ConfigureAwait(true);
            suppressSettingsSync = false;

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

                var monthState = await controller.GetCalendarMonthStateAsync(selectedDate, false).ConfigureAwait(true);
                if (requestVersion != previewRequestVersion)
                    return;

                ApodCalendarDayState dayState;
                if (monthState.TryGetDay(selectedDate, out dayState) && dayState.IsKnown && !dayState.IsSelectable)
                {
                    currentEntry = null;
                    ShowUnavailableEntryState();
                    return;
                }

                var workflowResult = await controller.LoadDayAsync(selectedDate).ConfigureAwait(true);
                if (requestVersion != previewRequestVersion)
                    return;

                currentEntry = workflowResult.Entry;

                if (workflowResult.Status == ApodWorkflowStatus.Unavailable &&
                    workflowResult.LatestPublishedDate.HasValue &&
                    selectedDate > workflowResult.LatestPublishedDate.Value)
                {
                    pictureDayDateTimePicker.Value = workflowResult.LatestPublishedDate.Value;
                    MessageBox.Show(workflowResult.Message,
                                    "Date selection error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    return;
                }

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

        private void SetRefreshDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            PersistSettings();
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
            Process.Start(new ProcessStartInfo("https://api.nasa.gov/")
            {
                UseShellExecute = true
            });
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
                    RefreshTime = setRefreshDateTimePicker.Value,
                    AutoRefreshEnabled = everyTimeCheckBox.Checked,
                    StartWithWindows = startWithWindowsCheckBox.Checked,
                    ImagesDirectoryPath = imagesFolderTextBox.Text.Trim(),
                    NasaApiKey = apiKeyTextBox.Text.Trim(),
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
    }
}
