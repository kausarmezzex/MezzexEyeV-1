using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using MezzexEyeV_1.Platforms.Windows;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.Controls;
using TimeZoneConverter;

public class HomeBase : ComponentBase, IDisposable
{
    [Inject] private HttpClient Http { get; set; }
    [Inject] private IJSRuntime JS { get; set; }
    [Inject] protected NavigationManager Navigation { get; set; } // Inject NavigationManager
    protected string CurrentTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    protected string StaffInTime { get; set; }
    protected string SelectedTaskType { get; set; } // Task name (string)
    protected string Comment { get; set; }
    protected bool IsError { get; set; } = false;
    protected bool IsStaffIn { get; set; } = false;
    protected bool IsStaffOut { get; set; } = false;
    protected string Username { get; set; } = "User"; // Default placeholder for username
    protected string UserLocation { get; set; } = "Fetching location..."; // Location field to display
    // TaskModel for task types
    protected List<TaskModel> TaskTypes = new List<TaskModel>();
    protected List<TaskItem> RunningTasks = new List<TaskItem>();
    protected List<TaskItem> EndedTasks = new List<TaskItem>();
    private string GoogleMapsApiKey = "AIzaSyBEIqDhk4kpJH8s0RmyaZ7jW1wmDr00bdU";
    protected int UserId;
    private System.Timers.Timer timer; // Explicitly use System.Timers.Timer


    // Modal message and visibility state
    protected string ModalMessage { get; set; }
    protected bool ShowModal { get; set; } = false;

    // Class-level IANA time zone
    protected string ianaTimeZone;

    // Initialization logic
    protected override async Task OnInitializedAsync()
    {
        // Load user info from local storage
        UserId = Convert.ToInt32(await JS.InvokeAsync<string>("localStorage.getItem", "userId"));
        Username = await JS.InvokeAsync<string>("localStorage.getItem", "username");

        // Convert to IANA time zone using TimeZoneConverter
        ianaTimeZone = TZConvert.WindowsToIana(TimeZoneInfo.Local.Id);
        // Initial loading of data
        await Refresh();
        await CheckStaffInStatus();

        // Call GetTaskTimerById API on initialization
        await LoadTaskTimerById();
        await LoadCompletedTasks();
        // Initialize and start the timer for updating the current time and working times
        StartTimer();
    }

    private async Task GetCurrentLocationAsync()
    {
        try
        {
            // 1. Get the user's current GPS coordinates (Latitude, Longitude)
            var (latitude, longitude) = await GetGpsCoordinatesAsync();

            // 2. Reverse geocode the GPS coordinates to get a full address
            var address = await GetAddressFromGoogleAsync(latitude, longitude);

            // 3. Update UserLocation with the full address
            UserLocation = address;
        }
        catch (Exception ex)
        {
            UserLocation = $"Unable to get location: {ex.Message}";
        }
    }

    private async Task<string> GetAddressFromGoogleAsync(double latitude, double longitude)
    {
        
        var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={GoogleMapsApiKey}";

        try
        {
            var response = await Http.GetFromJsonAsync<GoogleGeocodeResponse>(requestUrl);

            // Check if response and Results are valid
            if (response != null && response.Results != null && response.Results.Count > 0)
            {
                return response.Results[0].FormattedAddress;
            }

            return "Address not found";
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching address: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Unexpected error: {ex.Message}";
        }
    }

    public class GoogleGeocodeResponse
    {
        [JsonPropertyName("results")]
        public List<GoogleGeocodeResult> Results { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }

    public class GoogleGeocodeResult
    {
        [JsonPropertyName("formatted_address")]
        public string FormattedAddress { get; set; }
    }



    private async Task<(double Latitude, double Longitude)> GetGpsCoordinatesAsync()
    {
        try
        {
            var location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = TimeSpan.FromSeconds(30)
            });

            if (location != null)
            {
                return (location.Latitude, location.Longitude);
            }
            else
            {
                return (0, 0); // Handle error case
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting location: {ex.Message}", true);
            return (0, 0);
        }
    }

    // Method to load task timers by user ID and IANA time zone
    private async Task LoadTaskTimerById()
    {
        try
        {
            RunningTasks.Clear();
            var response = await Http.GetFromJsonAsync<List<TaskTimerResponse>>($"api/Data/getTaskTimers?userId={UserId}&clientTimeZone={ianaTimeZone}");

            if (response != null && response.Count > 0)
            {
                foreach (var timer in response)
                {
                    RunningTasks.Add(new TaskItem
                    {
                        Id = timer.Id,
                        StaffName = timer.UserName,
                        TaskType = timer.TaskName,
                        Comment = timer.TaskComment,
                        StartTime = timer.TaskStartTime.ToString("HH:mm:ss"),
                        EndTime = timer.TaskEndTime.HasValue ? timer.TaskEndTime.Value.ToString("HH:mm:ss") : null,
                        WorkingTime = timer.TaskEndTime.HasValue ? (timer.TaskEndTime.Value - timer.TaskStartTime).ToString(@"hh\:mm\:ss") : "00:00:00",
                        TimeDifference = timer.TimeDifference,
                        UserId = timer.UserId
                        
                    });
                }
            }
            else
            {
                ShowMessage("No running tasks found.", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error loading tasks: {ex.Message}", true);
        }
    }

    protected string GetRowClass(TaskItem task)
    {
        if (task.UserId == UserId)
        {
            // Highlight the row if the task belongs to the logged-in user
            return "highlighted-task";
        }
        else
        {
            // Dull the row if the task belongs to other users
            return "dull-task";
        }
    }

    // Method to load completed tasks for the logged-in user
    private async Task LoadCompletedTasks()
    {
        try
        {
            EndedTasks.Clear();
            var response = await Http.GetFromJsonAsync<List<TaskTimerResponse>>($"api/Data/getUserCompletedTasks?userId={UserId}&clientTimeZone={ianaTimeZone}");

            if (response != null && response.Count > 0)
            {
                // Populate EndedTasks from the API response
                EndedTasks = response.Select(timer => new TaskItem
                {
                    Id = timer.Id,
                    StaffName = timer.UserName,
                    TaskType = timer.TaskName,
                    Comment = timer.TaskComment,
                    StartTime = timer.TaskStartTime.ToString("HH:mm:ss"),
                    EndTime = timer.TaskEndTime.HasValue ? timer.TaskEndTime.Value.ToString("HH:mm:ss") : null,
                    WorkingTime = timer.TaskEndTime.HasValue
                        ? (timer.TaskEndTime.Value - timer.TaskStartTime).ToString(@"hh\:mm\:ss")
                        : "00:00:00",
                    TimeDifference = timer.TimeDifference
                }).ToList();
            }
            else
            {
                ShowMessage("No completed tasks found.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error loading completed tasks: {ex.Message}", true);
        }
    }

    private async Task CheckStaffInStatus()
    {
        try
        {
            var response = await Http.GetFromJsonAsync<StaffInResponse>($"api/Data/getStaffInTime?userId={UserId}&clientTimeZone={ianaTimeZone}");
            if (response != null)
            {
                StaffInTime = response.StaffInTime.ToString("HH:mm:ss");
                IsStaffIn = true;
                IsStaffOut = false;
            }
            else
            {
                IsStaffIn = false;
                IsStaffOut = true;
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error checking staff in status: {ex.Message}", true);
        }
    }


    // Timer logic for updating the current time
    private void StartTimer()
    {
        timer = new System.Timers.Timer(1000); // Timer triggers every second
        timer.Elapsed += UpdateTime;
        timer.Start();
    }

    private void UpdateTime(object sender, ElapsedEventArgs e)
    {
        InvokeAsync(() =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            UpdateRunningTaskTimes();
            StateHasChanged();
        });
    }

    private async void UpdateRunningTaskTimes()
    {
        foreach (var task in RunningTasks)
        {
            if (!string.IsNullOrEmpty(task.StartTime) && DateTime.TryParse(task.StartTime, out var startTime))
            {
                var adjustedCurrentTime = DateTime.Now.Add(task.TimeDifference);

                // Calculate the working time by subtracting the start time from the adjusted current time
                var elapsed = adjustedCurrentTime - startTime;
                task.WorkingTime = elapsed.ToString(@"hh\:mm\:ss");
            }
            else
            {
                task.WorkingTime = "Invalid Start Time"; // Handle invalid or missing StartTime
            }
        }
        await Refresh();
    }

    // Refresh method to reload all data
    protected async Task Refresh()
    {
        await LoadTaskTypes();
        await LoadTaskTimerById();
        await LoadCompletedTasks();
    }

    private async Task LoadTaskTypes()
    {
        try
        {
            // Fetch country from localStorage
            string country = await JS.InvokeAsync<string>("localStorage.getItem", "country");
            if (string.IsNullOrEmpty(country))
            {
                ShowMessage("Country is not set in local storage.", true);
                return;
            }

            // Fetch task types from the API based on the country
            TaskTypes = await Http.GetFromJsonAsync<List<TaskModel>>($"api/Data/getTasksListWithCountry?country={country}");

            if (TaskTypes == null || TaskTypes.Count == 0)
            {
                ShowMessage("No tasks found for the specified country.", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error fetching task types: {ex.Message}", true);
        }
    }


    // Method to start a new task after checking if any task is already running
    protected async Task StartTask()
    {
        // Check if "Other" task type is selected and if the comment is empty
        if (SelectedTaskType == "Other" && string.IsNullOrWhiteSpace(Comment))
        {
            ShowMessage("Please enter a comment for 'Other' task type.", true);
            return;
        }
        // If the user is not StaffIn, log them in automatically
        if (!IsStaffIn)
        {
            await StaffIn(); // Automatically StaffIn if not already in
        }
        // Check if there is already a running task
        var runningTaskResponse = await Http.GetFromJsonAsync<RunningTaskResponse>($"api/Data/getTaskTimeIdByUser?userId={UserId}");

        if (runningTaskResponse.TaskTimeId != -1)  // There's already a running task
        {
            ShowMessage("You already have a running task. Please end it before starting a new one.");
            return;
        }
        // Start task immediately without waiting for location
        var selectedTask = TaskTypes.FirstOrDefault(t => t.Name == SelectedTaskType);
        if (selectedTask != null)
        {
            // Create the new task with placeholder for location
            var taskTimerRequest = new TaskTimerUploadRequest
            {
                UserId = UserId,
                TaskId = selectedTask.Id,
                TaskComment = Comment,
                ClientTimeZone = ianaTimeZone,
                ActualAddress = "Fetching location..."  // Placeholder for address
            };

            try
            {
                // Start the task immediately
                await Http.PostAsJsonAsync("api/Data/saveTaskTimer", taskTimerRequest);

                var newTask = new TaskItem
                {
                    StaffName = Username,
                    TaskType = SelectedTaskType,
                    Comment = Comment,
                    StartTime = DateTime.Now.ToString("HH:mm:ss"),
                    WorkingTime = "00:00:00"
                };

                RunningTasks.Add(newTask);
                ShowMessage("Task started successfully.");

                // Fetch location asynchronously and update the task
                await FetchAndUpdateLocation(newTask);
                
                StateHasChanged();
                await Refresh();// Force UI to re-render after data is updated
            }
            catch (Exception ex)
            {
                ShowMessage($"Error starting task: {ex.Message}", true);
            }
        }
        else
        {
            ShowMessage("Task not found.", true);
        }
        
    }

    // Asynchronously fetch and update the task with the actual location
    private async Task FetchAndUpdateLocation(TaskItem task)
    
    {
        try
        {
            await GetCurrentLocationAsync();
            var runningTaskResponse = await Http.GetFromJsonAsync<RunningTaskResponse>($"api/Data/getTaskTimeIdByUser?userId={UserId}");
            var response = await Http.PostAsJsonAsync("api/Data/updateTaskAddress", new UpdateTaskAddressRequest
            {
                TaskTimeId = runningTaskResponse.TaskTimeId,
                ActualAddress = UserLocation
            });

        }
        catch (Exception ex)
        {
            ShowMessage($"Error fetching location: {ex.Message}", true);
        }
        await Refresh();
    }


    // Method to end the task after getting the running task's ID
    protected async Task EndTask(TaskItem task)
    {
        if (task.StaffName != Username)
        {
            ShowMessage("You cannot end another user's task.", true);
            return;
        }

        // Get the running task ID from the API
        var runningTaskResponse = await Http.GetFromJsonAsync<RunningTaskResponse>($"api/Data/getTaskTimeIdByUser?userId={UserId}");

        if (runningTaskResponse.TaskTimeId == -1)
        {
            ShowMessage("No running task found to end.");
            return;
        }

        var updateRequest = new UpdateTaskTimerRequest
        {
            Id = runningTaskResponse.TaskTimeId, // Set the running task ID from the API response
            ClientTimeZone = ianaTimeZone
        };

        try
        {
            var updateResponse = await Http.PostAsJsonAsync("api/Data/updateTaskTimer", updateRequest);

            if (updateResponse.IsSuccessStatusCode)
            {
                task.EndTime = DateTime.Now.ToString("HH:mm:ss");
                CalculateEndedTaskWorkingTime(task);
                EndedTasks.Add(task);
                RunningTasks.Remove(task);
                ShowMessage("Task ended successfully.");
                await Refresh();
            }
            else if (updateResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ShowMessage("TaskTimer not found.", true);
            }
            else
            {
                ShowMessage("Error ending task.", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error ending task: {ex.Message}", true);
        }
        
    }

    private void CalculateEndedTaskWorkingTime(TaskItem task)
    {
        if (DateTime.TryParse(task.StartTime, out var startTime) && DateTime.TryParse(task.EndTime, out var endTime))
        {
            // Adjust start and end times with the TimeDifference
            var adjustedStartTime = startTime.Add(task.TimeDifference);
            var adjustedEndTime = endTime.Add(task.TimeDifference);

            // Calculate the working time by subtracting adjusted start time from adjusted end time
            var workingTime = adjustedEndTime - adjustedStartTime;
            task.WorkingTime = workingTime.ToString(@"hh\:mm\:ss");
        }
    }


    protected async Task StaffIn()
    {
        var staffInRequest = new StaffInOut
        {
            UserId = UserId,
            ClientTimeZone = ianaTimeZone
        };

        try
        {
            var response = await Http.PostAsJsonAsync("api/Data/saveStaff", staffInRequest);

            if (response.IsSuccessStatusCode)
            {
                // Deserialize the response to extract StaffId
                var result = await response.Content.ReadFromJsonAsync<StaffResponse>();
                int staffId = result.StaffId;

                // Save StaffId to localStorage for later use
                await JS.InvokeVoidAsync("localStorage.setItem", "staffId", staffId.ToString());

                IsStaffIn = true;
                StaffInTime = DateTime.Now.ToString("HH:mm:ss");
                ShowMessage("Staff logged in successfully.");
            }
            else
            {
                ShowMessage("Error logging in staff.", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error during staff in: {ex.Message}", true);
        }
    }


    protected async Task StaffOut()
    {
        try
        {
            // Retrieve StaffId from localStorage
            var staffIdString = await JS.InvokeAsync<string>("localStorage.getItem", "staffId");
            if (string.IsNullOrEmpty(staffIdString) || !int.TryParse(staffIdString, out int staffId))
            {
                ShowMessage("StaffId not found in local storage. Cannot log out.", true);
                return;
            }

            /*// Step 1: End the running task if any
            var runningTaskResponse = await Http.GetFromJsonAsync<RunningTaskResponse>($"api/Data/getTaskTimeIdByUser?userId={UserId}");
            if (runningTaskResponse != null && runningTaskResponse.TaskTimeId != -1)
            {
                var updateTaskRequest = new UpdateTaskTimerRequest
                {
                    Id = runningTaskResponse.TaskTimeId, // ID of the running task
                    ClientTimeZone = ianaTimeZone
                };

                var endTaskResponse = await Http.PostAsJsonAsync("api/Data/updateTaskTimer", updateTaskRequest);
                if (!endTaskResponse.IsSuccessStatusCode)
                {
                    ShowMessage("Error ending the running task.",, true);
                    return;
                }
            }*/

            // Step 2: Log out the staff by sending StaffId
            var staffOutRequest = new StaffInOut
            {
                Id = staffId, // Use the StaffId retrieved from localStorage
                UserId = UserId,
                StaffOutTime = DateTime.Now, // Mark staff out with current time
                ClientTimeZone = ianaTimeZone
            };

            // Step 2: Log out the user
            var email = await JS.InvokeAsync<string>("localStorage.getItem", "email"); // Get email from local storage
            if (string.IsNullOrEmpty(email))
            {
                ShowMessage("Email not found in local storage. Cannot log out.", true);
                return;
            }

            var logoutRequest = new { Email = email };
            var logoutResponse = await Http.PostAsJsonAsync("/api/AccountApi/logout", logoutRequest);
            if (logoutResponse.IsSuccessStatusCode)
            {
                ShowMessage("Staff logged out successfully.");

                // Step 3: Clear local storage and redirect to login page
                await JS.InvokeVoidAsync("localStorage.removeItem", "userId");
                await JS.InvokeVoidAsync("localStorage.removeItem", "username");
                await JS.InvokeVoidAsync("localStorage.removeItem", "country");
                await JS.InvokeVoidAsync("localStorage.removeItem", "email");
                IsStaffIn = true;
                IsStaffOut = false;
                Navigation.NavigateTo("/");

                // Manually trigger full-screen mode after navigation
                var mauiWindow = Application.Current?.Windows.FirstOrDefault();
                if (mauiWindow != null)
                {
                    var hWnd = mauiWindow.Handler.PlatformView as Microsoft.UI.Xaml.Window;
                    IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(hWnd);

                    // Ensure full-screen mode is triggered after navigation
                    WindowHelper.DisableWindowButtons(windowHandle);
                    WindowHelper.SetFullScreenMode(windowHandle, true);
                }
            }
            else
            {
                ShowMessage("Logout failed.", true);
            }

            var response = await Http.PostAsJsonAsync("api/Data/updateStaff", staffOutRequest);
            if (response.IsSuccessStatusCode)
            {
                ShowMessage("Staff logged out successfully.");
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                ShowMessage($"Error during staff out: {errorMessage}", true);
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Error during staff out: {ex.Message}", true);
        }
    }

    public class StaffResponse
    {
        public string Message { get; set; }
        public int StaffId { get; set; }
    }


    // Function to show messages in the modal
    protected void ShowMessage(string message, bool isError = false)
    {
        ModalMessage = message;
        ShowModal = true;
        IsError = isError;

        // If it's a success message, close the modal automatically after 2 seconds
        if (!isError)
        {
            Task.Delay(1000).ContinueWith(_ =>
            {
                InvokeAsync(() =>
                {
                    ShowModal = false;
                    StateHasChanged();
                });
            });
        }

        StateHasChanged();
    }

    protected void CloseModal()
    {
        ShowModal = false;
        StateHasChanged();
    }

    public void Dispose()
    {
        timer?.Dispose();
    }

    // Data models
    public class TaskModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    protected class TaskItem
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string StaffName { get; set; }
        public string TaskType { get; set; }
        public string Comment { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string WorkingTime { get; set; }
        public TimeSpan TimeDifference { get; set; } // New property for time difference
        public string? ActualAddress { get; set; } // Navigation property
    }


    protected class StaffInOut
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime? StaffOutTime { get; set; }
        public string ClientTimeZone { get; set; }
    }

    protected class TaskTimerUploadRequest
    {
        public int UserId { get; set; }
        public int TaskId { get; set; }
        public string TaskComment { get; set; }
        public string ClientTimeZone { get; set; }
        public string? ActualAddress { get; set; } // Navigation property
    }

    protected class UpdateTaskTimerRequest
    {
        public int Id { get; set; }
        public string ClientTimeZone { get; set; }
    }

    protected class StaffInResponse
    {
        public DateTime StaffInTime { get; set; }
        public int StaffId { get; set; }
    }

    public class RunningTaskResponse
    {
        public int TaskTimeId { get; set; }
    }

    public class TaskTimerResponse
    {
        public int Id { get; set; }          // Task Timer ID
        public int UserId { get; set; }       // ID of the user
        public string UserName { get; set; }  // Name of the user (e.g., "FirstName LastName")
        public int TaskId { get; set; }       // ID of the task
        public string TaskName { get; set; }  // Name of the task
        public string TaskComment { get; set; } // Comment added to the task
        public DateTime TaskStartTime { get; set; } // When the task started
        public DateTime? TaskEndTime { get; set; }  // When the task ended (null if still running)
        public TimeSpan TimeDifference { get; set; }
    }


    public class IpInfo
    {
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
    }
    public class UpdateTaskAddressRequest
    {
        public int TaskTimeId { get; set; }
        public string ActualAddress { get; set; }
    }
}

