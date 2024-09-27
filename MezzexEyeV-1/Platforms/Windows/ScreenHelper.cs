using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
namespace MezzexEyeV_1.Platforms.Windows
{
    

    public static class ScreenHelper
    {
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        public static (int Width, int Height) GetScreenSize()
        {
            // Get the screen width and height using Win32 APIs
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            return (screenWidth, screenHeight);
        }
    }

}
