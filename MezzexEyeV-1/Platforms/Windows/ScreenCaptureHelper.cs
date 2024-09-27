using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MezzexEyeV_1.Platforms.Windows
{
    public static class ScreenCaptureHelper
    {
        // P/Invoke for GDI functions
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        public static extern int BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("user32.dll")]
        public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDc);

        private const int SRCCOPY = 0x00CC0020;  // Source copy flag for BitBlt

        // Capture the screen and return a bitmap
        public static Bitmap CaptureScreen()
        {
            var (screenWidth, screenHeight) = ScreenHelper.GetScreenSize();

            if (screenWidth == 0 || screenHeight == 0)
            {
                Console.WriteLine("Invalid screen dimensions.");
                return null;
            }

            Bitmap screenshot = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(screenshot))
            {
                IntPtr hdcDest = g.GetHdc();
                IntPtr hdcSrc = GetDC(IntPtr.Zero);

                BitBlt(hdcDest, 0, 0, screenWidth, screenHeight, hdcSrc, 0, 0, SRCCOPY);

                g.ReleaseHdc(hdcDest);
                ReleaseDC(IntPtr.Zero, hdcSrc);
            }

            return screenshot;
        }

        // Upload the captured screenshot to API and return the file name from the response
        public static async Task<string> UploadScreenshotToApi(Bitmap screenshot, string apiUrl, string username)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    // Save the screenshot to memory stream
                    screenshot.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);

                    using (var client = new HttpClient())
                    {
                        var content = new MultipartFormDataContent();
                        var imageContent = new ByteArrayContent(ms.ToArray());
                        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                        // Create the form-data with the image and username-based file name
                        content.Add(imageContent, "file", $"{username}_{DateTime.Now:yyyyMMddHHmmss}.png");

                        // Send the POST request to upload the screenshot
                        var response = await client.PostAsync(apiUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            // Deserialize the response as a JSON object
                            var fileUploadResponse = await response.Content.ReadFromJsonAsync<FileUploadResponse>();

                            if (fileUploadResponse != null && !string.IsNullOrEmpty(fileUploadResponse.FileName))
                            {
                                // Return the file name from the response
                                return fileUploadResponse.FileName;
                            }
                            else
                            {
                                Console.WriteLine("Error: File name not found in the response.");
                                return null;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Failed to upload screenshot. Status: {response.StatusCode}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading screenshot: {ex.Message}");
                return null;
            }
        }

        // Class to represent the API response structure
        public class FileUploadResponse
        {
            public string FileName { get; set; }
        }
    }
}
