using MezzexEyeV_1.Platforms.Windows;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace MezzexEyeV_1.Components.Pages
{
    public partial class Login :IDisposable
    {
        protected string Username { get; set; }
        protected string Password { get; set; }
        protected string PasswordInputType { get; set; } = "password"; // Default to password type
        protected string LogoutPassword { get; set; } // For the logout confirmation modal
        protected string LoginMessage { get; set; } // Message for login success/failure
        protected string ianaTimeZone;
        protected string LoginMessageCssClass { get; set; } // CSS class for alert styling
        protected List<string> UsernamesCache = new();
        protected List<string> suggestions = new();
        protected string SystemName { get; set; }
        protected bool ShowLogoutModal { get; set; } = false; // Flag to show/hide logout modal
        protected bool IsLoading { get; set; } = false;
        [Inject] protected IJSRuntime JS { get; set; } // Inject JavaScript interop for localStorage access
        [Inject] protected HttpClient Http { get; set; }
        [Inject] private ScreenshotCaptureService ScreenshotService { get; set; }

        [Inject] protected NavigationManager Navigation { get; set; } // Inject NavigationManager
        /*   [Inject] protected IWindow Window { get; set; }*/
        protected override async Task OnInitializedAsync()
        {
            ianaTimeZone = TZConvert.WindowsToIana(TimeZoneInfo.Local.Id);
            KeyInterceptor.BlockKeys();

            await PreloadUsernames();
            await LoadSystemNameFromLocalStorage();
        }

        protected void TogglePasswordVisibility(ChangeEventArgs e)
        {
            // Toggle between "text" and "password"
            PasswordInputType = (bool)e.Value ? "text" : "password";
        }

        protected async Task PreloadUsernames()
        {
            try
            {
                UsernamesCache = await Http.GetFromJsonAsync<List<string>>("/api/AccountApi/getUsernames");
            }
            catch (Exception ex)
            {
                LoginMessage = $"Error loading usernames: {ex.Message}";
                LoginMessageCssClass = "alert-danger";
            }
        }

        protected void OnInput(ChangeEventArgs e)
        {
            string input = e.Value.ToString();
            if (!string.IsNullOrEmpty(input))
            {
                suggestions = UsernamesCache
                    .Where(u => u.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();
            }
            else
            {
                suggestions.Clear();
            }
        }

        protected void SelectUsername(string selectedUsername)
        {
            Username = selectedUsername;
            suggestions.Clear();
        }

        protected async Task HandleLogin()
        {
            IsLoading = true; // Show the loader
            var currentMachineName = Environment.MachineName;

            var userPart = Username.Contains("@") ? Username.Split('@')[0] : Username;
            SystemName = $"{userPart}-{currentMachineName}";

            // Save the system name locally using JavaScript interop (localStorage)
            await JS.InvokeVoidAsync("localStorage.setItem", "systemName", SystemName);

            // Prepare the login request data
            var data = new { Email = Username, Password, SystemName };

            try
            {
                var response = await Http.PostAsJsonAsync("/api/AccountApi/login", data);
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

                if (response.IsSuccessStatusCode && result.ContainsKey("message") && result["message"].ToString() == "Login successful")
                {
                    // Store the user details to localStorage
                    string userId = result.ContainsKey("userId") ? result["userId"].ToString() : null;
                    string username = result.ContainsKey("username") ? result["username"].ToString() : null;
                    string country = result.ContainsKey("country") ? result["country"].ToString() : null;

                    await JS.InvokeVoidAsync("localStorage.setItem", "userId", userId);
                    await JS.InvokeVoidAsync("localStorage.setItem", "username", username);
                    await JS.InvokeVoidAsync("localStorage.setItem", "country", country);
                    await JS.InvokeVoidAsync("localStorage.setItem", "email", Username);

                    // Check if the values are set correctly in localStorage
                    var storedUsername = await JS.InvokeAsync<string>("localStorage.getItem", "username");
                    var storedUserId = await JS.InvokeAsync<string>("localStorage.getItem", "userId");
                    var systemname = await JS.InvokeAsync<string>("localStorage.getItem", "systemName");
                    // Save the email (username) to localStorage
                    await JS.InvokeVoidAsync("localStorage.setItem", "email", Username);
                    var mauiWindow = Application.Current?.Windows.FirstOrDefault(); // Get the current window
                    if (mauiWindow != null)
                    {
                        var hWnd = mauiWindow.Handler.PlatformView as Microsoft.UI.Xaml.Window; // Get the platform-specific window handle
                        IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(hWnd);

                        // Restore window buttons and exit full screen mode
                        WindowHelper.RestoreWindowButtons(windowHandle);
                        WindowHelper.SetFullScreenMode(windowHandle, false);
                    }

                    // Unblock keyboard keys
                    KeyInterceptor.UnblockKeys();
                    if (!string.IsNullOrEmpty(storedUsername) && !string.IsNullOrEmpty(storedUserId) && !string.IsNullOrEmpty(systemname))
                    {
                        // Only start the ScreenshotService after confirming values are set
                        Preferences.Set("username", storedUsername);
                        Preferences.Set("userId", storedUserId);
                        Preferences.Set("systemname", systemname);
                        // Pass the username and userId to the ScreenshotCaptureService
                        ScreenshotService.SetUserData(storedUsername, storedUserId, systemname, ianaTimeZone);
                        ScreenshotService.StartScreenCapture(); // Now it's safe to start
                    }

                    // Set login message and navigate to home
                    LoginMessage = "Login successful!";
                    LoginMessageCssClass = "alert-success";
                    Navigation.NavigateTo("/home");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && result.ContainsKey("message") && result["message"].ToString() == "User already logged in on another device today.")
                {
                    // User is already logged in, show the logout modal

                    // Load the saved machine name from local storage
                    var savedMachineName = await JS.InvokeAsync<string>("localStorage.getItem", "systemName");

                    if (savedMachineName == SystemName)
                    {
                        // If the saved machine name and current machine name are the same
                        LoginMessage = "You are already logged in on another device. Do you want to logout and login again?";
                    }
                    else
                    {
                        // If the saved machine name and current machine name are different
                        LoginMessage = (MarkupString)$"You are already logged in on another device. " +
                                       $"Do you want to logout from <strong style='color: red;'>{savedMachineName}</strong> " +
                                       $"and login again on <strong style='color: blue;'>{SystemName}</strong>?";
                    }


                    LoginMessageCssClass = "alert-warning"; // Yellow alert for warning
                    ShowLogoutModal = true; // Show the logout confirmation modal
                }
                else
                {
                    LoginMessage = result.ContainsKey("message") ? result["message"].ToString() : "Login failed.";
                    LoginMessageCssClass = "alert-danger"; // Red alert for failure
                }
            }
            catch (Exception ex)
            {
                LoginMessage = $"Error during login: {ex.Message}";
                LoginMessageCssClass = "alert-danger";
            }
            finally
            {
                IsLoading = false; // Hide the loader
            }
        }


        protected async Task HandleLogout()
        {
            var data = new { Email = Username, Password = LogoutPassword };
            try
            {
                var response = await Http.PostAsJsonAsync("/api/AccountApi/logout", data);
                if (response.IsSuccessStatusCode)
                {
                    // Logout successful, now clear localStorage
                    await JS.InvokeVoidAsync("localStorage.removeItem", "userId");
                    await JS.InvokeVoidAsync("localStorage.removeItem", "username");
                    await JS.InvokeVoidAsync("localStorage.removeItem", "country");
                    await JS.InvokeVoidAsync("localStorage.removeItem", "systemName");
                    // Logout successful, now try to login again
                    ShowLogoutModal = false; // Hide the modal
                    await HandleLogin(); // Re-attempt login with existing credentials
                }
                else
                {
                    LoginMessage = "Logout failed. Please check your password.";
                    LoginMessageCssClass = "alert-danger";
                }
            }
            catch (Exception ex)
            {
                LoginMessage = $"Error during logout: {ex.Message}";
                LoginMessageCssClass = "alert-danger";
            }
        }

        protected async Task LoadSystemNameFromLocalStorage()
        {
            try
            {
                // Try to retrieve the system name from localStorage
                SystemName = await JS.InvokeAsync<string>("localStorage.getItem", "systemName");

                if (!string.IsNullOrEmpty(SystemName))
                {
                    LoginMessage = $"Welcome back! System Name: {SystemName}";
                    LoginMessageCssClass = "alert-info"; // Informational message
                }
            }
            catch (Exception ex)
            {
                LoginMessage = $"Error loading system name: {ex.Message}";
                LoginMessageCssClass = "alert-danger";
            }
        }

        protected void ShutdownSystem()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/s /t 1",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                LoginMessage = $"Error: {ex.Message}";
                LoginMessageCssClass = "alert-danger";
            }
        }

        protected void RestartSystem()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 1",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                LoginMessage = $"Error: {ex.Message}";
                LoginMessageCssClass = "alert-danger";
            }
        }

        public void Dispose()
        {
            // Clean up any resources here if needed.

        }
    }
}
