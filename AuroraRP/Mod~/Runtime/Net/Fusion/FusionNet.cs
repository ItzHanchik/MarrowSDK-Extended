#if AURORA_FUSION
using System;
using System.Collections.Generic;
using LabFusion.Entities;
using LabFusion.Network;
using LabFusion.Player;
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

        private bool _helloSent;
        private float _lastPeerPoll;

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

            if (Time.realtimeSinceStartup - _lastPeerPoll < 1f)
            {
                return;
            }

            _lastPeerPoll = Time.realtimeSinceStartup;
            PollPeers();
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
                    RaisePeerLeft(gone);
                }
            }

            _knownIds.Clear();
            foreach (var id in current)
            {
                _knownIds.Add(id);
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
