using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace UnoraLaunchpad;

public sealed class FileService
{
    public Settings LoadSettings(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<Settings>(json);
        }

        return new Settings();
    }

    public void SaveSettings(Settings settings, string path)
    {
        var directoryPath = Path.GetDirectoryName(path);

        if ((directoryPath != null) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        var json = JsonConvert.SerializeObject(settings);
        File.WriteAllText(path, json);
    }
}