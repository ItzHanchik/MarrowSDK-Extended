using System;
using System.IO;
using System.Text;

namespace AuroraRP
{
    /// <summary>
    /// Сохранение состояния сессии, профиля игрока и журнала транзакций.
    /// Всё лежит в MelonLoader/UserData/AuroraRP/
    /// </summary>
    public static class AuroraStorage
    {
        public static string StatePath => Path.Combine(AuroraUtils.UserDataDirectory, "session.json");
        public static string ProfilePath => Path.Combine(AuroraUtils.UserDataDirectory, "profile.json");
        public static string TransactionPath => Path.Combine(AuroraUtils.UserDataDirectory, "transactions.log");
        public static string BankCachePath => Path.Combine(AuroraUtils.UserDataDirectory, "discord_bank_cache.json");

        private static float _lastTransactionFlush;
        private static readonly StringBuilder PendingTransactions = new StringBuilder();

        // ------------------------------------------------------------------ сессия

        public static void SaveState(AuroraState state)
        {
            if (state == null)
            {
                return;
            }

            try
            {
                File.WriteAllText(StatePath, state.ToSnapshot().ToJson(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "save state");
            }
        }

        public static AuroraSnapshot LoadState()
        {
            try
            {
                if (File.Exists(StatePath))
                {
                    return AuroraSnapshot.FromJson(File.ReadAllText(StatePath, Encoding.UTF8));
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "load state");
            }

            return null;
        }

        // ------------------------------------------------------------------ профиль

        /// <summary>Локальный профиль: выбранный язык, громкость, последняя роль (для быстрого входа).</summary>
        [Serializable]
        public class Profile
        {
            public string language = "ru";
            public float sfxVolume = 0.7f;
            public float menuScale = 1f;
            public bool wristHud = true;
            public bool haptics = true;
            public long lastKnownBalance = 5000;
            public int lastRole = 0;
            public string discordWebhookObfuscated = "";
        }

        public static Profile LoadProfile()
        {
            try
            {
                if (File.Exists(ProfilePath))
                {
                    var p = AuroraJson.Read<Profile>(File.ReadAllText(ProfilePath, Encoding.UTF8));
                    if (p != null)
                    {
                        return p;
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "load profile");
            }

            return new Profile();
        }

        public static void SaveProfile(Profile profile)
        {
            if (profile == null)
            {
                return;
            }

            try
            {
                File.WriteAllText(ProfilePath, AuroraJson.Write(profile), Encoding.UTF8);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "save profile");
            }
        }

        // -------------------------------------------------------- журнал транзакций

        public static void LogTransaction(byte playerId, long before, long after, string reason)
        {
            long delta = after - before;
            string sign = delta >= 0 ? "+" : "-";

            PendingTransactions.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] id=").Append(playerId)
                .Append(' ').Append(sign).Append(Math.Abs(delta)).Append(" (").Append(before).Append(" -> ").Append(after)
                .Append(") ").Append(reason).Append('\n');

            if (UnityEngine.Time.realtimeSinceStartup - _lastTransactionFlush > 3f)
            {
                FlushTransactions();
            }
        }

        public static void FlushTransactions()
        {
            if (PendingTransactions.Length == 0)
            {
                return;
            }

            try
            {
                File.AppendAllText(TransactionPath, PendingTransactions.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "flush transactions");
            }
            finally
            {
                PendingTransactions.Clear();
                _lastTransactionFlush = UnityEngine.Time.realtimeSinceStartup;
            }
        }
    }
}
