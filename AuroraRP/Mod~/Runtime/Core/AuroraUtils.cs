using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Мелкие утилиты, которые используются везде.
    /// </summary>
    public static class AuroraUtils
    {
        /// <summary>Папка данных мода: MelonLoader/UserData/AuroraRP</summary>
        public static string UserDataDirectory
        {
            get
            {
                if (_userData == null)
                {
                    string melon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
                    if (!Directory.Exists(melon))
                    {
                        melon = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuroraRP");
                    }

                    _userData = Path.Combine(melon, "AuroraRP");
                    Directory.CreateDirectory(_userData);
                }

                return _userData;
            }
        }

        private static string _userData;

        // ---------------------------------------------------------------- цвета

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return Color.white;
            }

            if (hex[0] == '#')
            {
                hex = hex.Substring(1);
            }

            if (hex.Length < 6)
            {
                return Color.white;
            }

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                byte a = 255;
                if (hex.Length >= 8)
                {
                    a = Convert.ToByte(hex.Substring(6, 2), 16);
                }

                return new Color32(r, g, b, a);
            }
            catch
            {
                return Color.white;
            }
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ------------------------------------------------------------- формат

        public static string Money(long amount)
        {
            return "$" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
        }

        public static string ShortMoney(long amount)
        {
            if (amount >= 1000000)
            {
                return "$" + (amount / 1000000f).ToString("0.#") + "M";
            }

            if (amount >= 10000)
            {
                return "$" + (amount / 1000f).ToString("0.#") + "K";
            }

            return "$" + amount;
        }

        public static string Time(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        // ------------------------------------------------------- главный поток

        private static readonly Queue<Action> MainThreadQueue = new Queue<Action>();
        private static readonly object MainThreadLock = new object();
        private static int _mainThreadId = -1;

        /// <summary>Вызывается один раз из AuroraDriver в Awake.</summary>
        public static void CaptureMainThread()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread => _mainThreadId == -1 || System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>Выполнить код в главном потоке Unity (безопасно вызывать из фоновых потоков).</summary>
        public static void RunOnMain(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (IsMainThread)
            {
                action();
                return;
            }

            lock (MainThreadLock)
            {
                MainThreadQueue.Enqueue(action);
            }
        }

        /// <summary>Дренаж очереди — вызывается из Update драйвера.</summary>
        public static void PumpMainThread()
        {
            while (true)
            {
                Action a;
                lock (MainThreadLock)
                {
                    if (MainThreadQueue.Count == 0)
                    {
                        return;
                    }

                    a = MainThreadQueue.Dequeue();
                }

                try
                {
                    a();
                }
                catch (Exception e)
                {
                    AuroraLog.Exception(e, "main thread action");
                }
            }
        }

        public static Coroutine RunCoroutine(IEnumerator routine)
        {
            if (AuroraDriver.Instance == null)
            {
                return null;
            }

            return AuroraDriver.Instance.StartCoroutine(routine);
        }

        // ------------------------------------------------------------ обфускация

        /// <summary>
        /// XOR + Base64. Это НЕ криптография (ключ лежит внутри мода), но вебхук не лежит в конфиге открытым текстом.
        /// </summary>
        public static string Obfuscate(string plain)
        {
            if (string.IsNullOrEmpty(plain))
            {
                return string.Empty;
            }

            byte[] data = Encoding.UTF8.GetBytes(plain);
            byte[] key = Encoding.UTF8.GetBytes(ObfuscationKey);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i] ^ key[i % key.Length] ^ (byte)(i * 31));
            }

            return Convert.ToBase64String(data);
        }

        public static string Deobfuscate(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return string.Empty;
            }

            try
            {
                byte[] data = Convert.FromBase64String(encoded);
                byte[] key = Encoding.UTF8.GetBytes(ObfuscationKey);

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)(data[i] ^ key[i % key.Length] ^ (byte)(i * 31));
                }

                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public const string ObfuscationKey = "AuroraRP::Bank::v1::StressLevelZero";

        // ---------------------------------------------------------------- прочее

        public static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, Mathf.Max(0, max - 1)) + "…";
        }

        public static float DistanceSqr(Transform a, Transform b)
        {
            if (a == null || b == null)
            {
                return float.MaxValue;
            }

            return (a.position - b.position).sqrMagnitude;
        }

        /// <summary>Стабильный хеш строки (одинаков на всех машинах, в отличие от string.GetHashCode).</summary>
        public static string StableHash(string text)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                uint hash = offset;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= prime;
                }

                return hash.ToString("X8");
            }
        }

        public static T FindObjectOfType<T>() where T : UnityEngine.Object
        {
            try
            {
                return UnityEngine.Object.FindObjectOfType<T>();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "FindObjectOfType<" + typeof(T).Name + ">");
                return null;
            }
        }

        /// <summary>Ищет компонент по имени типа — нужно для скриптов из паков (minecart.FunctionalDoor и т.п.), которых нет на этапе компиляции.</summary>
        public static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            try
            {
                var components = go.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    var c = components[i];
                    if (c == null)
                    {
                        continue;
                    }

                    string full = c.GetIl2CppType() != null ? c.GetIl2CppType().FullName : c.GetType().Name;
                    if (full == typeName || full.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return c;
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "FindComponentByTypeName " + typeName);
            }

            return null;
        }
    }
}
