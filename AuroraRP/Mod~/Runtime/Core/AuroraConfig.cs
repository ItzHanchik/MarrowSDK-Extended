using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Конфиг мода. Основной файл лежит в палете (MODS\AuroraRP\config.json) — то есть
    /// «система» приезжает к игрокам вместе с контентом. Личный файл хоста
    /// (MelonLoader/UserData/AuroraRP/config.json) создаётся при правках и перекрывает палетный.
    /// Здесь же — дефолтный каталог оружия: barcode'ы берутся из паков (Hoodpack, Kazus Micropack и др.).
    /// </summary>
    [Serializable]
    public class AuroraConfig
    {
        // ------------------------------------------------------------------ общие

        /// <summary>Язык интерфейса: "ru" или "en".</summary>
        public string language = "ru";

        /// <summary>Уровень логов: 0 = debug, 1 = info, 2 = warn, 3 = error.</summary>
        public int logLevel = 1;

        /// <summary>Множитель размера меню (0.5 .. 2.0).</summary>
        public float menuScale = 1f;

        /// <summary>Показывать браслет с балансом и ролью на левом предплечье.</summary>
        public bool wristHud = true;

        /// <summary>Показывать над головами игроков их роль (LabFusion).</summary>
        public bool showRoleTags = true;

        /// <summary>Вибрация контроллеров при наведении/нажатии.</summary>
        public bool haptics = true;

        /// <summary>Громкость звуков мода (0..1).</summary>
        public float sfxVolume = 0.7f;

        /// <summary>Клавиша/кнопка открытия меню: "both_triggers_double" (по умолчанию), "thumbstick", "menu_tap".</summary>
        public string menuOpenGesture = "y_and_a";

        /// <summary>Окно двойного щелчка триггеров (запасной жест), сек.</summary>
        public float doubleClickWindow = 0.55f;

        // ------------------------------------------------------------------ экономика

        /// <summary>Стартовый баланс нового игрока.</summary>
        public long startBalance = 5000;

        /// <summary>Сколько секунд нужно держать руку игрока для перевода денег.</summary>
        public float transferHoldSeconds = 5f;

        /// <summary>Максимальная дистанция между руками для перевода (метры).</summary>
        public float transferHandDistance = 0.35f;

        /// <summary>Переводить деньги автоматически сразу после удержания (true) или требовать подтверждение (false).</summary>
        public bool transferAutoConfirm = true;

        /// <summary>Шаг кнопок суммы перевода.</summary>
        public int transferStepSmall = 100;

        public int transferStepBig = 1000;

        /// <summary>Максимальная сумма одного перевода (0 = без ограничения).</summary>
        public long transferMax = 0;

        // ------------------------------------------------------------------ Discord

        /// <summary>Включить синхронизацию банка с Discord.</summary>
        public bool discordBankEnabled = true;

        /// <summary>
        /// Обфусцированный вебхук (Base64 + XOR). Заполняется автоматически из discordWebhookPlain.
        /// Никогда не публикуйте это значение: обфускация защищает только от случайного просмотра, не от читеров.
        /// </summary>
        public string discordWebhookObfuscated = "";

        /// <summary>Сюда можно вставить вебхук открытым текстом — при первом запуске он будет зашифрован и поле очистится.</summary>
        public string discordWebhookPlain = "";

        /// <summary>Имя вебхука (для красоты сообщения).</summary>
        public string discordBotName = "AuroraRP Bank";

        /// <summary>Минимальный интервал между правками сообщения банка, сек (Discord режет частые правки).</summary>
        public float discordUpdateInterval = 15f;

        /// <summary>Шапка сообщения банка.</summary>
        public string discordBankHeader = "🏦 Банк штата Aurora";

        // ------------------------------------------------------------------ двери

        /// <summary>Цена двери.</summary>
        public int doorPrice = 2000;

        /// <summary>Максимум дверей на игрока.</summary>
        public int doorMaxPerPlayer = 1;

        /// <summary>Запрещать владение дверьми полиции.</summary>
        public bool doorPoliceForbidden = true;

        /// <summary>Сколько нажатий B нужно для продажи двери.</summary>
        public int doorSellPresses = 3;

        /// <summary>Окно между нажатиями B для продажи, сек.</summary>
        public float doorSellPressWindow = 2.5f;

        /// <summary>Процент возврата при продаже двери (100 = полный возврат).</summary>
        public int doorRefundPercent = 100;

        /// <summary>Освобождать дверь, когда владелец вышел из игры.</summary>
        public bool doorReleaseOnLeave = true;

        /// <summary>Признаки дверей: barcode'ы и имя типа скрипта пака.</summary>
        public List<string> doorBarcodeHints = new List<string>
        {
            "minecart.FunctionalDoor",
            "minecart.FunctionalDoors",
            "FunctionalDoor"
        };

        /// <summary>Признаки дверей по имени GameObject'а.</summary>
        public List<string> doorNameHints = new List<string>
        {
            "FunctionalDoor",
            "Door",
            "Дверь"
        };

        // ------------------------------------------------------------------ магазин

        /// <summary>Наценка продавца оружия при продаже игрокам, %.</summary>
        public int dealerMarkupPercent = 25;

        /// <summary>Показывать магазин только продавцу оружия.</summary>
        public bool shopDealerOnly = true;

        /// <summary>Требовать лицензию на покупку оружия (выдаёт полиция/продавец).</summary>
        public bool requireWeaponLicense = false;

        /// <summary>Давать продавцу оружия стартовый сет.</summary>
        public bool dealerStarterKit = true;

        /// <summary>Давать полиции стартовый сет (AR-15 + GLOCK-17).</summary>
        public bool policeStarterKit = true;

        /// <summary>barcode'ы стартового сета полиции.</summary>
        public List<string> policeStarterKitBarcodes = new List<string>
        {
            "SLZ.Bonelab.Spawnable.AR15",
            "SLZ.Bonelab.Spawnable.Glock17"
        };

        /// <summary>barcode'ы стартового сета продавца.</summary>
        public List<string> dealerStarterKitBarcodes = new List<string>
        {
            "SLZ.Bonelab.Spawnable.Glock17"
        };

        /// <summary>Каталог оружия: заполните barcode'и из ваших паков (Hoodpack, Kazus Micropack и т.п.).</summary>
        public List<ShopEntry> shopItems = ShopEntry.DefaultCatalog();

        // ------------------------------------------------------------------ роли

        /// <summary>Разрешить игрокам самим выбирать роль.</summary>
        public bool allowSelfRoleSelect = true;

        /// <summary>Автоматически ставить роль «Гражданин» новым игрокам.</summary>
        public bool autoAssignCitizen = true;

        /// <summary>Максимум наёмников на сервере.</summary>
        public int hitmanMaxCount = 1;

        /// <summary>Награда за смену роли (0 = бесплатно).</summary>
        public int roleChangeCost = 0;

        /// <summary>Метка роли над головой: показывать только когда игрок близко.</summary>
        public float roleTagDistance = 25f;

        // ------------------------------------------------------------------ грабежи и аресты

        /// <summary>Сколько секунд держать жертву, чтобы ограбить (роль Гангстер).</summary>
        public float robHoldSeconds = 4f;

        /// <summary>Какой процент наличных забирается при грабеже.</summary>
        public int robPercent = 20;

        /// <summary>Кулдаун между грабежами, сек.</summary>
        public float robCooldown = 60f;

        /// <summary>Разрешить полиции арестовывать (телепорт в «камеру» = позиция спавна уровня).</summary>
        public bool arrestsEnabled = true;

        /// <summary>Штраф/залог за арест.</summary>
        public int arrestFine = 500;

        // ------------------------------------------------------------------ принтеры денег

        /// <summary>Barcode принтера денег из палета AuroraRP (или любого пака).</summary>
        public string printerBarcode = "ItzHanchik.AuroraRP.Spawnable.MoneyPrinter";

        /// <summary>Как часто принтер печатает деньги, сек.</summary>
        public float printerInterval = 30f;

        /// <summary>Сколько печатает за раз.</summary>
        public int printerPayout = 250;

        // ------------------------------------------------------------------ распространение контента

        /// <summary>
        /// ID палета AuroraRP на mod.io (страница мода — из её адреса: mod.io/g/bonelab/m/&lt;имя&gt;;
        /// число видно в кабинете разработчика). 0 — авто-скачивания нет.
        /// Если задан, игроки без палета получат его автоматически через LabFusion.
        /// </summary>
        public int modioModId = 0;

        /// <summary>Barcode любого предмета из палета AuroraRP — по нему проверяем, стоит ли палет.</summary>
        public string contentBarcode = "ItzHanchik.AuroraRP.Spawnable.MoneyPrinter";

        /// <summary>Пытаться скачать палет AuroraRP с mod.io автоматически, когда его нет.</summary>
        public bool autoPullPallet = true;

        /// <summary>Barcode банковского терминала: модель собирается кодом внутри DLL.</summary>
        public string terminalBarcode = "ItzHanchik.AuroraRP.Spawnable.AuroraTerminal";

        /// <summary>Сколько раз за сессию пытаться скачать палет, если не вышло.</summary>
        public int contentPullAttempts = 3;

        // ------------------------------------------------- мост для игроков без мода

        /// <summary>
        /// Игрок без AuroraRP ставит только палет (Fusion качает его сам). Хост «читает»
        /// его руки и захваты и выполняет то, что видно физически: оплату за руку и покупку двери.
        /// </summary>
        public bool peerBridgeEnabled = true;

        /// <summary>Сколько платит игрок без мода, когда держит руку игрока с модом.</summary>
        public int passiveTransferAmount = 500;

        /// <summary>Сколько секунд держать руку, чтобы платёж от игрока без мода прошёл.</summary>
        public float passiveTransferHoldSeconds = 5f;

        /// <summary>Пауза между такими платежами одной пары игроков, сек.</summary>
        public float passivePairCooldown = 20f;

        /// <summary>Разрешить игрокам без мода покупать двери удержанием.</summary>
        public bool passiveDoorBuyEnabled = true;

        /// <summary>Сколько секунд нужно подержать дверь, чтобы купить её без мода.</summary>
        public float passiveDoorHoldSeconds = 4f;

        /// <summary>На сколько метров можно сместиться за удержание (чтобы это не была ходьба через дверь).</summary>
        public float passiveDoorStandStill = 0.5f;

        // ------------------------------------------- красота (внутри DLL и/или в палете)

        /// <summary>
        /// Брать иконки, скин меню, партиклы, анимации и звуки из пака внутри AuroraRP.dll.
        /// Это основной путь: игроку достаточно одного плагина. Палет для красоты не нужен.
        /// </summary>
        public bool useEmbeddedVisuals = true;

        /// <summary>Запасной путь: если в DLL пака нет — взять визуал из палета AuroraRP.</summary>
        public bool usePalletVisuals = false;

        /// <summary>Barcode спавнабла с визуалом (нужен только для палетного пути).</summary>
        public string visualSetBarcode = "ItzHanchik.AuroraRP.Spawnable.VisualSet";

        /// <summary>Показывать эффект передачи денег (пачка купюр летит из руки в руку).</summary>
        public bool transferFxEnabled = true;

        /// <summary>Сколько секунд летит купюра, сек.</summary>
        public float transferFxSeconds = 0.5f;

        // ------------------------------------------------------------------ загрузка/сохранение

        [NonSerialized] private static AuroraConfig _current;

        public static AuroraConfig Current
        {
            get
            {
                if (_current == null)
                {
                    Load();
                }

                return _current;
            }
        }

        public static string FilePath => Path.Combine(AuroraUtils.UserDataDirectory, "config.json");

        public static void Load()
        {
            AuroraConfig cfg = null;
            bool loaded = false;

            // 0) Дефолтный конфиг из DLL: пак внутри AuroraRP.dll («всё в одном плагине»).
            try
            {
                if (!string.IsNullOrEmpty(AuroraPack.ConfigJson))
                {
                    var embedded = AuroraJson.Read<AuroraConfig>(AuroraPack.ConfigJson);

                    if (embedded != null)
                    {
                        cfg = embedded;
                        loaded = true;
                        AuroraLog.Info("Конфиг взят из DLL (встроенный в пак).");
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "embedded config load");
            }

            // 1) Конфиг из палета: приезжает вместе с контентом, работает у всех.
            try
            {
                string palletPath = AuroraUtils.PalletConfigPath;

                if (!string.IsNullOrEmpty(palletPath) && File.Exists(palletPath))
                {
                    var palletConfig = AuroraJson.Read<AuroraConfig>(File.ReadAllText(palletPath));

                    if (palletConfig != null)
                    {
                        cfg = palletConfig;
                        loaded = true;
                        AuroraLog.Info("Конфиг взят из палета: " + palletPath);
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "pallet config load");
            }

            // 2) Личный конфиг хоста (UserData) перекрывает палетный целиком.
            try
            {
                if (File.Exists(FilePath))
                {
                    var user = AuroraJson.Read<AuroraConfig>(File.ReadAllText(FilePath));

                    if (user != null)
                    {
                        bool wasPallet = loaded;
                        cfg = user;
                        loaded = true;
                        AuroraLog.Info(wasPallet
                            ? "Личный конфиг UserData перекрыл палетный."
                            : "Конфиг взят из UserData.");
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "config load");
            }

            _current = cfg ?? new AuroraConfig();

            // Ничего не нашли — создаём личный конфиг, как раньше.
            if (!loaded)
            {
                Save();
            }

            _current.MigrateLegacyFields();
            _current.Normalize();
        }

        public static void Save()
        {
            if (_current == null)
            {
                return;
            }

            try
            {
                File.WriteAllText(FilePath, AuroraJson.Write(_current));
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "config save");
            }
        }

        /// <summary>Переносит открытый вебхук в зашифрованное поле и очищает исходное.</summary>
        private void MigrateLegacyFields()
        {
            if (!string.IsNullOrEmpty(discordWebhookPlain))
            {
                string url = discordWebhookPlain.Trim();
                if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    discordWebhookObfuscated = AuroraUtils.Obfuscate(url);
                    AuroraLog.Info("Вебхук Discord зашифрован и сохранён в конфиг.");
                }

                discordWebhookPlain = string.Empty;
                Save();
            }
        }

        private void Normalize()
        {
            if (menuScale < 0.5f) menuScale = 0.5f;
            if (menuScale > 2f) menuScale = 2f;
            if (passiveTransferAmount < 0) passiveTransferAmount = 0;
            if (passiveTransferHoldSeconds < 0.5f) passiveTransferHoldSeconds = 0.5f;
            if (passiveDoorHoldSeconds < 1f) passiveDoorHoldSeconds = 1f;
            if (transferFxSeconds < 0.1f) transferFxSeconds = 0.1f;
            if (startBalance < 0) startBalance = 0;
            if (transferHoldSeconds < 0.5f) transferHoldSeconds = 0.5f;
            if (doorPrice < 0) doorPrice = 0;
            if (doorMaxPerPlayer < 0) doorMaxPerPlayer = 0;
            if (doorSellPresses < 1) doorSellPresses = 1;
            if (transferStepSmall < 1) transferStepSmall = 1;
            if (transferStepBig < 1) transferStepBig = 1;
            if (hitmanMaxCount < 1) hitmanMaxCount = 1;
            if (sfxVolume < 0f) sfxVolume = 0f;
            if (sfxVolume > 1f) sfxVolume = 1f;

            if (string.IsNullOrWhiteSpace(menuOpenGesture)) menuOpenGesture = "y_and_a";

            if (modioModId < 0) modioModId = 0;
            if (contentPullAttempts < 0) contentPullAttempts = 0;
            if (string.IsNullOrWhiteSpace(contentBarcode)) contentBarcode = printerBarcode;
            if (string.IsNullOrWhiteSpace(terminalBarcode)) terminalBarcode = "ItzHanchik.AuroraRP.Spawnable.AuroraTerminal";

            if (shopItems == null)
            {
                shopItems = ShopEntry.DefaultCatalog();
            }

            if (doorBarcodeHints == null) doorBarcodeHints = new List<string>();
            if (doorNameHints == null) doorNameHints = new List<string>();

            AuroraL.Current = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                ? AuroraL.Lang.EN
                : AuroraL.Lang.RU;

            AuroraLog.MinLevel = (AuroraLog.Level)Mathf.Clamp(logLevel, 0, 3);
        }
    }

    /// <summary>Категория товара в магазине.</summary>
    public enum ShopCategory
    {
        Pistol = 0,
        Rifle = 1,
        Shotgun = 2,
        Sniper = 3,
        Illegal = 4
    }

    /// <summary>Позиция магазина: barcode пака + цена.</summary>
    [Serializable]
    public class ShopEntry
    {
        /// <summary>Имя для меню (например, «GLOCK-17»).</summary>
        public string title = "GLOCK-17";

        /// <summary>Barcode предмета в паках игры/мода.</summary>
        public string barcode = "";

        /// <summary>Категория (см. ShopCategory).</summary>
        public int category = 0;

        /// <summary>Цена покупки у поставщика.</summary>
        public int price = 1500;

        /// <summary>Доступно только продавцу оружия.</summary>
        public bool dealerOnly = true;

        /// <summary>Только по лицензии.</summary>
        public bool requiresLicense = false;

        public ShopCategory Category => (ShopCategory)Mathf.Clamp(category, 0, 4);

        /// <summary>
        /// Стандартный каталог: цены из ТЗ (пистолеты 1500, автоматы/дробовики 3000, снайперки 4000).
        /// Barcode'и — примеры из ванильного BONELAB; для паков замените на свои (см. Docs/Магазин.md).
        /// </summary>
        public static List<ShopEntry> DefaultCatalog()
        {
            return new List<ShopEntry>
            {
                // --- пистолеты (1500)
                new ShopEntry { title = "GLOCK-17",  barcode = "SLZ.Bonelab.Spawnable.Glock17",     category = 0, price = 1500 },
                new ShopEntry { title = "M1911",     barcode = "SLZ.Bonelab.Spawnable.M1911",      category = 0, price = 1500 },
                new ShopEntry { title = "Pistol (Pack)", barcode = "hoodpack.pistol.pistol",       category = 0, price = 1500 },

                // --- автоматы (3000)
                new ShopEntry { title = "AR-15",     barcode = "SLZ.Bonelab.Spawnable.AR15",       category = 1, price = 3000 },
                new ShopEntry { title = "AK-Draco",  barcode = "kazus.micropack.ak.draco",         category = 1, price = 3000 },
                new ShopEntry { title = "MP5",       barcode = "hoodpack.smg.mp5",                 category = 1, price = 3000 },

                // --- дробовики (3000)
                new ShopEntry { title = "Remington 870", barcode = "hoodpack.shotgun.remington870", category = 2, price = 3000 },
                new ShopEntry { title = "SPAS-12",   barcode = "kazus.micropack.spas12",           category = 2, price = 3000 },

                // --- снайперки (4000)
                new ShopEntry { title = "AWP",       barcode = "hoodpack.sniper.awp",              category = 3, price = 4000 },
                new ShopEntry { title = "SVD",       barcode = "kazus.micropack.svd",              category = 3, price = 4000 },

                // --- нелегальное (контрабандист)
                new ShopEntry { title = "Отмычка",   barcode = "aurora.illegal.lockpick",          category = 4, price = 800,  requiresLicense = false },
                new ShopEntry { title = "Маскировка",barcode = "aurora.illegal.disguise",          category = 4, price = 1200, requiresLicense = false }
            };
        }
    }
}
