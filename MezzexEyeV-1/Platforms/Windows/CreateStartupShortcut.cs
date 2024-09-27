using IWshRuntimeLibrary; // Add this after adding COM reference
using System;
using System.IO;

namespace MezzexEyeV_1.Platforms.Windows
{
    public class StartupShortcutService
    {
        public void CreateStartupShortcut(string appName, string exePath)
        {
            // Path to the startup folder for the current user
            string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

            // Check if the shortcut already exists
            if (!System.IO.File.Exists(shortcutPath))
            {
                // Create a new Windows Scripting Host shell object
                var shell = new WshShell();
                // Create the shortcut
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath; // Path to the .exe file
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath); // Working directory
                shortcut.Description = $"Shortcut for {appName}";
                shortcut.Save(); // Save the shortcut
            }
        }
    }
}
