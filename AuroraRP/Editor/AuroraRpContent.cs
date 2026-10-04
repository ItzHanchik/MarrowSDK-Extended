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

        private const string PalletFolder = "Assets/AuroraRP/Pallet";
        private const string PrefabFolder = "Assets/AuroraRP/Pallet/Prefabs";
        private const string MaterialFolder = "Assets/AuroraRP/Pallet/Materials";

        [MenuItem("AuroraRP/2. Создать контент палета (терминал + принтер)", false, 2)]
        public static void CreateContent()
        {
            try
            {
                var pallet = GetOrCreatePallet();
                var terminalPrefab = CreateTerminalPrefab();
                var printerPrefab = CreatePrinterPrefab();

                CreateSpawnableCrate(pallet, "Aurora Terminal", terminalPrefab, TerminalBarcode);
                CreateSpawnableCrate(pallet, "Aurora Money Printer", printerPrefab, PrinterBarcode);

                EditorUtility.SetDirty(pallet);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[AuroraRP] Контент палета создан: терминал + принтер денег.");

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
