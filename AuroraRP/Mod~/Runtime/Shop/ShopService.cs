using System;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Магазин оружия. Паки (Hoodpack, Kazus Micropack и др.) спавнит только продавец оружия,
    /// покупая их через меню. Цены из ТЗ: пистолеты 1500, автоматы/дробовики 3000, снайперки 4000.
    /// </summary>
    public class ShopService
    {
        private readonly AuroraState _state;
        private readonly WalletService _wallet;
        private readonly RoleService _roles;
        private readonly SpawnService _spawn;

        public ShopService(AuroraState state, WalletService wallet, RoleService roles, SpawnService spawn)
        {
            _state = state;
            _wallet = wallet;
            _roles = roles;
            _spawn = spawn;
        }

        public void Tick(float dt)
        {
            // Магазин не требует пошаговой логики — оставлено для будущих акций/скидок.
        }

        // ------------------------------------------------------------------ доступ

        public bool CanUse(byte playerId, out string reason)
        {
            reason = null;

            if (!AuroraConfig.Current.shopDealerOnly)
            {
                return true;
            }

            if (!_roles.Has(playerId, RoleAbilities.WeaponShop))
            {
                reason = AuroraL.Get("shop.locked", AuroraL.Role(AuroraRoleId.GunDealer));
                return false;
            }

            return true;
        }

        public bool CanUseLocalPlayer(out string reason) => CanUse(AuroraRuntime.LocalId, out reason);

        public bool Exists(ShopEntry entry, out string title)
        {
            title = entry?.title;

            if (entry == null || string.IsNullOrWhiteSpace(entry.barcode))
            {
                return false;
            }

            // Ванильные barcode'ы и паковские проверяем одинаково — через склад ассетов.
            return _spawn.Exists(entry.barcode, out title);
        }

        /// <summary>Цена для конкретного покупателя (для продавца — закупка, для остальных — с наценкой).</summary>
        public int PriceFor(ShopEntry entry, byte sellerId, byte buyerId)
        {
            if (entry == null)
            {
                return 0;
            }

            if (sellerId == buyerId)
            {
                return entry.price;
            }

            float markup = 1f + Mathf.Clamp(AuroraConfig.Current.dealerMarkupPercent, 0, 500) / 100f;
            return Mathf.RoundToInt(entry.price * markup);
        }

        /// <summary>Сколько заработает продавец, если продаст весь каталог по одной штуке.</summary>
        public long DealerNetProfit()
        {
            long sum = 0;
            var items = AuroraConfig.Current.shopItems;
            for (int i = 0; i < items.Count; i++)
            {
                sum += Mathf.RoundToInt(items[i].price * (1f + AuroraConfig.Current.dealerMarkupPercent / 100f)) - items[i].price;
            }

            return sum;
        }

        // ------------------------------------------------------------------ покупка

        /// <summary>
        /// Покупка. Если target != local — продавец продаёт оружие другому игроку по наценке.
        /// </summary>
        public void Purchase(ShopEntry entry, byte targetPlayerId)
        {
            if (entry == null)
            {
                return;
            }

            if (!CanUseLocalPlayer(out string reason))
            {
                AuroraNotifications.Send(reason, UiTheme.Warning);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            if (!Exists(entry, out _))
            {
                AuroraNotifications.Send("Barcode не найден: " + entry.barcode, UiTheme.Danger);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            byte sellerId = AuroraRuntime.LocalId;
            int price = PriceFor(entry, sellerId, targetPlayerId);

            // Проверка лицензии у покупателя.
            if (AuroraConfig.Current.requireWeaponLicense || entry.requiresLicense)
            {
                var buyer = _state.Get(targetPlayerId);
                if (buyer != null && !buyer.hasLicense)
                {
                    AuroraNotifications.Send(AuroraL.Get("shop.no.license"), UiTheme.Warning);
                    AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                    return;
                }
            }

            long payerBalance = _wallet.GetBalance(targetPlayerId);

            if (targetPlayerId == sellerId)
            {
                // Покупка у поставщика для себя: списываем цену закупки.
                if (!_wallet.TrySpend(sellerId, price, "закупка " + entry.title))
                {
                    AuroraNotifications.Send(AuroraL.Get("shop.no.funds"), UiTheme.Danger);
                    AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                    return;
                }

                _spawn.SpawnForPlayer(sellerId, entry.barcode);
                AuroraNotifications.Send(AuroraL.Get("shop.bought", entry.title, AuroraUtils.Money(price)), UiTheme.Success, 4f, true);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Purchase);
                return;
            }

            // Продажа другому игроку — движение денег делает хост.
            if (payerBalance < price)
            {
                AuroraNotifications.Send(AuroraL.Get("shop.no.funds"), UiTheme.Danger);
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.Error);
                return;
            }

            AuroraRuntime.Net.SendShopPurchase(sellerId, targetPlayerId, entry.barcode, price, entry.title);
        }

        /// <summary>Применение покупки на хосте (после проверок).</summary>
        public void ApplyPurchase(byte sellerId, byte buyerId, string barcode, int price, string title)
        {
            if (buyerId == sellerId)
            {
                _spawn.SpawnForPlayer(buyerId, barcode);
                return;
            }

            if (!_wallet.TryTransfer(buyerId, sellerId, price, "покупка " + title, out string error))
            {
                AuroraRuntime.Notify(sellerId, error ?? AuroraL.Get("shop.no.funds"), UiTheme.Danger);
                return;
            }

            _spawn.SpawnForPlayer(buyerId, barcode);
            AuroraRuntime.Notify(buyerId, AuroraL.Get("shop.bought", title, AuroraUtils.Money(price)), UiTheme.Success);
            AuroraRuntime.Notify(sellerId, AuroraL.Get("shop.bought", title, AuroraUtils.Money(price)), UiTheme.Money);
        }
    }
}
