using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>Снимок состояния одного игрока (сериализуется в JSON).</summary>
    [Serializable]
    public class PlayerRecord
    {
        public byte id;
        public string name = "";
        public int role = 0;
        public long balance;
        public bool hasLicense;
        public string doorHash = "";
        public bool connected = true;

        public AuroraRoleId Role => (AuroraRoleId)Mathf.Clamp(role, 0, RoleCatalog.MaxRoleIndex);

        public PlayerRecord Clone()
        {
            return new PlayerRecord
            {
                id = id,
                name = name,
                role = role,
                balance = balance,
                hasLicense = hasLicense,
                doorHash = doorHash,
                connected = connected
            };
        }
    }

    /// <summary>Снимок двери.</summary>
    [Serializable]
    public class DoorRecord
    {
        public string hash = "";
        public byte ownerId = 255;
        public string ownerName = "";
        public string label = "";

        public bool IsOwned => ownerId != 255;
    }

    /// <summary>Контракт наёмного убийцы.</summary>
    [Serializable]
    public class ContractRecord
    {
        public const int StatusPending = 0;
        public const int StatusAccepted = 1;
        public const int StatusDone = 2;
        public const int StatusCancelled = 3;

        public int id;
        public byte clientId;
        public string clientName = "";
        public byte targetId;
        public string targetName = "";
        public long reward;
        public int status;
        public float created;
    }

    /// <summary>Полный снимок состояния сессии: игроки, двери, контракты.</summary>
    [Serializable]
    public class AuroraSnapshot
    {
        public int revision;
        public List<PlayerRecord> players = new List<PlayerRecord>();
        public List<DoorRecord> doors = new List<DoorRecord>();
        public List<ContractRecord> contracts = new List<ContractRecord>();
        public int nextContractId = 1;

        public string ToJson() => JsonUtility.ToJson(this);

        public static AuroraSnapshot FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new AuroraSnapshot();
            }

            try
            {
                var snap = JsonUtility.FromJson<AuroraSnapshot>(json);
                if (snap == null)
                {
                    return new AuroraSnapshot();
                }

                snap.players ??= new List<PlayerRecord>();
                snap.doors ??= new List<DoorRecord>();
                snap.contracts ??= new List<ContractRecord>();
                return snap;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "snapshot parse");
                return new AuroraSnapshot();
            }
        }
    }

    /// <summary>
    /// Быстрый доступ к состоянию сессии + события для UI/сети.
    /// Хост (или одиночная игра) является источником правды.
    /// </summary>
    public class AuroraState
    {
        private readonly Dictionary<byte, PlayerRecord> _players = new Dictionary<byte, PlayerRecord>();
        private readonly Dictionary<string, DoorRecord> _doors = new Dictionary<string, DoorRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ContractRecord> _contracts = new List<ContractRecord>();

        public int Revision { get; private set; }

        public event Action<PlayerRecord> OnPlayerChanged;
        public event Action<DoorRecord> OnDoorChanged;
        public event Action<ContractRecord> OnContractChanged;
        public event Action OnChanged;

        public IEnumerable<PlayerRecord> Players => _players.Values;
        public IEnumerable<DoorRecord> Doors => _doors.Values;
        public IList<ContractRecord> Contracts => _contracts;

        public int PlayerCount => _players.Count;

        // ---------------------------------------------------------------- игроки

        public PlayerRecord GetOrCreate(byte id, string name)
        {
            if (!_players.TryGetValue(id, out var rec))
            {
                rec = new PlayerRecord
                {
                    id = id,
                    name = string.IsNullOrEmpty(name) ? ("Player " + id) : name,
                    role = (int)AuroraRoleId.Citizen,
                    balance = AuroraConfig.Current.startBalance,
                    hasLicense = false,
                    connected = true
                };

                _players[id] = rec;
                AuroraLog.Info("Зарегистрирован игрок {0} (id {1}) — старт {2}", rec.name, id, AuroraUtils.Money(rec.balance));
            }
            else if (!string.IsNullOrEmpty(name) && rec.name != name)
            {
                rec.name = name;
            }

            return rec;
        }

        public bool TryGet(byte id, out PlayerRecord rec) => _players.TryGetValue(id, out rec);

        public PlayerRecord Get(byte id) => _players.TryGetValue(id, out var rec) ? rec : null;

        public void Remove(byte id)
        {
            if (_players.Remove(id))
            {
                Revision++;
                OnChanged?.Invoke();
            }
        }

        public void MarkPlayerChanged(PlayerRecord rec)
        {
            Revision++;
            OnPlayerChanged?.Invoke(rec);
            OnChanged?.Invoke();
        }

        /// <summary>Все подключённые игроки (кроме локального), отсортированные по id.</summary>
        public List<PlayerRecord> OtherPlayers(byte localId)
        {
            var list = new List<PlayerRecord>();
            foreach (var p in _players.Values)
            {
                if (p.id != localId && p.connected)
                {
                    list.Add(p);
                }
            }

            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        // ---------------------------------------------------------------- двери

        public DoorRecord GetDoor(string hash)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return null;
            }

            _doors.TryGetValue(hash, out var rec);
            return rec;
        }

        public DoorRecord GetOrCreateDoor(string hash, string label)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return null;
            }

            if (!_doors.TryGetValue(hash, out var rec))
            {
                rec = new DoorRecord { hash = hash, ownerId = 255, label = label };
                _doors[hash] = rec;
            }
            else if (!string.IsNullOrEmpty(label))
            {
                rec.label = label;
            }

            return rec;
        }

        public void MarkDoorChanged(DoorRecord rec)
        {
            Revision++;
            OnDoorChanged?.Invoke(rec);
            OnChanged?.Invoke();
        }

        public void ReleaseDoorsOf(byte playerId)
        {
            List<DoorRecord> changed = null;
            foreach (var door in _doors.Values)
            {
                if (door.ownerId == playerId && door.IsOwned)
                {
                    door.ownerId = 255;
                    door.ownerName = "";
                    (changed ??= new List<DoorRecord>()).Add(door);
                }
            }

            if (changed != null)
            {
                foreach (var door in changed)
                {
                    MarkDoorChanged(door);
                }
            }
        }

        public DoorRecord FindDoorOf(byte playerId)
        {
            foreach (var door in _doors.Values)
            {
                if (door.ownerId == playerId && door.IsOwned)
                {
                    return door;
                }
            }

            return null;
        }

        // ------------------------------------------------------------- контракты

        public ContractRecord CreateContract(byte clientId, string clientName, byte targetId, string targetName, long reward)
        {
            var rec = new ContractRecord
            {
                id = _nextContractId++,
                clientId = clientId,
                clientName = clientName,
                targetId = targetId,
                targetName = targetName,
                reward = reward,
                status = ContractRecord.StatusPending,
                created = Time.realtimeSinceStartup
            };

            _contracts.Add(rec);
            Revision++;
            OnContractChanged?.Invoke(rec);
            OnChanged?.Invoke();
            return rec;
        }

        private int _nextContractId = 1;

        public ContractRecord FindContract(int id)
        {
            for (int i = 0; i < _contracts.Count; i++)
            {
                if (_contracts[i].id == id)
                {
                    return _contracts[i];
                }
            }

            return null;
        }

        public void MarkContractChanged(ContractRecord rec)
        {
            Revision++;
            OnContractChanged?.Invoke(rec);
            OnChanged?.Invoke();
        }

        public void RemoveContract(ContractRecord rec)
        {
            if (_contracts.Remove(rec))
            {
                Revision++;
                OnContractChanged?.Invoke(rec);
                OnChanged?.Invoke();
            }
        }

        public void ClearContractsFor(byte playerId)
        {
            _contracts.RemoveAll(c => c.clientId == playerId || c.targetId == playerId);
            Revision++;
            OnChanged?.Invoke();
        }

        // --------------------------------------------------------------- снимок

        public AuroraSnapshot ToSnapshot()
        {
            var snap = new AuroraSnapshot { revision = Revision, nextContractId = _nextContractId };

            foreach (var p in _players.Values)
            {
                snap.players.Add(p.Clone());
            }

            foreach (var d in _doors.Values)
            {
                snap.doors.Add(new DoorRecord
                {
                    hash = d.hash,
                    ownerId = d.ownerId,
                    ownerName = d.ownerName,
                    label = d.label
                });
            }

            snap.contracts.AddRange(_contracts);
            return snap;
        }

        /// <summary>Применяет снимок, пришедший по сети (клиентская сторона).</summary>
        public void ApplySnapshot(AuroraSnapshot snap, bool raiseEvents = true)
        {
            if (snap == null)
            {
                return;
            }

            byte localId = AuroraRuntime.Net != null ? AuroraRuntime.Net.LocalId : (byte)0;
            string localName = AuroraRuntime.Net != null ? AuroraRuntime.Net.LocalName : "Player";

            _players.Clear();
            foreach (var p in snap.players)
            {
                _players[p.id] = p.Clone();
            }

            // Локальный игрок должен существовать всегда, иначе меню пустое.
            if (!_players.ContainsKey(localId))
            {
                GetOrCreate(localId, localName);
            }

            _doors.Clear();
            foreach (var d in snap.doors)
            {
                _doors[d.hash] = d;
            }

            _contracts.Clear();
            _contracts.AddRange(snap.contracts);

            _nextContractId = Mathf.Max(1, snap.nextContractId);
            Revision = snap.revision;

            if (raiseEvents)
            {
                OnChanged?.Invoke();
            }
        }

        /// <summary>Полная очистка (новая сессия/уровень).</summary>
        public void Reset(bool keepPlayers = true)
        {
            if (!keepPlayers)
            {
                _players.Clear();
            }

            _doors.Clear();
            _contracts.Clear();
            _nextContractId = 1;
            Revision++;
        }
    }
}
