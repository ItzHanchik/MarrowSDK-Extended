using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>Типы сетевых сообщений AuroraRP.</summary>
    public enum AuroraMsgType : byte
    {
        Hello = 1,       // клиент -> хост: я в игре
        StateSync = 2,   // хост -> клиенты: полный снимок состояния
        Notify = 3,      // уведомление конкретному игроку
        SpawnItem = 4,   // хост -> клиент: заспавни предмет рядом со мной
        Action = 5,      // запрос клиента к хосту (см. AuroraAction)

        /// <summary>Хост просит клиента проиграть эффект из палета (деньги, покупка...).</summary>
        FxEvent = 6
    }

    /// <summary>Действия, которые применяет хост.</summary>
    public enum AuroraAction : byte
    {
        Transfer = 1,
        RoleChange = 2,
        DoorBuy = 3,
        DoorSell = 4,
        ShopPurchase = 5,
        ContractCreate = 6,
        ContractAccept = 7,
        ContractCancel = 8,
        Rob = 9,
        PrinterSteal = 10,
        PrinterPayout = 11,
        GiveAll = 12,
        Reset = 13,
        StateRequest = 14,

        /// <summary>Клиент сообщает хосту выставленную сумму перевода (нужно для игроков без мода).</summary>
        TransferIntent = 15
    }

    /// <summary>Участник сессии.</summary>
    public sealed class AuroraPeer
    {
        public byte Id;
        public string Name = "";

        public AuroraPeer(byte id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>Сообщение AuroraRP.</summary>
    public sealed class AuroraNetMsg
    {
        public AuroraMsgType Type;
        public byte Sender;
        public byte[] Data;

        public AuroraNetMsg(AuroraMsgType type, byte sender, byte[] data)
        {
            Type = type;
            Sender = sender;
            Data = data ?? Array.Empty<byte>();
        }
    }

    /// <summary>Кодирование/декодирование полезной нагрузки сообщений.</summary>
    public static class AuroraWire
    {
        public static byte[] Build(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                write(writer);
            }

            return stream.ToArray();
        }

        public static void Read(byte[] data, Action<BinaryReader> read)
        {
            using var stream = new MemoryStream(data ?? Array.Empty<byte>());
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            read(reader);
        }
    }

    /// <summary>
    /// Сетевой слой мода. Реализации: одиночная (OfflineNet) и LabFusion (FusionNet).
    /// </summary>
    public interface IAuroraNet
    {
        string Description { get; }
        bool IsConnected { get; }
        bool IsHost { get; }
        byte HostId { get; }
        byte LocalId { get; }
        string LocalName { get; }
        int PeerCount { get; }
        IReadOnlyList<AuroraPeer> Peers { get; }

        event Action<AuroraNetMsg> MessageReceived;
        event Action<AuroraPeer> PeerJoined;
        event Action<byte> PeerLeft;

        /// <summary>Игрок подключился без AuroraRP (у него нет метаданных мода).</summary>
        event Action<AuroraPeer> PeerMissingMod;

        void Tick(float delta);
        void Send(AuroraNetMsg msg, byte? target = null);
        void Shutdown();

        bool TryGetPeerHands(byte peerId, out Vector3? left, out Vector3? right);
        bool TryGetPeerHead(byte peerId, out Vector3 head);
        bool IsPlayerDead(byte peerId);

        /// <summary>Кто в сессии играет без AuroraRP (нужен мод-длл).</summary>
        IReadOnlyList<AuroraPeer> PeersWithoutMod { get; }

        /// <summary>
        /// Спавн через сетевой слой: в Fusion предмет появится у всех и станет сетевым.
        /// false — сеть недоступна, спавним локально (AssetSpawner).
        /// </summary>
        bool TryNetworkSpawn(string barcode, Vector3 position, Quaternion rotation, Action<GameObject> callback);

        /// <summary>Палет мода уже установлен у этого игрока.</summary>
        bool HasContentPallet { get; }

        /// <summary>Строка состояния контента для меню (может быть null).</summary>
        string ContentPalletStatus { get; }

        /// <summary>
        /// Пытается подтянуть палет AuroraRP с mod.io (через LabFusion).
        /// true — палет уже есть или загрузка началась.
        /// </summary>
        bool SyncContentPallet(bool force);

        // --- высокоуровневые заявки (все проходят через хоста)
        void SendTransferRequest(byte from, byte to, long amount);
        void SendRoleRequest(byte player, AuroraRoleId role);
        void SendDoorRequest(byte player, string doorHash, byte action);
        void SendShopPurchase(byte seller, byte buyer, string barcode, int price, string title);
        void SendContractCreate(byte client, byte target, long reward);
        void SendContractAccept(int contractId, byte hitmanId);
        void SendContractCancel(int contractId, byte requesterId);
        void SendRobRequest(byte robber, byte victim, int percent);
        void SendPrinterSteal(string hash, byte newOwner);
        void SendPrinterPayout(string hash, byte owner, int amount);
        void SendNotification(byte target, string text, string colorHex);
        void SendSpawnForPlayer(byte target, string barcode);
        void RequestRoleChange(AuroraRoleId role);
        void NotifyLocalDeath();
    }

    /// <summary>Общая часть реализаций сети: конструкторы сообщений.</summary>
    public abstract class AbstractAuroraNet : IAuroraNet
    {
        public abstract string Description { get; }
        public abstract bool IsConnected { get; }
        public abstract bool IsHost { get; }
        public abstract byte LocalId { get; }
        public virtual byte HostId => 0;
        public abstract string LocalName { get; }
        public abstract int PeerCount { get; }
        public abstract IReadOnlyList<AuroraPeer> Peers { get; }

        public event Action<AuroraNetMsg> MessageReceived;
        public event Action<AuroraPeer> PeerJoined;
        public event Action<byte> PeerLeft;
        public event Action<AuroraPeer> PeerMissingMod;

        public abstract void Tick(float delta);
        public abstract void Send(AuroraNetMsg msg, byte? target = null);

        public virtual void Shutdown() { }

        public virtual IReadOnlyList<AuroraPeer> PeersWithoutMod => EmptyPeers;

        public virtual bool TryNetworkSpawn(string barcode, Vector3 position, Quaternion rotation, Action<GameObject> callback) => false;

        public virtual bool HasContentPallet
        {
            get
            {
                try
                {
                    return AuroraRuntime.Spawn != null && AuroraRuntime.Spawn.Exists(AuroraConfig.Current.contentBarcode, out _);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public virtual string ContentPalletStatus => null;

        public virtual bool SyncContentPallet(bool force) => false;

        protected static readonly List<AuroraPeer> EmptyPeers = new List<AuroraPeer>();

        /// <summary>Стоит ли у игрока наш мод. Для игроков без мода работает «мост» хоста.</summary>
        public virtual bool HasMod(byte peerId) => true;

        public virtual bool TryGetPeerHands(byte peerId, out Vector3? left, out Vector3? right)
        {
            left = null;
            right = null;
            return false;
        }

        public virtual bool TryGetPeerHead(byte peerId, out Vector3 head)
        {
            head = Vector3.zero;
            return false;
        }

        public virtual bool IsPlayerDead(byte peerId) => false;

        protected void RaiseMessage(AuroraNetMsg msg)
        {
            try
            {
                MessageReceived?.Invoke(msg);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "net message " + msg.Type);
            }
        }

        protected void RaisePeerJoined(AuroraPeer peer)
        {
            try
            {
                PeerJoined?.Invoke(peer);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "peer joined");
            }
        }

        protected void RaisePeerLeft(byte id)
        {
            try
            {
                PeerLeft?.Invoke(id);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "peer left");
            }
        }

        protected void RaisePeerMissingMod(AuroraPeer peer)
        {
            try
            {
                PeerMissingMod?.Invoke(peer);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "peer missing mod");
            }
        }

        // ------------------------------------------------------------- помощники

        protected AuroraNetMsg Action(AuroraAction action, Action<BinaryWriter> write)
        {
            return new AuroraNetMsg(AuroraMsgType.Action, LocalId, AuroraWire.Build(w =>
            {
                w.Write((byte)action);
                write?.Invoke(w);
            }));
        }

        /// <summary>Сообщает хосту, какая сумма сейчас выставлена на перевод (0 = ничего).</summary>
        public void SendTransferIntent(long amount)
        {
            Send(Action(AuroraAction.TransferIntent, w =>
            {
                w.Write(LocalId);
                w.Write(amount);
            }));
        }

        public void SendTransferRequest(byte from, byte to, long amount)
        {
            Send(Action(AuroraAction.Transfer, w =>
            {
                w.Write(from);
                w.Write(to);
                w.Write(amount);
            }));
        }

        public void SendRoleRequest(byte player, AuroraRoleId role)
        {
            Send(Action(AuroraAction.RoleChange, w =>
            {
                w.Write(player);
                w.Write((byte)role);
            }));
        }

        public void SendDoorRequest(byte player, string doorHash, byte action)
        {
            var act = action == 0 ? AuroraAction.DoorBuy : AuroraAction.DoorSell;

            Send(Action(act, w =>
            {
                w.Write(player);
                w.Write(doorHash ?? "");
            }));
        }

        public void SendShopPurchase(byte seller, byte buyer, string barcode, int price, string title)
        {
            Send(Action(AuroraAction.ShopPurchase, w =>
            {
                w.Write(seller);
                w.Write(buyer);
                w.Write(barcode ?? "");
                w.Write(price);
                w.Write(title ?? "");
            }));
        }

        public void SendContractCreate(byte client, byte target, long reward)
        {
            Send(Action(AuroraAction.ContractCreate, w =>
            {
                w.Write(client);
                w.Write(target);
                w.Write(reward);
            }));
        }

        public void SendContractAccept(int contractId, byte hitmanId)
        {
            Send(Action(AuroraAction.ContractAccept, w =>
            {
                w.Write(contractId);
                w.Write(hitmanId);
            }));
        }

        public void SendContractCancel(int contractId, byte requesterId)
        {
            Send(Action(AuroraAction.ContractCancel, w =>
            {
                w.Write(contractId);
                w.Write(requesterId);
            }));
        }

        public void SendRobRequest(byte robber, byte victim, int percent)
        {
            Send(Action(AuroraAction.Rob, w =>
            {
                w.Write(robber);
                w.Write(victim);
                w.Write(percent);
            }));
        }

        public void SendPrinterSteal(string hash, byte newOwner)
        {
            Send(Action(AuroraAction.PrinterSteal, w =>
            {
                w.Write(hash ?? "");
                w.Write(newOwner);
            }));
        }

        public void SendPrinterPayout(string hash, byte owner, int amount)
        {
            Send(Action(AuroraAction.PrinterPayout, w =>
            {
                w.Write(hash ?? "");
                w.Write(owner);
                w.Write(amount);
            }));
        }

        /// <summary>Просит игрока проиграть эффект из палета (например, «money.receive»).</summary>
        public void SendFxEvent(byte target, string name)
        {
            if (target == LocalId)
            {
                AuroraVisuals.PlayFxEvent(name);
                return;
            }

            var data = AuroraWire.Build(w => w.Write(name ?? ""));

            Send(new AuroraNetMsg(AuroraMsgType.FxEvent, LocalId, data), target);
        }

        public void SendNotification(byte target, string text, string colorHex)
        {
            var data = AuroraWire.Build(w =>
            {
                w.Write(text ?? "");
                w.Write(colorHex ?? "FFFFFF");
            });

            Send(new AuroraNetMsg(AuroraMsgType.Notify, LocalId, data), target);
        }

        public void SendSpawnForPlayer(byte target, string barcode)
        {
            var data = AuroraWire.Build(w => w.Write(barcode ?? ""));
            Send(new AuroraNetMsg(AuroraMsgType.SpawnItem, LocalId, data), target);
        }

        public void RequestRoleChange(AuroraRoleId role)
        {
            SendRoleRequest(LocalId, role);
        }

        public void NotifyLocalDeath()
        {
            // Хосту достаточно состояния; здесь можно расширить логику (например, штрафы).
        }

        public void SendHello()
        {
            var data = AuroraWire.Build(w => w.Write(LocalName ?? "Player"));
            Send(new AuroraNetMsg(AuroraMsgType.Hello, LocalId, data));
        }
    }

    /// <summary>
    /// Одиночный режим: сообщения заворачиваются обратно в очередь и обрабатываются
    /// тем же кодом хоста — так одна и та же логика работает и в соло, и в сети.
    /// </summary>
    public sealed class OfflineNet : AbstractAuroraNet
    {
        public override string Description => "Локальный режим (одиночная игра)";
        public override bool IsConnected => false;
        public override bool IsHost => true;
        public override byte LocalId => 0;
        public override string LocalName => _localName;

        public override int PeerCount => 0;
        public override IReadOnlyList<AuroraPeer> Peers => EmptyPeers;

        private readonly Queue<AuroraNetMsg> _loopback = new Queue<AuroraNetMsg>();
        private string _localName;

        public OfflineNet()
        {
            _localName = "You";
        }

        public override void Tick(float delta)
        {
            while (_loopback.Count > 0)
            {
                RaiseMessage(_loopback.Dequeue());
            }
        }

        public override void Send(AuroraNetMsg msg, byte? target = null)
        {
            if (target.HasValue && target.Value != LocalId)
            {
                return;
            }

            _loopback.Enqueue(msg);
        }

        /// <summary>В одиночной игре проверяем здоровье локального игрока.</summary>
        public override bool IsPlayerDead(byte peerId)
        {
            if (peerId != LocalId)
            {
                return false;
            }

            try
            {
                var rig = BoneLib.Player.RigManager;
                return rig != null && rig.health != null && !rig.health.alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void SetLocalName(string name)
        {
            _localName = string.IsNullOrWhiteSpace(name) ? "You" : name;
        }
    }

    /// <summary>Выбор сетевой реализации в зависимости от наличия LabFusion.</summary>
    public static class NetFactory
    {
        public static IAuroraNet Create()
        {
#if AURORA_FUSION
            try
            {
                var fusion = new FusionNet();
                AuroraLog.Info("Найден LabFusion — включена сетевая игра");
                return fusion;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "LabFusion init");
            }
#endif

            var offline = new OfflineNet();

            try
            {
                var rm = BoneLib.Player.RigManager;
                if (rm != null)
                {
                    offline.SendHello();
                }
            }
            catch (Exception)
            {
            }

            return offline;
        }
    }
}
