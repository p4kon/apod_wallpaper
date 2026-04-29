using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace apod_wallpaper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "apod_wallpaper", out createdNew))
            {
                if (createdNew)
                {
                    var settingsStore = new JsonSettingsStore();
                    settingsStore.MigrateLegacySettingsIfNeeded();
                    IApplicationBackendFacade controller = new ApplicationController(
                        settingsStore,
                        new StartupService());
                    var initialization = controller.InitializeAsync().GetAwaiter().GetResult();
                    if (!initialization.Succeeded)
                    {
                        System.Windows.Forms.MessageBox.Show(
                            initialization.Error != null ? initialization.Error.Message : "Application initialization failed.",
                            "APOD Wallpaper",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TrayApplicationContext(controller));
                    return;
                }

                var current = Process.GetCurrentProcess();
                foreach (var process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        TrayApplicationContext.TrayIconDispose();
                        break;
                    }
                }
            }
        }
    }
}
