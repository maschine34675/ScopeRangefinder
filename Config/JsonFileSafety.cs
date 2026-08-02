using System.IO;

namespace ScopeRangefinder
{
    internal static class JsonFileSafety
    {
        public static void WriteAtomic(string path, string contents)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        public static void BackupBroken(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string backupPath = path + ".broken.json";
                    if (File.Exists(backupPath))
                    {
                        File.SetAttributes(backupPath, FileAttributes.Normal);
                    }

                    File.Copy(path, backupPath, true);
                    Plugin.LogSource?.LogWarning($"Preserved unreadable file as '{backupPath}'.");
                }
            }
            catch
            {
            }
        }
    }
}
