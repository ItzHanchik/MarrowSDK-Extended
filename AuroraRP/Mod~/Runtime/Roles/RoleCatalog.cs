using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>Идентификаторы ролей AuroraRP.</summary>
    public enum AuroraRoleId : byte
    {
        Citizen = 0,
        Police = 1,
        GunDealer = 2,
        Smuggler = 3,
        Gangster = 4,
        Hitman = 5
    }

    /// <summary>Флаги возможностей роли.</summary>
    [Flags]
    public enum RoleAbilities
    {
        None = 0,
        StarterPoliceKit = 1 << 0,
        StarterDealerKit = 1 << 1,
        WeaponShop = 1 << 2,      // доступ к магазину оружия (покупка у поставщика и продажа игрокам)
        IllegalGoods = 1 << 3,    // нелегальные товары (отмычки, маскировка)
        Rob = 1 << 4,             // грабёж прохожих
        StealPrinters = 1 << 5,   // воровать маники (принтеры денег)
        BreakIn = 1 << 6,         // взлом чужих домов
        Arrest = 1 << 7,          // арест
        LicenseCheck = 1 << 8,    // проверка лицензий
        IssueLicense = 1 << 9,    // выдача лицензий
        Contracts = 1 << 10       // контракты на устранение
    }

    /// <summary>Описание роли.</summary>
    public sealed class AuroraRoleInfo
    {
        public AuroraRoleId Id;
        public string NameKey;
        public string DescKey;
        public string ColorHex = "#FFFFFF";
        public int MaxCount = -1;              // -1 = без ограничения
        public bool CanOwnDoor = true;
        public RoleAbilities Abilities = RoleAbilities.None;
        public string ShortTag = "?";           // метка для тега над головой

        public Color Color => AuroraUtils.Hex(ColorHex);

        public string Name => AuroraL.Role(Id);

        public string Description => AuroraL.RoleDescription(Id);

        public bool Has(RoleAbilities ability) => (Abilities & ability) != 0;
    }

    /// <summary>Каталог всех ролей проекта (см. ТЗ проекта AuroraRP).</summary>
    public static class RoleCatalog
    {
        public const int MaxRoleIndex = 5;

        private static readonly AuroraRoleInfo[] Roles =
        {
            new AuroraRoleInfo
            {
                Id = AuroraRoleId.Citizen,
                ColorHex = "#9AD1FF",
                MaxCount = -1,
                CanOwnDoor = true,
                ShortTag = "CIT",
                Abilities = RoleAbilities.None
            },
            new AuroraRoleInfo
            {
                Id = AuroraRoleId.Police,
                ColorHex = "#3D7BFF",
                MaxCount = -1,
                CanOwnDoor = false, // за полицейского двери запрещены
                ShortTag = "LSPD",
                Abilities = RoleAbilities.StarterPoliceKit | RoleAbilities.Arrest | RoleAbilities.LicenseCheck | RoleAbilities.IssueLicense
            },
            new AuroraRoleInfo
            {
                Id = AuroraRoleId.GunDealer,
                ColorHex = "#FFB03A",
                MaxCount = -1,
                CanOwnDoor = true,
                ShortTag = "GUN",
                Abilities = RoleAbilities.WeaponShop | RoleAbilities.StarterDealerKit | RoleAbilities.IssueLicense
            },
            new AuroraRoleInfo
            {
                Id = AuroraRoleId.Smuggler,
                ColorHex = "#B57BFF",
                MaxCount = -1,
                CanOwnDoor = true,
                ShortTag = "SMG",
                Abilities = RoleAbilities.IllegalGoods | RoleAbilities.BreakIn
            },
            new AuroraRoleInfo
            {
                Id = AuroraRoleId.Gangster,
                ColorHex = "#FF5C8A",
                MaxCount = -1,
                CanOwnDoor = true,
                ShortTag = "GAN",
                Abilities = RoleAbilities.Rob | RoleAbilities.StealPrinters | RoleAbilities.BreakIn
            },
            new AuroraRoleInfo
            {
                Id = AuroraRoleId.Hitman,
                ColorHex = "#FF3B3B",
                MaxCount = 1, // максимум один наёмник на сервере
                CanOwnDoor = true,
                ShortTag = "HIT",
                Abilities = RoleAbilities.Contracts
            }
        };

        public static IReadOnlyList<AuroraRoleInfo> All => Roles;

        public static AuroraRoleInfo Get(AuroraRoleId id)
        {
            int idx = Mathf.Clamp((int)id, 0, Roles.Length - 1);
            return Roles[idx];
        }

        public static Color ColorOf(AuroraRoleId id) => Get(id).Color;

        public static List<AuroraRoleId> AllIds()
        {
            var list = new List<AuroraRoleId>(Roles.Length);
            for (int i = 0; i < Roles.Length; i++)
            {
                list.Add(Roles[i].Id);
            }

            return list;
        }
    }
}
