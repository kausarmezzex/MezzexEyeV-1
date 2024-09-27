using System;
using System.Runtime.InteropServices;

namespace MezzexEyeV_1.Platforms.Windows
{
    public static class WindowHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        const int GWL_STYLE = -16;
        const int WS_MINIMIZEBOX = 0x00020000;
        const int WS_MAXIMIZEBOX = 0x00010000;
        const int WS_SYSMENU = 0x00080000; // System menu
        const int WS_CAPTION = 0x00C00000; // Caption bar

        public static void DisableWindowButtons(IntPtr hWnd)
        {
            int style = GetWindowLong(hWnd, GWL_STYLE);
            style &= ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU | WS_CAPTION); // Disable Minimize, Maximize, and Close buttons
            SetWindowLong(hWnd, GWL_STYLE, style);
        }

        public static void RestoreWindowButtons(IntPtr hWnd)
        {
            int style = GetWindowLong(hWnd, GWL_STYLE);
            style |= (WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU | WS_CAPTION); // Restore buttons
            SetWindowLong(hWnd, GWL_STYLE, style);
        }

        public static void SetFullScreenMode(IntPtr hWnd, bool isFullScreen)
        {
            const int SW_MAXIMIZE = 3;
            const int SW_RESTORE = 9;

            if (isFullScreen)
            {
                ShowWindow(hWnd, SW_MAXIMIZE); // Maximize the window for full screen effect
            }
            else
            {
                ShowWindow(hWnd, SW_RESTORE); // Restore to normal after login
            }
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);


    }
}
