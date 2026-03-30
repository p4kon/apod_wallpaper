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
                    RuntimeSettingsSync.ApplyCurrentSettings();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TrayApplicationContext());
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
