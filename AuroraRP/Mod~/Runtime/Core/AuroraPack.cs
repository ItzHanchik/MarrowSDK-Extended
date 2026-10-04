using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// «Всё в одном плагине»: красота (бандлы Unity), дефолтный конфиг и карта спавна
    /// лежат ВНУТРИ AuroraRP.dll одним ресурсом «aurorarp.pack» (обычный zip).
    ///
    /// Структура пака:
    ///   manifest.json  — { version, spawnables:[{barcode,prefab}], materials:[{name,shader,texture,color}] }
    ///   config.json    — дефолтный конфиг (перекрывается палетным и личным)
    ///   *.bundle       — AssetBundle'ы из палета AuroraRP
    ///
    /// Если пака в DLL нет (сборка без Unity-контента) — всё просто работает на кодовых
    /// заглушках, как раньше.
    /// </summary>
    public static class AuroraPack
    {
        /// <summary>Имя встроенного ресурса (задаётся LogicalName в csproj).</summary>
        public const string ResourceName = "AuroraRP.aurorarp.pack";

        /// <summary>Имя префаба-контейнера красоты внутри бандла.</summary>
        public const string VisualSetPrefab = "AuroraVisualSet";

        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RuntimeAnimatorController> Controllers = new Dictionary<string, RuntimeAnimatorController>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> BarcodeToPrefab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<AssetBundle> Bundles = new List<AssetBundle>();

        [Serializable]
        private sealed class PackSpawnable
        {
            public string barcode = "";
            public string prefab = "";
        }

        [Serializable]
        private sealed class PackMaterial
        {
            public string name = "";
            public string shader = "";
            public string texture = "";
            public string color = "";
        }

        [Serializable]
        private sealed class PackManifest
        {
            public string version = "";
            public List<PackSpawnable> spawnables = new List<PackSpawnable>();
            public List<PackMaterial> materials = new List<PackMaterial>();
        }

        private static PackManifest _manifest;
        private static string _configJson;
        private static string _version = "";
        private static string _resourceName;
        private static bool _resourceChecked;
        private static bool _jsonTried;
        private static bool _tried;
        private static bool _loaded;
        private static bool _procedural;

        /// <summary>Пак вообще есть в этой сборке DLL?</summary>
        public static bool IsAvailable
        {
            get
            {
                if (!_resourceChecked)
                {
                    _resourceChecked = true;
                    _resourceName = FindResource();
                }

                return !string.IsNullOrEmpty(_resourceName);
            }
        }

        /// <summary>Пак загружен (бандлы разобраны, ассеты доступны)?</summary>
        public static bool IsLoaded => _loaded;

        /// <summary>
        /// Есть ли у мода свой контент вообще: пак из Unity внутри DLL или красота,
        /// собранная кодом (модель купюр, принтер, терминал, вспышки).
        /// По этому признаку спавн идёт «из DLL», не требуя палета.
        /// </summary>
        public static bool HasContent => _loaded || _procedural;

        /// <summary>Красота собрана кодом (пак не нужен).</summary>
        public static bool IsProcedural => _procedural;

        /// <summary>Сообщает реестру, что контент собран кодом.</summary>
        public static void MarkProcedural()
        {
            _procedural = true;
        }

        /// <summary>Регистрирует объект, собранный кодом, как обычный префаб пака.</summary>
        public static void RegisterProceduralPart(string name, GameObject part)
        {
            if (string.IsNullOrEmpty(name) || part == null)
            {
                return;
            }

            _procedural = true;
            Prefabs[name] = part;
        }

        /// <summary>Версия контента из manifest.json.</summary>
        public static string Version => _version;

        /// <summary>Дефолтный конфиг, зашитый в DLL (может быть null).</summary>
        public static string ConfigJson
        {
            get
            {
                if (_configJson == null && !_jsonTried)
                {
                    EnsureJson();
                }

                return _configJson;
            }
        }

        public static int PrefabCount => Prefabs.Count;

        public static IEnumerable<KeyValuePair<string, Sprite>> AllSprites => Sprites;
        public static IEnumerable<KeyValuePair<string, AudioClip>> AllClips => Clips;
        public static IEnumerable<KeyValuePair<string, Material>> AllMaterials => Materials;
        public static IEnumerable<KeyValuePair<string, RuntimeAnimatorController>> AllControllers => Controllers;

        // ------------------------------------------------------------------ ресурс

        private static string FindResource()
        {
            try
            {
                var assembly = typeof(AuroraPack).Assembly;
                string[] names = assembly.GetManifestResourceNames();

                if (names == null)
                {
                    return null;
                }

                foreach (string name in names)
                {
                    if (string.Equals(name, ResourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                // На случай другого LogicalName — ищем по расширению.
                foreach (string name in names)
                {
                    if (name.EndsWith(".pack", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "pack resource names");
            }

            return null;
        }

        // ------------------------------------------------------------------- загрузка

        /// <summary>
        /// Читает из пака только manifest.json и config.json (без загрузки бандлов).
        /// Нужно конфигу на старте — до того, как понадобится красота.
        /// </summary>
        public static bool EnsureJson()
        {
            if (_jsonTried)
            {
                return _manifest != null || _configJson != null;
            }

            _jsonTried = true;

            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                byte[] bytes = ReadResourceBytes(_resourceName);

                if (bytes == null || bytes.Length == 0)
                {
                    return false;
                }

                using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                {
                    ReadJson(zip);
                }

                return _manifest != null || _configJson != null;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "pack json");
                return false;
            }
        }

        /// <summary>Читает пак целиком и разбирает бандлы. Повторные вызовы бесплатны.</summary>
        public static bool EnsureLoaded()
        {
            if (_loaded)
            {
                return true;
            }

            if (_tried || !IsAvailable)
            {
                return false;
            }

            _tried = true;

            try
            {
                byte[] bytes = ReadResourceBytes(_resourceName);

                if (bytes == null || bytes.Length == 0)
                {
                    AuroraLog.Warn("Пак в DLL пустой — красоту берём из палета.");
                    return false;
                }

                using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                {
                    if (_manifest == null && _configJson == null)
                    {
                        ReadJson(zip);
                    }

                    LoadBundles(zip);
                }

                _loaded = Prefabs.Count > 0 || Sprites.Count > 0 || Clips.Count > 0 || Materials.Count > 0;

                AuroraLog.Info("Красота из DLL: {0} префабов, {1} иконок, {2} звуков, {3} материалов (версия {4})",
                    Prefabs.Count, Sprites.Count, Clips.Count, Materials.Count, string.IsNullOrEmpty(_version) ? "?" : _version);

                return _loaded;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "pack load");
                return false;
            }
        }

        private static byte[] ReadResourceBytes(string resource)
        {
            try
            {
                using (var stream = typeof(AuroraPack).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "pack read");
                return null;
            }
        }

        /// <summary>manifest.json + config.json из пака (читаются до бандлов).</summary>
        private static void ReadJson(ZipArchive zip)
        {
            foreach (var entry in zip.Entries)
            {
                string name = EntryName(entry);

                if (name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        _manifest = AuroraJson.Read<PackManifest>(ReadText(entry));

                        if (_manifest == null)
                        {
                            continue;
                        }

                        _version = _manifest.version ?? "";

                        if (_manifest.spawnables != null)
                        {
                            foreach (var spawnable in _manifest.spawnables)
                            {
                                if (spawnable == null || string.IsNullOrEmpty(spawnable.barcode) || string.IsNullOrEmpty(spawnable.prefab))
                                {
                                    continue;
                                }

                                BarcodeToPrefab[spawnable.barcode] = spawnable.prefab;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AuroraLog.Exception(e, "pack manifest");
                    }
                }
                else if (name.Equals("config.json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        _configJson = ReadText(entry);
                    }
                    catch (Exception e)
                    {
                        AuroraLog.Exception(e, "pack config");
                    }
                }
            }
        }

        private static void LoadBundles(ZipArchive zip)
        {
            foreach (var entry in zip.Entries)
            {
                string name = EntryName(entry);

                if (!name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    byte[] data = ReadBytes(entry);
                    var bundle = AssetBundle.LoadFromMemory(data);

                    if (bundle == null)
                    {
                        AuroraLog.Warn("Бандл {0} не загрузился.", name);
                        continue;
                    }

                    Bundles.Add(bundle);
                    Collect(bundle);
                }
                catch (Exception e)
                {
                    AuroraLog.Exception(e, "pack bundle " + name);
                }
            }
        }

        /// <summary>Раскладывает ассеты бандла по словарям (по именам объектов).</summary>
        private static void Collect(AssetBundle bundle)
        {
            var assets = bundle.LoadAllAssets();

            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i] as UnityEngine.Object;

                if (asset == null || string.IsNullOrEmpty(asset.name))
                {
                    continue;
                }

                try
                {
                    string name = asset.name;

                    var prefab = asset as GameObject;
                    if (prefab != null)
                    {
                        Prefabs[name] = prefab;
                        continue;
                    }

                    var sprite = asset as Sprite;
                    if (sprite != null)
                    {
                        Sprites[name] = sprite;
                        continue;
                    }

                    var clip = asset as AudioClip;
                    if (clip != null)
                    {
                        Clips[name] = clip;
                        continue;
                    }

                    var controller = asset as RuntimeAnimatorController;
                    if (controller != null)
                    {
                        Controllers[name] = controller;
                        continue;
                    }

                    var texture = asset as Texture2D;
                    if (texture != null)
                    {
                        Textures[name] = texture;
                        continue;
                    }

                    var material = asset as Material;
                    if (material != null)
                    {
                        Materials[name] = RepairMaterial(material);
                    }
                }
                catch (Exception e)
                {
                    AuroraLog.Exception(e, "collect " + asset.name);
                }
            }
        }

        // ------------------------------------------------------------------ материалы

        private static PackMaterial FindMaterialInfo(string name)
        {
            if (_manifest == null || _manifest.materials == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (var info in _manifest.materials)
            {
                if (info != null && string.Equals(info.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>
        /// В чужих бандлах шейдеры часто «вырезаны» сборкой (материал становится розовым).
        /// Собираем материал заново из безопасного шейдера, сохраняя текстуру и цвет.
        /// </summary>
        private static Material RepairMaterial(Material source)
        {
            try
            {
                if (source == null)
                {
                    return null;
                }

                var info = FindMaterialInfo(source.name);
                string wanted = info != null ? info.shader : null;

                Shader shader = null;
                if (!string.IsNullOrEmpty(wanted))
                {
                    try
                    {
                        shader = Shader.Find(wanted);
                    }
                    catch
                    {
                        shader = null;
                    }
                }

                bool broken = source.shader == null;

                if (!broken && !string.IsNullOrEmpty(source.shader.name) &&
                    source.shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    broken = true;
                }

                // Материал целый и шейдер тот, что задуман — оставляем как есть.
                if (!broken && (shader == null || source.shader == shader))
                {
                    return source;
                }

                if (shader == null || source.shader == shader)
                {
                    shader = FindFallbackShader();
                }

                if (shader == null)
                {
                    return source;
                }

                var repaired = new Material(shader) { name = source.name };

                Color color = source.color;
                if (info != null && !string.IsNullOrEmpty(info.color) && TryParseColor(info.color, out var parsed))
                {
                    color = parsed;
                }

                SetColor(repaired, color);
                SetTexture(repaired, ResolveTexture(info, source));

                return repaired;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "material repair");
                return source;
            }
        }

        private static Shader FindFallbackShader()
        {
            string[] candidates =
            {
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Particles/Unlit",
                "Sprites/Default",
                "Particles/Standard Unlit",
                "Standard",
                "Unlit/Texture",
                "Unlit/Color",
                "Legacy Shaders/Diffuse"
            };

            foreach (string candidate in candidates)
            {
                try
                {
                    var shader = Shader.Find(candidate);
                    if (shader != null)
                    {
                        return shader;
                    }
                }
                catch
                {
                    // пропускаем недоступные шейдеры
                }
            }

            return null;
        }

        private static Texture ResolveTexture(PackMaterial info, Material source)
        {
            if (info != null && !string.IsNullOrEmpty(info.texture) && Textures.TryGetValue(info.texture, out var texture))
            {
                return texture;
            }

            try
            {
                return source != null ? source.mainTexture : null;
            }
            catch
            {
                return null;
            }
        }

        private static void SetColor(Material material, Color color)
        {
            try
            {
                material.color = color;
            }
            catch
            {
                // у шейдера нет _Color
            }

            if (SafeHasProperty(material, "_BaseColor"))
            {
                try
                {
                    material.SetColor("_BaseColor", color);
                }
                catch
                {
                    // пропускаем
                }
            }
        }

        private static void SetTexture(Material material, Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            try
            {
                material.mainTexture = texture;
            }
            catch
            {
                // пропускаем
            }

            if (SafeHasProperty(material, "_BaseMap"))
            {
                try
                {
                    material.SetTexture("_BaseMap", texture);
                }
                catch
                {
                    // пропускаем
                }
            }
        }

        private static bool SafeHasProperty(Material material, string property)
        {
            try
            {
                return material.HasProperty(property);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseColor(string text, out Color color)
        {
            color = Color.white;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] parts = text.Split(',');

            if (parts.Length < 3)
            {
                return false;
            }

            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) ||
                !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b))
            {
                return false;
            }

            float a = 1f;

            if (parts.Length >= 4)
            {
                float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a);
            }

            color = new Color(r, g, b, a);
            return true;
        }

        // --------------------------------------------------------------------- доступ

        public static GameObject FindPrefab(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (Prefabs.TryGetValue(name, out var prefab))
            {
                return prefab;
            }

            // Мягкий поиск: «VisualSet», «AuroraVisualSet», «AuroraRP VisualSet».
            foreach (var pair in Prefabs)
            {
                if (pair.Key.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>Имя префаба для barcode (из manifest.json).</summary>
        public static string PrefabNameForBarcode(string barcode)
        {
            if (string.IsNullOrEmpty(barcode))
            {
                return null;
            }

            if (BarcodeToPrefab.TryGetValue(barcode, out var prefab) && !string.IsNullOrEmpty(prefab))
            {
                return prefab;
            }

            // «Всё в DLL»: пак может быть пустым — тогда имена предметов берём из кодовой красоты.
            if (!_procedural)
            {
                return null;
            }

            string lower = barcode.ToLowerInvariant();

            if (lower.Contains("visualset"))
            {
                return AuroraProcedural.VisualSetName;
            }

            if (lower.Contains("terminal") || lower.Contains("atm"))
            {
                return AuroraProcedural.TerminalName;
            }

            if (lower.Contains("printer") || lower.Contains("cash") || lower.Contains("money"))
            {
                return AuroraProcedural.PrinterName;
            }

            return null;
        }

        public static Sprite GetSprite(string name)
        {
            return !string.IsNullOrEmpty(name) && Sprites.TryGetValue(name, out var sprite) ? sprite : null;
        }

        public static AudioClip GetClip(string name)
        {
            return !string.IsNullOrEmpty(name) && Clips.TryGetValue(name, out var clip) ? clip : null;
        }

        public static Material GetMaterial(string name)
        {
            return !string.IsNullOrEmpty(name) && Materials.TryGetValue(name, out var material) ? material : null;
        }

        public static RuntimeAnimatorController GetController(string name)
        {
            return !string.IsNullOrEmpty(name) && Controllers.TryGetValue(name, out var controller) ? controller : null;
        }

        // ---------------------------------------------------------------------- спавн

        /// <summary>
        /// Создаёт объект из пака. stripSpawnComponents снимает компоненты пула/entity —
        /// нужно для объектов, которые игра не создавала через свой спавнер (красота, контейнеры).
        /// </summary>
        public static GameObject Instantiate(string prefabName, Vector3 position, Quaternion rotation, bool stripSpawnComponents = true)
        {
            if (!_loaded && !EnsureLoaded() && !_procedural)
            {
                return null;
            }

            var prefab = FindPrefab(prefabName);

            if (prefab == null)
            {
                return null;
            }

            try
            {
                var instance = UnityEngine.Object.Instantiate(prefab, position, rotation) as GameObject;

                if (instance == null)
                {
                    return null;
                }

                if (stripSpawnComponents)
                {
                    StripSpawnComponents(instance);
                }

                // Префабы кодовой красоты хранятся отключёнными (чтобы не висели в мире).
                if (!instance.activeSelf)
                {
                    instance.SetActive(true);
                }

                return instance;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "instantiate " + prefabName);
                return null;
            }
        }

        private static void StripSpawnComponents(GameObject go)
        {
            try
            {
                var poolee = go.GetComponent<Il2CppSLZ.Marrow.Pool.Poolee>();
                if (poolee != null)
                {
                    UnityEngine.Object.Destroy(poolee);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "strip poolee");
            }

            try
            {
                var entity = go.GetComponent<Il2CppSLZ.Marrow.Interaction.MarrowEntity>();
                if (entity != null)
                {
                    UnityEngine.Object.Destroy(entity);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "strip entity");
            }

            try
            {
                var body = go.GetComponent<Il2CppSLZ.Marrow.Interaction.MarrowBody>();
                if (body != null)
                {
                    UnityEngine.Object.Destroy(body);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "strip body");
            }
        }

        public static void Shutdown()
        {
            foreach (var bundle in Bundles)
            {
                try
                {
                    if (bundle != null)
                    {
                        bundle.Unload(false);
                    }
                }
                catch (Exception e)
                {
                    AuroraLog.Exception(e, "bundle unload");
                }
            }

            Bundles.Clear();
            Prefabs.Clear();
            Sprites.Clear();
            Textures.Clear();
            Clips.Clear();
            Controllers.Clear();
            Materials.Clear();
            BarcodeToPrefab.Clear();

            _loaded = false;
            _procedural = false;
            _tried = false;
            _jsonTried = false;
            _configJson = null;
            _manifest = null;
        }

        // ------------------------------------------------------------------ утилиты

        private static string EntryName(ZipArchiveEntry entry)
        {
            if (entry == null)
            {
                return "";
            }

            string name = entry.FullName ?? "";

            int slash = name.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < name.Length)
            {
                name = name.Substring(slash + 1);
            }

            return name;
        }

        private static string ReadText(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static byte[] ReadBytes(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }
    }
}
