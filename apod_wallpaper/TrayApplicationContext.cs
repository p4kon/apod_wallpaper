using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;

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
            TodayUrl.SetDate(DateTime.UtcNow);
            configurationForm form = new configurationForm();
            Thread thread = new Thread(form.DownloadWallpaper);
            thread.Start();
        }


        void ShowConfig(object sender, EventArgs e)
        {
            // If we are already showing the window meerly focus it.
            if (configWindow.Visible)
                configWindow.Focus();
            else
                configWindow.ShowDialog();
        }

        void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }
        public static void TrayIconDispose()
        {
            trayIcon.Dispose();
        }
    }
}