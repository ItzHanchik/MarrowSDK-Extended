using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using Il2CppSLZ.Marrow.Pool;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Недвижимость: поиск дверей, покупка (2000 $), продажа по B×3, максимум 1 дверь,
    /// запрет для полиции, освобождение двери при выходе владельца.
    /// </summary>
    public class DoorService
    {
        private readonly AuroraState _state;
        private readonly Dictionary<string, DoorBehaviour> _doors = new Dictionary<string, DoorBehaviour>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, DoorBehaviour> _byInstanceId = new Dictionary<int, DoorBehaviour>();

        private DoorBehaviour _heldDoor;
        private float _nextScanTime;
        private bool _inputSubscribed;

        public DoorBehaviour HeldDoor => _heldDoor;

        /// <summary>Все зарегистрированные двери (нужно хосту для игроков без мода).</summary>
        public IReadOnlyList<DoorBehaviour> AllDoors
        {
            get
            {
                if (_allDoors == null || _allDoors.Count != _doors.Count)
                {
                    _allDoors = new List<DoorBehaviour>(_doors.Values);
                }

                return _allDoors;
            }
        }

        private List<DoorBehaviour> _allDoors;

        public DoorService(AuroraState state)
        {
            _state = state;
        }

        // ---------------------------------------------------------------- события

        public void OnLevelLoaded()
        {
            ClearComponents();
            LoadFromState();
            RequestScan();
        }

        public void OnLevelUnloaded()
        {
            ClearComponents();
            _heldDoor = null;
        }

        private void ClearComponents()
        {
            _doors.Clear();
            _byInstanceId.Clear();
            _heldDoor = null;
        }

        public void RequestScan()
        {
            _nextScanTime = 0f;
        }

        private void SubscribeInput()
        {
            if (_inputSubscribed || AuroraRuntime.Input == null)
            {
                return;
            }

            AuroraRuntime.Input.OnBPressed += OnButtonB;
            _inputSubscribed = true;
        }

        // -------------------------------------------------------------------- тик

        public void Tick(float dt)
        {
            SubscribeInput();

            if (Time.realtimeSinceStartup >= _nextScanTime)
            {
                _nextScanTime = Time.realtimeSinceStartup + 3f;
                Scan();
                SweepDeadDoors();
            }
        }

        private void SweepDeadDoors()
        {
            List<string> dead = null;

            foreach (var pair in _doors)
            {
                if (pair.Value == null || pair.Value.Root == null)
                {
                    (dead ??= new List<string>()).Add(pair.Key);
                }
            }

            if (dead == null)
            {
                return;
            }

            foreach (var key in dead)
            {
                _doors.Remove(key);
            }

            if (_heldDoor != null && _heldDoor.Root == null)
            {
                _heldDoor = null;
            }
        }

        // ------------------------------------------------------------------ поиск

        /// <summary>Сканирует сцену и вешает логику на найденные двери (FunctionalDoor из пака minecart и др.).</summary>
        public void Scan()
        {
            try
            {
                var hosts = UnityEngine.Object.FindObjectsOfType<InteractableHost>();
                if (hosts == null)
                {
                    return;
                }

                for (int i = 0; i < hosts.Length; i++)
                {
                    var host = hosts[i];
                    if (host == null)
                    {
                        continue;
                    }

                    var go = host.gameObject;
                    if (!IsDoorCandidate(go, out string label, out string barcode))
                    {
                        continue;
                    }

                    int id = go.GetInstanceID();
                    if (_byInstanceId.ContainsKey(id))
                    {
                        continue;
                    }

                    RegisterDoor(go, label, barcode);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "door scan");
            }
        }

        /// <summary>Проверяет, похож ли объект на дверь из пака.</summary>
        public bool IsDoorCandidate(GameObject go, out string label, out string barcode)
        {
            label = go.name;
            barcode = null;

            if (go == null)
            {
                return false;
            }

            // 1) barcode из Poolee (самый надёжный путь).
            var poolee = go.GetComponentInParent<Poolee>();
            if (poolee != null)
            {
                try
                {
                    var crate = poolee.SpawnableCrate;
                    if (crate != null && crate.Barcode.ID != null)
                    {
                        barcode = crate.Barcode.ID;

                        if (ContainsHint(AuroraConfig.Current.doorBarcodeHints, barcode))
                        {
                            label = crate.Title ?? LastSegment(barcode);
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                    // не критично — идём дальше по эвристикам
                }
            }

            // 2) имя скрипта из пака (minecart.FunctionalDoor и т.п.)
            var components = go.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component is AuroraComponent)
                {
                    continue;
                }

                string typeName;
                try
                {
                    var il2cppType = component.GetIl2CppType();
                    typeName = il2cppType != null ? il2cppType.FullName : component.GetType().Name;
                }
                catch (Exception)
                {
                    typeName = component.GetType().Name;
                }

                if (ContainsHint(AuroraConfig.Current.doorBarcodeHints, typeName) || ContainsHint(AuroraConfig.Current.doorNameHints, typeName))
                {
                    if (typeName.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0 &&
                        typeName.IndexOf("двер", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    return true;
                }
            }

            // 3) имя объекта
            return ContainsHint(AuroraConfig.Current.doorNameHints, go.name);
        }

        private static bool ContainsHint(List<string> hints, string value)
        {
            if (hints == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < hints.Count; i++)
            {
                var hint = hints[i];
                if (!string.IsNullOrWhiteSpace(hint) && value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string LastSegment(string barcode)
        {
            if (string.IsNullOrEmpty(barcode))
            {
                return "Door";
            }

            int idx = barcode.LastIndexOf('.');
            return idx >= 0 && idx < barcode.Length - 1 ? barcode.Substring(idx + 1) : barcode;
        }

        private void RegisterDoor(GameObject go, string label, string barcode)
        {
            string hash = BuildDoorHash(go, barcode);
            var record = _state.GetOrCreateDoor(hash, label);

            var behaviour = new DoorBehaviour(go, hash, label, record);
            var component = AuroraComponent.Attach(go, "door", behaviour);

            if (component == null)
            {
                return;
            }

            _doors[hash] = behaviour;
            _byInstanceId[go.GetInstanceID()] = behaviour;

            AuroraLog.Info("Найдена дверь: {0} (hash {1}, владелец: {2})", label, hash, record.IsOwned ? record.ownerName : "нет");
        }

        /// <summary>
        /// Стабильный id двери, одинаковый у всех игроков: barcode + округлённые координаты.
        /// </summary>
        public static string BuildDoorHash(GameObject go, string barcode)
        {
            Vector3 p = go.transform.position;
            string posKey = string.Format("{0:0.0}_{1:0.0}_{2:0.0}", p.x, p.y, p.z);
            string key = (string.IsNullOrEmpty(barcode) ? go.name : barcode) + "@" + posKey;
            return AuroraUtils.StableHash(key);
        }

        // ------------------------------------------------------------------ захват

        public void OnGrabbed(GameObject go, Hand hand)
        {
            var host = go.GetComponentInParent<InteractableHost>();
            GameObject target = host != null ? host.gameObject : go;

            if (_byInstanceId.TryGetValue(target.GetInstanceID(), out var door))
            {
                _heldDoor = door;
                AuroraNotifications.Send(door.FooterHint, UiTheme.Accent);
                return;
            }

            if (IsDoorCandidate(target, out string label, out string barcode))
            {
                RegisterDoor(target, label, barcode);
                if (_byInstanceId.TryGetValue(target.GetInstanceID(), out door))
                {
                    _heldDoor = door;
                    AuroraNotifications.Send(door.FooterHint, UiTheme.Accent);
                }
            }
        }

        public void OnReleased(Hand hand)
        {
            _heldDoor = null;
        }

        private void OnButtonB(Handedness hand)
        {
            if (_heldDoor == null)
            {
                return;
            }

            _heldDoor.OnButtonB();
        }

        // ------------------------------------------------------------------ покупка

        public DoorBehaviour GetOwnedDoor(byte playerId)
        {
            foreach (var door in _doors.Values)
            {
                if (door.IsOwned && door.OwnerId == playerId)
                {
                    return door;
                }
            }

            return null;
        }

        public string OwnedDoorSummary()
        {
            var door = GetOwnedDoor(AuroraRuntime.LocalId);
            return door == null ? AuroraL.Get("door.none") : AuroraL.Get("door.owned.list", door.Label);
        }

        public List<DoorBehaviour> NearbyDoors(byte playerId, float radius)
        {
            var result = new List<DoorBehaviour>();
            Vector3 head;

            if (playerId == AuroraRuntime.LocalId)
            {
                var localHead = BoneLib.Player.Head;

                if (localHead == null)
                {
                    return result;
                }

                head = localHead.position;
            }
            else if (!AuroraRuntime.Net.TryGetPeerHead(playerId, out head))
            {
                return result;
            }

            float maxSqr = radius * radius;

            foreach (var door in _doors.Values)
            {
                if (door?.Root == null)
                {
                    continue;
                }

                if ((door.Root.transform.position - head).sqrMagnitude <= maxSqr)
                {
                    result.Add(door);
                }
            }

            result.Sort((a, b) =>
                (a.Root.transform.position - head).sqrMagnitude
                .CompareTo((b.Root.transform.position - head).sqrMagnitude));
            return result;
        }

        /// <summary>Клиент/хост: заявка на покупку (проверки делает хост).</summary>
        public void RequestPurchase(DoorBehaviour door)
        {
            if (door == null)
            {
                return;
            }

            var me = _state.GetOrCreate(AuroraRuntime.LocalId, AuroraRuntime.LocalName);

            if (door.IsOwned)
            {
                AuroraNotifications.Send(AuroraL.Get("door.owned.by", door.OwnerName), UiTheme.Warning);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            // Локальные проверки — чтобы игрок получил мгновенный ответ.
            var role = RoleCatalog.Get(me.Role);
            if (AuroraConfig.Current.doorPoliceForbidden && !role.CanOwnDoor)
            {
                AuroraNotifications.Send(AuroraL.Get("door.police.denied"), UiTheme.Danger);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            if (GetOwnedDoor(AuroraRuntime.LocalId) != null)
            {
                AuroraNotifications.Send(AuroraL.Get("door.already.owned"), UiTheme.Warning);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            if (me.balance < AuroraConfig.Current.doorPrice)
            {
                AuroraNotifications.Send(AuroraL.Get("door.too.expensive", AuroraUtils.Money(AuroraConfig.Current.doorPrice)), UiTheme.Danger);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            AuroraRuntime.Net.SendDoorRequest(AuroraRuntime.LocalId, door.Hash, 0);
        }

        public void RequestSale(DoorBehaviour door)
        {
            if (door == null || !door.IsOwned)
            {
                return;
            }

            if (door.OwnerId != AuroraRuntime.LocalId)
            {
                AuroraNotifications.Send(AuroraL.Get("common.error"), UiTheme.Danger);
                return;
            }

            AuroraRuntime.Net.SendDoorRequest(AuroraRuntime.LocalId, door.Hash, 1);
        }

        /// <summary>Применяет изменения двери (после решения хоста).</summary>
        public void ApplyRecord(DoorRecord record, bool notifyOwner = true)
        {
            if (record == null)
            {
                return;
            }

            if (_doors.TryGetValue(record.hash, out var door) && door != null)
            {
                door.ApplyRecord(record);
            }

            if (notifyOwner && record.ownerId == AuroraRuntime.LocalId)
            {
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.DoorBuy);
            }
        }

        /// <summary>Освобождает дверь игрока (выход из игры, смена роли и т.п.).</summary>
        public void ReleaseOwnedDoor(byte playerId, string reason)
        {
            foreach (var door in _doors.Values)
            {
                if (door != null && door.IsOwned && door.OwnerId == playerId)
                {
                    var record = _state.GetDoor(door.Hash);
                    if (record != null)
                    {
                        record.ownerId = 255;
                        record.ownerName = "";
                        _state.MarkDoorChanged(record);
                        door.ApplyRecord(record);
                        AuroraLog.Info("Дверь {0} освобождена ({1})", door.Label, reason);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ хранение

        public void LoadFromState()
        {
            // Записи о дверях живут в состоянии сессии — подтягиваем их к сцене после сканирования.
            foreach (var door in _doors.Values)
            {
                var record = _state.GetDoor(door.Hash);
                if (record != null)
                {
                    door.ApplyRecord(record);
                }
            }
        }

        public void Save()
        {
            AuroraStorage.SaveState(_state);
        }
    }
}
