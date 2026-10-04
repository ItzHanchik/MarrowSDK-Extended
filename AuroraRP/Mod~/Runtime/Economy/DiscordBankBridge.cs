using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Банк в Discord: у каждого игрока ОДНО сообщение в канале, которое мод создаёт при входе
    /// и потом только редактирует (новые сообщения не отправляются).
    /// Вебхук хранится обфусцированным (XOR + Base64) — это защищает от случайного просмотра,
    /// но не является криптографией: не публикуйте конфиг с вебхуком.
    /// </summary>
    public class DiscordBankBridge
    {
        [Serializable]
        private class MessageCache
        {
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        private class Entry
        {
            public string playerKey = "";
            public string messageId = "";
            public string channelId = "";
        }

        private readonly AuroraState _state;
        private readonly MessageCache _cache = new MessageCache();
        private readonly HashSet<string> _dirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _lastUpdate = new Dictionary<string, float>();

        private static readonly HttpClient Http = new HttpClient();
        private bool _busy;

        public bool IsEnabled => AuroraConfig.Current.discordBankEnabled && !string.IsNullOrEmpty(WebhookUrl);

        public string WebhookUrl
        {
            get
            {
                string encoded = AuroraConfig.Current.discordWebhookObfuscated;
                return string.IsNullOrEmpty(encoded) ? null : AuroraUtils.Deobfuscate(encoded);
            }
        }

        public DiscordBankBridge(AuroraState state)
        {
            _state = state;
            LoadCache();

            if (AuroraRuntime.Wallet != null)
            {
                AuroraRuntime.Wallet.OnBalanceChanged += OnBalanceChanged;
            }

            if (AuroraRuntime.Roles != null)
            {
                AuroraRuntime.Roles.OnRoleChanged += (player, oldRole, newRole) => MarkDirty(player);
            }
        }

        private void OnBalanceChanged(byte playerId, long before, long now, string reason)
        {
            MarkDirty(playerId);
        }

        public void MarkDirty(byte playerId)
        {
            if (!IsEnabled)
            {
                return;
            }

            var rec = _state.Get(playerId);
            if (rec == null)
            {
                return;
            }

            lock (_dirty)
            {
                _dirty.Add(PlayerKey(rec));
            }
        }

        public void MarkAllDirty()
        {
            foreach (var player in _state.Players)
            {
                MarkDirty(player.id);
            }
        }

        public void Tick(float dt)
        {
            if (!IsEnabled || _busy)
            {
                return;
            }

            // Клиенты не пишут в Discord: банк ведёт хост.
            if (AuroraRuntime.Net != null && !AuroraRuntime.Net.IsHost)
            {
                return;
            }

            string key = null;

            lock (_dirty)
            {
                float interval = Mathf.Max(5f, AuroraConfig.Current.discordUpdateInterval);

                foreach (var candidate in _dirty)
                {
                    if (_lastUpdate.TryGetValue(candidate, out float last) && Time.realtimeSinceStartup - last < interval)
                    {
                        continue;
                    }

                    key = candidate;
                    break;
                }
            }

            if (key == null)
            {
                return;
            }

            var rec = FindByKey(key);
            if (rec == null)
            {
                lock (_dirty)
                {
                    _dirty.Remove(key);
                }

                return;
            }

            lock (_dirty)
            {
                _dirty.Remove(key);
            }

            _lastUpdate[key] = Time.realtimeSinceStartup;
            _ = PublishAsync(rec);
        }

        private async Task PublishAsync(PlayerRecord record)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;

            try
            {
                string url = WebhookUrl;
                if (string.IsNullOrEmpty(url))
                {
                    return;
                }

                string key = PlayerKey(record);
                var entry = _cache.entries.Find(e => e.playerKey == key);
                string content = BuildMessage(record);

                string json = BuildPayload(content);

                if (entry == null || string.IsNullOrEmpty(entry.messageId))
                {
                    string response = await PostAsync(url + "?wait=true", json).ConfigureAwait(false);
                    string messageId = ExtractMessageId(response);

                    if (!string.IsNullOrEmpty(messageId))
                    {
                        entry ??= new Entry { playerKey = key };
                        entry.messageId = messageId;
                        entry.channelId = ExtractChannelId(response);

                        _cache.entries.RemoveAll(e => e.playerKey == key);
                        _cache.entries.Add(entry);

                        AuroraUtils.RunOnMain(() =>
                        {
                            SaveCache();
                            AuroraLog.Info("Банк Discord: создано сообщение для {0}", record.name);
                        });
                    }
                }
                else
                {
                    string editUrl = BuildEditUrl(url, entry.messageId);
                    await PatchAsync(editUrl, json).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Error("Discord: не удалось обновить банк — {0}", e.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private string BuildMessage(PlayerRecord record)
        {
            var sb = new StringBuilder();
            var role = RoleCatalog.Get(record.Role);
            var cfg = AuroraConfig.Current;

            sb.Append("**").Append(cfg.discordBankHeader).Append("**\n");
            sb.Append("```");
            sb.Append("\nИгрок:   ").Append(record.name);
            sb.Append("\nРоль:    ").Append(role.Name);
            sb.Append("\nБаланс:  ").Append(AuroraUtils.Money(record.balance));
            sb.Append("\nЛицензия: ").Append(record.hasLicense ? "есть" : "нет");

            var door = _state.FindDoorOf(record.id);
            if (door != null)
            {
                sb.Append("\nДверь:   ").Append(door.label);
            }

            int active = 0;
            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                if (_state.Contracts[i].targetId == record.id && _state.Contracts[i].status != ContractRecord.StatusDone)
                {
                    active++;
                }
            }

            if (active > 0)
            {
                sb.Append("\nКонтрактов на игрока: ").Append(active);
            }

            sb.Append("\nОбновлено: ").Append(DateTime.Now.ToString("HH:mm:ss"));
            sb.Append("\n```");

            return sb.ToString();
        }

        private static string BuildPayload(string content)
        {
            string escaped = content
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");

            string username = AuroraConfig.Current.discordBotName
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");

            return "{\"content\":\"" + escaped + "\",\"username\":\"" + username + "\"}";
        }

        private static string BuildEditUrl(string webhookUrl, string messageId)
        {
            int idx = webhookUrl.IndexOf("/webhooks/", StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? webhookUrl : webhookUrl.Substring(0, idx) + webhookUrl.Substring(idx) + "/messages/" + messageId;
        }

        private static async Task<string> PostAsync(string url, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(url, content).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static async Task PatchAsync(string url, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
        }

        private static string ExtractMessageId(string json)
        {
            return ExtractField(json, "\"id\":\"");
        }

        private static string ExtractChannelId(string json)
        {
            return ExtractField(json, "\"channel_id\":\"");
        }

        private static string ExtractField(string json, string marker)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            int idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                return null;
            }

            int start = idx + marker.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : null;
        }

        private static string PlayerKey(PlayerRecord record)
        {
            // Ключ — имя игрока (стабильно между сессиями).
            return record.name;
        }

        private PlayerRecord FindByKey(string key)
        {
            foreach (var player in _state.Players)
            {
                if (PlayerKey(player) == key)
                {
                    return player;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ кэш

        private void LoadCache()
        {
            try
            {
                if (File.Exists(AuroraStorage.BankCachePath))
                {
                    var loaded = AuroraJson.Read<MessageCache>(File.ReadAllText(AuroraStorage.BankCachePath));
                    if (loaded?.entries != null)
                    {
                        _cache.entries = loaded.entries;
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "bank cache load");
            }
        }

        private void SaveCache()
        {
            try
            {
                File.WriteAllText(AuroraStorage.BankCachePath, AuroraJson.Write(_cache));
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "bank cache save");
            }
        }

        public void FlushNow()
        {
            SaveCache();
        }
    }
}
