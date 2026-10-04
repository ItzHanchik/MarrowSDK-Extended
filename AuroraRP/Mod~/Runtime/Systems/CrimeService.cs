using System;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Преступные механики: грабёж прохожих (Гангстер) и взлом чужих домов.
    /// Работает по тому же принципу, что и перевод денег: держите руку жертвы.
    /// </summary>
    public class CrimeService
    {
        private readonly AuroraState _state;
        private readonly WalletService _wallet;

        public byte RobTargetId { get; private set; } = 255;
        public float RobProgress { get; private set; }
        public string RobTargetName { get; private set; } = "";

        private readonly System.Collections.Generic.Dictionary<byte, float> _cooldowns = new System.Collections.Generic.Dictionary<byte, float>();
        private float _lastRobTime = -999f;

        public CrimeService(AuroraState state, WalletService wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public bool CanRobLocal(out string reason)
        {
            reason = null;

            if (!AuroraRuntime.Roles.Has(AuroraRuntime.LocalId, RoleAbilities.Rob))
            {
                reason = AuroraL.Get("common.locked");
                return false;
            }

            float cooldown = AuroraConfig.Current.robCooldown;
            if (Time.realtimeSinceStartup - _lastRobTime < cooldown)
            {
                reason = string.Format("Перезарядка: {0} {1}", Mathf.RoundToInt(cooldown - (Time.realtimeSinceStartup - _lastRobTime)), AuroraL.Get("common.seconds"));
                return false;
            }

            return true;
        }

        public void Tick(float dt)
        {
            if (!AuroraRuntime.Roles.Has(AuroraRuntime.LocalId, RoleAbilities.Rob))
            {
                RobProgress = 0f;
                RobTargetId = 255;
                return;
            }

            if (!CanRobLocal(out _))
            {
                RobProgress = 0f;
                return;
            }

            byte target = FindVictim(out float distance);
            if (target == 255)
            {
                RobProgress = 0f;
                RobTargetId = 255;
                return;
            }

            if (RobTargetId != target)
            {
                RobTargetId = target;
                RobProgress = 0f;
                RobTargetName = _state.Get(target)?.name ?? ("Player " + target);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.HoldStart);
            }

            RobProgress += dt / Mathf.Max(0.5f, AuroraConfig.Current.robHoldSeconds);

            if (RobProgress >= 1f)
            {
                RobProgress = 0f;
                _lastRobTime = Time.realtimeSinceStartup;
                AuroraRuntime.Net.SendRobRequest(AuroraRuntime.LocalId, target, AuroraConfig.Current.robPercent);
            }

            // Взлом чужой двери: если смотрим на чужую дверь в упор, показываем подсказку.
        }

        private byte FindVictim(out float distance)
        {
            distance = float.MaxValue;
            byte best = 255;

            var myHand = BoneLib.Player.RightHand;
            if (myHand == null)
            {
                return 255;
            }

            Vector3 myPos = myHand.transform.position;
            float maxSqr = AuroraConfig.Current.transferHandDistance * AuroraConfig.Current.transferHandDistance * 1.4f;

            foreach (var peer in AuroraRuntime.Net.Peers)
            {
                if (peer.Id == AuroraRuntime.LocalId)
                {
                    continue;
                }

                if (!AuroraRuntime.Net.TryGetPeerHands(peer.Id, out var left, out var right))
                {
                    continue;
                }

                float dl = left.HasValue ? (left.Value - myPos).sqrMagnitude : float.MaxValue;
                float dr = right.HasValue ? (right.Value - myPos).sqrMagnitude : float.MaxValue;
                float d = Mathf.Min(dl, dr);

                if (d <= maxSqr && d < distance)
                {
                    distance = Mathf.Sqrt(d);
                    best = peer.Id;
                }
            }

            return best;
        }

        /// <summary>Применение грабежа на хосте.</summary>
        public void ApplyRob(byte robberId, byte victimId, int percent)
        {
            var robber = _state.GetOrCreate(robberId, "Player " + robberId);
            var victim = _state.GetOrCreate(victimId, "Player " + victimId);

            long amount = Math.Max(1, victim.balance * Mathf.Clamp(percent, 1, 100) / 100);
            if (victim.balance <= 0)
            {
                AuroraRuntime.Notify(robberId, AuroraL.Get("money.transfer.fail.funds"), UiTheme.Warning);
                return;
            }

            if (!_wallet.TryTransfer(victimId, robberId, amount, "грабёж", out _))
            {
                return;
            }

            AuroraRuntime.NotifyAll(
                AuroraL.Get("notify.robbery", robber.name, victim.name, AuroraUtils.Money(amount)),
                UiTheme.Danger);

            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Purchase, 0.3f);
        }
    }
}
