using System;
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

            // Каждый шаг в своём try/catch: если, например, не загрузится звук или сетевая часть,
            // остальные системы (в первую очередь меню) всё равно должны подняться.
            SafeInit("filelog", () => AuroraFileLog.Initialize());
            SafeInit("config", () => AuroraConfig.Load());
            SafeInit("sfx", () => AuroraSfx.Initialize());

            State = new AuroraState();
            SafeInit("roles", () => Roles = new RoleService(State));
            SafeInit("wallet", () => Wallet = new WalletService(State));
            SafeInit("transfers", () => Transfers = new TransferService(State, Wallet));
            SafeInit("spawn", () => Spawn = new SpawnService());
            SafeInit("doors", () => Doors = new DoorService(State));
            SafeInit("craft", () => Craft = new CraftService(State));
            SafeInit("crime", () => Crime = new CrimeService(State, Wallet));
            SafeInit("shop", () => Shop = new ShopService(State, Wallet, Roles, Spawn));
            SafeInit("contracts", () => Contracts = new ContractService(State, Wallet));
            SafeInit("net", () => Net = NetFactory.Create());
            SafeInit("authority", () =>
            {
                Authority = new AuroraAuthority(State, Wallet, Roles, Doors, Shop, Contracts, Craft, Crime);

                if (Net != null)
                {
                    Net.MessageReceived += Authority.HandleMessage;
                    Net.PeerMissingMod += OnPeerMissingMod;
                }
            });

            SafeInit("input", () => Input = new AuroraInput());
            SafeInit("menu", () => Menu = new HandMenu());
            SafeInit("scanner", () => Scanner = new WorldScanner());
            SafeInit("nametags", () => NameTags = new NameTagService());
            SafeInit("bridge", () => Bridge = new PeerBridgeService(State));
            SafeInit("bank", () => Bank = new DiscordBankBridge(State));

            SafeInit("hooks", GameHooks.Subscribe);

            Initialized = true;
            AuroraLog.Info("Сервисы запущены. Режим сети: {0}", Net != null ? Net.Description : "нет");
        }

        internal static void Shutdown()
        {
            if (!Initialized)
            {
                return;
            }

            try
            {
                AuroraVisuals.Shutdown();
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

            // Каждый сервис в своей «обёртке»: ошибка одного не должна останавливать остальные
            // (иначе, например, сломанное меню убивает и экономику, и спавн).
            SafeTick("net", () => Net?.Tick(dt));
            SafeTick("input", () => Input?.Tick(dt));
            SafeTick("transfers", () => Transfers?.Tick(dt));
            SafeTick("crime", () => Crime?.Tick(dt));
            SafeTick("contracts", () => Contracts?.Tick(dt));
            SafeTick("doors", () => Doors?.Tick(dt));
            SafeTick("shop", () => Shop?.Tick(dt));
            SafeTick("scanner", () => Scanner?.Tick(dt));
            SafeTick("menu", () => Menu?.Tick(dt));
            SafeTick("bank", () => Bank?.Tick(dt));
            SafeTick("craft", () => Craft?.Tick(dt));
            SafeTick("nametags", () => NameTags?.Tick(dt));
            SafeTick("bridge", () => Bridge?.Tick(dt));
            SafeTick("visuals", () => AuroraVisuals.Tick(dt));
        }

        // ------------------------------------------------------------------ страховка

        /// <summary>Инициализация сервиса с защитой: одна сломанная система не гасит весь мод.</summary>
        private static void SafeInit(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "init:" + name);
            }
        }

        private static readonly System.Collections.Generic.HashSet<string> ReportedTickErrors =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>Тик сервиса с защитой: ошибка логируется один раз и не рвёт остальные системы.</summary>
        private static void SafeTick(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (ReportedTickErrors.Add(name))
                {
                    AuroraLog.Exception(e, "tick:" + name);
                }
            }
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
