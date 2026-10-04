using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Выдача и проверка ролей. Хост — источник правды, остальные получают состояние по сети.
    /// </summary>
    public class RoleService
    {
        private readonly AuroraState _state;

        /// <summary>(player, старая роль, новая роль)</summary>
        public event Action<byte, AuroraRoleId, AuroraRoleId> OnRoleChanged;

        public RoleService(AuroraState state)
        {
            _state = state;
        }

        public AuroraRoleId GetRole(byte playerId)
        {
            var rec = _state.Get(playerId);
            return rec?.Role ?? AuroraRoleId.Citizen;
        }

        public AuroraRoleInfo GetInfo(byte playerId) => RoleCatalog.Get(GetRole(playerId));

        public bool Has(byte playerId, RoleAbilities ability)
        {
            return RoleCatalog.Get(GetRole(playerId)).Has(ability);
        }

        public int CountRole(AuroraRoleId role)
        {
            int count = 0;
            foreach (var p in _state.Players)
            {
                if (p.Role == role && p.connected)
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsRoleFull(AuroraRoleId role)
        {
            var info = RoleCatalog.Get(role);
            int max = role == AuroraRoleId.Hitman ? AuroraConfig.Current.hitmanMaxCount : info.MaxCount;
            if (max < 0)
            {
                return false;
            }

            return CountRole(role) >= max;
        }

        /// <summary>
        /// Проверяет, можно ли выдать роль (используется и хостом, и клиентом для мгновенной реакции в UI).
        /// </summary>
        public bool CanAssign(byte playerId, AuroraRoleId role, out string error)
        {
            error = null;

            var info = RoleCatalog.Get(role);
            var current = GetRole(playerId);

            if (current == role)
            {
                return true;
            }

            int max = role == AuroraRoleId.Hitman ? AuroraConfig.Current.hitmanMaxCount : info.MaxCount;
            if (max >= 0 && CountRole(role) >= max)
            {
                error = role == AuroraRoleId.Hitman
                    ? AuroraL.Get("roles.hitman.taken")
                    : AuroraL.Get("roles.taken", max);
                return false;
            }

            int cost = AuroraConfig.Current.roleChangeCost;
            if (cost > 0 && _state.Get(playerId) != null && _state.Get(playerId).balance < cost)
            {
                error = AuroraL.Get("shop.no.funds");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Применяет роль локально (вызывается хостом или при получении сообщения).
        /// </summary>
        public bool ApplyRole(byte playerId, AuroraRoleId role, bool announce = true)
        {
            var rec = _state.GetOrCreate(playerId, _state.Get(playerId)?.name);
            if (rec == null)
            {
                return false;
            }

            var old = rec.Role;
            if (old == role)
            {
                return true;
            }

            // Освобождаем дверь, если роль больше не позволяет владеть недвижимостью.
            var info = RoleCatalog.Get(role);
            if (!info.CanOwnDoor && AuroraRuntime.Doors != null)
            {
                AuroraRuntime.Doors.ReleaseOwnedDoor(playerId, "роль " + info.Name);
            }

            rec.role = (byte)role;
            int cost = AuroraConfig.Current.roleChangeCost;
            if (cost > 0)
            {
                rec.balance = Mathf.Max(0, (int)rec.balance - cost);
            }

            _state.MarkPlayerChanged(rec);

            OnRoleChanged?.Invoke(playerId, old, role);

            if (announce)
            {
                string text = AuroraL.Get("notify.role.assigned", info.Name);
                AuroraRuntime.Notify(playerId, text, info.Color);

                if (playerId == AuroraRuntime.LocalId)
                {
                    ApplyStarterKit(role);
                }
            }

            AuroraLog.Info("Игрок {0} сменил роль: {1} -> {2}", rec.name, old, role);
            return true;
        }

        /// <summary>Стартовый набор оружия: полиция — AR-15 и GLOCK-17, продавец — пистолет.</summary>
        public void ApplyStarterKit(AuroraRoleId role)
        {
            var info = RoleCatalog.Get(role);
            var cfg = AuroraConfig.Current;

            if (info.Has(RoleAbilities.StarterPoliceKit) && cfg.policeStarterKit)
            {
                AuroraRuntime.Spawn?.SpawnNearLocalPlayer(cfg.policeStarterKitBarcodes, "полицейский сет");
            }
            else if (info.Has(RoleAbilities.StarterDealerKit) && cfg.dealerStarterKit)
            {
                AuroraRuntime.Spawn?.SpawnNearLocalPlayer(cfg.dealerStarterKitBarcodes, "сет продавца");
            }
        }

        /// <summary>Игрок вышел — освобождаем уникальные роли и двери.</summary>
        public void OnPlayerLeft(byte playerId)
        {
            var rec = _state.Get(playerId);
            if (rec == null)
            {
                return;
            }

            rec.connected = false;

            if (AuroraConfig.Current.doorReleaseOnLeave && AuroraRuntime.Doors != null)
            {
                AuroraRuntime.Doors.ReleaseOwnedDoor(playerId, "игрок вышел");
            }
        }
    }
}
