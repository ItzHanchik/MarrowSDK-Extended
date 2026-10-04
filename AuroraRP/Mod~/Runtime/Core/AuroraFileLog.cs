using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AuroraRP
{
    /// <summary>
    /// Простое файловое логирование с буфером, чтобы не дёргать диск каждый кадр.
    /// </summary>
    public static class AuroraFileLog
    {
        private static readonly object Sync = new object();
        private static readonly List<string> Buffer = new List<string>(64);

        private static string _path;
        private static float _lastFlushTime;
        private static bool _disabled;

        public static string LogDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_path))
                {
                    _path = Path.Combine(AuroraUtils.UserDataDirectory, "aurora.log");
                }

                return Path.GetDirectoryName(_path);
            }
        }

        public static void Initialize()
        {
            try
            {
                Directory.CreateDirectory(AuroraUtils.UserDataDirectory);
                _path = Path.Combine(AuroraUtils.UserDataDirectory, "aurora.log");

                // Ротация: если лог стал больше 4 МБ — отрезаем его.
                if (File.Exists(_path) && new FileInfo(_path).Length > 4 * 1024 * 1024)
                {
                    File.Delete(_path);
                }

                File.AppendAllText(_path,
                    $"\n===== AuroraRP {AuroraRuntime.Version} | {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====\n",
                    Encoding.UTF8);

                _disabled = false;
            }
            catch (Exception)
            {
                _disabled = true;
            }
        }

        public static void Append(AuroraLog.Level level, string message)
        {
            if (_disabled || string.IsNullOrEmpty(_path))
            {
                return;
            }

            lock (Sync)
            {
                Buffer.Add($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");

                // сбрасываем на диск не чаще раза в 2 секунды
                if (UnityEngine.Time.realtimeSinceStartup - _lastFlushTime < 2f && Buffer.Count < 64)
                {
                    return;
                }

                Flush();
            }
        }

        public static void Flush()
        {
            if (_disabled || Buffer.Count == 0)
            {
                return;
            }

            try
            {
                File.AppendAllLines(_path, Buffer, Encoding.UTF8);
            }
            catch
            {
                _disabled = true;
            }
            finally
            {
                Buffer.Clear();
                _lastFlushTime = UnityEngine.Time.realtimeSinceStartup;
            }
        }
    }
}
