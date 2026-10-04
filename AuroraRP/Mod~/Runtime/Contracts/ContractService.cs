using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Заказы на устранение. Заказчик вносит награду (эскроу), наёмник выполняет,
    /// деньги уходят исполнителю после устранения цели. Наёмник на сервере — максимум один.
    /// </summary>
    public class ContractService
    {
        private readonly AuroraState _state;
        private readonly WalletService _wallet;

        /// <summary>Сумма, выставленная в меню для нового заказа.</summary>
        public long PendingReward { get; private set; } = 1000;

        public ContractService(AuroraState state, WalletService wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Tick(float dt)
        {
            // Проверяем, не устранены ли цели принятых контрактов.
            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                var contract = _state.Contracts[i];
                if (contract.status != ContractRecord.StatusAccepted)
                {
                    continue;
                }

                if (!IsPlayerDead(contract.targetId))
                {
                    continue;
                }

                CompleteContract(contract);
            }
        }

        private bool IsPlayerDead(byte playerId)
        {
            try
            {
                return AuroraRuntime.Net.IsPlayerDead(playerId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ меню

        public void AdjustPendingReward(long delta)
        {
            PendingReward = Math.Max(100, PendingReward + delta);
            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Click);
        }

        public List<ContractRecord> ActiveForHitman(byte hitmanId)
        {
            var list = new List<ContractRecord>();

            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                var c = _state.Contracts[i];
                if (c.status == ContractRecord.StatusPending || c.status == ContractRecord.StatusAccepted)
                {
                    if (c.targetId != hitmanId)
                    {
                        list.Add(c);
                    }
                }
            }

            return list;
        }

        public List<ContractRecord> CreatedBy(byte clientId)
        {
            var list = new List<ContractRecord>();

            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                var c = _state.Contracts[i];
                if (c.clientId == clientId && c.status == ContractRecord.StatusPending)
                {
                    list.Add(c);
                }
            }

            return list;
        }

        // ------------------------------------------------------------------ действия

        /// <summary>Создать заказ на игрока (награда списывается сразу, эскроу).</summary>
        public void CreateContract(byte targetId, long reward)
        {
            byte clientId = AuroraRuntime.LocalId;

            if (targetId == clientId)
            {
                AuroraNotifications.Send(AuroraL.Get("contract.target.self"), UiTheme.Warning);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            if (reward <= 0)
            {
                AuroraNotifications.Send(AuroraL.Get("contract.failed.funds"), UiTheme.Warning);
                return;
            }

            if (!_wallet.CanAfford(clientId, reward))
            {
                AuroraNotifications.Send(AuroraL.Get("contract.failed.funds"), UiTheme.Danger);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            AuroraRuntime.Net.SendContractCreate(clientId, targetId, reward);
        }

        public void AcceptContract(ContractRecord contract)
        {
            if (contract == null)
            {
                return;
            }

            if (!AuroraRuntime.Roles.Has(AuroraRuntime.LocalId, RoleAbilities.Contracts))
            {
                AuroraNotifications.Send(AuroraL.Get("contract.role.only"), UiTheme.Warning);
                return;
            }

            AuroraRuntime.Net.SendContractAccept(contract.id, AuroraRuntime.LocalId);
        }

        public void CancelContract(ContractRecord contract)
        {
            if (contract == null)
            {
                return;
            }

            AuroraRuntime.Net.SendContractCancel(contract.id, AuroraRuntime.LocalId);
        }

        // ------------------------------------------------------ применение (хост)

        public void ApplyCreate(byte clientId, byte targetId, long reward)
        {
            if (!_wallet.TrySpend(clientId, reward, "эскроу контракта"))
            {
                AuroraRuntime.Notify(clientId, AuroraL.Get("contract.failed.funds"), UiTheme.Danger);
                return;
            }

            var client = _state.GetOrCreate(clientId, "Player " + clientId);
            var target = _state.GetOrCreate(targetId, "Player " + targetId);

            var contract = _state.CreateContract(clientId, client.name, targetId, target.name, reward);

            AuroraRuntime.NotifyAll(
                AuroraL.Get("contract.created", target.name, AuroraUtils.Money(reward)),
                UiTheme.Danger);

            if (AuroraRuntime.Roles.CountRole(AuroraRoleId.Hitman) == 0)
            {
                AuroraRuntime.NotifyAll(AuroraL.Get("contract.hitman.none"), UiTheme.Warning);
            }
        }

        public void ApplyAccept(int contractId, byte hitmanId)
        {
            var contract = _state.FindContract(contractId);
            if (contract == null || contract.status != ContractRecord.StatusPending)
            {
                return;
            }

            if (!AuroraRuntime.Roles.Has(hitmanId, RoleAbilities.Contracts))
            {
                AuroraRuntime.Notify(hitmanId, AuroraL.Get("contract.role.only"), UiTheme.Warning);
                return;
            }

            contract.status = ContractRecord.StatusAccepted;
            _state.MarkContractChanged(contract);

            AuroraRuntime.Notify(hitmanId,
                AuroraL.Get("contract.accepted", contract.targetName, AuroraUtils.Money(contract.reward)),
                UiTheme.Danger);

            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Contract);
        }

        public void ApplyCancel(int contractId, byte requesterId)
        {
            var contract = _state.FindContract(contractId);
            if (contract == null || contract.status != ContractRecord.StatusPending)
            {
                return;
            }

            // Отменить может заказчик или хост.
            if (contract.clientId != requesterId && !AuroraRuntime.Net.IsHost)
            {
                return;
            }

            contract.status = ContractRecord.StatusCancelled;
            _wallet.Add(contract.clientId, contract.reward, "возврат за отмену контракта");

            AuroraRuntime.Notify(contract.clientId,
                AuroraL.Get("contract.cancelled", AuroraUtils.Money(contract.reward)),
                UiTheme.Warning);

            _state.RemoveContract(contract);
        }

        private void CompleteContract(ContractRecord contract)
        {
            contract.status = ContractRecord.StatusDone;

            byte killer = FindHitmanFor(contract);
            if (killer != 255)
            {
                _wallet.Add(killer, contract.reward, "оплата контракта #" + contract.id);
                AuroraRuntime.Notify(killer, AuroraL.Get("contract.done", AuroraUtils.Money(contract.reward)), UiTheme.Success);
            }

            AuroraRuntime.NotifyAll(AuroraL.Get("contract.kill.confirmed") + ": " + contract.targetName, UiTheme.Danger);
            _state.RemoveContract(contract);

            AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Success);
        }

        private byte FindHitmanFor(ContractRecord contract)
        {
            foreach (var player in _state.Players)
            {
                if (RoleCatalog.Get(player.Role).Has(RoleAbilities.Contracts))
                {
                    return player.id;
                }
            }

            return 255;
        }

        // --------------------------------------------------------- события игрока

        public void OnLocalPlayerDied()
        {
            // Локальный игрок — цель: контракт закроется сам на стороне хоста.
            AuroraRuntime.Transfers?.ResetPending();
        }

        public void OnLocalPlayerRespawned()
        {
        }
    }
}
