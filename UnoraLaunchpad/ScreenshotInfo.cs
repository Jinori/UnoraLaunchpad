using System;
using System.Windows.Media.Imaging;

namespace UnoraLaunchpad
{
    public class ScreenshotInfo
    {
        public string FilePath { get; set; }
        public BitmapImage Thumbnail { get; set; }
        // FullImage will be loaded on demand, so we might just store the path
        // and create the BitmapImage when needed, or store it here if pre-loaded.
        // For now, let's assume it's loaded from FilePath when selected.
        // public BitmapImage FullImage { get; set; }
        public DateTime CreationDate { get; set; }
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public string MapName { get; set; }

        public ScreenshotInfo(string filePath, DateTime creationDate)
        {
            FilePath = filePath;
            CreationDate = creationDate;
            MapName = "Unknown"; // Default value
            // Thumbnail will be set after construction, typically during the loading process.
        }
    }
}
