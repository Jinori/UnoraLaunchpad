using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic; // Make sure this is present for List<Account>

namespace UnoraLaunchpad;

public sealed class FileService
{
    public static Settings LoadSettings(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var settings = JsonConvert.DeserializeObject<Settings>(json);

            // Ensure SavedAccounts is not null after deserialization
            if (settings.SavedAccounts == null)
            {
                settings.SavedAccounts = new List<Account>();
            }
            return settings;
        }
        // When creating new Settings, SavedAccounts is already initialized by its property initializer.
        return new Settings();
    }

    public static void SaveSettings(Settings settings, string path)
    {
        var directoryPath = Path.GetDirectoryName(path);

        if ((directoryPath != null) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        var json = JsonConvert.SerializeObject(settings, Formatting.Indented); // Added Formatting.Indented for readability
        File.WriteAllText(path, json);
    }
}