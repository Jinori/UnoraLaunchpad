using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Tesseract; // Added for Tesseract OCR
using System.Drawing; // Added for System.Drawing.Rectangle and System.Drawing.Bitmap
using System.ComponentModel; // Added for Closing event

namespace UnoraLaunchpad
{
    public partial class ScreenshotBrowserWindow : Window, IDisposable
    {
        public ObservableCollection<ScreenshotInfo> Screenshots { get; set; }
        private string _screenshotsFolderPath;
        private string _gameFolderName;
        private TesseractEngine _ocrEngine;
        private bool _disposed = false; // To detect redundant calls to Dispose

        // Define the ROI for map name extraction. This is a guess and likely needs adjustment.
        // Assuming image width around 800-1024px.
        // X: (800/2) - (200/2) = 300. Y: 10 (near top). Width: 200. Height: 30.
        private readonly System.Drawing.Rectangle _mapNameRoi = new System.Drawing.Rectangle(300, 10, 200, 40);


        public ScreenshotBrowserWindow(string gameFolderName = "Unora")
        {
            InitializeComponent();
            _gameFolderName = gameFolderName;
            SetupPaths();
            InitializeOcrEngine();

            Screenshots = new ObservableCollection<ScreenshotInfo>();
            ThumbnailsItemsControl.ItemsSource = Screenshots;

            LoadScreenshots();
            this.Closing += ScreenshotBrowserWindow_Closing;
        }

        private void SetupPaths()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string screenshotsSubfolderPath = Path.Combine(baseDir, _gameFolderName, "screenshots");
            string gameRootPath = Path.Combine(baseDir, _gameFolderName);

            if (Directory.Exists(screenshotsSubfolderPath))
            {
                _screenshotsFolderPath = screenshotsSubfolderPath;
            }
            else if (Directory.Exists(gameRootPath))
            {
                _screenshotsFolderPath = gameRootPath;
            }
            else
            {
                _screenshotsFolderPath = gameRootPath; // Default for creation attempt
                StatusTextBlock.Text = $"Screenshots folder not found for '{_gameFolderName}'. Will attempt to use/create default.";
            }
        }

        private void InitializeOcrEngine()
        {
            try
            {
                var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                if (!Directory.Exists(tessDataPath) || !File.Exists(Path.Combine(tessDataPath, "eng.traineddata")))
                {
                    StatusTextBlock.Text = "Error: tessdata folder or eng.traineddata not found. OCR will not function.";
                    MessageBox.Show("Tesseract language data (tessdata/eng.traineddata) not found. OCR for map names will be disabled.", "OCR Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _ocrEngine = null; // Ensure engine is null if setup fails
                    return;
                }
                _ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"OCR Engine Error: {ex.Message}";
                MessageBox.Show($"Failed to initialize Tesseract OCR engine: {ex.Message}", "OCR Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _ocrEngine = null; // Ensure engine is null on error
            }
        }
        private string PerformOcr(string imagePath)
        {
            if (_ocrEngine == null) return "OCR Disabled";

            try
            {
                using (var img = Pix.LoadFromFile(imagePath))
                {
                    // Define ROI for map name (example: top-middle of the image)
                    // This ROI is relative to the image being processed.
                    // Ensure ROI coordinates are within image bounds.
                    int roiX = Math.Max(0, Math.Min(_mapNameRoi.X, img.Width - 1));
                    int roiY = Math.Max(0, Math.Min(_mapNameRoi.Y, img.Height - 1));
                    int roiWidth = Math.Min(_mapNameRoi.Width, img.Width - roiX);
                    int roiHeight = Math.Min(_mapNameRoi.Height, img.Height - roiY);

                    if (roiWidth <= 0 || roiHeight <= 0) {
                        Debug.WriteLine($"Invalid ROI for image {imagePath}. Image: {img.Width}x{img.Height}, ROI: {_mapNameRoi}");
                        return "ROI Error";
                    }

                    var rect = new Rect(roiX, roiY, roiWidth, roiHeight);

                    using (var page = _ocrEngine.Process(img, rect, PageSegMode.SingleLine))
                    {
                        var text = page.GetText()?.Trim();
                        return string.IsNullOrEmpty(text) ? "Unknown" : text;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR processing error for {imagePath}: {ex.Message}");
                return "OCR Error";
            }
        }

        private void LoadScreenshots()
        {
            Screenshots.Clear();
            LargePreviewImage.Source = null;

            if (!Directory.Exists(_screenshotsFolderPath))
            {
                StatusTextBlock.Text = $"Folder '{_screenshotsFolderPath}' not found.";
                try
                {
                    Directory.CreateDirectory(_screenshotsFolderPath);
                    StatusTextBlock.Text = $"Created folder: '{_screenshotsFolderPath}'. No screenshots yet.";
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Error creating folder '{_screenshotsFolderPath}': {ex.Message}";
                    MessageBox.Show($"Could not create directory: {_screenshotsFolderPath}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                return;
            }

            try
            {
                var screenshotFiles = Directory.EnumerateFiles(_screenshotsFolderPath, "lod*.bmp")
                                             .Select(f => new FileInfo(f))
                                             .ToList();

                if (!screenshotFiles.Any())
                {
                    StatusTextBlock.Text = "No screenshots found (lod*.bmp).";
                    return;
                }

                StatusTextBlock.Text = "Loading screenshots and performing OCR...";
                // To avoid blocking UI for too long, consider Task.Run for OCR, but that adds complexity with dispatcher.
                // For now, direct processing.
                Mouse.OverrideCursor = Cursors.Wait; // Show wait cursor

                foreach (var fileInfo in screenshotFiles.OrderByDescending(f => f.CreationTime))
                {
                    var screenshotInfo = new ScreenshotInfo(fileInfo.FullName, fileInfo.CreationTime);

                    // Perform OCR
                    if (_ocrEngine != null) // Only if OCR engine initialized successfully
                    {
                        screenshotInfo.MapName = PerformOcr(fileInfo.FullName);
                    }
                    else
                    {
                        screenshotInfo.MapName = "OCR N/A";
                    }

                    try
                    {
                        BitmapImage thumbnail = new BitmapImage();
                        thumbnail.BeginInit();
                        thumbnail.UriSource = new Uri(fileInfo.FullName);
                        thumbnail.DecodePixelWidth = 100;
                        thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                        thumbnail.EndInit();
                        thumbnail.Freeze();
                        screenshotInfo.Thumbnail = thumbnail;
                        Screenshots.Add(screenshotInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading thumbnail for {fileInfo.FullName}: {ex.Message}");
                    }
                }
                StatusTextBlock.Text = $"Loaded {Screenshots.Count} screenshots. OCR complete.";
                if (Screenshots.Any())
                {
                    DisplayFullImage(Screenshots.First());
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading screenshots: {ex.Message}";
                MessageBox.Show($"Error accessing screenshots directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null; // Restore cursor
            }
        }

        private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ScreenshotInfo selectedScreenshot)
            {
                DisplayFullImage(selectedScreenshot);
            }
        }

        private void DisplayFullImage(ScreenshotInfo screenshotInfo)
        {
            if (screenshotInfo == null) return;

            try
            {
                BitmapImage fullImage = new BitmapImage();
                fullImage.BeginInit();
                fullImage.UriSource = new Uri(screenshotInfo.FilePath);
                fullImage.CacheOption = BitmapCacheOption.OnLoad; // Load fully
                fullImage.EndInit();
                fullImage.Freeze(); // Freeze for performance
                LargePreviewImage.Source = fullImage;
                StatusTextBlock.Text = $"Displaying: {screenshotInfo.FileName}";
            }
            catch (Exception ex)
            {
                LargePreviewImage.Source = null; // Clear if loading failed
                StatusTextBlock.Text = $"Error loading image {screenshotInfo.FileName}: {ex.Message}";
                MessageBox.Show($"Could not load image: {screenshotInfo.FilePath}\n{ex.Message}", "Image Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SortNewestButton_Click(object sender, RoutedEventArgs e)
        {
            var sortedScreenshots = new ObservableCollection<ScreenshotInfo>(Screenshots.OrderByDescending(s => s.CreationDate));
            Screenshots.Clear();
            foreach (var s in sortedScreenshots) Screenshots.Add(s);
            StatusTextBlock.Text = "Sorted by newest first.";
             if (Screenshots.Any()) DisplayFullImage(Screenshots.First());
        }

        private void SortOldestButton_Click(object sender, RoutedEventArgs e)
        {
            var sortedScreenshots = new ObservableCollection<ScreenshotInfo>(Screenshots.OrderBy(s => s.CreationDate));
            Screenshots.Clear();
            foreach (var s in sortedScreenshots) Screenshots.Add(s);
            StatusTextBlock.Text = "Sorted by oldest first.";
            if (Screenshots.Any()) DisplayFullImage(Screenshots.First());
        }

        private void SortMapNameButton_Click(object sender, RoutedEventArgs e)
        {
            var sortedScreenshots = new ObservableCollection<ScreenshotInfo>(Screenshots.OrderBy(s => s.MapName));
            Screenshots.Clear();
            foreach (var s in sortedScreenshots) Screenshots.Add(s);
            StatusTextBlock.Text = "Sorted by map name (A-Z).";
            if (Screenshots.Any()) DisplayFullImage(Screenshots.First());
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadScreenshots();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_screenshotsFolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _screenshotsFolderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                else
                {
                    MessageBox.Show($"Screenshots folder not found: {_screenshotsFolderPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ScreenshotBrowserWindow_Closing(object sender, CancelEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    if (_ocrEngine != null)
                    {
                        _ocrEngine.Dispose();
                        _ocrEngine = null;
                    }
                }
                _disposed = true;
            }
        }

        // Optional: Finalizer, if you have unmanaged resources directly in this class
        // ~ScreenshotBrowserWindow()
        // {
        //     Dispose(false);
        // }
    }
}
