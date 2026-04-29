using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace apod_wallpaper
{
    internal class TrayApplicationContext : ApplicationContext
    {
        private static NotifyIcon trayIcon = new NotifyIcon();
        private readonly IApplicationSessionFacade sessionFacade;
        private readonly IApplicationSettingsFacade settingsFacade;
        private readonly IApodWorkflowFacade workflowFacade;
        private readonly IApplicationDiagnosticsFacade diagnosticsFacade;
        private readonly IApplicationBackendFacade backendFacade;
        private ConfigurationForm configWindow;

        internal TrayApplicationContext(IApplicationBackendFacade backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            sessionFacade = backend;
            settingsFacade = backend;
            workflowFacade = backend;
            diagnosticsFacade = backend;
            backendFacade = backend;
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

            var trayActionResult = settingsFacade.ShouldApplyOnTrayDoubleClick();
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
                    var styleResult = settingsFacade.GetSelectedWallpaperStyle();
                    if (!styleResult.Succeeded)
                    {
                        ShowTrayError(styleResult.Error != null ? styleResult.Error.Message : "Unable to read wallpaper style.");
                        return;
                    }

                    var operationResult = workflowFacade.ApplyLatestPublished(styleResult.Value);
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
                    diagnosticsFacade.LogWarning("Tray double-click apply failed.", ex);
                    ShowTrayError(GetUserFriendlyErrorMessage(ex));
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        void ShowConfig(object sender, EventArgs e)
        {
            var window = EnsureConfigurationWindow();
            if (!window.Visible)
            {
                var preferredDateResult = settingsFacade.GetPreferredDisplayDate();
                if (preferredDateResult.Succeeded)
                    window.SyncDisplayedDate(preferredDateResult.Value);
            }

            if (window.Visible)
                window.Focus();
            else
                window.ShowDialog();
        }

        void Exit(object sender, EventArgs e)
        {
            DisposeConfigurationWindow();
            trayIcon.DoubleClick -= TrayDoubleClickAction;
            var shutdownResult = sessionFacade.Shutdown();
            if (!shutdownResult.Succeeded)
                diagnosticsFacade.LogWarning(shutdownResult.Error != null ? shutdownResult.Error.Message : "Controller shutdown failed.");
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

        private ConfigurationForm EnsureConfigurationWindow()
        {
            if (configWindow == null || configWindow.IsDisposed)
            {
                configWindow = new ConfigurationForm(backendFacade);
                configWindow.FormClosed += ConfigurationWindow_FormClosed;
            }

            return configWindow;
        }

        private void ConfigurationWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (configWindow != null)
            {
                configWindow.FormClosed -= ConfigurationWindow_FormClosed;
                configWindow = null;
            }
        }

        private void DisposeConfigurationWindow()
        {
            if (configWindow == null)
                return;

            configWindow.FormClosed -= ConfigurationWindow_FormClosed;
            if (!configWindow.IsDisposed)
                configWindow.Dispose();
            configWindow = null;
        }

        private string GetUserFriendlyErrorMessage(Exception exception, string fallbackMessage = "Something went wrong while processing the APOD request.")
        {
            var result = diagnosticsFacade.GetUserFriendlyErrorMessage(exception, fallbackMessage);
            return result.Succeeded && !string.IsNullOrWhiteSpace(result.Value)
                ? result.Value
                : fallbackMessage;
        }
    }
}
