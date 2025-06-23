using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace UnoraLaunchpad
{
    public partial class ScreenshotBrowserWindow : Window
    {
        public ObservableCollection<ScreenshotInfo> Screenshots { get; set; }
        private string _screenshotsFolderPath; // To be configured, e.g., "Unora/screenshots"
        private string _gameFolderName; // e.g., "Unora"

        // Constructor that accepts the game folder name
        public ScreenshotBrowserWindow(string gameFolderName = "Unora") // Default to "Unora" for now
        {
            InitializeComponent();
            _gameFolderName = gameFolderName;
            // It's common for games to save screenshots directly in the main game folder or a subfolder.
            // Let's assume a "screenshots" subfolder first. If not found, try the game root.
            string screenshotsSubfolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _gameFolderName, "screenshots");
            string gameRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _gameFolderName);

            if (Directory.Exists(screenshotsSubfolderPath))
            {
                _screenshotsFolderPath = screenshotsSubfolderPath;
            }
            else if (Directory.Exists(gameRootPath)) // Fallback to game root if "screenshots" subfolder doesn't exist
            {
                _screenshotsFolderPath = gameRootPath;
            }
            else
            {
                // Neither path exists, disable functionality or show error
                _screenshotsFolderPath = gameRootPath; // Default to gameRoot for creation attempt
                StatusTextBlock.Text = $"Screenshots folder not found for '{_gameFolderName}'. Will attempt to use/create default.";
                // Optionally disable buttons if folder is critical and not found
            }

            Screenshots = new ObservableCollection<ScreenshotInfo>();
            ThumbnailsItemsControl.ItemsSource = Screenshots;
            DataContext = this; // Not strictly necessary here as ItemsSource is set directly

            LoadScreenshots();
        }


        private void LoadScreenshots()
        {
            Screenshots.Clear();
            LargePreviewImage.Source = null; // Clear previous large preview

            if (!Directory.Exists(_screenshotsFolderPath))
            {
                StatusTextBlock.Text = $"Screenshots folder '{_screenshotsFolderPath}' not found.";
                try
                {
                    Directory.CreateDirectory(_screenshotsFolderPath);
                    StatusTextBlock.Text = $"Created screenshots folder: '{_screenshotsFolderPath}'. No screenshots yet.";
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Error creating folder '{_screenshotsFolderPath}': {ex.Message}";
                    MessageBox.Show($"Could not create screenshots directory: {_screenshotsFolderPath}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                foreach (var fileInfo in screenshotFiles.OrderByDescending(f => f.CreationTime)) // Default sort: newest first
                {
                    var screenshotInfo = new ScreenshotInfo(fileInfo.FullName, fileInfo.CreationTime);

                    // Create and cache thumbnail
                    try
                    {
                        BitmapImage thumbnail = new BitmapImage();
                        thumbnail.BeginInit();
                        thumbnail.UriSource = new Uri(fileInfo.FullName);
                        thumbnail.DecodePixelWidth = 100; // Create a smaller image for thumbnail
                        thumbnail.CacheOption = BitmapCacheOption.OnLoad; // Cache it in memory
                        thumbnail.EndInit();
                        thumbnail.Freeze(); // Important for performance in collections
                        screenshotInfo.Thumbnail = thumbnail;
                        Screenshots.Add(screenshotInfo);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle error for individual file loading
                        Debug.WriteLine($"Error loading thumbnail for {fileInfo.FullName}: {ex.Message}");
                        // Optionally, add a placeholder or skip this screenshot
                    }
                }
                StatusTextBlock.Text = $"Loaded {Screenshots.Count} screenshots.";
                if (Screenshots.Any())
                {
                    // Display the first screenshot's full image by default
                    DisplayFullImage(Screenshots.First());
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error loading screenshots: {ex.Message}";
                MessageBox.Show($"Error accessing screenshots directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
