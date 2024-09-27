using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MezzexEyeV_1
{
    public static class KeyInterceptor
    {
        private static IntPtr _hookID = IntPtr.Zero;
        private static bool _altPressed = false; // To track if Alt key is pressed

        // Define virtual key codes for the keys you want to block
        private const int VK_LWIN = 0x5B;  // Left Windows Key
        private const int VK_RWIN = 0x5C;  // Right Windows Key
        private const int VK_ESCAPE = 0x1B; // Escape Key
        private const int VK_LCONTROL = 0xA2; // Left Control Key
        private const int VK_RCONTROL = 0xA3; // Right Control Key
        private const int VK_CONTROL = 0x11; // General Control Key (covers both left and right in some cases)
        private const int VK_MENU = 0x12; // Alt Key (Menu key in virtual key codes)
        private const int VK_TAB = 0x09; // Tab key

        // Delegate for hook callback
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static LowLevelKeyboardProc _proc = HookCallback;

        // Windows API functions
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13; // Low-Level Keyboard Hook

        // Blocking keys
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // Enable the hook and block keys
        public static void BlockKeys()
        {
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
            }
        }

        // Remove the hook and unblock keys
        public static void UnblockKeys()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        // Set the hook
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // Callback function for the hook
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // Track if Alt is pressed
                if (vkCode == VK_MENU)
                {
                    _altPressed = true;
                }

                // Block specific keys or combinations by their virtual key codes
                if (vkCode == VK_LWIN || vkCode == VK_RWIN || vkCode == VK_ESCAPE ||
                    vkCode == VK_LCONTROL || vkCode == VK_RCONTROL || vkCode == VK_CONTROL ||
                    vkCode == VK_MENU || vkCode == VK_TAB || (_altPressed && vkCode == VK_TAB))
                {
                    return (IntPtr)1; // Block the key event
                }
            }

            // Reset Alt key state when released
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_MENU)
                {
                    _altPressed = false;
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
    }
}
