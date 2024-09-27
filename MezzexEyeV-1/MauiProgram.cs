using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Hosting;
using System.Runtime.InteropServices;
using MezzexEyeV_1.Platforms.Windows;
namespace MezzexEyeV_1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Register HttpClient to be used for API calls
            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7045/") // Base URL for your API
            });


#if DEBUG
            // Add developer tools during debugging
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // Inside your MauiProgram class
            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(window =>
                    {
                        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        WindowHelper.DisableWindowButtons(hWnd);
                        WindowHelper.SetFullScreenMode(hWnd, true);
                    });
                });
#endif
            });

#if WINDOWS
            builder.Services.AddSingleton<ScreenshotCaptureService>();
#endif

#if WINDOWS
            // Register the service that will create a shortcut in the Startup folder
            builder.Services.AddSingleton<StartupShortcutService>();
#endif
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