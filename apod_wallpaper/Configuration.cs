using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace apod_wallpaper
{
    public partial class configurationForm : Form
    {
        private readonly ApodWallpaperService apodWallpaperService = new ApodWallpaperService();
        private readonly StartupService startupService = new StartupService();
        private bool suppressSettingsSync = true;
        private bool not_found = false;
        private bool nonImageMedia = false;
        private bool loading_picture = false;
        private bool download_only = false;
        private ApodEntry currentEntry;
        private ToolTip toolTip = new ToolTip();

        public configurationForm()
        {
            InitializeComponent();
            pictureDayDateTimePicker.Value = DateTime.UtcNow;
            wallpaperStyleComboBox.DataSource = Enum.GetValues(typeof(WallpaperStyle));
            imagesFolderTextBox.TextChanged += imagesFolderTextBox_TextChanged;
            downloadSetCheckBox.CheckedChanged += settingsControl_Changed;
            startWithWindowsCheckBox.CheckedChanged += settingsControl_Changed;
            everyTimeCheckBox.CheckedChanged += settingsControl_Changed;
            wallpaperStyleComboBox.SelectedIndexChanged += settingsControl_Changed;
            setRefreshDateTimePicker.ValueChanged += settingsControl_Changed;
            apiKeyTextBox.TextChanged += settingsControl_Changed;
        }

        private void LoadSettings(object sender, EventArgs e)
        {
            suppressSettingsSync = true;
            downloadSetCheckBox.Checked = apod_wallpaper.Properties.Settings.Default.TrayDoubleClickAction;
            wallpaperStyleComboBox.SelectedIndex = apod_wallpaper.Properties.Settings.Default.StyleComboBox;
            setRefreshDateTimePicker.Value = apod_wallpaper.Properties.Settings.Default.TimeRefresh;
            everyTimeCheckBox.Checked = apod_wallpaper.Properties.Settings.Default.AutoRefreshEnabled;
            startWithWindowsCheckBox.Checked = apod_wallpaper.Properties.Settings.Default.StartWithWindows;
            apiKeyTextBox.Text = apod_wallpaper.Properties.Settings.Default.NasaApiKey;
            imagesFolderTextBox.Text = FileStorage.ImagesDirectory;
            FileStorage.SetSessionImagesDirectory(imagesFolderTextBox.Text);
            RuntimeSettingsSync.ApplyCurrentSettings();
            UpdateSchedulerFromSettings();
            suppressSettingsSync = false;
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
                currentEntry = apodWallpaperService.GetEntry(selectedDate);
                if (!currentEntry.HasImage)
                {
                    ShowUnavailableEntryState();
                    return;
                }

                ApodDownloadResult downloadResult;
                if (download_only)
                {
                    downloadResult = apodWallpaperService.Download(selectedDate);
                }
                else
                {
                    var applyResult = apodWallpaperService.Apply(selectedDate, GetSelectedWallpaperStyle());
                    downloadResult = new ApodDownloadResult
                    {
                        Entry = applyResult.Entry,
                        ImagePath = applyResult.ImagePath,
                        DownloadedNow = applyResult.DownloadedNow,
                    };
                }

                nonImageMedia = false;
                UpdatePreviewImage(downloadResult.ImagePath);
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

        private void PictureDayDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            not_found = false;
            nonImageMedia = false;
            pictureDayDateTimePicker.Enabled = false;
            downloadButton.Enabled = false;
            PictureShowPreview();
        }

        private void PictureShowPreview()
        {
            var selectedDate = pictureDayDateTimePicker.Value.Date;
            currentEntry = null;

            try
            {
                var latestAvailableDate = apodWallpaperService.GetLatestAvailableDate();
                if (selectedDate > latestAvailableDate)
                {
                    pictureDayDateTimePicker.Value = latestAvailableDate;
                    MessageBox.Show("NASA has not published APOD for this date yet.",
                                    "Date selection error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    return;
                }

                PreviewPictureBox.ImageLocation = null;
                PreviewPictureBox.Image = resources_apod.loading_image_progress;
                loading_picture = true;

                var preview = apodWallpaperService.Preview(selectedDate);
                currentEntry = preview.Entry;

                if (!preview.Entry.HasImage || string.IsNullOrWhiteSpace(preview.PreviewLocation))
                {
                    ShowUnavailableEntryState();
                    return;
                }

                nonImageMedia = false;
                UpdatePreviewImage(preview.PreviewLocation);
            }
            catch
            {
                ShowUnavailableEntryState();
            }
        }

        private void SetRefreshDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            UpdateSchedulerFromSettings();
        }

        private void everyTimeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSchedulerFromSettings();
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            var thread = new System.Threading.Thread(DownloadWallpaper);
            thread.IsBackground = true;
            thread.Start();
            PreviewPictureBox.Focus();
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
                    Process.Start(new ProcessStartInfo(apodWallpaperService.OpenPost(pictureDayDateTimePicker.Value.Date))
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
                    fullScreenFrom.Icon = resources_apod.apod_icon;
                    fullScreenFrom.WindowState = FormWindowState.Maximized;
                    fullScreenFrom.KeyPreview = true;
                    fullScreenFrom.KeyDown += new KeyEventHandler(FullScreenFrom_KeyDown);
                    fullScreenFrom.ShowDialog();
                }
            }
            else if (not_found && Control.ModifierKeys == Keys.Shift)
            {
                Process.Start(new ProcessStartInfo(apodWallpaperService.OpenPost(pictureDayDateTimePicker.Value.Date))
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
                : (WallpaperStyle)apod_wallpaper.Properties.Settings.Default.StyleComboBox;
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

        private void ShowUnavailableEntryState()
        {
            nonImageMedia = currentEntry != null && !currentEntry.HasImage;

            if (InvokeRequired)
            {
                Invoke((MethodInvoker)ShowUnavailableEntryState);
                return;
            }

            PreviewPictureBox.ImageLocation = null;
            PreviewPictureBox.Image = resources_apod.image_not_found;
            not_found = true;
            pictureDayDateTimePicker.Enabled = true;
            downloadButton.Enabled = false;
            loading_picture = false;
        }

        private void UpdateSchedulerFromSettings()
        {
            Scheduler.EveryHour = setRefreshDateTimePicker.Value.Hour;
            Scheduler.EveryMinute = setRefreshDateTimePicker.Value.Minute;
            Scheduler.EverySecond = setRefreshDateTimePicker.Value.Second;
            Scheduler.UpdateSchedule();

            if (!everyTimeCheckBox.Checked)
            {
                Scheduler.Stop();
                return;
            }

            Scheduler.Start(ApplyTodayFromScheduler);
        }

        private void ApplyTodayFromScheduler()
        {
            try
            {
                apodWallpaperService.ApplyLatestAvailable((WallpaperStyle)apod_wallpaper.Properties.Settings.Default.StyleComboBox);
            }
            catch
            {
            }
        }

        private void ApplyPendingImagesDirectory()
        {
            var configuredImagesPath = imagesFolderTextBox.Text.Trim();
            FileStorage.SetSessionImagesDirectory(configuredImagesPath);
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
                var configuredImagesPath = imagesFolderTextBox.Text.Trim();
                var nasaApiKey = apiKeyTextBox.Text.Trim();

                apod_wallpaper.Properties.Settings.Default.TrayDoubleClickAction = downloadSetCheckBox.Checked;
                apod_wallpaper.Properties.Settings.Default.StyleComboBox = wallpaperStyleComboBox.SelectedIndex;
                apod_wallpaper.Properties.Settings.Default.TimeRefresh = setRefreshDateTimePicker.Value;
                apod_wallpaper.Properties.Settings.Default.AutoRefreshEnabled = everyTimeCheckBox.Checked;
                apod_wallpaper.Properties.Settings.Default.StartWithWindows = startWithWindowsCheckBox.Checked;
                apod_wallpaper.Properties.Settings.Default.ImagesDirectoryPath = configuredImagesPath;
                apod_wallpaper.Properties.Settings.Default.NasaApiKey = string.IsNullOrWhiteSpace(nasaApiKey)
                    ? "DEMO_KEY"
                    : nasaApiKey;

                apod_wallpaper.Properties.Settings.Default.Save();
                RuntimeSettingsSync.ApplyCurrentSettings();
                FileStorage.SetSessionImagesDirectory(configuredImagesPath);
                startupService.SetStartWithWindows(startWithWindowsCheckBox.Checked);
                UpdateSchedulerFromSettings();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to persist settings immediately.", ex);
            }
        }
    }
}
