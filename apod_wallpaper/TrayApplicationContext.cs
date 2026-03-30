using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;

namespace apod_wallpaper
{
    public class TrayApplicationContext : ApplicationContext
    {
        static NotifyIcon trayIcon = new NotifyIcon();
        configurationForm configWindow = new configurationForm();

        public TrayApplicationContext()
        {
            MenuItem configMenuItem = new MenuItem("Configuration", new EventHandler(ShowConfig));
            MenuItem exitMenuItem = new MenuItem("Exit", new EventHandler(Exit));
            Icon apod_icon = new Icon(resources_apod.apod_icon, 32, 32);

            trayIcon.Icon = apod_icon;
            trayIcon.DoubleClick += new EventHandler(TrayDoubleClickAction);
            trayIcon.ContextMenu = new ContextMenu(new MenuItem[] { configMenuItem, exitMenuItem });
            trayIcon.Visible = true;
            trayIcon.Text = "Astronomy Picture of the Day";
        }

        void TrayDoubleClickAction(object sender, EventArgs e)
        {
            if (!apod_wallpaper.Properties.Settings.Default.TrayDoubleClickAction)
            {
                ShowConfig(sender, e);
                return;
            }

            Thread thread = new Thread(() =>
            {
                try
                {
                    var service = new ApodWallpaperService();
                    var style = (WallpaperStyle)apod_wallpaper.Properties.Settings.Default.StyleComboBox;
                    service.ApplyLatestAvailable(style);
                }
                catch (Exception ex)
                {
                    ShowTrayError(ex);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        void ShowConfig(object sender, EventArgs e)
        {
            if (configWindow.Visible)
                configWindow.Focus();
            else
                configWindow.ShowDialog();
        }

        void Exit(object sender, EventArgs e)
        {
            Scheduler.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }

        public static void TrayIconDispose()
        {
            trayIcon.Dispose();
        }

        private static void ShowTrayError(Exception ex)
        {
            var message = ex is HttpRequestException
                ? "Unable to reach NASA APOD right now."
                : ApodErrorTranslator.ToUserMessage(ex);

            trayIcon.BalloonTipTitle = "APOD Wallpaper";
            trayIcon.BalloonTipText = message;
            trayIcon.ShowBalloonTip(4000);
        }
    }
}
