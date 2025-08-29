using System;
using System.IO;

namespace Mater2026.Services
{
    public static class SettingsService
    {
        static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mater2026");
        static string FilePath => Path.Combine(Dir, "settings.ini");

        public static void SaveLastRoot(string path)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, path ?? "");
            }
            catch { }
        }

        public static string? LoadLastRoot()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = File.ReadAllText(FilePath).Trim();
                    return string.IsNullOrWhiteSpace(s) ? null : s;
                }
            }
            catch { }
            return null;
        }
    }
}
