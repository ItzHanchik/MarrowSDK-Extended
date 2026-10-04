using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Мост для игроков БЕЗ AuroraRP («обычный игрок» ничего не ставит, кроме палета,
    /// который Fusion скачивает сам). Код работает только на хосте и «читает» то,
    /// что и так синхронизирует Fusion: руки, голову и захваты игроков.
    ///
    /// Что получается без DLL у игрока:
    ///  • он расплачивается, если подержать руку игрока с модом (тот не выставил сумму);
    ///  • покупает дверь, если подержаться за неё, стоя на месте;
    ///  • всё остальное (предметы, принтеры, терминал) он видит и трогает физически.
    ///
    /// Меню и роли такому игроку показать нельзя — интерфейс рисует только код.
    /// </summary>
    public class PeerBridgeService
    {
        private const byte None = 255;

        /// <summary>Насколько близко к двери должна быть рука, чтобы это считалось удержанием.</summary>
        private const float DoorReach = 0.6f;

        private const float ScanInterval = 0.1f;

        private readonly AuroraState _state;

        private readonly Dictionary<long, float> _pairHold = new Dictionary<long, float>();
        private readonly Dictionary<long, float> _pairCooldown = new Dictionary<long, float>();
        private readonly Dictionary<string, float> _doorHold = new Dictionary<string, float>();
        private readonly Dictionary<string, Vector3> _doorHead = new Dictionary<string, Vector3>();

        private float _lastScan;

        public PeerBridgeService(AuroraState state)
        {
            _state = state;
        }

        public void Tick(float dt)
        {
            var cfg = AuroraConfig.Current;

            if (!cfg.peerBridgeEnabled)
            {
                return;
            }

            var net = AuroraRuntime.Net;

            if (net == null || !net.IsConnected || !net.IsHost)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;

            if (now - _lastScan < ScanInterval)
            {
                return;
            }

            float step = Mathf.Clamp(now - _lastScan, 0.05f, 0.5f);
            _lastScan = now;

            try
            {
                RememberPeers(net);
                TickHandshakes(net, step, now);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "peer bridge hands");
            }

            try
            {
                TickDoors(net, step, now);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "peer bridge doors");
            }
        }

        // ------------------------------------------------------------------ игроки

        /// <summary>Заводит записи для игроков без мода, чтобы работали балансы и имена.</summary>
        private void RememberPeers(IAuroraNet net)
        {
            foreach (var peer in net.Peers)
            {
                if (peer.Id == net.LocalId)
                {
                    continue;
                }

                var record = _state.GetOrCreate(peer.Id, peer.Name);

                if (record != null && !string.IsNullOrEmpty(peer.Name) && record.name != peer.Name)
                {
                    record.name = peer.Name;
                }
            }
        }

        private bool TryGetHands(byte id, out Vector3? left, out Vector3? right)
        {
            left = null;
            right = null;

            if (id == AuroraRuntime.LocalId)
            {
                try
                {
                    var lh = BoneLib.Player.LeftHand;
                    var rh = BoneLib.Player.RightHand;

                    if (lh != null)
                    {
                        left = lh.transform.position;
                    }

                    if (rh != null)
                    {
                        right = rh.transform.position;
                    }

                    return left.HasValue || right.HasValue;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return AuroraRuntime.Net.TryGetPeerHands(id, out left, out right);
        }

        private bool TryGetHead(byte id, out Vector3 head)
        {
            if (id == AuroraRuntime.LocalId)
            {
                head = Vector3.zero;

                try
                {
                    var t = BoneLib.Player.Head;
                    if (t == null)
                    {
                        return false;
                    }

                    head = t.position;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return AuroraRuntime.Net.TryGetPeerHead(id, out head);
        }

        // -------------------------------------------------------------- рукопожатия

        private void TickHandshakes(IAuroraNet net, float step, float now)
        {
            var peers = net.Peers;
            var cfg = AuroraConfig.Current;

            float reach = Mathf.Max(0.1f, cfg.transferHandDistance);
            float need = Mathf.Max(0.5f, cfg.passiveTransferHoldSeconds);
            float reachSqr = reach * reach;

            for (int i = 0; i < peers.Count; i++)
            {
                for (int j = i + 1; j < peers.Count; j++)
                {
                    byte a = peers[i].Id;
                    byte b = peers[j].Id;

                    // Оба с модом — у них своя логика перевода. Оба без — платить некому.
                    if (net.HasMod(a) == net.HasMod(b))
                    {
                        continue;
                    }

                    long key = Key(a, b);

                    if (!TryGetHands(a, out var al, out var ar) || !TryGetHands(b, out var bl, out var br))
                    {
                        _pairHold[key] = 0f;
                        continue;
                    }

                    float distance = MinHandDistanceSqr(al, ar, bl, br);

                    if (float.IsPositiveInfinity(distance) || distance > reachSqr)
                    {
                        _pairHold[key] = 0f;
                        continue;
                    }

                    if (_pairCooldown.TryGetValue(key, out float until) && now < until)
                    {
                        _pairHold[key] = 0f;
                        continue;
                    }

                    _pairHold.TryGetValue(key, out float held);
                    held += step;
                    _pairHold[key] = held;

                    if (held < need)
                    {
                        continue;
                    }

                    _pairHold[key] = 0f;
                    _pairCooldown[key] = now + Mathf.Max(1f, cfg.passivePairCooldown);

                    ApplyHandshake(net, a, b);
                }
            }
        }

        private void ApplyHandshake(IAuroraNet net, byte a, byte b)
        {
            bool modA = net.HasMod(a);
            byte passive = modA ? b : a;
            byte modded = modA ? a : b;

            var authority = AuroraRuntime.Authority;

            if (authority == null)
            {
                return;
            }

            // Если игрок с модом выставил сумму — он сам и платит, мы не мешаем.
            if (authority.GetPendingIntent(modded) > 0)
            {
                return;
            }

            long amount = Mathf.Max(0, AuroraConfig.Current.passiveTransferAmount);

            if (amount <= 0)
            {
                return;
            }

            long balance = AuroraRuntime.Wallet != null
                ? AuroraRuntime.Wallet.GetBalance(passive)
                : 0;

            if (balance < amount)
            {
                AuroraRuntime.NotifyLocal(
                    AuroraL.Get("bridge.passive.poor", PeerName(net, passive)),
                    UiTheme.Warning);
                return;
            }

            AuroraLog.Info("Игрок без мода {0} платит {1} ({2})", passive, modded, amount);

            AuroraRuntime.NotifyLocal(
                AuroraL.Get("bridge.passive.paid", PeerName(net, passive), AuroraUtils.Money(amount)),
                UiTheme.Money);

            authority.ApplyTransfer(passive, modded, amount);
        }

        private static string PeerName(IAuroraNet net, byte id)
        {
            foreach (var peer in net.Peers)
            {
                if (peer.Id == id)
                {
                    return peer.Name;
                }
            }

            return "Player " + id;
        }

        // -------------------------------------------------------------------- двери

        private void TickDoors(IAuroraNet net, float step, float now)
        {
            var cfg = AuroraConfig.Current;

            if (!cfg.passiveDoorBuyEnabled)
            {
                return;
            }

            var doors = AuroraRuntime.Doors != null ? AuroraRuntime.Doors.AllDoors : null;

            if (doors == null || doors.Count == 0)
            {
                return;
            }

            float need = Mathf.Max(1f, cfg.passiveDoorHoldSeconds);
            float still = Mathf.Max(0.05f, cfg.passiveDoorStandStill);

            foreach (var peer in net.Peers)
            {
                if (peer.Id == net.LocalId || net.HasMod(peer.Id))
                {
                    continue;
                }

                if (!TryGetHands(peer.Id, out var left, out var right) || !TryGetHead(peer.Id, out var head))
                {
                    continue;
                }

                for (int i = 0; i < doors.Count; i++)
                {
                    var door = doors[i];

                    if (door == null || door.Root == null || door.IsOwned)
                    {
                        continue;
                    }

                    string key = peer.Id + ":" + door.Hash;

                    float distance = MinBoundsDistanceSqr(left, right, door.WorldBounds);

                    if (distance > DoorReach * DoorReach)
                    {
                        _doorHold.Remove(key);
                        _doorHead.Remove(key);
                        continue;
                    }

                    if (!_doorHead.TryGetValue(key, out Vector3 start))
                    {
                        _doorHead[key] = head;
                        start = head;
                    }

                    _doorHold.TryGetValue(key, out float held);
                    held += step;
                    _doorHold[key] = held;

                    if (held < need)
                    {
                        continue;
                    }

                    // Стоял на месте: иначе это была просто ходьба через дверь.
                    if (Vector3.Distance(start, head) > still)
                    {
                        _doorHold[key] = 0f;
                        _doorHead[key] = head;
                        continue;
                    }

                    _doorHold.Remove(key);
                    _doorHead.Remove(key);

                    AuroraLog.Info("Игрок без мода {0} выкупает дверь {1} (удержание {2} с)",
                        peer.Name, door.Hash, need);

                    AuroraRuntime.NotifyLocal(
                        AuroraL.Get("bridge.door.passive", peer.Name, door.Label),
                        UiTheme.Money);

                    AuroraRuntime.Authority?.ApplyDoor(peer.Id, door.Hash, true);
                }
            }
        }

        // ------------------------------------------------------------------ мелочи

        private static long Key(byte a, byte b)
        {
            return a < b ? ((long)a << 8) | b : ((long)b << 8) | a;
        }

        private static float MinHandDistanceSqr(Vector3? al, Vector3? ar, Vector3? bl, Vector3? br)
        {
            float best = float.MaxValue;

            Accumulate(al, bl, false, ref best);
            Accumulate(al, br, false, ref best);
            Accumulate(ar, bl, true, ref best);
            Accumulate(ar, br, true, ref best);

            return best < float.MaxValue ? best * best : float.PositiveInfinity;
        }

        private static void Accumulate(Vector3? first, Vector3? second, bool bonus, ref float best)
        {
            if (!first.HasValue || !second.HasValue)
            {
                return;
            }

            float d = Vector3.Distance(first.Value, second.Value);

            // Правая рука предпочтительнее — как в основном механизме перевода.
            if (bonus)
            {
                d *= 0.85f;
            }

            if (d < best)
            {
                best = d;
            }
        }

        private static float MinBoundsDistanceSqr(Vector3? left, Vector3? right, Bounds bounds)
        {
            float best = float.PositiveInfinity;

            if (left.HasValue)
            {
                best = Mathf.Min(best, bounds.SqrDistance(left.Value));
            }

            if (right.HasValue)
            {
                best = Mathf.Min(best, bounds.SqrDistance(right.Value));
            }

            return best;
        }
    }
}
