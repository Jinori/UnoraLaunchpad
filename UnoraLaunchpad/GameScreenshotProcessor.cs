using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using System;
using System.Threading.Tasks;
using Windows.Foundation; // Required for Rect

// We'll use Windows.Foundation.Rect for defining crop areas as it's more aligned with WinRT APIs.
// Note that Windows.Foundation.Rect uses doubles for X, Y, Width, Height.

namespace UnoraLaunchpad
{
    public class GameScreenshotProcessor
    {
        private OcrEngine ocrEngine;

    // Crop areas based on user-provided coordinates for 640x480 resolution
    // Zone: (left, top, width, height) = (218, 451, 181, 29)
    // ID:   (left, top, width, height) = (558, 329, 82, 32)
    private Rect zoneNameCropArea = new Rect(218, 451, 181, 29);
    private Rect characterIdCropArea = new Rect(558, 329, 82, 32);

    public GameScreenshotProcessor()
    {
        this.ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

        if (this.ocrEngine == null)
        {
            var language = new Windows.Globalization.Language("en-US"); // Fallback to English
            if (OcrEngine.IsLanguageSupported(language))
            {
                this.ocrEngine = OcrEngine.TryCreateFromLanguage(language);
            }
            else
            {
                // This case should be rare on modern Windows systems
                System.Diagnostics.Debug.WriteLine("OCR Error: English language not supported.");
                throw new InvalidOperationException("OCR Error: English language not supported, and no user profile languages are available/supported.");
            }
        }

        if (this.ocrEngine == null)
        {
             System.Diagnostics.Debug.WriteLine("OCR Error: Engine could not be initialized.");
             throw new InvalidOperationException("OCR Engine could not be initialized. Check if the language pack for OCR is installed in Windows settings (e.g., English Optical Character Recognition).");
        }
    }

    public async Task<(string ZoneName, string CharacterId)> ExtractInfoFromFileAsync(string imagePath)
    {
        if (ocrEngine == null)
        {
            System.Diagnostics.Debug.WriteLine("OCR Engine was not initialized when ExtractInfoFromFileAsync was called.");
            return ("OCR Engine not ready.", "OCR Engine not ready.");
        }

        try
        {
            StorageFile imageFile = await StorageFile.GetFileFromPathAsync(imagePath);
            using (IRandomAccessStream stream = await imageFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                if (softwareBitmap == null) {
                    System.Diagnostics.Debug.WriteLine($"Failed to decode image to SoftwareBitmap: {imagePath}");
                    return ("Bitmap decoding error.", "Bitmap decoding error.");
                }

                string rawZoneName = "Zone OCR Error";
                string rawCharacterId = "ID OCR Error";

                try
                {
                    SoftwareBitmap croppedZoneBitmap = await CreateCroppedSoftwareBitmapAsync(softwareBitmap, zoneNameCropArea);
                    if (croppedZoneBitmap != null)
                    {
                        OcrResult zoneOcrResult = await ocrEngine.RecognizeAsync(croppedZoneBitmap);
                        rawZoneName = zoneOcrResult.Text;
                        croppedZoneBitmap.Dispose(); // Dispose of the cropped bitmap
                    } else
                    {
                        rawZoneName = "Zone crop failed";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error OCR'ing zone: {ex.Message}");
                    rawZoneName = $"Zone OCR Exception: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 50))}";
                }

                try
                {
                    SoftwareBitmap croppedIdBitmap = await CreateCroppedSoftwareBitmapAsync(softwareBitmap, characterIdCropArea);
                    if (croppedIdBitmap != null)
                    {
                        OcrResult idOcrResult = await ocrEngine.RecognizeAsync(croppedIdBitmap);
                        rawCharacterId = idOcrResult.Text;
                        croppedIdBitmap.Dispose(); // Dispose of the cropped bitmap
                    } else
                    {
                        rawCharacterId = "ID crop failed";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error OCR'ing ID: {ex.Message}");
                    rawCharacterId = $"ID OCR Exception: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 50))}";
                }

                softwareBitmap.Dispose(); // Dispose of the original software bitmap

                // Clean the extracted text
                string cleanedZoneName = CleanOcrText(rawZoneName);
                string cleanedCharacterId = CleanOcrText(rawCharacterId);

                // Further validation or specific parsing can be added here if needed
                // For example, if character IDs always follow a certain pattern (e.g., alphanumeric, specific length)

                return (cleanedZoneName, cleanedCharacterId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during OCR process for {imagePath}: {ex.ToString()}");
            return ($"File/OCR Error: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 50))}",
                    $"File/OCR Error: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 50))}");
        }
    }

    private async Task<SoftwareBitmap> CreateCroppedSoftwareBitmapAsync(SoftwareBitmap inputBitmap, Rect cropRectangle)
    {
        // Validate and clamp cropRectangle to inputBitmap dimensions
        var clampedRect = new BitmapBounds
        {
            X = (uint)Math.Max(0, Math.Min(inputBitmap.PixelWidth - 1, cropRectangle.X)),
            Y = (uint)Math.Max(0, Math.Min(inputBitmap.PixelHeight - 1, cropRectangle.Y)),
            Width = (uint)Math.Max(1, Math.Min(cropRectangle.Width, inputBitmap.PixelWidth - cropRectangle.X)),
            Height = (uint)Math.Max(1, Math.Min(cropRectangle.Height, inputBitmap.PixelHeight - cropRectangle.Y))
        };

        // Ensure width and height are not zero after clamping, and X/Y are within bounds.
        if (clampedRect.Width == 0 || clampedRect.Height == 0 ||
            clampedRect.X >= inputBitmap.PixelWidth || clampedRect.Y >= inputBitmap.PixelHeight)
        {
            System.Diagnostics.Debug.WriteLine($"Invalid crop dimensions after clamping: X:{clampedRect.X}, Y:{clampedRect.Y}, W:{clampedRect.Width}, H:{clampedRect.Height} for bitmap {inputBitmap.PixelWidth}x{inputBitmap.PixelHeight}");
            return null;
        }


        using (var memoryStream = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, memoryStream);

            encoder.SetSoftwareBitmap(inputBitmap);

            encoder.BitmapTransform.Bounds = clampedRect;
            encoder.BitmapTransform.ScaledWidth = clampedRect.Width; // Ensure no scaling, just cropping
            encoder.BitmapTransform.ScaledHeight = clampedRect.Height;
            encoder.IsThumbnailGenerated = false; // Don't need a thumbnail

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BitmapEncoder FlushAsync error: {ex.Message}. This can happen with invalid bounds.");
                return null; // Failed to encode the cropped section
            }

            // Check if stream has data
            if (memoryStream.Size == 0)
            {
                System.Diagnostics.Debug.WriteLine("MemoryStream is empty after BitmapEncoder.FlushAsync. Cropping likely failed.");
                return null;
            }

            memoryStream.Seek(0); // Reset stream position for the decoder

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(memoryStream);

            SoftwareBitmap croppedBitmap = await decoder.GetSoftwareBitmapAsync(inputBitmap.BitmapPixelFormat, inputBitmap.BitmapAlphaMode);
            return croppedBitmap;
        }
    }

    private string CleanOcrText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty; // Return empty if input is null, empty, or just whitespace
        }

        // Trim leading and trailing whitespace (including newlines)
        string cleanedText = rawText.Trim();

        // Replace internal newline characters with a space, then collapse multiple spaces.
        // This handles cases where a single piece of text might be split into multiple lines by OCR.
        cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"\s*[\r\n]+\s*", " ");

        // Optional: Replace multiple spaces with a single space
        cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"\s{2,}", " ");

        // Optional: Add any game-specific character corrections if known
        // For example, if '0' is often mistaken for 'O' in numbers, or vice-versa in names.
        // cleanedText = cleanedText.Replace("0", "O"); // Example: if IDs are purely alphabetical

        // Check if the text contains typical error messages we might have injected
        if (cleanedText.StartsWith("Zone OCR Exception:") || cleanedText.StartsWith("ID OCR Exception:") ||
            cleanedText.StartsWith("File/OCR Error:") || cleanedText.Equals("Zone crop failed") ||
            cleanedText.Equals("ID crop failed") || cleanedText.Equals("Bitmap decoding error.") ||
            cleanedText.Equals("OCR Engine not ready."))
        {
            return "Error"; // Or a more specific error indicator, or the original error message if preferred
        }

        // If after cleaning, the text is empty (e.g., it was only newlines), return empty.
        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            return string.Empty;
        }

        return cleanedText;
    }
}
}
