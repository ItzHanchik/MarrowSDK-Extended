#if AURORA_FUSION
using System;
using System.Collections.Generic;
using LabFusion.Downloading;
using LabFusion.Downloading.ModIO;
using LabFusion.Entities;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.RPC;
using LabFusion.SDK.Modules;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Данные, которые AuroraRP гоняет по сети LabFusion.
    /// Одно универсальное сообщение: тип + полезная нагрузка.
    /// </summary>
    public class AuroraFusionData : LabFusion.Network.Serialization.INetSerializable
    {
        public byte Type;
        public byte Sender;
        public byte[] Payload;

        public int? GetSize() => null; // Fusion сам подберёт буфер

        public void Serialize(LabFusion.Network.Serialization.INetSerializer serializer)
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Sender);
            serializer.SerializeValue(ref Payload);
        }
    }

    /// <summary>Обработчик сообщений AuroraRP в LabFusion.</summary>
    public class AuroraFusionMsgHandler : ModuleMessageHandler
    {
        public static FusionNet Instance;

        protected override void OnHandleMessage(ReceivedMessage received)
        {
            try
            {
                var data = received.ReadData<AuroraFusionData>();
                byte sender = received.Sender ?? data.Sender;

                Instance?.HandleIncoming(data.Type, sender, data.Payload);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "fusion handler");
            }
        }
    }

    /// <summary>Модуль AuroraRP для LabFusion: регистрирует сообщения.</summary>
    public class AuroraFusionModule : LabFusion.SDK.Modules.Module
    {
        public override string Name => "AuroraRP";
        public override string Author => AuroraRuntime.ModAuthor;
        public override Version Version => new Version(1, 0, 0);
        public override ConsoleColor Color => ConsoleColor.Cyan;

        protected override void OnModuleRegistered()
        {
            ModuleMessageManager.RegisterHandler<AuroraFusionMsgHandler>();
            AuroraLog.Info("Модуль AuroraRP зарегистрирован в LabFusion");
        }
    }

    /// <summary>
    /// Сетевая реализация через LabFusion: хост авторитетен, клиенты шлют заявки.
    /// </summary>
    public class FusionNet : AbstractAuroraNet
    {
        public override string Description => "LabFusion";
        public override bool IsConnected => NetworkInfo.HasServer;
        public override bool IsHost => NetworkInfo.IsHost;
        public override byte LocalId => PlayerIDManager.LocalSmallID;

        public override byte HostId => PlayerIDManager.HostSmallID;
        public override string LocalName => GetLocalName();

        public override int PeerCount => Mathf.Max(0, PlayerIDManager.PlayerCount - 1);

        private readonly List<AuroraPeer> _peers = new List<AuroraPeer>();
        private readonly HashSet<byte> _knownIds = new HashSet<byte>();

        public override IReadOnlyList<AuroraPeer> Peers => _peers;

        /// <summary>Игроки без AuroraRP (определяем по метаданным Fusion).</summary>
        private readonly List<AuroraPeer> _peersWithoutMod = new List<AuroraPeer>();
        private readonly HashSet<byte> _missingModReported = new HashSet<byte>();

        public override IReadOnlyList<AuroraPeer> PeersWithoutMod => _peersWithoutMod;

        private bool _helloSent;
        private float _lastPeerPoll;

        private bool _metadataApplied;
        private int _metadataAttempts;
        private float _lastMetadataAttempt;
        private bool _palletRequested;
        private int _palletAttempts;
        private float _lastPalletAttempt;

        /// <summary>Ключ в метаданных Fusion: «у меня установлен AuroraRP».</summary>
        private const string ModMetadataKey = "AuroraRP";

        /// <summary>Состояние загрузки палета — для меню.</summary>
        public static bool PalletDownloading { get; private set; }
        public static float PalletProgress { get; private set; }
        public static string PalletStatus { get; private set; } = "";

        public FusionNet()
        {
            AuroraFusionMsgHandler.Instance = this;

            try
            {
                ModuleManager.RegisterModule<AuroraFusionModule>();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "module register");
            }
        }

        public override void Shutdown()
        {
            AuroraFusionMsgHandler.Instance = null;
        }

        private static string GetLocalName()
        {
            try
            {
                var player = PlayerIDManager.LocalID;
                if (player != null)
                {
                    string name = player.Metadata?.Username?.GetValueOrEmpty();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            catch (Exception)
            {
            }

            return "Player " + PlayerIDManager.LocalSmallID;
        }

        public override void Tick(float delta)
        {
            if (!IsConnected)
            {
                return;
            }

            // Приветствие хосту — один раз на сессию.
            if (!_helloSent)
            {
                _helloSent = true;
                SendHello();
            }

            // Помечаем себя в метаданных: «этот игрок с AuroraRP».
            ApplyModMetadata();

            // Если палета мода нет — тянем его с mod.io (через LabFusion).
            if (AuroraConfig.Current.autoPullPallet)
            {
                TryAutoPullPallet();
            }

            if (Time.realtimeSinceStartup - _lastPeerPoll < 1f)
            {
                return;
            }

            _lastPeerPoll = Time.realtimeSinceStartup;
            PollPeers();
        }

        // ------------------------------------------------------- метаданные / контент

        private void ApplyModMetadata()
        {
            if (_metadataApplied)
            {
                return;
            }

            // Не спамим запросами: пока сервер применяет метаданные.
            if (Time.realtimeSinceStartup - _lastMetadataAttempt < 3f)
            {
                return;
            }

            if (_metadataAttempts++ > 20)
            {
                _metadataApplied = true;
                return;
            }

            _lastMetadataAttempt = Time.realtimeSinceStartup;

            try
            {
                var metadata = LocalPlayer.Metadata?.Metadata;
                if (metadata == null)
                {
                    return;
                }

                string current = metadata.GetMetadata(ModMetadataKey);

                if (current == AuroraRuntime.Version)
                {
                    _metadataApplied = true;
                    return;
                }

                // TrySetMetadata у LocalPlayer сам разошлёт значение по сети.
                LocalPlayer.Metadata.Metadata.TrySetMetadata(ModMetadataKey, AuroraRuntime.Version);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "apply metadata");
            }
        }

        /// <summary>Проверяет, стоит ли палет, и при необходимости качает его с mod.io.</summary>
        private void TryAutoPullPallet()
        {
            if (_palletRequested || PalletDownloading || !NetworkInfo.HasServer)
            {
                return;
            }

            // Пока палеты ещё не загружены (идёт уровень) — подождать.
            if (AuroraRuntime.Spawn == null || !AuroraRuntime.Spawn.IsReady)
            {
                return;
            }

            if (HasContentPallet)
            {
                _palletRequested = true;
                return;
            }

            var cfg = AuroraConfig.Current;

            if (cfg.modioModId <= 0 || _palletAttempts >= cfg.contentPullAttempts)
            {
                return;
            }

            if (Time.realtimeSinceStartup - _lastPalletAttempt < 20f)
            {
                return;
            }

            _lastPalletAttempt = Time.realtimeSinceStartup;
            _palletAttempts++;

            SyncContentPallet(false);
        }

        public override string ContentPalletStatus
        {
            get
            {
                if (HasContentPallet)
                {
                    return null;
                }

                if (PalletDownloading)
                {
                    return AuroraL.Get("content.pallet.starting") + " " + Mathf.RoundToInt(PalletProgress * 100f) + "%";
                }

                return string.IsNullOrWhiteSpace(PalletStatus) ? null : PalletStatus;
            }
        }

        /// <summary>
        /// Качает палет AuroraRP напрямую с mod.io по modioModId.
        /// Палет подгружается в рантайме — игру перезапускать не нужно.
        /// </summary>
        public override bool SyncContentPallet(bool force)
        {
            if (force)
            {
                _palletAttempts = 0;
                _palletRequested = false;
            }

            if (!NetworkInfo.HasServer)
            {
                AuroraRuntime.NotifyLocal(AuroraL.Get("content.pallet.failed"), UiTheme.Warning);
                return false;
            }

            if (HasContentPallet)
            {
                PalletStatus = AuroraL.Get("content.pallet.present");
                _palletRequested = true;
                return true;
            }

            int modId = AuroraConfig.Current.modioModId;

            if (modId <= 0)
            {
                AuroraRuntime.NotifyLocal(AuroraL.Get("content.pallet.noid"), UiTheme.Warning);
                PalletStatus = AuroraL.Get("content.pallet.noid");
                return false;
            }

            if (PalletDownloading || ModIODownloader.GetTransaction(modId) != null)
            {
                AuroraRuntime.NotifyLocal(AuroraL.Get("content.pallet.busy"), UiTheme.TextDim);
                return true;
            }

            try
            {
                PalletDownloading = true;
                PalletProgress = 0f;
                PalletStatus = AuroraL.Get("content.pallet.starting");

                AuroraRuntime.NotifyLocal(PalletStatus, UiTheme.Accent);

                var transaction = new ModTransaction
                {
                    ModFile = new ModIOFile(modId),
                    Temporary = false,
                    Callback = OnPalletDownloaded,
                    Reporter = new ProgressReporter(OnPalletProgress),
                };

                ModIODownloader.EnqueueDownload(transaction);
                return true;
            }
            catch (Exception e)
            {
                PalletDownloading = false;
                PalletStatus = AuroraL.Get("content.pallet.failed");
                AuroraLog.Exception(e, "pallet download");
                AuroraRuntime.NotifyLocal(PalletStatus, UiTheme.Danger);
                return false;
            }
        }

        private static void OnPalletProgress(float value)
        {
            PalletProgress = Mathf.Clamp01(value);
        }

        private void OnPalletDownloaded(DownloadCallbackInfo info)
        {
            PalletDownloading = false;
            _palletRequested = true;

            if (info.Result == ModResult.SUCCEEDED)
            {
                PalletProgress = 1f;
                PalletStatus = AuroraL.Get("content.pallet.done");
                AuroraLog.Info("Палет AuroraRP загружен: {0}", info.Pallet != null ? info.Pallet.Title : "?");
            }
            else
            {
                PalletStatus = AuroraL.Get("content.pallet.failed");
                AuroraLog.Warn("Палет AuroraRP скачать не удалось ({0})", info.Result);
            }

            string text = PalletStatus;
            Color color = info.Result == ModResult.SUCCEEDED ? UiTheme.Success : UiTheme.Warning;

            AuroraUtils.RunOnMain(() => AuroraRuntime.NotifyLocal(text, color));
        }

        /// <summary>Пробрасывает прогресс загрузки в UI (Fusion дёргает его не из главного потока).</summary>
        private sealed class ProgressReporter : IProgress<float>
        {
            private readonly Action<float> _onReport;
            private float _lastReported = -1f;

            public ProgressReporter(Action<float> onReport)
            {
                _onReport = onReport;
            }

            public void Report(float value)
            {
                if (_onReport == null)
                {
                    return;
                }

                // Не чаще, чем раз в 5%.
                if (Mathf.Abs(value - _lastReported) < 0.05f && value < 1f)
                {
                    return;
                }

                _lastReported = value;
                AuroraUtils.RunOnMain(() => _onReport(value));
            }
        }

        public void ForgetHello()
        {
            _helloSent = false;
        }

        private void PollPeers()
        {
            var current = new HashSet<byte>();

            try
            {
                foreach (var playerId in PlayerIDManager.PlayerIDs)
                {
                    byte id = playerId.SmallID;
                    current.Add(id);

                    if (_knownIds.Contains(id))
                    {
                        continue;
                    }

                    string name = "Player " + id;

                    try
                    {
                        string value = playerId.Metadata?.Username?.GetValueOrEmpty();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            name = value;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    var peer = new AuroraPeer(id, name);
                    _peers.Add(peer);
                    RaisePeerJoined(peer);

                    CheckModMetadata(peer, playerId);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "poll peers");
            }

            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                if (!current.Contains(_peers[i].Id))
                {
                    byte gone = _peers[i].Id;
                    _peers.RemoveAt(i);
                    RemoveMissingModPeer(gone);
                    RaisePeerLeft(gone);
                }
            }

            _knownIds.Clear();
            foreach (var id in current)
            {
                _knownIds.Add(id);
            }
        }

        /// <summary>Смотрит метаданные игрока: если мода нет — сообщаем хосту один раз.</summary>
        private void CheckModMetadata(AuroraPeer peer, PlayerID playerId)
        {
            try
            {
                if (peer.Id == LocalId)
                {
                    return;
                }

                string value = playerId?.Metadata?.Metadata?.GetMetadata(ModMetadataKey);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    RemoveMissingModPeer(peer.Id);
                    return;
                }

                if (!_missingModReported.Add(peer.Id))
                {
                    return;
                }

                _peersWithoutMod.Add(peer);
                RaisePeerMissingMod(peer);
                AuroraLog.Warn("Игрок {0} без AuroraRP (нет метаданных мода)", peer.Name);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "check mod metadata");
            }
        }

        private void RemoveMissingModPeer(byte id)
        {
            _missingModReported.Remove(id);

            for (int i = _peersWithoutMod.Count - 1; i >= 0; i--)
            {
                if (_peersWithoutMod[i].Id == id)
                {
                    _peersWithoutMod.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ спавн

        /// <summary>
        /// Сетевой спавн: предмет появится у всех игроков и станет сетевым.
        /// Если у кого-то нет палета — Fusion сам скачает его с mod.io и заспавнит предмет.
        /// </summary>
        public override bool TryNetworkSpawn(string barcode, Vector3 position, Quaternion rotation, Action<GameObject> callback)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(barcode))
            {
                return false;
            }

            try
            {
                var spawnable = SpawnService.CreateSpawnable(barcode);

                NetworkAssetSpawner.Spawn(new NetworkAssetSpawner.SpawnRequestInfo
                {
                    Spawnable = spawnable,
                    Position = position,
                    Rotation = rotation,
                    SpawnEffect = true,
                    SpawnSource = EntitySource.Player,
                    SpawnCallback = info =>
                    {
                        if (info.Spawned != null)
                        {
                            AuroraLog.Info("Сетевой спавн {0}: {1}", barcode, info.Spawned.name);
                            callback?.Invoke(info.Spawned);
                        }
                    },
                });

                return true;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "network spawn " + barcode);
                return false;
            }
        }

        // ------------------------------------------------------------------ отправка

        public override void Send(AuroraNetMsg msg, byte? target = null)
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                var data = new AuroraFusionData
                {
                    Type = (byte)msg.Type,
                    Sender = LocalId,
                    Payload = msg.Data
                };

                MessageRoute route = target.HasValue
                    ? new MessageRoute(target.Value, NetworkChannel.Reliable)
                    : CommonMessageRoutes.ReliableToOtherClients;

                MessageRelay.RelayModule<AuroraFusionMsgHandler, AuroraFusionData>(data, route);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "fusion send " + msg.Type);
            }
        }

        internal void HandleIncoming(byte type, byte sender, byte[] payload)
        {
            RaiseMessage(new AuroraNetMsg((AuroraMsgType)type, sender, payload));
        }

        // --------------------------------------------------------------- игроки

        public override bool TryGetPeerHands(byte peerId, out Vector3? left, out Vector3? right)
        {
            left = null;
            right = null;

            try
            {
                if (!NetworkPlayerManager.TryGetPlayer(peerId, out var player) || player == null || !player.HasRig)
                {
                    return false;
                }

                var refs = player.RigRefs;
                if (refs.LeftHand != null)
                {
                    left = refs.LeftHand.transform.position;
                }

                if (refs.RightHand != null)
                {
                    right = refs.RightHand.transform.position;
                }

                return left.HasValue || right.HasValue;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool TryGetPeerHead(byte peerId, out Vector3 head)
        {
            head = Vector3.zero;

            try
            {
                if (!NetworkPlayerManager.TryGetPlayer(peerId, out var player) || player == null || !player.HasRig)
                {
                    return false;
                }

                var refs = player.RigRefs;
                if (refs.Head == null)
                {
                    return false;
                }

                head = refs.Head.position;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool IsPlayerDead(byte peerId)
        {
            try
            {
                if (peerId == LocalId)
                {
                    var rig = BoneLib.Player.RigManager;
                    return rig != null && rig.health != null && !rig.health.alive;
                }

                if (!NetworkPlayerManager.TryGetPlayer(peerId, out var player) || player == null || !player.HasRig)
                {
                    return false;
                }

                var health = player.RigRefs.Health;
                return health != null && !health.alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
#endif
