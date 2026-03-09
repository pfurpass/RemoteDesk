using System;
using System.IO;
using System.Text.Json;

namespace RemoteDesktopClient.Properties
{
    internal sealed class Settings
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemoteDesktopClient", "settings.json");

        private static Settings? _instance;
        public static Settings Default => _instance ??= Load();

        public string ServerUrl { get; set; } = "https://localhost:7001";
        public string PcId { get; set; } = Environment.MachineName;
        public int JpegQuality { get; set; } = 60;
        public int CaptureInterval { get; set; } = 100;

        private static Settings Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings();
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}