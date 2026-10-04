using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Авторитетная логика: только хост меняет состояние (деньги, роли, двери, контракты, принтеры).
    /// Клиенты отправляют заявки, хост проверяет правила и рассылает новый снимок состояния.
    /// </summary>
    public class AuroraAuthority
    {
        private readonly AuroraState _state;
        private readonly WalletService _wallet;
        private readonly RoleService _roles;
        private readonly DoorService _doors;
        private readonly ShopService _shop;
        private readonly ContractService _contracts;
        private readonly CraftService _craft;
        private readonly CrimeService _crime;

        /// <summary>Кнопка «сбросить экономику» требует второго нажатия.</summary>
        public bool ResetConfirmPending { get; set; }

        /// <summary>Ограничитель частоты рассылки состояния.</summary>
        private float _lastSyncTime;
        private bool _syncQueued;
        private readonly float SyncInterval = 0.25f;

        public AuroraAuthority(AuroraState state, WalletService wallet, RoleService roles, DoorService doors,
            ShopService shop, ContractService contracts, CraftService craft, CrimeService crime)
        {
            _state = state;
            _wallet = wallet;
            _roles = roles;
            _doors = doors;
            _shop = shop;
            _contracts = contracts;
            _craft = craft;
            _crime = crime;

            _state.OnChanged += QueueSync;
        }

        // ------------------------------------------------------------------ синк

        private void QueueSync()
        {
            _syncQueued = true;
        }

        /// <summary>Рассылает актуальный снимок всем (не чаще 4 раз в секунду).</summary>
        public void FlushSync(bool force = false)
        {
            if (!_syncQueued && !force)
            {
                return;
            }

            if (!force && Time.realtimeSinceStartup - _lastSyncTime < SyncInterval)
            {
                return;
            }

            _syncQueued = false;
            _lastSyncTime = Time.realtimeSinceStartup;

            if (AuroraRuntime.Net == null || !AuroraRuntime.Net.IsHost)
            {
                return;
            }

            string json = _state.ToSnapshot().ToJson();
            var data = AuroraWire.Build(w => w.Write(json));
            AuroraRuntime.Net.Send(new AuroraNetMsg(AuroraMsgType.StateSync, AuroraRuntime.Net.LocalId, data));
        }

        public void BroadcastState(byte? target)
        {
            if (AuroraRuntime.Net == null || !AuroraRuntime.Net.IsHost)
            {
                return;
            }

            string json = _state.ToSnapshot().ToJson();
            var data = AuroraWire.Build(w => w.Write(json));
            AuroraRuntime.Net.Send(new AuroraNetMsg(AuroraMsgType.StateSync, AuroraRuntime.Net.LocalId, data), target);
        }

        // --------------------------------------------------------------- входящие

        public void HandleMessage(AuroraNetMsg msg)
        {
            try
            {
                switch (msg.Type)
                {
                    case AuroraMsgType.Hello:
                        HandleHello(msg);
                        break;

                    case AuroraMsgType.StateSync:
                        HandleStateSync(msg);
                        break;

                    case AuroraMsgType.Notify:
                        HandleNotify(msg);
                        break;

                    case AuroraMsgType.SpawnItem:
                        HandleSpawnItem(msg);
                        break;

                    case AuroraMsgType.Action:
                        HandleAction(msg);
                        break;
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "authority message " + msg.Type);
            }

            FlushSync();
        }

        private void HandleHello(AuroraNetMsg msg)
        {
            string name = "Player " + msg.Sender;
            AuroraWire.Read(msg.Data, r => name = r.ReadString());

            if (!AuroraRuntime.Net.IsHost)
            {
                return;
            }

            var rec = _state.GetOrCreate(msg.Sender, name);
            rec.connected = true;

            if (AuroraConfig.Current.autoAssignCitizen && rec.role == 0)
            {
                rec.role = (int)AuroraRoleId.Citizen;
            }

            _state.MarkPlayerChanged(rec);
            BroadcastState(msg.Sender);

            AuroraRuntime.NotifyAll(
                AuroraL.Get("notify.player.joined", rec.name, RoleCatalog.Get(rec.Role).Name),
                RoleCatalog.ColorOf(rec.Role));
        }

        private void HandleStateSync(AuroraNetMsg msg)
        {
            // Снимок принимаем только от хоста.
            if (AuroraRuntime.Net.IsHost || msg.Sender != AuroraRuntime.Net.HostId)
            {
                return;
            }

            string json = null;
            AuroraWire.Read(msg.Data, r => json = r.ReadString());

            _state.ApplySnapshot(AuroraSnapshot.FromJson(json));
            _doors.LoadFromState();
        }

        private void HandleNotify(AuroraNetMsg msg)
        {
            // Уведомления клиентам рассылает только хост.
            if (!AuroraRuntime.Net.IsHost && msg.Sender != AuroraRuntime.Net.HostId)
            {
                return;
            }

            string text = null;
            string color = "FFFFFF";

            AuroraWire.Read(msg.Data, r =>
            {
                text = r.ReadString();
                color = r.ReadString();
            });

            if (!string.IsNullOrEmpty(text))
            {
                AuroraNotifications.Send(text, AuroraUtils.Hex("#" + color));
            }
        }

        private void HandleSpawnItem(AuroraNetMsg msg)
        {
            // Спавн «по указке» принимаем только от хоста.
            if (!AuroraRuntime.Net.IsHost && msg.Sender != AuroraRuntime.Net.HostId)
            {
                return;
            }

            string barcode = null;
            AuroraWire.Read(msg.Data, r => barcode = r.ReadString());

            if (!string.IsNullOrEmpty(barcode))
            {
                AuroraRuntime.Spawn.TrySpawn(barcode, AuroraRuntime.Spawn.SpawnPositionInFrontOfLocalPlayer(), Quaternion.identity, out _);
            }
        }

        private void HandleAction(AuroraNetMsg msg)
        {
            AuroraAction action = AuroraAction.Transfer;

            // Действия применяет только хост.
            if (!AuroraRuntime.Net.IsHost)
            {
                // Хосту могут понадобиться локальные эффекты (например, уведомления) — обрабатываем как обычно.
                return;
            }

            AuroraWire.Read(msg.Data, reader =>
            {
                action = (AuroraAction)reader.ReadByte();

                switch (action)
                {
                    case AuroraAction.Transfer:
                    {
                        byte from = reader.ReadByte();
                        byte to = reader.ReadByte();
                        long amount = reader.ReadInt64();
                        ApplyTransfer(from, to, amount);
                        break;
                    }

                    case AuroraAction.RoleChange:
                    {
                        byte player = reader.ReadByte();
                        var role = (AuroraRoleId)reader.ReadByte();
                        ApplyRoleChange(player, role);
                        break;
                    }

                    case AuroraAction.DoorBuy:
                    case AuroraAction.DoorSell:
                    {
                        byte player = reader.ReadByte();
                        string hash = reader.ReadString();
                        ApplyDoor(player, hash, action == AuroraAction.DoorBuy);
                        break;
                    }

                    case AuroraAction.ShopPurchase:
                    {
                        byte seller = reader.ReadByte();
                        byte buyer = reader.ReadByte();
                        string barcode = reader.ReadString();
                        int price = reader.ReadInt32();
                        string title = reader.ReadString();
                        _shop.ApplyPurchase(seller, buyer, barcode, price, title);
                        break;
                    }

                    case AuroraAction.ContractCreate:
                    {
                        byte client = reader.ReadByte();
                        byte target = reader.ReadByte();
                        long reward = reader.ReadInt64();
                        _contracts.ApplyCreate(client, target, reward);
                        break;
                    }

                    case AuroraAction.ContractAccept:
                    {
                        int id = reader.ReadInt32();
                        byte hitman = reader.ReadByte();
                        _contracts.ApplyAccept(id, hitman);
                        break;
                    }

                    case AuroraAction.ContractCancel:
                    {
                        int id = reader.ReadInt32();
                        byte requester = reader.ReadByte();
                        _contracts.ApplyCancel(id, requester);
                        break;
                    }

                    case AuroraAction.Rob:
                    {
                        byte robber = reader.ReadByte();
                        byte victim = reader.ReadByte();
                        int percent = reader.ReadInt32();
                        _crime.ApplyRob(robber, victim, percent);
                        break;
                    }

                    case AuroraAction.PrinterSteal:
                    {
                        string hash = reader.ReadString();
                        byte owner = reader.ReadByte();
                        _craft.ApplySteal(hash, owner);
                        break;
                    }

                    case AuroraAction.PrinterPayout:
                    {
                        string hash = reader.ReadString();
                        byte owner = reader.ReadByte();
                        int amount = reader.ReadInt32();
                        _craft.ApplyPayout(hash, owner, amount);
                        break;
                    }

                    case AuroraAction.GiveAll:
                    {
                        HostGiveEveryoneAmount(1000);
                        break;
                    }

                    case AuroraAction.StateRequest:
                    {
                        BroadcastState(msg.Sender);
                        break;
                    }

                    case AuroraAction.Reset:
                    {
                        HostResetEconomyInternal();
                        break;
                    }
                }
            });
        }

        // --------------------------------------------------------------- правила

        public void ApplyTransfer(byte from, byte to, long amount)
        {
            if (!_wallet.TryTransfer(from, to, amount, "перевод игроку", out string error))
            {
                AuroraRuntime.Notify(from, error ?? AuroraL.Get("money.transfer.fail.funds"), UiTheme.Danger);
                return;
            }

            var fromName = _state.Get(from)?.name ?? "?";
            var toName = _state.Get(to)?.name ?? "?";

            AuroraRuntime.Notify(from, AuroraL.Get("money.transfer.sent", AuroraUtils.Money(amount), toName), UiTheme.Money);
            AuroraRuntime.Notify(to, AuroraL.Get("money.transfer.received", AuroraUtils.Money(amount), fromName), UiTheme.Success);
        }

        public void ApplyRoleChange(byte player, AuroraRoleId role)
        {
            if (!_roles.CanAssign(player, role, out string error))
            {
                AuroraRuntime.Notify(player, error, UiTheme.Warning);
                return;
            }

            _roles.ApplyRole(player, role);
        }

        public void ApplyDoor(byte player, string hash, bool isPurchase)
        {
            var record = _state.GetDoor(hash);

            if (record == null)
            {
                // Дверь ещё не зарегистрирована в состоянии — создаём запись.
                record = _state.GetOrCreateDoor(hash, "Door");
            }

            if (record == null)
            {
                return;
            }

            if (isPurchase)
            {
                if (record.IsOwned)
                {
                    AuroraRuntime.Notify(player, AuroraL.Get("door.owned.by", record.ownerName), UiTheme.Warning);
                    return;
                }

                var rec = _state.GetOrCreate(player, "Player " + player);
                var role = RoleCatalog.Get(rec.Role);

                if (AuroraConfig.Current.doorPoliceForbidden && !role.CanOwnDoor)
                {
                    AuroraRuntime.Notify(player, AuroraL.Get("door.police.denied"), UiTheme.Danger);
                    return;
                }

                if (_state.FindDoorOf(player) != null)
                {
                    AuroraRuntime.Notify(player, AuroraL.Get("door.already.owned"), UiTheme.Warning);
                    return;
                }

                int price = AuroraConfig.Current.doorPrice;
                if (!_wallet.TrySpend(player, price, "покупка двери"))
                {
                    AuroraRuntime.Notify(player, AuroraL.Get("door.too.expensive", AuroraUtils.Money(price)), UiTheme.Danger);
                    return;
                }

                record.ownerId = player;
                record.ownerName = rec.name;
                _state.MarkDoorChanged(record);
                _doors.ApplyRecord(record);

                AuroraRuntime.Notify(player, AuroraL.Get("door.bought", AuroraUtils.Money(price)), UiTheme.Success);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.DoorBuy);
            }
            else
            {
                if (!record.IsOwned)
                {
                    return;
                }

                if (record.ownerId != player && !AuroraRuntime.Net.IsHost)
                {
                    return;
                }

                int refund = Mathf.RoundToInt(AuroraConfig.Current.doorPrice * Mathf.Clamp(AuroraConfig.Current.doorRefundPercent, 0, 100) / 100f);

                record.ownerId = 255;
                record.ownerName = "";
                _state.MarkDoorChanged(record);
                _doors.ApplyRecord(record);

                if (refund > 0)
                {
                    _wallet.Add(player, refund, "продажа двери");
                }

                AuroraRuntime.Notify(player, AuroraL.Get("door.sold", AuroraUtils.Money(refund)), UiTheme.Success);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.DoorSell);
            }
        }

        // ------------------------------------------------------------ хост-панель

        public void HostGiveEveryone(long amount)
        {
            if (!AuroraRuntime.Net.IsHost)
            {
                AuroraRuntime.Notify(AuroraRuntime.LocalId, AuroraL.Get("common.locked"), UiTheme.Warning);
                return;
            }

            HostGiveEveryoneAmount(amount);
        }

        private void HostGiveEveryoneAmount(long amount)
        {
            foreach (var player in _state.Players)
            {
                _wallet.Add(player.id, amount, "выдача администратором");
            }

            AuroraRuntime.NotifyAll("+" + AuroraUtils.Money(amount), UiTheme.Money);
        }

        public void HostResetEconomy()
        {
            ResetConfirmPending = false;

            if (!AuroraRuntime.Net.IsHost)
            {
                return;
            }

            HostResetEconomyInternal();
        }

        private void HostResetEconomyInternal()
        {
            foreach (var player in _state.Players)
            {
                player.balance = AuroraConfig.Current.startBalance;
                _state.MarkPlayerChanged(player);
            }

            foreach (var door in _state.Doors)
            {
                door.ownerId = 255;
                door.ownerName = "";
                _state.MarkDoorChanged(door);
                _doors.ApplyRecord(door);
            }

            AuroraRuntime.NotifyAll("Экономика сброшена", UiTheme.Warning);
            AuroraLog.Info("Экономика сброшена хостом");
        }

        /// <summary>Игрок вышел: освобождаем роль/дверь и рассылаем состояние.</summary>
        public void HandlePeerLeft(byte playerId)
        {
            if (!AuroraRuntime.Net.IsHost)
            {
                return;
            }

            _roles.OnPlayerLeft(playerId);
            _state.ClearContractsFor(playerId);

            var rec = _state.Get(playerId);
            if (rec != null)
            {
                rec.connected = false;
                _state.MarkPlayerChanged(rec);
            }

            AuroraRuntime.NotifyAll(AuroraL.Get("notify.player.left", rec?.name ?? ("Player " + playerId)), UiTheme.TextDim);
            FlushSync(true);
        }
    }
}
