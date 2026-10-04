using System.Collections.Generic;

namespace AuroraRP
{
    /// <summary>
    /// Локализация. Ключи — английские идентификаторы, значения — русский (по умолчанию) и английский.
    /// Язык задаётся в конфиге: "ru" или "en".
    /// </summary>
    public static class AuroraL
    {
        public enum Lang
        {
            RU = 0,
            EN = 1
        }

        public static Lang Current = Lang.RU;

        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ------------------------------------------------------------------ общее
            { "mod.name",                 new[] { "AuroraRP", "AuroraRP" } },
            { "common.yes",               new[] { "Да", "Yes" } },
            { "common.no",                new[] { "Нет", "No" } },
            { "common.back",              new[] { "Назад", "Back" } },
            { "common.close",             new[] { "Закрыть", "Close" } },
            { "common.back",              new[] { "Назад", "Back" } },
            { "common.buy",               new[] { "Купить", "Buy" } },
            { "common.sell",              new[] { "Продать", "Sell" } },
            { "common.cancel",            new[] { "Отмена", "Cancel" } },
            { "common.confirm",           new[] { "Подтвердить", "Confirm" } },
            { "common.price",             new[] { "Цена", "Price" } },
            { "common.owner",             new[] { "Владелец", "Owner" } },
            { "common.nobody",            new[] { "Никто", "Nobody" } },
            { "common.you",               new[] { "Вы", "You" } },
            { "common.locked",            new[] { "Недоступно", "Locked" } },
            { "common.error",             new[] { "Ошибка", "Error" } },
            { "common.success",           new[] { "Готово", "Done" } },
            { "common.amount",            new[] { "Сумма", "Amount" } },
            { "common.player",            new[] { "Игрок", "Player" } },
            { "common.players",           new[] { "Игроки", "Players" } },
            { "common.role",              new[] { "Роль", "Role" } },
            { "common.money",             new[] { "Баланс", "Balance" } },
            { "common.select",            new[] { "Выбрать", "Select" } },
            { "common.take",              new[] { "Взять", "Take" } },
            { "common.hold",              new[] { "Удерживайте", "Hold" } },
            { "common.seconds",           new[] { "сек.", "sec." } },
            { "common.refresh",           new[] { "Обновить", "Refresh" } },

            // ------------------------------------------------------------------ меню
            { "menu.title",               new[] { "AURORA RP", "AURORA RP" } },
            { "menu.subtitle",            new[] { "Терминал штата", "State terminal" } },
            { "menu.tab.main",            new[] { "Общее", "Main" } },
            { "menu.tab.roles",           new[] { "Роли", "Roles" } },
            { "menu.tab.wallet",          new[] { "Кошелёк", "Wallet" } },
            { "menu.tab.shop",            new[] { "Магазин", "Shop" } },
            { "menu.tab.doors",           new[] { "Двери", "Doors" } },
            { "menu.tab.contracts",       new[] { "Контракты", "Contracts" } },
            { "menu.tab.settings",        new[] { "Настройки", "Settings" } },
            { "menu.hint.open",           new[] { "Y + A — открыть меню", "Y + A to open the menu" } },
            { "menu.hint.press",          new[] { "Нажмите триггер, чтобы выбрать пункт", "Press the trigger to select an item" } },
            { "menu.hint.poke",           new[] { "Или ткните правой рукой прямо в пункт", "Or poke an item with your right hand" } },
            { "menu.footer",              new[] { "AuroraRP • Phoenix Framework", "AuroraRP • Phoenix Framework" } },

            // ------------------------------------------------------------------ роли
            { "role.Citizen",             new[] { "Гражданин", "Citizen" } },
            { "role.Police",              new[] { "Полицейский", "Police" } },
            { "role.GunDealer",           new[] { "Продавец оружия", "Gun Dealer" } },
            { "role.Smuggler",            new[] { "Контрабандист", "Smuggler" } },
            { "role.Gangster",            new[] { "Гангстер", "Gangster" } },
            { "role.Hitman",              new[] { "Наёмный убийца", "Hitman" } },

            { "role.Citizen.desc",        new[] { "Базовая роль. Стройте легальный бизнес и отыгрывайте свой сценарий.", "Base role. Build a legal business and roleplay freely." } },
            { "role.Police.desc",         new[] { "Юнит закона: аресты, проверка лицензий, патруль. Стартовый набор: AR-15 и GLOCK-17.", "Law unit: arrests, license checks, patrol. Starter kit: AR-15 and GLOCK-17." } },
            { "role.GunDealer.desc",      new[] { "Покупает оружие оптом и перепродаёт игрокам с наценкой. Только он может спавнить паки.", "Buys weapons wholesale and resells them with a markup. The only role able to spawn weapon packs." } },
            { "role.Smuggler.desc",       new[] { "Торгует нелегальным: отмычки, маскировка. Маскируется под жителя, но роль видна всем.", "Sells illegal goods: lockpicks, disguises. Blends in as a resident, but everyone sees the role." } },
            { "role.Gangster.desc",       new[] { "Грабит прохожих, ворует маники и взламывает чужие дома.", "Rob passers-by, steal money printers and break into houses." } },
            { "role.Hitman.desc",         new[] { "Выполняет заказы на устранение за деньги. Максимум 1 на сервере. Заказы принимаются в меню.", "Takes assassination contracts for money. Max 1 per server. Contracts are managed in the menu." } },

            { "roles.header",             new[] { "Выбор роли", "Role selection" } },
            { "roles.current",            new[] { "Текущая роль", "Current role" } },
            { "roles.change",             new[] { "Сменить роль", "Change role" } },
            { "roles.taken",              new[] { "Занята (макс. {0})", "Taken (max {0})" } },
            { "roles.hitman.taken",       new[] { "Наёмник уже есть на сервере", "A hitman already exists on this server" } },
            { "roles.changed",            new[] { "Вы теперь: {0}", "You are now: {0}" } },
            { "roles.denied",             new[] { "Смена роли запрещена хостом", "Role change is disabled by the host" } },
            { "roles.selfselect",         new[] { "Свободный выбор роли", "Free role selection" } },

            // ------------------------------------------------------------------ деньги
            { "money.header",             new[] { "Кошелёк", "Wallet" } },
            { "money.balance",            new[] { "На счету: {0}", "Balance: {0}" } },
            { "money.transfer",           new[] { "Передать деньги", "Transfer money" } },
            { "money.transfer.to",        new[] { "Получатель", "Recipient" } },
            { "money.transfer.amount",    new[] { "Сумма перевода", "Transfer amount" } },
            { "money.transfer.howto",     new[] { "Как передать: возьмите ПРАВОЙ рукой руку игрока и удерживайте 5 секунд.", "How to transfer: grab the player's hand with your RIGHT hand and hold for 5 seconds." } },
            { "money.transfer.howto2",    new[] { "Выставьте сумму кнопками ниже и не отпускайте руку.", "Set the amount with the buttons below and keep holding." } },
            { "money.transfer.pending",   new[] { "Выставлено к передаче:", "Queued to transfer:" } },
            { "money.transfer.progress",  new[] { "Удержание: {0}%", "Holding: {0}%" } },
            { "money.transfer.sent",      new[] { "Передано {0} игроку {1}", "Sent {0} to {1}" } },
            { "money.transfer.received",  new[] { "Получено {0} от {1}", "Received {0} from {1}" } },
            { "money.transfer.fail.funds",new[] { "Недостаточно средств", "Not enough money" } },
            { "money.transfer.fail.self", new[] { "Нельзя передать деньги самому себе", "You cannot transfer money to yourself" } },
            { "money.transfer.fail.sum",  new[] { "Нужна сумма больше нуля", "Amount must be greater than zero" } },
            { "money.transfer.fail.player",new[]{ "Игрок не найден", "Player not found" } },
            { "money.transfer.instruction",new[]{ "Сумма: {0}. Возьмите руку игрока правой рукой и держите 5 сек.", "Amount: {0}. Grab the player's hand with your right hand and hold 5 sec." } },
            { "money.plus100",            new[] { "+100", "+100" } },
            { "money.minus100",           new[] { "-100", "-100" } },
            { "money.plus1000",           new[] { "+1000", "+1000" } },
            { "money.minus1000",          new[] { "-1000", "-1000" } },
            { "money.reset",              new[] { "Сброс", "Reset" } },
            { "money.bank.sync",          new[] { "Банк синхронизирован с Discord", "Bank synced with Discord" } },
            { "money.bank.off",           new[] { "Discord-вебхук не настроен", "Discord webhook is not configured" } },

            // ------------------------------------------------------------------ магазин
            { "shop.header",              new[] { "Магазин оружия", "Weapon shop" } },
            { "shop.locked",              new[] { "Магазин доступен только роли «{0}»", "Shop is only available to the \"{0}\" role" } },
            { "shop.buy",                 new[] { "Купить за {0}", "Buy for {0}" } },
            { "shop.spawn",               new[] { "Заспавнить ({0})", "Spawn ({0})" } },
            { "shop.category.pistol",     new[] { "Пистолеты", "Pistols" } },
            { "shop.category.rifle",      new[] { "Автоматы", "Rifles" } },
            { "shop.category.shotgun",    new[] { "Дробовики", "Shotguns" } },
            { "shop.category.sniper",     new[] { "Снайперские", "Snipers" } },
            { "shop.category.illegal",    new[] { "Нелегальное", "Illegal goods" } },
            { "shop.empty",               new[] { "Нет позиций: заполните barcodes в конфиге", "No items: fill in barcodes in the config" } },
            { "shop.bought",              new[] { "Куплено: {0} за {1}", "Bought: {0} for {1}" } },
            { "shop.no.funds",            new[] { "Недостаточно средств для покупки", "Not enough money" } },
            { "shop.no.license",          new[] { "У покупателя нет лицензии на оружие", "The buyer has no weapon license" } },
            { "shop.license.issued",      new[] { "Лицензия выдана игроку {0}", "License issued to {0}" } },
            { "shop.license.revoked",     new[] { "Лицензия отозвана у игрока {0}", "License revoked from {0}" } },
            { "shop.dealer.markup",       new[] { "Наценка продавца: +{0}%", "Dealer markup: +{0}%" } },
            { "shop.hand.full",           new[] { "Руки заняты — предмет выброшен рядом", "Hands are busy — the item was dropped nearby" } },

            // ------------------------------------------------------------------ двери
            { "door.header",              new[] { "Недвижимость", "Real estate" } },
            { "door.for.sale",            new[] { "ПРОДАЁТСЯ", "FOR SALE" } },
            { "door.price",               new[] { "Цена: {0}", "Price: {0}" } },
            { "door.owned.by",            new[] { "Владелец: {0}", "Owner: {0}" } },
            { "door.howto.buy",           new[] { "Возьмите дверь рукой и нажмите B — покупка", "Grab the door and press B — buy" } },
            { "door.howto.sell",          new[] { "Нажмите B 3 раза подряд — продажа", "Press B 3 times in a row — sell" } },
            { "door.howto.yours",         new[] { "Это ваша дверь. Нажмите B 3 раза, чтобы продать", "This is your door. Press B 3 times to sell" } },
            { "door.too.expensive",       new[] { "Не хватает денег: нужно {0}", "Not enough money: {0} needed" } },
            { "door.bought",              new[] { "Дверь куплена за {0}", "Door purchased for {0}" } },
            { "door.sold",                new[] { "Дверь продана, возвращено {0}", "Door sold, {0} refunded" } },
            { "door.already.owned",       new[] { "У вас уже есть дверь (максимум 1)", "You already own a door (max 1)" } },
            { "door.police.denied",       new[] { "Полицейским запрещено владеть дверями", "Police officers cannot own doors" } },
            { "door.limit",               new[] { "Максимум 1 дверь", "Maximum 1 door" } },
            { "door.sell.progress",       new[] { "Продажа: {0}/3 (B)", "Selling: {0}/3 (B)" } },
            { "door.owned.list",          new[] { "Ваша дверь: {0}", "Your door: {0}" } },
            { "door.none",                new[] { "У вас нет дверей", "You don't own any doors" } },
            { "door.released",            new[] { "Дверь освобождена: владелец вышел", "Door released: owner left" } },
            { "door.help",                new[] { "Смотрите на дверь и нажмите «Купить» — или возьмите её рукой и нажмите B.", "Look at a door and press \"Buy\" — or grab it and press B." } },

            // ------------------------------------------------------------------ контракты
            { "contract.header",          new[] { "Контракты на устранение", "Assassination contracts" } },
            { "contract.role.only",       new[] { "Доступно только наёмному убийце", "Only the hitman has access" } },
            { "contract.new",             new[] { "Новый заказ", "New contract" } },
            { "contract.target",          new[] { "Цель", "Target" } },
            { "contract.reward",          new[] { "Награда", "Reward" } },
            { "contract.order",           new[] { "Заказать", "Order" } },
            { "contract.accept",          new[] { "Принять", "Accept" } },
            { "contract.complete",        new[] { "Выполнено", "Completed" } },
            { "contract.cancel",          new[] { "Отменить", "Cancel" } },
            { "contract.escrow",          new[] { "Награда списывается сразу и уходит исполнителю после устранения.", "The reward is escrowed and paid to the killer after the elimination." } },
            { "contract.created",         new[] { "Заказ создан: {0} за {1}", "Contract created: {0} for {1}" } },
            { "contract.accepted",        new[] { "Вы приняли заказ на {0} ({1})", "You accepted a contract on {0} ({1})" } },
            { "contract.done",            new[] { "Контракт выполнен! +{0}", "Contract complete! +{0}" } },
            { "contract.cancelled",       new[] { "Контракт отменён, возврат {0}", "Contract cancelled, {0} refunded" } },
            { "contract.failed.funds",    new[] { "Недостаточно денег для награды", "Not enough money for the reward" } },
            { "contract.none",            new[] { "Активных контрактов нет", "No active contracts" } },
            { "contract.target.self",     new[] { "Нельзя заказать самого себя", "You cannot order yourself" } },
            { "contract.hitman.none",     new[] { "На сервере нет наёмника — заказ будет ждать", "No hitman in the server — the contract will wait" } },
            { "contract.kill.confirmed",  new[] { "Цель устранена", "Target eliminated" } },

            // ------------------------------------------------------------------ настройки
            { "settings.header",          new[] { "Настройки", "Settings" } },
            { "settings.language",        new[] { "Язык", "Language" } },
            { "settings.sfx",             new[] { "Звуки", "Sounds" } },
            { "settings.sfx.volume",      new[] { "Громкость", "Volume" } },
            { "settings.menu.scale",      new[] { "Размер меню", "Menu scale" } },
            { "settings.hud",             new[] { "Браслет-подсказка", "Wrist HUD" } },
            { "settings.haptics",         new[] { "Вибрация", "Haptics" } },
            { "settings.host.panel",      new[] { "Панель хоста", "Host panel" } },
            { "settings.host.selfselect", new[] { "Разрешить выбор ролей", "Allow role selection" } },
            { "settings.host.reset",      new[] { "Сбросить экономику", "Reset economy" } },
            { "settings.host.reset.confirm", new[] { "Нажмите ещё раз для подтверждения", "Press again to confirm" } },
            { "settings.host.give",       new[] { "Выдать $1000 всем", "Give $1000 to everyone" } },
            { "settings.help",            new[] { "Помощь", "Help" } },
            { "settings.items",           new[] { "Предметы AuroraRP", "AuroraRP items" } },
            { "settings.spawn.terminal",  new[] { "Поставить банковский терминал", "Place bank terminal" } },
            { "settings.spawn.terminal.hint", new[] { "берёшь в руку и жмёшь B — откроется меню", "grab it and press B to open the menu" } },
            { "settings.spawn.printer",   new[] { "Поставить принтер денег", "Place money printer" } },
            { "settings.spawn.printer.hint", new[] { "печатает деньги владельцу, пока он рядом", "prints money for its owner while he is near" } },
            { "settings.spawn.ok",        new[] { "Появилось рядом", "Spawned nearby" } },
            { "settings.spawn.fail",      new[] { "Не получилось поставить", "Could not place it" } },
            { "settings.help.body",       new[] { "Управление: Y + A — меню (настраивается в конфиге), X — суммы перевода, B — двери и принтеры.", "Controls: Y + A — menu (configurable), X — transfer amounts, B — doors and printers." } },

            // ------------------------------------------------------------------ контент / сеть
            { "content.header",           new[] { "Контент мода", "Mod content" } },
            { "content.pallet.present",   new[] { "Палет AuroraRP установлен", "AuroraRP pallet installed" } },
            { "content.pallet.missing",   new[] { "Палета AuroraRP нет — предметы мода невидимы", "AuroraRP pallet is missing — mod items are invisible" } },
            { "content.pallet.button",    new[] { "Скачать палет с mod.io", "Download pallet from mod.io" } },
            { "content.pallet.starting",  new[] { "Скачиваю палет AuroraRP с mod.io…", "Downloading the AuroraRP pallet from mod.io…" } },
            { "content.pallet.done",      new[] { "Палет AuroraRP установлен и загружен", "AuroraRP pallet downloaded and loaded" } },
            { "content.pallet.failed",    new[] { "Не удалось скачать палет. Войдите в mod.io в игре и повторите", "Could not download the pallet. Log into mod.io in-game and try again" } },
            { "content.pallet.noid",      new[] { "В конфиге не указан modioModId — скачайте палет вручную", "modioModId is not set in the config — install the pallet manually" } },
            { "content.pallet.busy",      new[] { "Загрузка уже идёт…", "Download already in progress…" } },
            { "content.players.missing",  new[] { "Без AuroraRP: {0}", "Without AuroraRP: {0}" } },
            { "content.players.allok",    new[] { "У всех игроков есть AuroraRP", "Everyone has AuroraRP" } },
            { "bridge.passive.paid",      new[] { "{0} (без мода) расплатился: {1}", "{0} (no mod) paid: {1}" } },
            { "bridge.passive.poor",      new[] { "{0} (без мода) хотел заплатить, но не хватило денег", "{0} (no mod) tried to pay but is short on money" } },
            { "bridge.door.passive",      new[] { "{0} (без мода) выкупил дверь «{1}»", "{0} (no mod) bought the door \"{1}\"" } },
            { "content.peer.missing",     new[] { "{0} играет без AuroraRP — роли и деньги у него не работают", "{0} is playing without AuroraRP — roles and money will not work for them" } },
            { "notify.pallet.download",   new[] { "Хост спавнит предмет AuroraRP — качаю палет с mod.io", "Host spawned an AuroraRP item — downloading the pallet from mod.io" } },
            { "notify.pallet.ready",      new[] { "Палет AuroraRP загружен", "AuroraRP pallet loaded" } },

            // ------------------------------------------------------------------ уведомления
            { "notify.role.assigned",     new[] { "Ваша роль: {0}", "Your role: {0}" } },
            { "notify.weapon.spawned",    new[] { "Оружие заспавнено рядом с вами", "A weapon was spawned next to you" } },
            { "notify.license.check",     new[] { "Проверка лицензии: {0} — {1}", "License check: {0} — {1}" } },
            { "notify.license.valid",     new[] { "лицензия есть", "licensed" } },
            { "notify.license.invalid",   new[] { "лицензии нет", "not licensed" } },
            { "notify.arrest",            new[] { "{0} арестован", "{0} was arrested" } },
            { "notify.arrest.you",        new[] { "Вас арестовали", "You were arrested" } },
            { "notify.robbery",           new[] { "{0} ограбил {1} на {2}", "{0} robbed {1} for {2}" } },
            { "notify.player.joined",     new[] { "{0} в городе · роль: {1}", "{0} joined · role: {1}" } },
            { "notify.player.left",       new[] { "{0} покинул город", "{0} left the city" } },
            { "notify.hitman.chosen",     new[] { "Наёмником стал {0}", "{0} is the hitman" } },
            { "notify.shop.open",         new[] { "Магазин: выберите оружие в меню", "Shop: pick a weapon in the menu" } }
        };

        public static string Get(string key)
        {
            if (Table.TryGetValue(key, out var pair))
            {
                int idx = (int)Current;
                if (idx < pair.Length && !string.IsNullOrEmpty(pair[idx]))
                {
                    return pair[idx];
                }

                return pair[0];
            }

            return key;
        }

        public static string Get(string key, params object[] args)
        {
            string text = Get(key);
            if (args == null || args.Length == 0)
            {
                return text;
            }

            try
            {
                return string.Format(text, args);
            }
            catch (System.FormatException)
            {
                return text;
            }
        }

        /// <summary>Строка "роль" в текущем языке по её идентификатору.</summary>
        public static string Role(AuroraRoleId role)
        {
            return Get("role." + role);
        }

        public static string RoleDescription(AuroraRoleId role)
        {
            return Get("role." + role + ".desc");
        }
    }
}
