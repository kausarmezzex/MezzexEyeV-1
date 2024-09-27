using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using MezzexEyeV_1.Platforms.Windows;

namespace MezzexEyeV_1
{
    public static class MauiProgram
    {
        // Global mutex for single instance prevention
        private static Mutex _mutex = null;

        public static MauiApp CreateMauiApp()
        {
            // Use a unique identifier for your application
            const string appMutexName = "Global\\MyMauiBlazorAppMutex";

            // Ensure only one instance is running
            _mutex = new Mutex(true, appMutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // If there is already an instance running, exit the new instance
                return null; // This will prevent the second instance from being created.
            }

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Register Blazor WebView
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

#if WINDOWS
            // Register the service that will create a shortcut in the Startup folder
            builder.Services.AddSingleton<StartupShortcutService>();
#endif

            // Add Windows lifecycle events
            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(window =>
                    {
                        // Get the current .exe path and create a shortcut in the startup folder
                        var exePath = Path.Combine(AppContext.BaseDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.exe");

                        // Ensure the shortcut is created in the startup folder
                        var startupService = builder.Services.BuildServiceProvider().GetRequiredService<StartupShortcutService>();
                        startupService.CreateStartupShortcut("MyMauiBlazorApp", exePath);
                    });
                });
#endif
            });

            return builder.Build();
        }
    }
}
