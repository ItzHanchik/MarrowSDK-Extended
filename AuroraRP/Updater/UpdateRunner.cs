using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MelonLoader;

namespace AuroraRPUpdater
{
    /// <summary>Манифест обновлений (AuroraRP/update.json в репозитории).</summary>
    public sealed class UpdateManifest
    {
        public string version { get; set; }
        public string dll { get; set; }
        public string dllSha256 { get; set; }
        public string pallet { get; set; }
        public string palletVersion { get; set; }
        public string notes { get; set; }
    }

    /// <summary>
    /// Качает обновления: AuroraRP.dll в папку Mods и палет в MODS игры.
    /// Никогда не бросает исключения наружу — только пишет в лог.
    /// </summary>
    internal sealed class UpdateRunner
    {
        private const string PalletMarker = ".aurora_update";

        private static readonly HttpClient Http = CreateClient();

        private readonly MelonLogger.Instance _log;

        public UpdateRunner(MelonLogger.Instance log)
        {
            _log = log;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.Add("User-Agent", "AuroraRPUpdater/" + AuroraRpUpdaterPlugin.PluginVersion);
            return client;
        }

        public void Run(string manifestUrl, bool installPallet)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                return;
            }

            var manifest = FetchManifest(manifestUrl);
            if (manifest == null)
            {
                return;
            }

            UpdateMod(manifest);

            if (installPallet)
            {
                UpdatePallet(manifest);
            }
        }

        // ------------------------------------------------------------------ манифест

        private UpdateManifest FetchManifest(string url)
        {
            try
            {
                string json = Http.GetStringAsync(url).GetAwaiter().GetResult();
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.version))
                {
                    _log.Warning("Манифест обновлений пустой или без версии: " + url);
                    return null;
                }

                return manifest;
            }
            catch (Exception e)
            {
                _log.Warning("Не удалось прочитать манифест обновлений (" + url + "): " + e.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------------- мод

        private void UpdateMod(UpdateManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.dll))
            {
                return;
            }

            var remote = ParseVersion(manifest.version);
            if (remote == null)
            {
                _log.Warning("Версия в манифесте непонятная: " + manifest.version);
                return;
            }

            var local = ReadLocalVersion(AuroraRpUpdaterPlugin.ModAssemblyPath);

            if (local != null && local >= remote)
            {
                _log.Msg("AuroraRP.dll актуален (версия {0}).", local);
                return;
            }

            string what = local == null ? "не установлен" : "старый (" + local + ")";
            _log.Msg("AuroraRP.dll {0}, качаю версию {1}...", what, remote);

            byte[] bytes;

            try
            {
                bytes = Http.GetByteArrayAsync(manifest.dll).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                _log.Error("Не удалось скачать AuroraRP.dll: " + e.Message);
                return;
            }

            if (bytes.Length < 1024)
            {
                _log.Error("Скачанный AuroraRP.dll подозрительно маленький ({0} байт), пропускаю.", bytes.Length);
                return;
            }

            if (!string.IsNullOrWhiteSpace(manifest.dllSha256) &&
                !HashMatches(bytes, manifest.dllSha256.Trim()))
            {
                _log.Error("У скачанного AuroraRP.dll не сошлась контрольная сумма (sha256). Пропускаю.");
                return;
            }

            string target = AuroraRpUpdaterPlugin.ModAssemblyPath;
            string temp = target + ".new";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
                File.WriteAllBytes(temp, bytes);
                File.Copy(temp, target, true);
                File.Delete(temp);
                _log.Msg("AuroraRP.dll обновлён до {0}. Мод загрузится в этом же запуске.", remote);
            }
            catch (Exception e)
            {
                _log.Error("Не удалось записать AuroraRP.dll (" + target + "): " + e.Message);
                TryDelete(temp);
            }
        }

        private static Version ReadLocalVersion(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var name = AssemblyName.GetAssemblyName(path);

                // Сбрасываем 4-е число, чтобы не путать с версией манифеста.
                return new Version(Math.Max(0, name.Version.Major), Math.Max(0, name.Version.Minor),
                    Math.Max(0, name.Version.Build));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Version ParseVersion(string text)
        {
            try
            {
                if (Version.TryParse(text.Trim(), out var version))
                {
                    return version;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static bool HashMatches(byte[] bytes, string expected)
        {
            try
            {
                using var sha = SHA256.Create();
                string actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");

                return string.Equals(actual, expected.Replace("-", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // --------------------------------------------------------------------- палет

        private string PalletModsPath
        {
            get
            {
                try
                {
                    // ...\AppData\Local -> ...\AppData\LocalLow
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string appData = Path.GetDirectoryName(localAppData.TrimEnd(Path.DirectorySeparatorChar));

                    if (string.IsNullOrEmpty(appData))
                    {
                        return null;
                    }

                    return Path.Combine(appData, "LocalLow", "Stress Level Zero", "BONELAB", "MODS");
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private void UpdatePallet(UpdateManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.pallet) || string.IsNullOrWhiteSpace(manifest.palletVersion))
            {
                return;
            }

            string mods = PalletModsPath;
            if (string.IsNullOrEmpty(mods))
            {
                _log.Warning("Не нашёл папку MODS игры — палет не скачиваю.");
                return;
            }

            string palletRoot = Path.Combine(mods, "AuroraRP");
            string marker = Path.Combine(palletRoot, PalletMarker);

            try
            {
                if (File.Exists(marker) && File.ReadAllText(marker).Trim() == manifest.palletVersion.Trim())
                {
                    _log.Msg("Палет AuroraRP актуален ({0}).", manifest.palletVersion);
                    return;
                }
            }
            catch (Exception)
            {
            }

            _log.Msg("Качаю палет AuroraRP ({0})...", manifest.palletVersion);

            string tempZip = Path.Combine(Path.GetTempPath(), "AuroraRP_pallet_" + Guid.NewGuid().ToString("N") + ".zip");
            string tempDir = Path.Combine(Path.GetTempPath(), "AuroraRP_pallet_" + Guid.NewGuid().ToString("N"));

            try
            {
                File.WriteAllBytes(tempZip, Http.GetByteArrayAsync(manifest.pallet).GetAwaiter().GetResult());

                string palletJson = ExtractAndFind(tempZip, tempDir);

                if (string.IsNullOrEmpty(palletJson))
                {
                    _log.Error("В скачанном архиве нет pallet.json — не устанавливаю.");
                    return;
                }

                string sourceDir = Path.GetDirectoryName(palletJson);

                // Если pallet.json лежит не в корне архива — ставим подпапку как есть.
                string targetDir = palletRoot;
                if (!string.IsNullOrEmpty(sourceDir) &&
                    !string.Equals(Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                {
                    targetDir = Path.Combine(mods, Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar)));
                }

                if (Directory.Exists(targetDir) && !File.Exists(Path.Combine(targetDir, "pallet.json")) &&
                    !File.Exists(Path.Combine(targetDir, PalletMarker)))
                {
                    _log.Error("Папка " + targetDir + " занята чем-то другим — палет не трогаю.");
                    return;
                }

                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                CopyDirectory(sourceDir ?? tempDir, targetDir);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.WriteAllText(Path.Combine(targetDir, PalletMarker), manifest.palletVersion + Environment.NewLine);
                _log.Msg("Палет AuroraRP установлен в {0}.", targetDir);
            }
            catch (Exception e)
            {
                _log.Error("Не удалось обновить палет: " + e.Message);
            }
            finally
            {
                TryDelete(tempZip);

                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>Распаковывает zip (безопасно) и возвращает путь к pallet.json или null.</summary>
        private static string ExtractAndFind(string zipPath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) && string.IsNullOrEmpty(entry.FullName.TrimEnd('/', '\\')))
                    {
                        continue;
                    }

                    string destination = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

                    // Защита от «zip-slip».
                    if (!destination.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? targetDir);
                    entry.ExtractToFile(destination, true);
                }
            }

            var found = Directory.GetFiles(targetDir, "pallet.json", SearchOption.AllDirectories);
            return found.Length > 0 ? found[0] : null;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
