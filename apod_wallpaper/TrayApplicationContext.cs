using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace apod_wallpaper
{
    internal class TrayApplicationContext : ApplicationContext
    {
        private static NotifyIcon trayIcon = new NotifyIcon();
        private readonly ApplicationController controller;
        private readonly ConfigurationForm configWindow;

        internal TrayApplicationContext(ApplicationController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            configWindow = new ConfigurationForm(this.controller);
            MenuItem configMenuItem = new MenuItem("Configuration", new EventHandler(ShowConfig));
            MenuItem exitMenuItem = new MenuItem("Exit", new EventHandler(Exit));
            Icon apod_icon = new Icon(ApodResources.apod_icon, 32, 32);

            trayIcon.Icon = apod_icon;
            trayIcon.DoubleClick += new EventHandler(TrayDoubleClickAction);
            trayIcon.ContextMenu = new ContextMenu(new MenuItem[] { configMenuItem, exitMenuItem });
            trayIcon.Visible = true;
            trayIcon.Text = "Astronomy Picture of the Day";
        }

        void TrayDoubleClickAction(object sender, EventArgs e)
        {
            if (!controller.ShouldApplyOnTrayDoubleClick())
            {
                ShowConfig(sender, e);
                return;
            }

            Thread thread = new Thread(() =>
            {
                var workflowResult = controller.ApplyLatestPublished(controller.GetSelectedWallpaperStyle());
                if (!workflowResult.IsSuccess)
                {
                    ShowTrayError(workflowResult.Message);
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
            controller.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }

        public static void TrayIconDispose()
        {
            trayIcon.Dispose();
        }

        private static void ShowTrayError(string message)
        {
            trayIcon.BalloonTipTitle = "APOD Wallpaper";
            trayIcon.BalloonTipText = message;
            trayIcon.ShowBalloonTip(4000);
        }
    }
}
