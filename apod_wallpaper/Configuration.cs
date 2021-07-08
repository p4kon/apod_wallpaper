using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace apod_wallpaper
{
    public partial class configurationForm : Form
    {
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

                LinkTextBox.Text = Parser.img_url;
                var image = new Image(Parser.img_url, TodayUrl.GetName());
                image.DownloadImage();
                image.SaveImage(image.name, image.format);
                
                PreviewPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                PreviewPictureBox.Image = image.GetImage();
            }
            else
            {
                LinkTextBox.Text = "Image not found";
            }
        }

        private void PictureDayDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            Parser.ImgUrl = null;
            Parser.IsExistUrl = false;

            TodayUrl.SetDate(pictureDayDateTimePicker.Value);
            try
            {
                var parser = new Parser(TodayUrl.GetUrl());
                parser.GetUrl();
                PreviewPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
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

        private void SetRefreshDateTimePicker_ValueChanged(Object sender, EventArgs e)
        {
            SetTimeRefresh();
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            DownloadWallpaper();
        }

        private void setButton_Click(object sender, EventArgs e)
        {
            WallpaperStyle style = (WallpaperStyle)wallpaperStyleComboBox.SelectedItem;
            var image = new Image(Parser.img_url, TodayUrl.GetName());

            Wallpaper.SilentSet(AppDomain.CurrentDomain.BaseDirectory + image.image_path + image.name, style);
        }
    }
}
