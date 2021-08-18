using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace apod_wallpaper
{
    public partial class configurationForm : Form
    {
        private bool not_found = false;
        private bool loading_picture = false;
        private bool download_only = false;
        private string full_path_image;
        private ToolTip toolTip = new ToolTip();

        public configurationForm()
        {
            InitializeComponent();
            SetTimeRefresh();
            Scheduler.Check();
            pictureDayDateTimePicker.Value = DateTime.UtcNow;
            Network.AllowInvalidCertificate();
            wallpaperStyleComboBox.DataSource = Enum.GetValues(typeof(WallpaperStyle));
        }

        private void LoadSettings(object sender, EventArgs e)
        {
            downloadSetCheckBox.Checked = apod_wallpaper.Properties.Settings.Default.TrayDoubleClickAction;
            wallpaperStyleComboBox.SelectedIndex = apod_wallpaper.Properties.Settings.Default.StyleComboBox;
            setRefreshDateTimePicker.Value = apod_wallpaper.Properties.Settings.Default.TimeRefresh;
        }

        private void SaveSettings(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK)
            {
                apod_wallpaper.Properties.Settings.Default.TrayDoubleClickAction = downloadSetCheckBox.Checked;
                apod_wallpaper.Properties.Settings.Default.StyleComboBox = wallpaperStyleComboBox.SelectedIndex;
                apod_wallpaper.Properties.Settings.Default.TimeRefresh = setRefreshDateTimePicker.Value;
                apod_wallpaper.Properties.Settings.Default.Save();
            }
        }

        private void SetTimeRefresh()
        {
            Scheduler.EveryHour = setRefreshDateTimePicker.Value.Hour;
            Scheduler.EveryMinute = setRefreshDateTimePicker.Value.Minute;
            Scheduler.EverySecond = setRefreshDateTimePicker.Value.Second;
        }

        public void DownloadWallpaper()
        {
            if (Control.ModifierKeys == Keys.Shift)
            {
                download_only = true;
            }
            else
            {
                download_only = false;
            }

            var image = new Image(Parser.img_url, TodayUrl.GetName());
            if (Parser.isExistUrl || File.Exists(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name))
            {
                TodayUrl.SetDate(pictureDayDateTimePicker.Value);

                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name))
                {
                    Application.UseWaitCursor = true;
                    image.DownloadImage();
                    image.SaveImage(image.name, image.format);
                }
                Application.UseWaitCursor = false;

                if (this.InvokeRequired)
                {
                    PreviewPictureBox.Invoke((MethodInvoker)delegate
                    {
                        PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                        PreviewPictureBox.ImageLocation = AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name;
                    });

                    wallpaperStyleComboBox.Invoke((MethodInvoker)delegate
                    {
                        WallpaperStyle style = (WallpaperStyle)wallpaperStyleComboBox.SelectedItem;
                        if (!download_only) 
                        {
                            Wallpaper.SilentSet(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name, style);
                        }
                    });
                }
                else
                {
                    WallpaperStyle style = (WallpaperStyle)apod_wallpaper.Properties.Settings.Default.StyleComboBox;
                    if (!download_only)
                    {
                        Wallpaper.SilentSet(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name, style);
                    }
                }
            }
            else
            {
                PreviewPictureBox.Image = resources_apod.image_not_found;
                not_found = true;
                pictureDayDateTimePicker.Enabled = true;
                if (this.InvokeRequired)
                {
                    downloadButton.Invoke((MethodInvoker)delegate
                    {
                        downloadButton.Enabled = false;
                    });
                }
            }
        }

        private void PictureDayDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            not_found = false;
            pictureDayDateTimePicker.Enabled = false;
            downloadButton.Enabled = false;
            PictureShowPreview();
        }

        private void PictureShowPreview()
        {
            Parser.ImgUrl = null;
            Parser.IsExistUrl = false;

            TodayUrl.SetDate(pictureDayDateTimePicker.Value);

            var image = new Image(Parser.img_url, TodayUrl.GetName());
            full_path_image = AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name;

            if (File.Exists(full_path_image))
            {
                PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                PreviewPictureBox.ImageLocation = full_path_image;
            }
            else
            {
                try
                {
                    PreviewPictureBox.Image = resources_apod.loading_image_progress;
                    loading_picture = true;
                    var parser = new Parser(TodayUrl.GetUrl());
                    parser.GetUrl();

                    if (!Parser.isExistUrl)
                    {
                        PreviewPictureBox.Image = resources_apod.image_not_found;
                        not_found = true;
                        pictureDayDateTimePicker.Enabled = true;
                        downloadButton.Enabled = false;
                    }

                    PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    PreviewPictureBox.ImageLocation = Parser.img_url;
                }
                catch (System.ArgumentNullException ane)
                {
                    Console.WriteLine(ane.Message);

                    if (pictureDayDateTimePicker.Value > DateTime.UtcNow)
                    {
                        pictureDayDateTimePicker.Value = DateTime.UtcNow;
                        MessageBox.Show("You cannot choose a date from the future",
                                        "Date selection error",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void SetRefreshDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            SetTimeRefresh();
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            MyExtensions.CheckFolder();
            Thread thread = new Thread(DownloadWallpaper);
            thread.Start();
            PreviewPictureBox.Focus();
        }

        private void PreviewPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (!not_found && !loading_picture) 
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo(TodayUrl.GetUrl());
                    Process.Start(processStartInfo);
                }
                else
                {
                    Form fullScreenFrom = new Form();
                    PictureBox fullScreenPictureBox = new PictureBox();
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
                ProcessStartInfo processStartInfo = new ProcessStartInfo(TodayUrl.GetUrl());
                Process.Start(processStartInfo);
            }
        }

        private void FullScreenFrom_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Form.ActiveForm.Close();
            }
        }

        private void PreviewPictureBox_MouseHover(object sender, EventArgs e)
        {
            if (!not_found && !loading_picture)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Hand;
                toolTip.SetToolTip(this.PreviewPictureBox,
                                   "Click: open fullsize preview" + Environment.NewLine + "Shift+Click: open nasa post <Astronomy Picture of the Day>");
            }
            else if (!not_found && loading_picture)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Default;
                toolTip.SetToolTip(this.PreviewPictureBox,
                                   "Loading may take some time");
            }
            else if (not_found)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Hand;
                toolTip.SetToolTip(this.PreviewPictureBox,
                                   "Shift+Click: open nasa post <Astronomy Picture of the Day>");
            }
            else
            {
                toolTip.Active = false;
                PreviewPictureBox.Cursor = Cursors.Default;
            }
            
        }

        private void PreviewPictureBox_LoadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            pictureDayDateTimePicker.Enabled = true;
            downloadButton.Enabled = true;
            loading_picture = false;
        }

        private void wallpaperStyleComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            configurationForm.ActiveForm.Focus();
        }

        private void downloadButton_MouseHover(object sender, EventArgs e)
        {
            toolTip.Active = true;
            toolTip.SetToolTip(this.downloadButton,
                               "Shift+Click for only download picture");
        }
    }
}
