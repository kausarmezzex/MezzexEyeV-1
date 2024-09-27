#if WINDOWS
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop; // Required for JavaScript interop to access localStorage
using System.Net.Http.Json;  // Import for JSON-related methods
using System.Linq;
using System.Collections.Generic;

namespace MezzexEyeV_1.Platforms.Windows
{
    internal class ScreenshotCaptureService : BackgroundService
    {
        private Timer _timer;
        private readonly string _apiUrl;
        private readonly IJSRuntime _jsRuntime;
        private string _username;
        private string _userId;
        private string _systemname;
        private string _timeZone;

        public ScreenshotCaptureService(IJSRuntime jsRuntime)
        {
            _apiUrl = "https://localhost:7045/api/UploadData/upload-Images";
            _jsRuntime = jsRuntime;
        }

        public void SetUserData(string username, string userId, string systemname, string ianaTimeZone)
        {
            _username = username;
            _userId = userId;
            _systemname = systemname;
            _timeZone = ianaTimeZone;
        }

        // Start screenshot capturing
        public void StartScreenCapture()
        {
            _timer = new Timer(TakeScreenshot, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        // Stop screenshot capturing
        public void StopScreenCapture()
        {
            _timer?.Dispose();
            _timer = null;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // The main execution logic can be empty since we're controlling it via StartScreenCapture/StopScreenCapture
            return Task.CompletedTask;
        }

        private async void TakeScreenshot(object state)
        {
            try
            {
                if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_userId))
                {
                    Console.WriteLine("Skipping screenshot: username or userId is not set.");
                    return;
                }

                // Step 1: Fetch task timer data
                int taskTimerId = await GetTaskTimerById(int.Parse(_userId));
                if (taskTimerId == -1)
                {
                    Console.WriteLine("No active task found, skipping screenshot.");
                    return;
                }

                // Capture the screen
                Console.WriteLine("Attempting to capture screen...");
                Bitmap screenshot = ScreenCaptureHelper.CaptureScreen();
                if (screenshot == null)
                {
                    Console.WriteLine("CaptureScreen returned null.");
                    return; // Avoid further execution if capture failed
                }

                // Upload screenshot to API
                Console.WriteLine("Uploading screenshot to API...");
                string imageFileName = await ScreenCaptureHelper.UploadScreenshotToApi(screenshot, _apiUrl, _username);
                if (string.IsNullOrEmpty(imageFileName))
                {
                    Console.WriteLine("Failed to upload screenshot.");
                    return;
                }

                Console.WriteLine("Screenshot uploaded, file name returned: " + imageFileName);

                // Save the screenshot and task timer data
                await SaveScreenCaptureData(imageFileName, _username, _userId, _timeZone, taskTimerId, _systemname);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Screenshot capture failed: {ex.Message}");
            }
        }

        // Method to call GetTaskTimeIdByUser API
        private async Task<int> GetTaskTimerById(int userId)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Calling the API to get the TaskTimerId
                    var response = await client.GetFromJsonAsync<RunningTaskResponse>($"https://localhost:7045/api/Data/getTaskTimeIdByUser?userId={userId}");

                    if (response != null)
                    {
                        return response.TaskTimeId; // Return the TaskTimerId
                    }
                    else
                    {
                        Console.WriteLine("Failed to fetch task timer data.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching task timer data: {ex.Message}");
            }

            return -1; // Return -1 if something goes wrong or no task is found
        }

        // Method to save the screenshot along with task timer data
        private async Task SaveScreenCaptureData(string imageFileName, string username, string userId, string timeZone, int taskTimerId, string _systemname)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var model = new
                    {
                        ImageUrl = imageFileName,
                        Username = username,
                        SystemName = _systemname,
                        TaskTimerId = taskTimerId
                    };

                    var response = await client.PostAsJsonAsync("https://localhost:7045/api/Data/SaveScreenCaptureData", model);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Screen capture data saved successfully.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to save screen capture data. Status: {response.StatusCode}");
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Error: " + errorContent);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving screen capture data: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }

    // Mock for RunningTaskResponse, replace with actual model in your code
    public class RunningTaskResponse
    {
        public int TaskTimeId { get; set; }
    }
}
#endif
