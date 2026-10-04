using System;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Механика передачи денег: выставьте сумму в меню (кнопка X на контроллере) и
    /// подержите руку другого игрока правой рукой 5 секунд — деньги уходят со счёта на счёт.
    /// </summary>
    public class TransferService
    {
        private readonly AuroraState _state;
        private readonly WalletService _wallet;

        /// <summary>Сумма, выставленная в меню (кнопка X).</summary>
        public long PendingAmount { get; private set; }

        /// <summary>Игрок, чью руку мы сейчас держим (255 = никто).</summary>
        public byte HoldTargetId { get; private set; } = 255;

        /// <summary>Прогресс удержания 0..1.</summary>
        public float HoldProgress { get; private set; }

        /// <summary>Имя того, кого держим (для HUD).</summary>
        public string HoldTargetName { get; private set; } = "";

        /// <summary>Идёт ли сейчас удержание.</summary>
        public bool IsHolding => HoldTargetId != 255 && HoldProgress > 0.01f;

        public event Action OnPendingAmountChanged;
        public event Action OnTransferCompleted;

        private float _lastTransferTime = -999f;
        private const float TransferCooldown = 1.5f;

        public TransferService(AuroraState state, WalletService wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        // ------------------------------------------------------------- выставление суммы

        public void SetPendingAmount(long amount)
        {
            PendingAmount = Math.Max(0, amount);

            var max = AuroraConfig.Current.transferMax;
            if (max > 0 && PendingAmount > max)
            {
                PendingAmount = max;
            }

            OnPendingAmountChanged?.Invoke();
        }

        public void AddPendingAmount(long delta)
        {
            SetPendingAmount(PendingAmount + delta);
        }

        public void ResetPending()
        {
            SetPendingAmount(0);
        }

        public void SetMaxPending()
        {
            SetPendingAmount(_wallet.GetBalance(AuroraRuntime.LocalId));
        }

        // ------------------------------------------------------------------ тик

        public void Tick(float dt)
        {
            if (PendingAmount <= 0 || AuroraConfig.Current.transferHoldSeconds <= 0f)
            {
                ResetHold();
                return;
            }

            byte target = 255;
            float distance = float.MaxValue;

            if (AuroraRuntime.Net != null && AuroraRuntime.Net.IsConnected)
            {
                target = FindHandToHold(out distance);
            }

            if (target == 255)
            {
                ResetHold();
                return;
            }

            if (HoldTargetId != target)
            {
                HoldTargetId = target;
                HoldProgress = 0f;
                HoldTargetName = _state.Get(target)?.name ?? ("Player " + target);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.HoldStart);
            }

            float needed = AuroraConfig.Current.transferHoldSeconds;
            HoldProgress += dt / needed;

            // Мягкая вибрация по мере удержания.
            if (AuroraConfig.Current.haptics && Time.frameCount % 6 == 0)
            {
                AuroraRuntime.Input?.HapticBoth(0.03f + 0.05f * Mathf.Clamp01(HoldProgress), 0.05f);
            }

            if (HoldProgress >= 1f)
            {
                CompleteTransfer(target);
            }
        }

        /// <summary>
        /// Ищет чужую руку рядом с нашей правой рукой. Возвращает id игрока или 255.
        /// </summary>
        private byte FindHandToHold(out float bestDistance)
        {
            bestDistance = float.MaxValue;
            byte best = 255;

            var myHand = BoneLib.Player.RightHand;
            if (myHand == null)
            {
                return 255;
            }

            Vector3 myPos = myHand.transform.position;
            float maxSqr = AuroraConfig.Current.transferHandDistance * AuroraConfig.Current.transferHandDistance;

            foreach (var peer in AuroraRuntime.Net.Peers)
            {
                if (peer.Id == AuroraRuntime.LocalId)
                {
                    continue;
                }

                if (!AuroraRuntime.Net.TryGetPeerHands(peer.Id, out var leftHand, out var rightHand))
                {
                    continue;
                }

                float dl = leftHand.HasValue ? (leftHand.Value - myPos).sqrMagnitude : float.MaxValue;
                float dr = rightHand.HasValue ? (rightHand.Value - myPos).sqrMagnitude : float.MaxValue;

                // Правая рука предпочтительнее — как в ТЗ («правой рукой взять за руку»).
                float d = Mathf.Min(dl, dr * 0.85f);
                if (d <= maxSqr && d < bestDistance)
                {
                    bestDistance = Mathf.Sqrt(d);
                    best = peer.Id;
                }
            }

            return best;
        }

        private void CompleteTransfer(byte target)
        {
            if (Time.realtimeSinceStartup - _lastTransferTime < TransferCooldown)
            {
                return;
            }

            _lastTransferTime = Time.realtimeSinceStartup;
            long amount = PendingAmount;

            ResetHold();

            if (amount <= 0)
            {
                return;
            }

            // Деньги двигает хост: клиент отправляет заявку, хост проверяет и рассылает результат.
            AuroraRuntime.Net?.SendTransferRequest(AuroraRuntime.LocalId, target, amount);

            if (AuroraConfig.Current.transferAutoConfirm)
            {
                PendingAmount = 0;
                OnPendingAmountChanged?.Invoke();
            }

            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.SendMoney, 0.1f);
            OnTransferCompleted?.Invoke();
        }

        private void ResetHold()
        {
            HoldTargetId = 255;
            HoldProgress = 0f;
            HoldTargetName = "";
        }

        /// <summary>Пассивный перевод по списку игроков (альтернатива удержанию руки).</summary>
        public bool TransferTo(byte target, long amount, out string error)
        {
            error = null;

            if (!AuroraConfig.Current.transferAutoConfirm)
            {
                // В ручном режиме просто выставляем сумму и ждём удержания руки.
                SetPendingAmount(amount);
                return true;
            }

            AuroraRuntime.Net?.SendTransferRequest(AuroraRuntime.LocalId, target, amount);
            return true;
        }
    }
}
