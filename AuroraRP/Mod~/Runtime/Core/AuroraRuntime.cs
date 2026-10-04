using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Сервис-локатор мода. Все системы доступны отсюда, ничего не нужно искать по сцене.
    /// </summary>
    public static class AuroraRuntime
    {
        public const string Version = "1.0.0";
        public const string ModName = "AuroraRP";
        public const string ModAuthor = "ItzHanchik";

        public static AuroraConfig Cfg => AuroraConfig.Current;

        public static AuroraState State { get; internal set; }
        public static RoleService Roles { get; internal set; }
        public static WalletService Wallet { get; internal set; }
        public static TransferService Transfers { get; internal set; }
        public static DoorService Doors { get; internal set; }
        public static ShopService Shop { get; internal set; }
        public static ContractService Contracts { get; internal set; }
        public static SpawnService Spawn { get; internal set; }
        public static CraftService Craft { get; internal set; }
        public static CrimeService Crime { get; internal set; }
        public static NameTagService NameTags { get; internal set; }
        public static IAuroraNet Net { get; internal set; }
        public static AuroraAuthority Authority { get; internal set; }
        public static AuroraInput Input { get; internal set; }
        public static HandMenu Menu { get; internal set; }
        public static AuroraSfx Sfx { get; internal set; }
        public static DiscordBankBridge Bank { get; internal set; }
        public static WorldScanner Scanner { get; internal set; }
        public static PeerBridgeService Bridge { get; internal set; }
        public static AuroraDriver Driver { get; internal set; }

        public static bool Initialized { get; private set; }

        /// <summary>Локальный сетевой id (0 = хост). В одиночной игре всегда 0.</summary>
        public static byte LocalId => Net != null ? Net.LocalId : (byte)0;

        public static string LocalName => Net != null ? Net.LocalName : "You";

        public static PlayerRecord LocalPlayer
        {
            get
            {
                State ??= new AuroraState();
                return State.GetOrCreate(LocalId, LocalName);
            }
        }

        // ------------------------------------------------------------ инициализация

        internal static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            AuroraLog.Info("AuroraRP {0} — инициализация", Version);

            AuroraFileLog.Initialize();
            AuroraConfig.Load();
            AuroraSfx.Initialize();

            State = new AuroraState();
            Roles = new RoleService(State);
            Wallet = new WalletService(State);
            Transfers = new TransferService(State, Wallet);
            Spawn = new SpawnService();
            Doors = new DoorService(State);
            Craft = new CraftService(State);
            Crime = new CrimeService(State, Wallet);
            Shop = new ShopService(State, Wallet, Roles, Spawn);
            Contracts = new ContractService(State, Wallet);
            Net = NetFactory.Create();
            Authority = new AuroraAuthority(State, Wallet, Roles, Doors, Shop, Contracts, Craft, Crime);
            Net.MessageReceived += Authority.HandleMessage;
            Net.PeerMissingMod += OnPeerMissingMod;
            Input = new AuroraInput();
            Menu = new HandMenu();
            Scanner = new WorldScanner();
            NameTags = new NameTagService();
            Bridge = new PeerBridgeService(State);
            Bank = new DiscordBankBridge(State);

            GameHooks.Subscribe();

            Initialized = true;
            AuroraLog.Info("Сервисы запущены. Режим сети: {0}", Net.Description);
        }

        internal static void Shutdown()
        {
            if (!Initialized)
            {
                return;
            }

            try
            {
                Menu?.Shutdown();
                Bank?.FlushNow();
                Doors?.Save();
                AuroraStorage.SaveState(State);
                AuroraFileLog.Flush();
            }
            catch (System.Exception e)
            {
                AuroraLog.Exception(e, "shutdown");
            }
            finally
            {
                Initialized = false;
            }
        }

        internal static void Tick(float dt)
        {
            if (!Initialized)
            {
                return;
            }

            AuroraUtils.PumpMainThread();

            Net?.Tick(dt);
            Input?.Tick(dt);
            Transfers?.Tick(dt);
            Crime?.Tick(dt);
            Contracts?.Tick(dt);
            Doors?.Tick(dt);
            Shop?.Tick(dt);
            Scanner?.Tick(dt);
            Menu?.Tick(dt);
            Bank?.Tick(dt);
            Craft?.Tick(dt);
            NameTags?.Tick(dt);
            Bridge?.Tick(dt);
        }

        // -------------------------------------------------------------- утилиты

        /// <summary>Уведомление только себе (без сети).</summary>
        public static void NotifyLocal(string text, Color color)
        {
            AuroraNotifications.Send(text, color);
        }

        /// <summary>Игрок без AuroraRP: говорим об этом хосту (и всем с модом в будущем).</summary>
        private static void OnPeerMissingMod(AuroraPeer peer)
        {
            try
            {
                if (peer == null || !Net.IsHost)
                {
                    return;
                }

                NotifyLocal(AuroraL.Get("content.peer.missing", peer.Name), UiTheme.Warning);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "peer missing mod notify");
            }
        }

        /// <summary>Проверить/скачать палет мода (кнопка в меню).</summary>
        public static bool SyncContentPallet(bool force)
        {
            return Net != null && Net.SyncContentPallet(force);
        }

        /// <summary>Отправить уведомление конкретному игроку (по сети или локально).</summary>
        public static void Notify(byte targetId, string text, Color color)
        {
            if (targetId == LocalId)
            {
                AuroraNotifications.Send(text, color);
                return;
            }

            Net?.SendNotification(targetId, text, ColorUtility.ToHtmlStringRGB(color));
        }

        /// <summary>Уведомление всем.</summary>
        public static void NotifyAll(string text, Color color)
        {
            AuroraNotifications.Send(text, color);

            foreach (var p in State.Players)
            {
                if (p.id != LocalId)
                {
                    Net?.SendNotification(p.id, text, ColorUtility.ToHtmlStringRGB(color));
                }
            }
        }

        /// <summary>Строка для подсказок: «$1 234 · Гражданин».</summary>
        public static string StatusLine()
        {
            var rec = LocalPlayer;
            return AuroraUtils.Money(rec.balance) + "  ·  " + RoleCatalog.Get(rec.Role).Name;
        }
    }
}
