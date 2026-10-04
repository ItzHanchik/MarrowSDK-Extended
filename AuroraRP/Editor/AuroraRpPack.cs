using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AuroraRP.EditorTools
{
    /// <summary>
    /// «Всё в одном плагине»: собирает пак (zip) из бандлов палета + дефолтного конфига +
    /// manifest.json и кладёт его в <c>Assets/AuroraRP/Mod~/Runtime/Assets/aurorarp.pack</c>.
    /// Дальше этот файл вшивается внутрь AuroraRP.dll как встроенный ресурс, и мод берёт
    /// красоту прямо из DLL — палет игроку не нужен.
    ///
    /// Формат пака:
    ///   manifest.json  — версия, карта barcode → префаб, шейдеры/текстуры материалов
    ///   config.json    — дефолтный конфиг (перекрывается палетным и личным)
    ///   bundles/*.bundle — AssetBundle'ы палета
    /// </summary>
    public static class AuroraRpPack
    {
        /// <summary>Куда кладём пак (отсюда его берёт csproj как EmbeddedResource).</summary>
        public static string PackPath => Path.Combine(AuroraRpPaths.ModRoot, "Runtime", "Assets", "aurorarp.pack");

        private const string MaterialFolder = "Assets/AuroraRP/Pallet/Materials";

        [MenuItem("AuroraRP/7. Вшить красоту в DLL (собрать пак)", false, 23)]
        public static void BuildPackMenu()
        {
            string palletFolder = FindPalletFolder();

            if (string.IsNullOrEmpty(palletFolder))
            {
                EditorUtility.DisplayDialog("AuroraRP",
                    "Не нашёл собранный палет.\n\nСначала нажмите «3. СОБРАТЬ ВСЁ» — тогда появится папка " +
                    "Assets/AuroraRP/Pallet/ServerData.\n\nЗатем нажмите «3. СОБРАТЬ ВСЁ» ещё раз: пак вшивается в DLL.",
                    "Ок");
                return;
            }

            string pack = CreatePack(palletFolder, out string message);
            EditorUtility.DisplayDialog("AuroraRP", message, "Ок");

            if (pack != null)
            {
                EditorUtility.RevealInFinder(pack);
            }
        }

        /// <summary>Ищет папку собранного палета (Addressables: ServerData/&lt;платформа&gt;).</summary>
        public static string FindPalletFolder()
        {
            string serverData = Path.Combine(AuroraRpPaths.PalletRoot, "ServerData");

            if (Directory.Exists(serverData))
            {
                foreach (string dir in Directory.GetDirectories(serverData))
                {
                    if (HasBundles(dir))
                    {
                        return dir;
                    }
                }
            }

            if (HasBundles(AuroraRpPaths.PalletRoot))
            {
                return AuroraRpPaths.PalletRoot;
            }

            return null;
        }

        private static bool HasBundles(string folder)
        {
            try
            {
                return Directory.Exists(folder) &&
                       Directory.GetFiles(folder, "*.bundle", SearchOption.AllDirectories).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Собирает пак. Возвращает путь к нему или null; текст результата — в message.
        /// </summary>
        public static string CreatePack(string palletFolder, out string message)
        {
            message = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(palletFolder) || !Directory.Exists(palletFolder))
                {
                    message = "Папка палета не найдена: " + palletFolder;
                    return null;
                }

                var bundles = new List<string>(Directory.GetFiles(palletFolder, "*.bundle", SearchOption.AllDirectories));

                string target = PackPath;
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                using (var stream = new FileStream(target, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (string bundle in bundles)
                    {
                        var entry = zip.CreateEntry("bundles/" + Path.GetFileName(bundle), CompressionLevel.Optimal);

                        using (var entryStream = entry.Open())
                        using (var fileStream = File.OpenRead(bundle))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }

                    WriteText(zip, "manifest.json", BuildManifest());
                    WriteText(zip, "config.json", BuildConfig());
                }

                AssetDatabase.Refresh();

                long size = new FileInfo(target).Length;

                message = "Пак собран и вшит в сборку мода.\n\n" +
                          "Файл: " + target + "\n" +
                          "Бандлов: " + bundles.Count + "\n" +
                          "Размер пака: " + (size / 1024) + " КБ (столько примерно прибавится к AuroraRP.dll)\n\n" +
                          "Теперь нажмите «3. СОБРАТЬ ВСЁ» — DLL соберётся уже с красотой внутри.";

                Debug.Log("[AuroraRP] Пак собран: " + target + " (" + size + " байт, бандлов: " + bundles.Count + ")");

                return target;
            }
            catch (Exception e)
            {
                message = "Ошибка сборки пака: " + e.Message;
                Debug.LogException(e);
                return null;
            }
        }

        /// <summary>Удаляет пак (сборка DLL без красоты — только логика).</summary>
        public static void RemovePack()
        {
            try
            {
                if (File.Exists(PackPath))
                {
                    File.Delete(PackPath);
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AuroraRP] Не удалось удалить пак: " + e.Message);
            }
        }

        // ------------------------------------------------------------ manifest.json

        private static string BuildManifest()
        {
            var json = new StringBuilder();

            json.Append("{\n");
            json.Append("  \"version\": \"").Append(Escape(AuroraRpPaths.ModVersion)).Append("\",\n");

            json.Append("  \"spawnables\": [\n");
            AppendSpawnable(json, AuroraRpContent.TerminalBarcode, "AuroraTerminal", true);
            AppendSpawnable(json, AuroraRpContent.PrinterBarcode, "AuroraMoneyPrinter", true);
            AppendSpawnable(json, AuroraRpContent.VisualSetBarcode, "AuroraVisualSet", false);
            json.Append("  ],\n");

            json.Append("  \"materials\": [\n");
            AppendMaterials(json);
            json.Append("  ]\n");

            json.Append("}\n");

            return json.ToString();
        }

        private static void AppendSpawnable(StringBuilder json, string barcode, string prefab, bool withComma)
        {
            json.Append("    { \"barcode\": \"").Append(Escape(barcode))
                .Append("\", \"prefab\": \"").Append(Escape(prefab)).Append("\" }")
                .Append(withComma ? "," : "")
                .Append("\n");
        }

        private static void AppendMaterials(StringBuilder json)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
            int written = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    continue;
                }

                string shader = material.shader != null ? material.shader.name : string.Empty;
                string texture = material.mainTexture != null ? material.mainTexture.name : string.Empty;
                Color color = material.color;

                if (written > 0)
                {
                    json.Append(",\n");
                }

                json.Append("    { \"name\": \"").Append(Escape(material.name))
                    .Append("\", \"shader\": \"").Append(Escape(shader))
                    .Append("\", \"texture\": \"").Append(Escape(texture))
                    .Append("\", \"color\": \"")
                    .Append(color.r.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(color.g.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(color.b.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                    .Append(color.a.ToString("0.####", CultureInfo.InvariantCulture)).Append("\" }");

                written++;
            }

            json.Append(written > 0 ? "\n" : "");
        }

        // -------------------------------------------------------------- config.json

        private static string BuildConfig()
        {
            string path = Path.Combine(AuroraRpPaths.PalletRoot, "config.json");

            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AuroraRP] Не прочитался config.json для пака: " + e.Message);
            }

            return "{}";
        }

        // ------------------------------------------------------------------ утилиты

        private static void WriteText(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);

            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content ?? string.Empty);
            }
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
