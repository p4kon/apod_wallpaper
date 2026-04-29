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
            if (Control.ModifierKeys == Keys.Shift)
            {
                ApplyLatestFromTray();
                return;
            }

            var trayActionResult = controller.ShouldApplyOnTrayDoubleClick();
            if (!trayActionResult.Succeeded)
            {
                ShowTrayError(trayActionResult.Error != null ? trayActionResult.Error.Message : "Unable to read tray action settings.");
                return;
            }

            if (!trayActionResult.Value)
            {
                ShowConfig(sender, e);
                return;
            }

            ApplyLatestFromTray();
        }

        private void ApplyLatestFromTray()
        {
            Thread thread = new Thread(() =>
            {
                try
                {
                    var styleResult = controller.GetSelectedWallpaperStyle();
                    if (!styleResult.Succeeded)
                    {
                        ShowTrayError(styleResult.Error != null ? styleResult.Error.Message : "Unable to read wallpaper style.");
                        return;
                    }

                    var operationResult = controller.ApplyLatestPublished(styleResult.Value);
                    if (!operationResult.Succeeded)
                    {
                        ShowTrayError(operationResult.Error != null ? operationResult.Error.Message : "Unable to apply the latest APOD.");
                        return;
                    }

                    var workflowResult = operationResult.Value;
                    if (!workflowResult.IsSuccess)
                        ShowTrayError(workflowResult.Message);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Tray double-click apply failed.", ex);
                    ShowTrayError(ApodErrorTranslator.ToUserMessage(ex));
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        void ShowConfig(object sender, EventArgs e)
        {
            if (!configWindow.Visible)
            {
                var preferredDateResult = controller.GetPreferredDisplayDate();
                if (preferredDateResult.Succeeded)
                    configWindow.SyncDisplayedDate(preferredDateResult.Value);
            }

            if (configWindow.Visible)
                configWindow.Focus();
            else
                configWindow.ShowDialog();
        }

        void Exit(object sender, EventArgs e)
        {
            var shutdownResult = controller.Shutdown();
            if (!shutdownResult.Succeeded)
                AppLogger.Warn(shutdownResult.Error != null ? shutdownResult.Error.Message : "Controller shutdown failed.");
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
