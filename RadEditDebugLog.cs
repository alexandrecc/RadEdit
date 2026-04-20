using System;
using System.Diagnostics;
using System.IO;

namespace RadEdit
{
    internal static class RadEditDebugLog
    {
        private const string EnvironmentVariableName = "RADEDIT_DEBUG_LOG";
        private const string EnableFlagFileName = "debug-log.enabled";
        private static readonly object LogLock = new();
        private static readonly Lazy<bool> Enabled = new(IsEnabledCore);
        private static readonly Lazy<string> LogPathValue = new(InitializeLogPath);

        public static bool IsEnabled
        {
            get
            {
                bool enabled = Enabled.Value;
                if (enabled)
                {
                    EnsureLogFileExists();
                }

                return enabled;
            }
        }

        public static string LogPath => LogPathValue.Value;

        public static Stopwatch? StartTiming()
        {
            return IsEnabled ? Stopwatch.StartNew() : null;
        }

        public static void Write(string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            try
            {
                string path = LogPath;
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                lock (LogLock)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Best-effort logging only.
            }
        }

        public static void WriteSlowOperation(string operation, long elapsedMilliseconds, int thresholdMilliseconds, string? details = null)
        {
            if (!IsEnabled || elapsedMilliseconds < thresholdMilliseconds)
            {
                return;
            }

            string suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : " " + details;
            Write($"SLOW {operation} {elapsedMilliseconds} ms{suffix}");
        }

        private static bool IsEnabledCore()
        {
#if DEBUG
            return true;
#else
            string envValue = (Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? string.Empty).Trim();
            if (string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envValue, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string flagPath = Path.Combine(appData, "RadEdit", EnableFlagFileName);
            return File.Exists(flagPath);
#endif
        }

        private static void EnsureLogFileExists()
        {
            try
            {
                string path = LogPathValue.Value;
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(path))
                {
                    using FileStream _ = File.Create(path);
                }
            }
            catch
            {
                // Best-effort logging only.
            }
        }

        private static string InitializeLogPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "RadEdit", "radedit-debug.log");
        }
    }
}
