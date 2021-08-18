using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace apod_wallpaper
{
    static class Program
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern
        bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern
        bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        [STAThread]
        static void Main()
        {

            bool createdNew = true;
            using (Mutex mutex = new Mutex(true, "apod_wallpaper", out createdNew))
            {
                if (createdNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TrayApplicationContext());
                }
                else
                {
                    Process current = Process.GetCurrentProcess();
                    foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id)
                        {
                            IntPtr hWnd = process.MainWindowHandle; //cant find handle configuratiomForm

                            //if (IsIconic(hWnd)) //zero, not work
                            //{
                                //ShowWindowAsync(hWnd, SW_SHOW);
                                //ShowWindowAsync(hWnd, SW_RESTORE);
                                TrayApplicationContext.TrayIconDispose();
                            //}

                            //SetForegroundWindow(hWnd); //not work

                            break;
                        }
                    }
                }
            }
        }
    }
}