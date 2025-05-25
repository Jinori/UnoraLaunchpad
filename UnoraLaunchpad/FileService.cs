using System.IO;
using Newtonsoft.Json;

namespace UnoraLaunchpad;

public sealed class FileService
{
    public static Settings LoadSettings(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<Settings>(json);
        }

        return new Settings();
    }

    public static void SaveSettings(Settings settings, string path)
    {
        var directoryPath = Path.GetDirectoryName(path);

        if ((directoryPath != null) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        var json = JsonConvert.SerializeObject(settings);
        File.WriteAllText(path, json);
    }
}