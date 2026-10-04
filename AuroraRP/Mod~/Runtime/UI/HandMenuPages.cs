using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Страницы меню. Каждая собирается заново при смене вкладки или изменении состояния.
    /// </summary>
    internal static class HandMenuPages
    {
        // ------------------------------------------------------------------ главная

        public static void BuildMain(HandMenu menu)
        {
            var rec = AuroraRuntime.LocalPlayer;
            var role = RoleCatalog.Get(rec.Role);

            menu.AddSection(AuroraL.Get("common.role"), UiTheme.Accent);

            var roleButton = menu.AddRow(
                role.Name,
                "users",
                role.Description,
                () => menu.Open(MenuPage.Roles),
                AuroraButton.Style.Primary);

            menu.AddSpacer(4f);

            menu.AddSection(AuroraL.Get("common.money"), UiTheme.Money);
            menu.AddRow(
                AuroraUtils.Money(rec.balance),
                "wallet",
                AuroraL.Get("money.transfer.howto2"),
                () => menu.Open(MenuPage.Wallet),
                AuroraButton.Style.Default);

            menu.AddRow(
                AuroraL.Get("menu.tab.doors"),
                "door",
                AuroraRuntime.Doors?.OwnedDoorSummary() ?? AuroraL.Get("door.none"),
                () => menu.Open(MenuPage.Doors));

            var shopInfo = AuroraRuntime.Shop.CanUseLocalPlayer(out string shopReason)
                ? AuroraL.Get("shop.dealer.markup", AuroraConfig.Current.dealerMarkupPercent)
                : shopReason;

            menu.AddRow(
                AuroraL.Get("menu.tab.shop"),
                "gun",
                shopInfo,
                () => menu.Open(MenuPage.Shop),
                AuroraButton.Style.Default,
                AuroraRuntime.Shop.CanUseLocalPlayer(out _));

            menu.AddRow(
                AuroraL.Get("menu.tab.contracts"),
                "contract",
                AuroraRuntime.Roles.Has(AuroraRuntime.LocalId, RoleAbilities.Contracts)
                    ? AuroraL.Get("contract.header")
                    : AuroraL.Get("contract.role.only"),
                () => menu.Open(MenuPage.Contracts));

            // Кто в городе
            var players = AuroraRuntime.State.OtherPlayers(AuroraRuntime.LocalId);
            menu.AddSpacer(6f);
            menu.AddSection(AuroraL.Get("common.players") + $" ({players.Count + 1})", UiTheme.Violet);

            int shown = 0;
            foreach (var p in players)
            {
                if (shown++ >= 3)
                {
                    break;
                }

                var info = RoleCatalog.Get(p.Role);
                menu.AddRow(
                    p.name,
                    "users",
                    $"<color=#{ColorUtility.ToHtmlStringRGB(info.Color)}>{info.Name}</color>",
                    null);
            }

            if (players.Count == 0)
            {
                menu.AddInfo(AuroraL.Get("common.players") + ": " + AuroraL.Get("common.nobody"), UiTheme.TextDim);
            }

            // Контент мода: палет и игроки без мода.
            menu.AddSpacer(6f);
            menu.AddSection(AuroraL.Get("content.header"), UiTheme.Violet);

            bool palletReady = AuroraRuntime.Net.HasContentPallet;

            menu.AddRow(
                palletReady ? AuroraL.Get("content.pallet.present") : AuroraL.Get("content.pallet.missing"),
                "info",
                AuroraRuntime.Net.ContentPalletStatus,
                () => AuroraRuntime.SyncContentPallet(true),
                palletReady ? AuroraButton.Style.Ghost : AuroraButton.Style.Primary);

            var missing = AuroraRuntime.Net.PeersWithoutMod;

            if (missing.Count > 0)
            {
                string names = "";
                foreach (var peer in missing)
                {
                    names += (names.Length > 0 ? ", " : "") + peer.Name;
                }

                menu.AddInfo(AuroraL.Get("content.players.missing", AuroraUtils.Truncate(names, 60)), UiTheme.Warning, 40f);
            }
            else if (AuroraRuntime.Net.IsConnected)
            {
                menu.AddInfo(AuroraL.Get("content.players.allok"), UiTheme.TextDim, 30f);
            }

            if (AuroraRuntime.Net.IsHost)
            {
                menu.AddSpacer(6f);
                menu.AddSection(AuroraL.Get("settings.host.panel"), UiTheme.Warning);
                menu.AddRow(
                    AuroraL.Get("settings.host.give"),
                    "cash",
                    AuroraL.Get("menu.tab.settings"),
                    () => AuroraRuntime.Authority.HostGiveEveryone(1000));
                menu.AddRow(
                    AuroraL.Get("settings.header"),
                    "gear",
                    AuroraL.Get("settings.host.selfselect"),
                    () => menu.Open(MenuPage.Settings),
                    AuroraButton.Style.Ghost);
            }
        }

        // -------------------------------------------------------------------- роли

        public static void BuildRoles(HandMenu menu)
        {
            var rec = AuroraRuntime.LocalPlayer;
            var current = RoleCatalog.Get(rec.Role);

            menu.AddSection(AuroraL.Get("roles.current"), UiTheme.Accent);
            menu.AddRow(
                current.Name,
                "shield",
                current.Description,
                null,
                AuroraButton.Style.Primary,
                selected: true);

            menu.AddSpacer(4f);
            menu.AddSection(AuroraL.Get("roles.header"), UiTheme.Violet);

            if (!AuroraConfig.Current.allowSelfRoleSelect && !AuroraRuntime.Net.IsHost)
            {
                menu.AddInfo(AuroraL.Get("roles.denied"), UiTheme.Warning);
            }

            var roles = RoleCatalog.AllIds();
            int perPage = 3;
            int pages = Mathf.CeilToInt(roles.Count / (float)perPage);

            int page = Mathf.Clamp(menu.RolePageIndex, 0, Mathf.Max(0, pages - 1));
            int start = page * perPage;

            for (int i = start; i < Mathf.Min(start + perPage, roles.Count); i++)
            {
                var id = roles[i];
                var info = RoleCatalog.Get(id);
                if (id == rec.Role)
                {
                    continue;
                }

                bool full = AuroraRuntime.Roles.IsRoleFull(id);
                bool allowed = AuroraConfig.Current.allowSelfRoleSelect || AuroraRuntime.Net.IsHost;
                bool interactable = allowed && !full;

                string subtitle = full
                    ? AuroraL.Get(id == AuroraRoleId.Hitman ? "roles.hitman.taken" : "roles.taken", info.MaxCount)
                    : info.Description;

                var roleId = id;
                menu.AddRow(
                    info.Name,
                    RoleIcon(id),
                    subtitle,
                    () => AuroraRuntime.Net.RequestRoleChange(roleId),
                    full ? AuroraButton.Style.Ghost : AuroraButton.Style.Default,
                    interactable);
            }

            if (pages > 1)
            {
                AddPager(menu, page, pages, () => menu.RolePageIndex++, () => menu.RolePageIndex--);
            }
        }

        private static string RoleIcon(AuroraRoleId role) => role switch
        {
            AuroraRoleId.Police => "shield",
            AuroraRoleId.GunDealer => "gun",
            AuroraRoleId.Smuggler => "star",
            AuroraRoleId.Gangster => "skull",
            AuroraRoleId.Hitman => "contract",
            _ => "users"
        };

        // ------------------------------------------------------------------ кошелёк

        public static void BuildWallet(HandMenu menu)
        {
            var rec = AuroraRuntime.LocalPlayer;
            var transfers = AuroraRuntime.Transfers;

            menu.AddSection(AuroraL.Get("money.header"), UiTheme.Money);
            menu.AddRow(
                AuroraUtils.Money(rec.balance),
                "wallet",
                AuroraL.Get("money.transfer.instruction", AuroraUtils.Money(transfers.PendingAmount)),
                null,
                AuroraButton.Style.Primary,
                selected: true);

            menu.AddSpacer(2f);
            menu.AddSection(AuroraL.Get("money.transfer.amount"), UiTheme.Accent);

            // Кнопки суммы: +100 / -100 / +1000 / -1000 (как в ТЗ)
            menu.AddPairRow("+100", () => transfers.AddPendingAmount(AuroraConfig.Current.transferStepSmall),
                "-100", () => transfers.AddPendingAmount(-AuroraConfig.Current.transferStepSmall), "cash");

            menu.AddPairRow("+1000", () => transfers.AddPendingAmount(AuroraConfig.Current.transferStepBig),
                "-1000", () => transfers.AddPendingAmount(-AuroraConfig.Current.transferStepBig), "cash");

            menu.AddRow(
                AuroraL.Get("common.amount") + ": " + AuroraUtils.Money(transfers.PendingAmount),
                "cash",
                AuroraL.Get("money.transfer.pending"),
                () => transfers.SetMaxPending(),
                AuroraButton.Style.Ghost,
                selected: transfers.PendingAmount > 0);

            menu.AddInfo(AuroraL.Get("money.transfer.howto"), UiTheme.Warning, 62f);
            menu.AddInfo(AuroraL.Get("money.transfer.howto2"), UiTheme.TextDim, 40f);

            // Прогресс удержания
            if (transfers.IsHolding)
            {
                menu.AddProgress(transfers.HoldTargetName, transfers.HoldProgress, UiTheme.Success, out _);
                menu.AddInfo(
                    AuroraL.Get("money.transfer.progress", Mathf.RoundToInt(transfers.HoldProgress * 100f)) + " → " + transfers.HoldTargetName,
                    UiTheme.Success);
            }

            // Список игроков
            var players = AuroraRuntime.State.OtherPlayers(AuroraRuntime.LocalId);
            menu.AddSpacer(4f);
            menu.AddSection(AuroraL.Get("money.transfer.to"), UiTheme.Violet);

            if (players.Count == 0)
            {
                menu.AddInfo(AuroraL.Get("contract.hitman.none"), UiTheme.TextDim);
                return;
            }

            int perPage = 3;
            int pages = Mathf.CeilToInt(players.Count / (float)perPage);
            int page = Mathf.Clamp(menu.PlayerPageIndex, 0, Mathf.Max(0, pages - 1));
            int start = page * perPage;

            for (int i = start; i < Mathf.Min(start + perPage, players.Count); i++)
            {
                var target = players[i];
                byte id = target.id;
                var role = RoleCatalog.Get(target.Role);

                menu.AddRow(
                    target.name,
                    "users",
                    $"<color=#{ColorUtility.ToHtmlStringRGB(role.Color)}>{role.Name}</color> · " + AuroraL.Get("common.take") + " " + AuroraUtils.Money(transfers.PendingAmount),
                    () => transfers.TransferTo(id, transfers.PendingAmount, out _));
            }

            if (pages > 1)
            {
                AddPager(menu, page, pages, () => menu.PlayerPageIndex++, () => menu.PlayerPageIndex--);
            }
        }

        // ------------------------------------------------------------------ магазин

        public static void BuildShop(HandMenu menu)
        {
            var shop = AuroraRuntime.Shop;

            menu.AddSection(AuroraL.Get("shop.header"), UiTheme.Warning);

            if (!shop.CanUseLocalPlayer(out string reason))
            {
                menu.AddRow(AuroraL.Get("common.locked"), "gun", reason, null, AuroraButton.Style.Ghost, false);
                menu.AddInfo(AuroraL.Get("shop.locked", AuroraL.Role(AuroraRoleId.GunDealer)), UiTheme.Warning, 60f);
                menu.AddInfo(AuroraUtils.Money(shop.DealerNetProfit()) + " — " + AuroraL.Get("shop.dealer.markup", AuroraConfig.Current.dealerMarkupPercent), UiTheme.TextDim);
                return;
            }

            menu.AddInfo(AuroraL.Get("shop.dealer.markup", AuroraConfig.Current.dealerMarkupPercent), UiTheme.TextDim, 36f);

            var items = AuroraConfig.Current.shopItems;
            int perPage = 4;
            int pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)perPage));
            int page = Mathf.Clamp(menu.ShopPageIndex, 0, pages - 1);
            int start = page * perPage;

            ShopCategory lastCategory = (ShopCategory)(-1);

            for (int i = start; i < Mathf.Min(start + perPage, items.Count); i++)
            {
                var entry = items[i];

                if (entry.Category != lastCategory && i == start)
                {
                    menu.AddSection(CategoryName(entry.Category), UiTheme.Accent);
                    lastCategory = entry.Category;
                }

                bool available = shop.Exists(entry, out _);
                var e = entry;

                menu.AddRow(
                    entry.title,
                    CategoryIcon(entry.Category),
                    (available ? AuroraUtils.Money(entry.price) : "<color=#FF5C6C>нет в паках</color>")
                    + "  ·  " + CategoryName(entry.Category),
                    () => shop.Purchase(e, AuroraRuntime.LocalId),
                    AuroraButton.Style.Default,
                    available);
            }

            if (items.Count == 0)
            {
                menu.AddInfo(AuroraL.Get("shop.empty"), UiTheme.TextDim, 60f);
            }

            if (pages > 1)
            {
                AddPager(menu, page, pages, () => menu.ShopPageIndex++, () => menu.ShopPageIndex--);
            }

            menu.AddSpacer(6f);
            menu.AddInfo(AuroraL.Get("shop.license.issued", AuroraL.Get("common.players")), UiTheme.TextDim, 36f);
        }

        private static string CategoryName(ShopCategory category) => category switch
        {
            ShopCategory.Pistol => AuroraL.Get("shop.category.pistol"),
            ShopCategory.Rifle => AuroraL.Get("shop.category.rifle"),
            ShopCategory.Shotgun => AuroraL.Get("shop.category.shotgun"),
            ShopCategory.Sniper => AuroraL.Get("shop.category.sniper"),
            _ => AuroraL.Get("shop.category.illegal")
        };

        private static string CategoryIcon(ShopCategory category) => category switch
        {
            ShopCategory.Pistol => "gun",
            ShopCategory.Rifle => "gun",
            ShopCategory.Shotgun => "gun",
            ShopCategory.Sniper => "gun",
            _ => "star"
        };

        // -------------------------------------------------------------------- двери

        public static void BuildDoors(HandMenu menu)
        {
            var doors = AuroraRuntime.Doors;

            menu.AddSection(AuroraL.Get("door.header"), UiTheme.Accent);

            var owned = doors.GetOwnedDoor(AuroraRuntime.LocalId);
            if (owned != null)
            {
                menu.AddRow(
                    owned.Label,
                    "door",
                    AuroraL.Get("door.owned.by", AuroraL.Get("common.you")),
                    null,
                    AuroraButton.Style.Primary,
                    selected: true);
                menu.AddInfo(AuroraL.Get("door.howto.sell"), UiTheme.Warning, 40f);
            }
            else
            {
                menu.AddRow(
                    AuroraL.Get("door.none"),
                    "door",
                    AuroraL.Get("door.help"),
                    null);
            }

            menu.AddSpacer(4f);
            menu.AddSection(AuroraL.Get("door.price", AuroraUtils.Money(AuroraConfig.Current.doorPrice)), UiTheme.Money);
            menu.AddInfo(AuroraL.Get("door.howto.buy"), UiTheme.Text);
            menu.AddInfo(AuroraL.Get("door.limit"), UiTheme.TextDim, 34f);

            if (AuroraConfig.Current.doorPoliceForbidden)
            {
                menu.AddInfo(AuroraL.Get("door.police.denied"), UiTheme.Warning, 40f);
            }

            var held = doors.HeldDoor;
            if (held != null)
            {
                menu.AddSpacer(2f);
                menu.AddRow(
                    held.IsOwned ? AuroraL.Get("door.owned.by", held.OwnerName) : AuroraL.Get("door.for.sale"),
                    "door",
                    held.PriceLine,
                    held.IsOwned && held.OwnerId == AuroraRuntime.LocalId ? () => doors.RequestSale(held) : () => doors.RequestPurchase(held),
                    AuroraButton.Style.Primary);
            }

            // Список дверей поблизости
            var nearby = doors.NearbyDoors(AuroraRuntime.LocalId, 20f);
            if (nearby.Count > 0)
            {
                menu.AddSpacer(4f);
                menu.AddSection(AuroraL.Get("common.select"), UiTheme.Violet);

                int perPage = 3;
                int pages = Mathf.CeilToInt(nearby.Count / (float)perPage);
                int page = Mathf.Clamp(menu.DoorPageIndex, 0, Mathf.Max(0, pages - 1));
                int start = page * perPage;

                for (int i = start; i < Mathf.Min(start + perPage, nearby.Count); i++)
                {
                    var door = nearby[i];
                    var d = door;
                    menu.AddRow(
                        door.Label,
                        "door",
                        door.PriceLine,
                        door.IsOwned && door.OwnerId == AuroraRuntime.LocalId ? () => doors.RequestSale(d) : () => doors.RequestPurchase(d));
                }

                if (pages > 1)
                {
                    AddPager(menu, page, pages, () => menu.DoorPageIndex++, () => menu.DoorPageIndex--);
                }
            }
        }

        // --------------------------------------------------------------- контракты

        public static void BuildContracts(HandMenu menu)
        {
            var contracts = AuroraRuntime.Contracts;
            bool isHitman = AuroraRuntime.Roles.Has(AuroraRuntime.LocalId, RoleAbilities.Contracts);

            menu.AddSection(AuroraL.Get("contract.header"), UiTheme.Danger);

            if (!isHitman)
            {
                menu.AddRow(
                    AuroraL.Get("common.role") + ": " + AuroraL.Role(AuroraRuntime.LocalPlayer.Role),
                    "contract",
                    AuroraL.Get("contract.role.only"),
                    null,
                    AuroraButton.Style.Ghost,
                    false);

                bool hitmanFree = !AuroraRuntime.Roles.IsRoleFull(AuroraRoleId.Hitman);
                menu.AddRow(
                    AuroraL.Role(AuroraRoleId.Hitman),
                    "contract",
                    hitmanFree ? RoleCatalog.Get(AuroraRoleId.Hitman).Description : AuroraL.Get("roles.hitman.taken"),
                    () => AuroraRuntime.Net.RequestRoleChange(AuroraRoleId.Hitman),
                    AuroraButton.Style.Danger,
                    hitmanFree && (AuroraConfig.Current.allowSelfRoleSelect || AuroraRuntime.Net.IsHost));
                return;
            }

            var active = contracts.ActiveForHitman(AuroraRuntime.LocalId);

            if (active.Count == 0)
            {
                menu.AddInfo(AuroraL.Get("contract.none"), UiTheme.TextDim);
            }
            else
            {
                int perPage = 3;
                int pages = Mathf.CeilToInt(active.Count / (float)perPage);
                int page = Mathf.Clamp(menu.ContractPageIndex, 0, Mathf.Max(0, pages - 1));
                int start = page * perPage;

                for (int i = start; i < Mathf.Min(start + perPage, active.Count); i++)
                {
                    var contract = active[i];
                    var c = contract;

                    menu.AddRow(
                        AuroraL.Get("contract.target") + ": " + contract.TargetName,
                        "skull",
                        AuroraL.Get("contract.reward") + ": " + AuroraUtils.Money(contract.Reward)
                        + (contract.Status == ContractRecord.StatusAccepted ? "  ·  " + AuroraL.Get("contract.accept") : ""),
                        () => contracts.AcceptContract(c),
                        contract.Status == ContractRecord.StatusAccepted ? AuroraButton.Style.Primary : AuroraButton.Style.Default,
                        true,
                        contract.Status == ContractRecord.StatusAccepted);

                    menu.AddRow(
                        AuroraL.Get("contract.cancel"),
                        "info",
                        AuroraL.Get("contract.escrow"),
                        () => contracts.CancelContract(c),
                        AuroraButton.Style.Ghost);
                }

                if (pages > 1)
                {
                    AddPager(menu, page, pages, () => menu.ContractPageIndex++, () => menu.ContractPageIndex--);
                }
            }

            menu.AddSpacer(6f);
            menu.AddSection(AuroraL.Get("contract.new"), UiTheme.Warning);

            var players = AuroraRuntime.State.OtherPlayers(AuroraRuntime.LocalId);
            menu.AddInfo(AuroraL.Get("contract.escrow"), UiTheme.TextDim, 56f);

            foreach (var player in players)
            {
                if (!menu.HasRoom(120f))
                {
                    break;
                }

                var p = player;
                menu.AddRow(
                    p.name,
                    "skull",
                    AuroraL.Get("contract.reward") + ": " + AuroraUtils.Money(contracts.PendingReward),
                    () => contracts.CreateContract(p.id, contracts.PendingReward),
                    AuroraButton.Style.Danger);
            }

            if (players.Count == 0)
            {
                menu.AddInfo(AuroraL.Get("contract.target.self"), UiTheme.TextDim, 34f);
            }

            // Изменение суммы заказа
            menu.AddPairRow("+500", () => contracts.AdjustPendingReward(500), "-500", () => contracts.AdjustPendingReward(-500), "cash");
        }

        // ------------------------------------------------------------------ настройки

        public static void BuildSettings(HandMenu menu)
        {
            var cfg = AuroraConfig.Current;

            menu.AddSection(AuroraL.Get("settings.header"), UiTheme.Accent);

            menu.AddRow(
                AuroraL.Get("settings.language") + ": " + (AuroraL.Current == AuroraL.Lang.RU ? "Русский" : "English"),
                "info",
                null,
                () =>
                {
                    AuroraL.Current = AuroraL.Current == AuroraL.Lang.RU ? AuroraL.Lang.EN : AuroraL.Lang.RU;
                    cfg.language = AuroraL.Current == AuroraL.Lang.RU ? "ru" : "en";
                    AuroraConfig.Save();
                    menu.Rebuild();
                });

            menu.AddRow(
                AuroraL.Get("settings.sfx") + ": " + Mathf.RoundToInt(cfg.sfxVolume * 100f) + "%",
                "cash",
                AuroraL.Get("settings.sfx.volume"),
                () =>
                {
                    cfg.sfxVolume = cfg.sfxVolume >= 0.99f ? 0f : Mathf.Min(1f, cfg.sfxVolume + 0.25f);
                    AuroraConfig.Save();
                    AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Click);
                    menu.Rebuild();
                });

            menu.AddRow(
                AuroraL.Get("settings.menu.scale") + ": " + cfg.menuScale.ToString("0.00"),
                "gear",
                null,
                () =>
                {
                    cfg.menuScale = cfg.menuScale >= 1.49f ? 0.75f : Mathf.Min(1.5f, cfg.menuScale + 0.25f);
                    AuroraConfig.Save();
                    menu.ApplyScale();
                    menu.Rebuild();
                });

            menu.AddRow(
                AuroraL.Get("settings.haptics") + ": " + (cfg.haptics ? AuroraL.Get("common.yes") : AuroraL.Get("common.no")),
                "info",
                null,
                () =>
                {
                    cfg.haptics = !cfg.haptics;
                    AuroraConfig.Save();
                    menu.Rebuild();
                });

            menu.AddRow(
                AuroraL.Get("settings.hud") + ": " + (cfg.wristHud ? AuroraL.Get("common.yes") : AuroraL.Get("common.no")),
                "info",
                null,
                () =>
                {
                    cfg.wristHud = !cfg.wristHud;
                    AuroraConfig.Save();
                    menu.ApplyScale();
                    menu.Rebuild();
                });

            menu.AddRow(
                AuroraL.Get("roles.selfselect") + ": " + (cfg.allowSelfRoleSelect ? AuroraL.Get("common.yes") : AuroraL.Get("common.no")),
                "users",
                null,
                () =>
                {
                    cfg.allowSelfRoleSelect = !cfg.allowSelfRoleSelect;
                    AuroraConfig.Save();
                    menu.Rebuild();
                });

            if (!AuroraRuntime.Net.IsHost)
            {
                return;
            }

            menu.AddSpacer(6f);
            menu.AddSection(AuroraL.Get("settings.host.panel"), UiTheme.Warning);

            menu.AddRow(
                AuroraL.Get("settings.host.give"),
                "cash",
                null,
                () => AuroraRuntime.Authority.HostGiveEveryone(1000),
                AuroraButton.Style.Primary);

            menu.AddRow(
                AuroraL.Get("settings.host.reset"),
                "info",
                AuroraRuntime.Authority.ResetConfirmPending ? AuroraL.Get("settings.host.reset.confirm") : null,
                () =>
                {
                    if (AuroraRuntime.Authority.ResetConfirmPending)
                    {
                        AuroraRuntime.Authority.HostResetEconomy();
                    }
                    else
                    {
                        AuroraRuntime.Authority.ResetConfirmPending = true;
                        menu.Rebuild();
                    }
                },
                AuroraButton.Style.Danger);

            menu.AddInfo(AuroraL.Get("settings.help.body"), UiTheme.TextDim, 70f);
        }

        // ------------------------------------------------------------------- помощники

        private static void AddPager(HandMenu menu, int page, int pages, Action next, Action prev)
        {
            menu.AddPairRow("◂", prev, "▸", next, "info");
            menu.AddInfo($"{page + 1} / {pages}", UiTheme.TextDim, 30f);
        }
    }
}
