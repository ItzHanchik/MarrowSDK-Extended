using System;
using System.IO;
using SLZ.Marrow;
using SLZ.Marrow.Interaction;
using SLZ.Marrow.Pool;
using SLZ.Marrow.Warehouse;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace AuroraRP.EditorTools
{
    /// <summary>
    /// Создаёт контент палета AuroraRP: банковский терминал и принтер денег.
    /// Префабы — обычные меши с интерракшном Marrow: код мода находит их в игре по имени
    /// и вешает свою логику в рантайме, поэтому палет не зависит от DLL мода.
    /// </summary>
    public static class AuroraRpContent
    {
        public const string TerminalBarcode = "ItzHanchik.AuroraRP.Spawnable.Terminal";
        public const string PrinterBarcode = "ItzHanchik.AuroraRP.Spawnable.MoneyPrinter";

        /// <summary>Спавнабл «вся красота»: иконки, скин меню, партиклы, купюры, звуки.</summary>
        public const string VisualSetBarcode = "ItzHanchik.AuroraRP.Spawnable.VisualSet";

        private const string PalletFolder = "Assets/AuroraRP/Pallet";
        private const string PrefabFolder = "Assets/AuroraRP/Pallet/Prefabs";
        private const string MaterialFolder = "Assets/AuroraRP/Pallet/Materials";
        private const string SpriteFolder = "Assets/AuroraRP/Pallet/Sprites";
        private const string AudioFolder = "Assets/AuroraRP/Pallet/Audio";

        [MenuItem("AuroraRP/2. Создать контент палета (терминал + принтер + красота)", false, 2)]
        public static void CreateContent()
        {
            try
            {
                var pallet = GetOrCreatePallet();
                var terminalPrefab = CreateTerminalPrefab();
                var printerPrefab = CreatePrinterPrefab();
                var visualPrefab = CreateVisualSetPrefab();

                CreateSpawnableCrate(pallet, "Aurora Terminal", terminalPrefab, TerminalBarcode);
                CreateSpawnableCrate(pallet, "Aurora Money Printer", printerPrefab, PrinterBarcode);
                CreateSpawnableCrate(pallet, "Aurora Visual Set", visualPrefab, VisualSetBarcode);

                EditorUtility.SetDirty(pallet);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[AuroraRP] Контент палета создан: терминал + принтер + набор красоты (VisualSet).");

                EditorGUIUtility.PingObject(pallet);
                Selection.activeObject = pallet;
            }
            catch (Exception e)
            {
                Debug.LogError("[AuroraRP] Не удалось создать контент: " + e);
            }
        }

        // ---------------------------------------------------------------- палет

        public static Pallet GetOrCreatePallet()
        {
            EnsureFolders();

            string expectedPath = Path.Combine(PalletFolder, "AuroraRP.pallet").Replace('\\', '/');

            // Ищем уже существующий палет AuroraRP в проекте.
            var guids = AssetDatabase.FindAssets("t:Pallet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var existing = AssetDatabase.LoadAssetAtPath<Pallet>(path);
                if (existing != null && !string.IsNullOrEmpty(existing.Title) &&
                    existing.Title.Equals(AuroraRpPaths.PalletTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            var pallet = Pallet.CreatePallet(AuroraRpPaths.PalletTitle, AuroraRpPaths.PalletAuthor);
            pallet.Author = AuroraRpPaths.PalletAuthor;
            pallet.Version = AuroraRpPaths.ModVersion;
            pallet.Description = "AuroraRP — роль-плей система для BONELAB: роли, банк, двери, магазин и контракты.";

            string assetPath = Path.Combine(PalletFolder, pallet.GetAssetFilename()).Replace('\\', '/');

            try
            {
                AssetDatabase.CreateAsset(pallet, assetPath);
            }
            catch (Exception)
            {
                // Если расширение .pallet не поддерживается AssetDatabase — создаём как .asset.
                assetPath = expectedPath + ".asset";
                AssetDatabase.CreateAsset(pallet, assetPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AuroraRP] Создан палет: " + assetPath);
            return pallet;
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets/AuroraRP");
            CreateFolder(PalletFolder);
            CreateFolder(PrefabFolder);
            CreateFolder(MaterialFolder);
            CreateFolder(SpriteFolder);
            CreateFolder(AudioFolder);
        }

        private static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                CreateFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        // -------------------------------------------------------------- материалы

        private static Material CreateMaterial(string name, Color color, Color? emission = null, float metallic = 0.1f, float smoothness = 0.35f)
        {
            string path = Path.Combine(MaterialFolder, name + ".mat").Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission.Value * 3f);
                }
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // --------------------------------------------------------------- префабы

        public static string CreateTerminalPrefab()
        {
            string path = Path.Combine(PrefabFolder, "AuroraTerminal.prefab").Replace('\\', '/');

            var root = new GameObject("AuroraRP Terminal");
            try
            {
                SetupInteractable(root, mass: 14f, size: new Vector3(0.42f, 0.30f, 0.06f));

                var bodyMat = CreateMaterial("TerminalBody", new Color(0.07f, 0.09f, 0.14f), null, 0.6f, 0.55f);
                var screenMat = CreateMaterial("TerminalScreen", new Color(0.02f, 0.05f, 0.09f), new Color(0.15f, 0.75f, 1f), 0.2f, 0.9f);

                AddBox(root.transform, "Body", new Vector3(0.42f, 0.30f, 0.05f), Vector3.zero, bodyMat);
                AddBox(root.transform, "Screen", new Vector3(0.34f, 0.20f, 0.01f), new Vector3(0f, 0.01f, -0.032f), screenMat);
                AddBox(root.transform, "Border", new Vector3(0.40f, 0.26f, 0.02f), new Vector3(0f, 0f, -0.028f), CreateMaterial("TerminalEdge", new Color(0.25f, 0.85f, 1f), new Color(0.2f, 0.8f, 1f), 0.8f, 0.8f));

                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static string CreatePrinterPrefab()
        {
            string path = Path.Combine(PrefabFolder, "AuroraMoneyPrinter.prefab").Replace('\\', '/');

            var root = new GameObject("AuroraRP Money Printer");
            try
            {
                SetupInteractable(root, mass: 22f, size: new Vector3(0.46f, 0.34f, 0.40f));

                var bodyMat = CreateMaterial("PrinterBody", new Color(0.11f, 0.12f, 0.14f), null, 0.5f, 0.4f);
                var moneyMat = CreateMaterial("PrinterMoney", new Color(0.55f, 0.75f, 0.35f), new Color(0.3f, 0.9f, 0.2f), 0.1f, 0.6f);
                var lampMat = CreateMaterial("PrinterLamp", new Color(0.9f, 0.8f, 0.2f), new Color(1f, 0.8f, 0.1f), 0.4f, 0.8f);

                AddBox(root.transform, "Body", new Vector3(0.46f, 0.30f, 0.40f), Vector3.zero, bodyMat);
                AddBox(root.transform, "Tray", new Vector3(0.36f, 0.03f, 0.34f), new Vector3(0f, 0.17f, 0.06f), moneyMat);
                AddBox(root.transform, "Lamp", new Vector3(0.06f, 0.06f, 0.03f), new Vector3(0.16f, 0.10f, -0.21f), lampMat);

                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------- набор красоты (VisualSet)

        /// <summary>
        /// Префаб «вся красота»: код мода ищет объекты по именам (MenuSkin, Icon_*, Fx_*,
        /// Prop_Cash, Sfx_*) и берёт из них материалы, спрайты, партиклы и звуки.
        /// Сюда можно положить свои модельки, текстуры и эффекты — DLL менять не нужно.
        /// </summary>
        public static string CreateVisualSetPrefab()
        {
            string path = Path.Combine(PrefabFolder, "AuroraVisualSet.prefab").Replace('\\', '/');

            var root = new GameObject("AuroraRP VisualSet");
            try
            {
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = 4f;
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;

                var collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.4f, 0.4f, 0.4f);
                collider.isTrigger = true;

                root.AddComponent<Poolee>();
                root.AddComponent<MarrowEntity>();
                root.AddComponent<MarrowBody>();

                BuildVisualChildren(root.transform);
                return SavePrefab(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildVisualChildren(Transform root)
        {
            // 1) Скин меню — спрайт 512x512, ставится фоном панели.
            var menuSkin = CreateSpriteAsset("MenuSkin", 256, new Color(0.06f, 0.10f, 0.18f), new Color(0.30f, 0.85f, 1f));
            AddSprite(root, "MenuSkin", menuSkin, new Vector3(-0.4f, 0f, 0f));

            // 2) Иконки: имена совпадают с теми, что рисует UiTheme.
            // Имена — ровно те, что просят страницы меню (UiTheme.Icon) и подписи ролей.
            string[] icons =
            {
                "coin", "cash", "wallet", "gun", "door", "contract", "gear",
                "info", "users", "skull", "star", "shield",
                "police", "dealer", "hitman", "smuggler", "gangster", "citizen", "role"
            };

            for (int i = 0; i < icons.Length; i++)
            {
                var sprite = CreateSpriteAsset("Icon_" + icons[i], 96, new Color(0.10f, 0.16f, 0.26f), new Color(0.75f, 0.92f, 1f));
                AddSprite(root, "Icon_" + icons[i], sprite, new Vector3(-0.3f + i * 0.05f, 0.3f, 0f));
            }

            // 3) Эффекты: партиклы перевода и получения денег.
            var moneyMat = CreateMaterial("FxMoney", new Color(0.55f, 0.9f, 0.45f), new Color(0.35f, 1f, 0.3f), 0.2f, 0.7f);
            CreateParticles(root, "Fx_Transfer", moneyMat, 26, new Color(0.65f, 1f, 0.6f), 1.4f);
            CreateParticles(root, "Fx_Receive", moneyMat, 34, new Color(1f, 0.95f, 0.55f), 1.8f);

            // 4) Пачка купюр, которая летит из руки в руку.
            var cashMat = CreateMaterial("CashBundle", new Color(0.45f, 0.66f, 0.32f), new Color(0.2f, 0.6f, 0.15f), 0.05f, 0.4f);
            var cash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cash.name = "Prop_Cash";
            cash.transform.SetParent(root, false);
            cash.transform.localScale = new Vector3(0.11f, 0.035f, 0.06f);
            cash.transform.localPosition = new Vector3(0.25f, 0.15f, 0f);

            var cashCollider = cash.GetComponent<Collider>();
            if (cashCollider != null)
            {
                Object.DestroyImmediate(cashCollider);
            }

            var cashRenderer = cash.GetComponent<MeshRenderer>();
            if (cashRenderer != null)
            {
                cashRenderer.sharedMaterial = cashMat;
            }

            // 5) Звуки: кладутся в палет как обычные wav-ассеты.
            AddSound(root, "Sfx_SendMoney", CreateCoinWav("AuroraCoin", 0.55f, 1180f, 1560f));
            AddSound(root, "Sfx_ReceiveMoney", CreateCoinWav("AuroraReceive", 0.7f, 880f, 1320f));

            // 6) Материал палета, если художник захочет перекрасить меню из палета.
            var accent = CreateMaterial("MenuAccent", new Color(0.30f, 0.88f, 1f), new Color(0.25f, 0.8f, 1f), 0.3f, 0.8f);
            var accentGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            accentGo.name = "MenuAccent";
            accentGo.transform.SetParent(root, false);
            accentGo.transform.localPosition = new Vector3(-0.25f, -0.2f, 0f);

            var accentCollider = accentGo.GetComponent<Collider>();
            if (accentCollider != null)
            {
                Object.DestroyImmediate(accentCollider);
            }

            var accentRenderer = accentGo.GetComponent<MeshRenderer>();
            if (accentRenderer != null)
            {
                accentRenderer.sharedMaterial = accent;
            }
        }

        private static void AddSprite(Transform parent, string name, Sprite sprite, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * 0.1f;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
        }

        private static void AddSound(Transform parent, string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private static void CreateParticles(Transform parent, string name, Material material, int count, Color color, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.duration = 1.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 1.1f;
            main.startSize = 0.055f;
            main.startColor = color;
            main.maxParticles = count * 4;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.15f;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            system.Stop();
        }

        // ---------------------------------------------------- генерация ассетов

        private static Sprite CreateSpriteAsset(string name, int size, Color background, Color border)
        {
            string path = Path.Combine(SpriteFolder, name + ".png").Replace('\\', '/');

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size * 0.5f;
                    float dy = y - size * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    Color color = background;
                    color.a = distance <= radius ? 1f : 0f;

                    if (distance > radius - 3f && distance <= radius)
                    {
                        color = border;
                        color.a = 1f;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Короткий «звон монет» — обычный wav, который потом можно заменить своим.</summary>
        private static AudioClip CreateCoinWav(string name, float duration, float first, float second)
        {
            string path = Path.Combine(AudioFolder, name + ".wav").Replace('\\', '/');

            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
            {
                return existing;
            }

            const int rate = 44100;
            int samples = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
            var data = new short[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Exp(-t * 9f);

                float value =
                    Mathf.Sin(2f * Mathf.PI * first * t) * 0.55f +
                    Mathf.Sin(2f * Mathf.PI * second * t) * 0.35f * Mathf.Exp(-t * 16f) +
                    Mathf.Sin(2f * Mathf.PI * first * 1.5f * t) * 0.2f * Mathf.Exp(-t * 24f);

                data[i] = (short)Mathf.Clamp(Mathf.RoundToInt(value * envelope * 12000f), short.MinValue, short.MaxValue);
            }

            WriteWav(path, data, rate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void WriteWav(string path, short[] samples, int rate)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;

                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    writer.Write(samples[i]);
                }
            }
        }

        private static void AddBox(Transform parent, string name, Vector3 size, Vector3 localPosition, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.transform.localPosition = localPosition;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>Набор компонентов Marrow, чтобы предмет можно было взять и он попадал в пул спавна.</summary>
        private static void SetupInteractable(GameObject root, float mass, Vector3 size)
        {
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = mass;
            rigidbody.drag = 0.2f;
            rigidbody.angularDrag = 2f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;

            root.AddComponent<Poolee>();
            root.AddComponent<MarrowEntity>();
            root.AddComponent<MarrowBody>();

            var host = root.AddComponent<InteractableHost>();
            SetBoolField(host, "ignoreBodyOnGrab", false);

            // Точка захвата: коллайдер + грипп. Поля гриппа задаём через SerializedObject,
            // чтобы не зависеть от версии SDK (часть полей прячется в приватных сериализованных).
            var gripGo = new GameObject("Grip");
            gripGo.transform.SetParent(root.transform, false);

            var gripCollider = gripGo.AddComponent<BoxCollider>();
            gripCollider.size = size * 0.85f;
            gripCollider.isTrigger = true;

            var grip = gripGo.AddComponent<BoxGrip>();
            SetFloatField(grip, "_radius", Mathf.Max(size.x, size.y) * 0.4f);
            SetFloatField(grip, "radius", Mathf.Max(size.x, size.y) * 0.4f);
            SetObjectField(grip, "_host", host);
            SetObjectField(grip, "_interactableHost", host);
        }

        // ------------------------------------------------- утилиты настройки полей

        private static void SetFloatField(Object target, string fieldName, float value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
            {
                property.floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBoolField(Object target, string fieldName, bool value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetObjectField(Object target, string fieldName, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static string SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[AuroraRP] Префаб сохранён: " + path);
            return path;
        }

        // ----------------------------------------------------------------- краты

        private static void CreateSpawnableCrate(Pallet pallet, string title, string prefabPath, string barcode)
        {
            // Уже есть такой crate? Обновляем ссылку на префаб.
            foreach (var crate in pallet.Crates)
            {
                if (crate != null && crate.Barcode.ID == barcode)
                {
                    crate.MainAsset = new MarrowAsset(AssetDatabase.AssetPathToGUID(prefabPath));
                    EditorUtility.SetDirty(crate);
                    return;
                }
            }

            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("[AuroraRP] Не найден префаб: " + prefabPath);
                return;
            }

            var spawnable = Crate.CreateCrateT<SpawnableCrate>(pallet, title, new MarrowAsset(guid));
            spawnable.Barcode = new Barcode(barcode);
            spawnable.Description = title + " — контент AuroraRP";

            string assetPath = Path.Combine(PalletFolder, spawnable.GetAssetFilename()).Replace('\\', '/');
            AssetDatabase.CreateAsset(spawnable, assetPath);
            pallet.Crates.Add(spawnable);

            EditorUtility.SetDirty(pallet);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AuroraRP] Создан спавнабл: {title} ({barcode})");
        }
    }
}
