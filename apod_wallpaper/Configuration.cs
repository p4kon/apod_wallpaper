﻿using System;
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
            downloadSetCheckBox.Checked = apod_wallpaper.Properties.Settings.Default.ShowMessage;
            wallpaperStyleComboBox.SelectedIndex = apod_wallpaper.Properties.Settings.Default.StyleComboBox;
            setRefreshDateTimePicker.Value = apod_wallpaper.Properties.Settings.Default.TimeRefresh;
        }

        private void SaveSettings(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK)
            {
                apod_wallpaper.Properties.Settings.Default.ShowMessage = downloadSetCheckBox.Checked;
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

        private void DownloadWallpaper()
        {
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

                PreviewPictureBox.Invoke((MethodInvoker)delegate 
                {
                    PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    PreviewPictureBox.ImageLocation = AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name;
                });

                wallpaperStyleComboBox.Invoke((MethodInvoker)delegate
                {
                    WallpaperStyle style = (WallpaperStyle)wallpaperStyleComboBox.SelectedItem;
                    Wallpaper.SilentSet(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name, style);
                });
            }
            else
            {
                PreviewPictureBox.Image = resources_apod.image_not_found;
                not_found = true;
                pictureDayDateTimePicker.Enabled = true;
                downloadButton.Invoke((MethodInvoker)delegate
                {
                    downloadButton.Enabled = false;
                });
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
            Thread thread = new Thread(DownloadWallpaper);
            thread.Start();
        }

        private void PreviewPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (!not_found) 
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
                    fullScreenFrom.ShowDialog();
                }
            }
        }

        private void PreviewPictureBox_MouseHover(object sender, EventArgs e)
        {
            if (!not_found)
            {
                toolTip.Active = true;
                PreviewPictureBox.Cursor = Cursors.Hand;
                toolTip.SetToolTip(this.PreviewPictureBox,
                                   "Click: open fullsize preview" + Environment.NewLine + "Shift+Click: open nasa post <Astronomy Picture of the Day>");
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
        }
    }
}
