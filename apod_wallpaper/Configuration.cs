using System;
using System.IO;
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
        private bool _progress = false;
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
            showMessageCheckBox.Checked = apod_wallpaper.Properties.Settings.Default.ShowMessage;
            wallpaperStyleComboBox.SelectedIndex = apod_wallpaper.Properties.Settings.Default.StyleComboBox;
            setRefreshDateTimePicker.Value = apod_wallpaper.Properties.Settings.Default.TimeRefresh;
        }

        private void SaveSettings(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK)
            {
                apod_wallpaper.Properties.Settings.Default.ShowMessage = showMessageCheckBox.Checked;
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
            if (Parser.isExistUrl)
            {
                TodayUrl.SetDate(pictureDayDateTimePicker.Value);

                var image = new Image(Parser.img_url, TodayUrl.GetName());
                image.DownloadImage();
                image.SaveImage(image.name, image.format);
                LinkTextBox.Invoke((MethodInvoker)delegate 
                {
                    LinkTextBox.Text = Parser.img_url;
                });

                PreviewPictureBox.Invoke((MethodInvoker)delegate 
                {
                    PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    PreviewPictureBox.Image = image.GetImage();
                });
            }
            else
            {
                LinkTextBox.Text = "Image not found";
            }
        }

        private void PictureDayDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            PictureShowPreview();
        }

        private void PictureShowPreview()
        {
            Parser.ImgUrl = null;
            Parser.IsExistUrl = false;

            TodayUrl.SetDate(pictureDayDateTimePicker.Value);

            var image = new Image(Parser.img_url, TodayUrl.GetName());

            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name))
            {

                PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                PreviewPictureBox.ImageLocation = AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name;

            }
            else
            {
                //_progress = true;
                Thread thread = new Thread(setImageProgress);
                thread.Start();
                try
                {
                    var parser = new Parser(TodayUrl.GetUrl());
                    parser.GetUrl();
                    PreviewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    // Console.WriteLine(_progress);
                    //_progress = false;
                    //Console.WriteLine(_progress);
                    PreviewPictureBox.ImageLocation = Parser.img_url;
                }
                catch (System.ArgumentNullException ane)
                {
                    Console.WriteLine(ane.Message);
                    if (pictureDayDateTimePicker.Value > DateTime.UtcNow)
                    {
                        LinkTextBox.Text = "You cannot choose a date from the future";
                        pictureDayDateTimePicker.Value = DateTime.UtcNow;
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

        private void setButton_Click(object sender, EventArgs e)
        {
            WallpaperStyle style = (WallpaperStyle)wallpaperStyleComboBox.SelectedItem;
            var image = new Image(Parser.img_url, TodayUrl.GetName());

            Wallpaper.SilentSet(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name, style);
        }

        private void setImageProgress()
        {
            
            PreviewPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
            PreviewPictureBox.ImageLocation = "C:/Users/p4kon/Desktop/p4kon/GitHub/apod_wallpaper/apod_wallpaper/Resources/download_image_progress.gif";
            
        }

        private void PreviewPictureBox_LoadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            _progress = false;
            Console.WriteLine("load completed");
        }

        private void PreviewPictureBox_LoadProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _progress = true;
            Console.WriteLine("load progress");
        }
    }
}
