using System;
using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// «Красота» мода, собранная прямо в коде — без Unity, без палета и без паков.
    ///
    /// Модель пачки купюр, принтер денег, банковский терминал, вспышки и искры собираются
    /// из примитивов, материалов и спрайтов в момент запуска игры. Это делает AuroraRP
    /// полностью самодостаточным: положил одну DLL в Mods — и всё видно.
    ///
    /// Если в DLL вшит пак из Unity (aurorarp.pack), он имеет приоритет; кодовая красота
    /// работает как честная замена и как страховка, если пак не собрался.
    /// </summary>
    public static class AuroraProcedural
    {
        public const string VisualSetName = "AuroraVisualSet";
        public const string CashName = "Prop_Cash";
        public const string PrinterName = "Prop_Printer";
        public const string TerminalName = "Prop_Terminal";
        public const string TransferFxName = "Fx_Transfer";
        public const string ReceiveFxName = "Fx_Receive";

        // Фирменные цвета мода.
        public static readonly Color Money = new Color(0.36f, 0.87f, 0.52f, 1f);
        public static readonly Color MoneyDeep = new Color(0.10f, 0.45f, 0.26f, 1f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.32f, 1f);
        public static readonly Color Dark = new Color(0.11f, 0.13f, 0.17f, 1f);
        public static readonly Color DarkSoft = new Color(0.20f, 0.23f, 0.29f, 1f);
        public static readonly Color Screen = new Color(0.15f, 0.55f, 1f, 1f);

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Красота уже собрана в этой сессии?</summary>
        public static bool Built { get; private set; }

        // ------------------------------------------------------------------ сборка

        /// <summary>
        /// Собирает все части и регистрирует их в реестре пака — дальше мод использует их
        /// точно так же, как ассеты из палета или из aurorarp.pack.
        /// </summary>
        public static GameObject BuildAndRegister()
        {
            var root = new GameObject(VisualSetName);

            // Части должны пережить смену уровня: в игре они играют роль префабов.
            try
            {
                UnityEngine.Object.DontDestroyOnLoad(root);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "procedural dontsave");
            }

            root.SetActive(false);

            GameObject cash = null;
            GameObject printer = null;
            GameObject terminal = null;
            GameObject transferFx = null;
            GameObject receiveFx = null;

            try { cash = BuildCash(); } catch (Exception e) { AuroraLog.Exception(e, "build cash"); }
            try { printer = BuildPrinter(); } catch (Exception e) { AuroraLog.Exception(e, "build printer"); }
            try { terminal = BuildTerminal(); } catch (Exception e) { AuroraLog.Exception(e, "build terminal"); }
            try { transferFx = BuildFlash(TransferFxName, Gold); } catch (Exception e) { AuroraLog.Exception(e, "build fx transfer"); }
            try { receiveFx = BuildFlash(ReceiveFxName, Money); } catch (Exception e) { AuroraLog.Exception(e, "build fx receive"); }

            Attach(root, cash);
            Attach(root, printer);
            Attach(root, terminal);
            Attach(root, transferFx);
            Attach(root, receiveFx);

            AuroraPack.MarkProcedural();
            AuroraPack.RegisterProceduralPart(CashName, cash);
            AuroraPack.RegisterProceduralPart(PrinterName, printer);
            AuroraPack.RegisterProceduralPart(TerminalName, terminal);
            AuroraPack.RegisterProceduralPart(TransferFxName, transferFx);
            AuroraPack.RegisterProceduralPart(ReceiveFxName, receiveFx);
            AuroraPack.RegisterProceduralPart(VisualSetName, root);

            Built = true;
            AuroraLog.Info("Красота собрана кодом: купюры, принтер, терминал, вспышки.");
            return root;
        }

        private static void Attach(GameObject root, GameObject part)
        {
            if (root == null || part == null)
            {
                return;
            }

            try
            {
                part.transform.SetParent(root.transform, false);

                // Префабы всегда лежат отключёнными; копия включается при спавне.
                if (part.activeSelf)
                {
                    part.SetActive(false);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "attach " + part.name);
            }
        }

        // ------------------------------------------------------------- модель купюр

        /// <summary>Пачка купюр: стопка банкнот и банковская лента вокруг.</summary>
        public static GameObject BuildCash()
        {
            var root = new GameObject(CashName);
            root.SetActive(false);

            Material note = Material("cash", Money);
            Material band = Material("band", Gold);

            const int count = 9;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);

                // Лёгкий разворот — как у настоящей пачки в руке.
                float yaw = (i % 2 == 0 ? 1f : -1f) * (0.9f + t * 1.6f);
                float lift = -0.018f + t * 0.036f;

                Box(root.transform, "Banknote_" + i,
                    new Vector3(0.0009f * (i % 3 - 1), lift, 0.0007f * (i % 4 - 1)),
                    new Vector3(0.156f, 0.0035f, 0.068f),
                    note,
                    new Vector3(0f, yaw, 0f));
            }

            // Банковская лента поперёк пачки.
            Box(root.transform, "CashBand", new Vector3(0f, 0f, 0f), new Vector3(0.058f, 0.055f, 0.074f), band, Vector3.zero);

            // Немного денег сверху — пачка выглядит «пухлой».
            for (int i = 0; i < 3; i++)
            {
                Box(root.transform, "Note_Top_" + i,
                    new Vector3(0.004f * (i - 1), 0.030f + i * 0.0035f, 0.002f * i),
                    new Vector3(0.150f, 0.0035f, 0.064f),
                    note,
                    new Vector3(0f, 6f + i * 5f, 0f));
            }

            return root;
        }

        // ----------------------------------------------------------- принтер денег

        /// <summary>Принтер денег: корпус, лоток, экран и стопка напечатанных купюр.</summary>
        public static GameObject BuildPrinter()
        {
            // Имя корня важно: WorldScanner/CraftService узнают объекты мода по нему.
            var root = new GameObject("AuroraRP Printer");
            root.SetActive(false);

            Material body = Material("printer_body", Dark);
            Material trim = Material("printer_trim", DarkSoft);
            Material screen = Material("printer_screen", Screen);
            Material money = Material("cash", Money);

            Box(root.transform, "Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.42f, 0.34f, 0.36f), body, Vector3.zero);
            Box(root.transform, "Lid", new Vector3(0f, 0.35f, 0f), new Vector3(0.44f, 0.03f, 0.38f), trim, Vector3.zero);
            Box(root.transform, "Base", new Vector3(0f, 0.015f, 0f), new Vector3(0.46f, 0.03f, 0.40f), trim, Vector3.zero);

            // Экран сбоку — светится синим.
            Box(root.transform, "ScreenFrame", new Vector3(0f, 0.24f, -0.185f), new Vector3(0.26f, 0.12f, 0.012f), trim, Vector3.zero);
            Box(root.transform, "Screen", new Vector3(0f, 0.24f, -0.193f), new Vector3(0.22f, 0.09f, 0.01f), screen, Vector3.zero);

            // Лоток выдачи.
            Box(root.transform, "Tray", new Vector3(0f, 0.055f, 0.20f), new Vector3(0.30f, 0.02f, 0.10f), trim, Vector3.zero);

            // Напечатанные купюры в лотке.
            for (int i = 0; i < 5; i++)
            {
                Box(root.transform, "Printed_" + i,
                    new Vector3(0f, 0.066f + i * 0.004f, 0.20f),
                    new Vector3(0.140f, 0.0035f, 0.062f),
                    money,
                    new Vector3(0f, (i % 2 == 0 ? 3f : -3f), 0f));
            }

            // Лампочка «печатает».
            var lamp = Glow(root.transform, "Lamp", Money, 0.055f);
            lamp.transform.localPosition = new Vector3(0.15f, 0.31f, -0.185f);

            // Физика: принтер можно толкать и брать в руку.
            try
            {
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.17f, 0f);
                collider.size = new Vector3(0.44f, 0.36f, 0.40f);

                var body3d = root.AddComponent<Rigidbody>();
                body3d.mass = 12f;
                body3d.drag = 1.4f;
                body3d.angularDrag = 2.5f;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "printer physics");
            }

            try
            {
                root.AddComponent<Il2CppSLZ.Marrow.InteractableHost>();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "printer interactable");
            }

            return root;
        }

        // --------------------------------------------------------- банковский терминал

        /// <summary>Банковский терминал: стойка с экраном и клавиатурой (декор и точка входа в меню).</summary>
        public static GameObject BuildTerminal()
        {
            var root = new GameObject("AuroraRP Terminal");
            root.SetActive(false);

            Material body = Material("terminal_body", Dark);
            Material trim = Material("terminal_trim", DarkSoft);
            Material screen = Material("terminal_screen", Screen);
            Material accent = Material("terminal_accent", Gold);

            Box(root.transform, "Column", new Vector3(0f, 0.55f, 0f), new Vector3(0.34f, 1.10f, 0.28f), body, Vector3.zero);
            Box(root.transform, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.46f, 0.06f, 0.40f), trim, Vector3.zero);
            Box(root.transform, "Head", new Vector3(0f, 1.22f, 0.02f), new Vector3(0.44f, 0.30f, 0.20f), trim, new Vector3(12f, 0f, 0f));
            Box(root.transform, "HeadScreen", new Vector3(0f, 1.21f, -0.09f), new Vector3(0.36f, 0.22f, 0.012f), screen, new Vector3(12f, 0f, 0f));
            Box(root.transform, "Keyboard", new Vector3(0f, 0.94f, -0.19f), new Vector3(0.34f, 0.03f, 0.16f), accent, new Vector3(-18f, 0f, 0f));

            var lamp = Glow(root.transform, "Lamp", Gold, 0.09f);
            lamp.transform.localPosition = new Vector3(0f, 1.40f, 0f);

            try
            {
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.62f, 0f);
                collider.size = new Vector3(0.44f, 1.30f, 0.40f);

                var body3d = root.AddComponent<Rigidbody>();
                body3d.mass = 40f;
                body3d.drag = 2f;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "terminal physics");
            }

            try
            {
                root.AddComponent<Il2CppSLZ.Marrow.InteractableHost>();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "terminal interactable");
            }

            return root;
        }

        // --------------------------------------------------------------- вспышки

        /// <summary>Мягкая вспышка (используется как Fx_*): светящийся спрайт в мире.</summary>
        public static GameObject BuildFlash(string name, Color color)
        {
            var root = new GameObject(name);
            root.SetActive(false);

            var glow = Glow(root.transform, "Glow", color, 0.35f);
            glow.transform.localPosition = Vector3.zero;

            var core = Glow(root.transform, "Core", new Color(1f, 1f, 1f, color.a), 0.16f);
            core.transform.localPosition = Vector3.zero;

            return root;
        }

        // -------------------------------------------------------------- примитивы

        /// <summary>Коробка из примитива: без коллайдера и без чужих материалов.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler)
        {
            GameObject go = null;

            try
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;

                var collider = go.GetComponent<Collider>();

                if (collider != null)
                {
                    UnityEngine.Object.Destroy(collider);
                }

                if (material != null)
                {
                    var renderer = go.GetComponent<MeshRenderer>();

                    if (renderer != null)
                    {
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "box " + name);

                if (go == null)
                {
                    go = new GameObject(name);
                }
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        /// <summary>Светящийся спрайт — искры, лампочки, вспышки.</summary>
        public static GameObject Glow(Transform parent, string name, Color color, float size)
        {
            var go = new GameObject(name);

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = UiTheme.Glow;
                renderer.color = color;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "glow " + name);
            }

            go.transform.localScale = new Vector3(size, size, 1f);
            return go;
        }

        // ------------------------------------------------------------- материалы

        /// <summary>Материал по ключу: создаётся один раз и переиспользуется.</summary>
        public static Material Material(string key, Color color)
        {
            if (string.IsNullOrEmpty(key))
            {
                key = "default";
            }

            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            Material material = null;

            try
            {
                var shader = FindShader();

                if (shader != null)
                {
                    material = new Material(shader);
                    material.name = "Aurora_" + key;
                    Tint(material, color);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "material " + key);
            }

            if (material != null)
            {
                Cache[key] = material;
            }

            return material;
        }

        private static Shader FindShader()
        {
            string[] names =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Standard",
                "Unlit/Color",
                "Legacy Shaders/Diffuse",
                "Sprites/Default"
            };

            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var shader = Shader.Find(names[i]);

                    if (shader != null)
                    {
                        return shader;
                    }
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private static void Tint(Material material, Color color)
        {
            try
            {
                material.color = color;
            }
            catch (Exception)
            {
            }

            try
            {
                material.SetColor("_BaseColor", color);
            }
            catch (Exception)
            {
            }

            try
            {
                material.SetColor("_Color", color);
            }
            catch (Exception)
            {
            }

            try
            {
                material.SetFloat("_Smoothness", 0.35f);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Цвет искры для перевода денег (используется эффектами).</summary>
        public static Color SparkColor(bool receive)
        {
            return receive ? Money : Gold;
        }
    }
}
