using System;
using System.IO;
using System.Text;

namespace Mater2026.Services
{
    public static class LogService
    {
        static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mater2026", "logs");
        static readonly string LogPath = Path.Combine(LogDir, "mater.log");
        const long MaxBytes = 1_000_000; // 1MB

        public static void Info(string msg)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                RollIfNeeded();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {msg}\n", Encoding.UTF8);
            }
            catch { }
        }

        static void RollIfNeeded()
        {
            try
            {
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var bak = Path.Combine(LogDir, $"mater_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.Move(LogPath, bak);
                }
            }
            catch { }
        }
    }
}
