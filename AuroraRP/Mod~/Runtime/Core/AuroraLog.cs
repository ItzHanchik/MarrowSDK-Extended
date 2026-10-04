using System;
using System.Diagnostics;
using MelonLoader;

namespace AuroraRP
{
    /// <summary>
    /// Единая точка логирования AuroraRP.
    /// Пишет и в консоль MelonLoader, и (опционально) в файл UserData/AuroraRP/aurora.log
    /// </summary>
    public static class AuroraLog
    {
        public enum Level
        {
            Debug = 0,
            Info = 1,
            Warn = 2,
            Error = 3
        }

        /// <summary>Минимальный уровень, который попадает в лог. Меняется из конфига.</summary>
        public static Level MinLevel = Level.Info;

        private const string Prefix = "<color=#7FD8FF>[AuroraRP]</color> ";

        public static void Debug(string message, params object[] args) => Write(Level.Debug, message, args);
        public static void Info(string message, params object[] args) => Write(Level.Info, message, args);
        public static void Warn(string message, params object[] args) => Write(Level.Warn, message, args);
        public static void Error(string message, params object[] args) => Write(Level.Error, message, args);

        public static void Exception(Exception e, string context)
        {
            Error("{0}: {1}\n{2}", context, e.Message, e.StackTrace);
        }

        private static void Write(Level level, string message, object[] args)
        {
            if (level < MinLevel)
            {
                return;
            }

            string text;
            try
            {
                text = (args != null && args.Length > 0) ? string.Format(message, args) : message;
            }
            catch (FormatException)
            {
                text = message;
            }

            string line = level switch
            {
                Level.Debug => "<color=#8AB4F8>[D]</color> " + text,
                Level.Info => text,
                Level.Warn => "<color=#FFD166>⚠ " + text + "</color>",
                Level.Error => "<color=#FF5C5C>✖ " + text + "</color>",
                _ => text
            };

            try
            {
                MelonLogger.Msg(Prefix + line);
            }
            catch
            {
                // Консоль MelonLoader может быть недоступна при горячей перезагрузке — тогда пишем только в файл.
            }

            AuroraFileLog.Append(level, text);
        }

        [Conditional("AURORA_TRACE")]
        public static void Trace(string message, params object[] args) => Debug(message, args);
    }
}
