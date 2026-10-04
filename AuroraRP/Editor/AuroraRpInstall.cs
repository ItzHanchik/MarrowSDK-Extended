#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AuroraRP.EditorTools
{
    /// <summary>
    /// Установщик AuroraRP «в одну кнопку» (меню AuroraRP сверху в Unity).
    ///
    /// Что делает кнопка «Установить всё в BONELAB»:
    ///   1. находит папку BONELAB (стим-библиотеки, реестр, или спросит вручную);
    ///   2. ставит MelonLoader 0.6.6 x64, если его нет (качает официальный zip);
    ///   3. кладёт AuroraRP.dll в папку Mods (и AuroraRPUpdater.dll в Plugins);
    ///   4. доставляет BoneLib (обязателен) и LabFusion (мультиплеер), если их нет;
    ///   5. пишет отчёт рядом с игрой и предлагает запустить BONELAB.
    ///
    /// Скрипт намеренно не зависит от MarrowSDK: он компилируется в любом проекте Unity.
    /// </summary>
    public static class AuroraRpInstall
    {
        private const string PrefKey = "AuroraRP.GamePath";
        private const string SteamAppId = "1592190"; // BONELAB

        private const string MelonLoaderUrl = "https://github.com/LavaGang/MelonLoader/releases/download/v0.6.6/MelonLoader.x64.zip";
        private const string BoneLibUrl = "https://github.com/yowchap/BoneLib/releases/download/v3.2.2/BoneLib.zip";
        private const string FusionUrl = "https://github.com/Lakatrazz/BONELAB-Fusion/releases/download/v1.14.2/LabFusion.dll";
        private const string FusionUpdaterUrl = "https://github.com/Lakatrazz/BONELAB-Fusion/releases/download/v1.14.2/LabFusionUpdater.dll";

        private const string ReportName = "AuroraRP-Отчёт.txt";

        // ------------------------------------------------------------------ меню

        [MenuItem("AuroraRP/⚡ Установить всё в BONELAB (одна кнопка)", false, 0)]
        public static void InstallAll()
        {
            Run(true);
        }

        [MenuItem("AuroraRP/Проверить установку и прочитать лог игры", false, 20)]
        public static void CheckOnly()
        {
            Run(false);
        }

        [MenuItem("AuroraRP/Показать папку BONELAB", false, 40)]
        public static void OpenGameFolder()
        {
            string game = FindGame(false);

            if (string.IsNullOrEmpty(game))
            {
                EditorUtility.DisplayDialog("AuroraRP", "Папка BONELAB не найдена. Нажми «Установить всё» — я найду её сам или спрошу.", "Ок");
                return;
            }

            EditorUtility.RevealInFinder(game);
        }

        [MenuItem("AuroraRP/Указать папку BONELAB вручную…", false, 41)]
        public static void PickGameFolder()
        {
            string picked = EditorUtility.OpenFolderPanel("Выбери папку BONELAB (там, где BONELAB.exe)", string.Empty, string.Empty);

            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            if (!LooksLikeGame(picked))
            {
                EditorUtility.DisplayDialog("AuroraRP", "В этой папке нет BONELAB.exe. Выбери папку с игрой (обычно steamapps\\common\\BONELAB).", "Ок");
                return;
            }

            EditorPrefs.SetString(PrefKey, picked);
            EditorUtility.DisplayDialog("AuroraRP", "Запомнил папку игры:\n" + picked, "Ок");
        }

        [MenuItem("AuroraRP/Открыть инструкцию (ЧИТАЙ_МЕНЯ.md)", false, 60)]
        public static void OpenReadme()
        {
            string readme = Path.Combine(DataDir, "ЧИТАЙ_МЕНЯ.md");

            if (!File.Exists(readme))
            {
                EditorUtility.DisplayDialog("AuroraRP", "Файл инструкции не найден:\n" + readme, "Ок");
                return;
            }

            EditorUtility.OpenWithDefaultApp(readme);
        }

        [MenuItem("AuroraRP/Добавить MarrowSDK в проект (для палета)", false, 80)]
        public static void AddMarrowSdk()
        {
            const string url = "https://github.com/notnotnotswipez/MarrowSDKExt.git";

            try
            {
                UnityEditor.PackageManager.Client.Add(url);
                UnityEngine.Debug.Log("[AuroraRP] Ставлю MarrowSDK: " + url + " (нужен установленный git, Unity скачает пакет сама).");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[AuroraRP] MarrowSDK не добавился: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ основной сценарий

        private static void Run(bool install)
        {
            var report = new StringBuilder();
            var summary = new List<string>();

            report.AppendLine("AuroraRP — отчёт " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine();

            try
            {
                string game = FindGame(true);

                if (string.IsNullOrEmpty(game))
                {
                    EditorUtility.DisplayDialog("AuroraRP", "Не нашёл папку BONELAB.\nНажми «AuroraRP → Указать папку BONELAB вручную» и повтори.", "Ок");
                    return;
                }

                summary.Add("Папка игры: " + game);
                report.AppendLine("Папка игры: " + game);
                report.AppendLine();

                // --- 1. MelonLoader
                bool hasVersion = File.Exists(Path.Combine(game, "version.dll"));
                bool hasLoader = Directory.Exists(Path.Combine(game, "MelonLoader"));

                if (hasVersion && hasLoader)
                {
                    summary.Add("MelonLoader: уже стоит ✓");
                }
                else if (!install)
                {
                    summary.Add("MelonLoader: НЕ найден ✗ (запусти «Установить всё»)");
                }
                else
                {
                    summary.Add(InstallMelonLoader(game, report) ? "MelonLoader: установлен ✓" : "MelonLoader: НЕ удалось поставить ✗ (см. отчёт)");
                }

                // --- 2. папки и мод
                string mods = Path.Combine(game, "Mods");
                string plugins = Path.Combine(game, "Plugins");

                if (install)
                {
                    Directory.CreateDirectory(mods);
                    Directory.CreateDirectory(plugins);
                }

                // Если мод случайно положили в Plugins — переносим в Mods.
                string wrongPlace = Path.Combine(plugins, "AuroraRP.dll");
                if (File.Exists(wrongPlace) && install)
                {
                    try
                    {
                        File.Copy(wrongPlace, Path.Combine(mods, "AuroraRP.dll"), true);
                        File.Delete(wrongPlace);
                        summary.Add("AuroraRP.dll лежал в Plugins — перенёс в Mods ✓");
                    }
                    catch (Exception e)
                    {
                        report.AppendLine("перенос из Plugins: " + e.Message);
                    }
                }

                string dllSrc = Path.Combine(DataDir, "AuroraRP.dll");
                string dllDst = Path.Combine(mods, "AuroraRP.dll");

                if (!File.Exists(dllSrc))
                {
                    summary.Add("AuroraRP.dll отсутствует в архивe (AuroraRP-Files) ✗");
                }
                else if (install)
                {
                    File.Copy(dllSrc, dllDst, true);
                    summary.Add("AuroraRP.dll → Mods ✓ (" + new FileInfo(dllDst).Length + " байт)");
                }
                else
                {
                    summary.Add(File.Exists(dllDst) ? "AuroraRP.dll в Mods: есть ✓" : "AuroraRP.dll в Mods: нет ✗");
                }

                // Апдейтер — необязательный, но полезный.
                string updaterSrc = Path.Combine(DataDir, "AuroraRPUpdater.dll");
                if (install && File.Exists(updaterSrc))
                {
                    File.Copy(updaterSrc, Path.Combine(plugins, "AuroraRPUpdater.dll"), true);
                    summary.Add("AuroraRPUpdater.dll → Plugins ✓");
                }

                // --- 3. зависимости
                if (install)
                {
                    EnsureBoneLib(game, mods, report, summary);
                    EnsureFusion(game, mods, plugins, report, summary);
                }
                else
                {
                    summary.Add(File.Exists(Path.Combine(mods, "BoneLib.dll")) ? "BoneLib: есть ✓" : "BoneLib: нет ✗ (нужен обязательно)");
                    summary.Add(File.Exists(Path.Combine(mods, "LabFusion.dll")) ? "LabFusion: есть ✓" : "LabFusion: нет (нужен только для мультиплеера)");
                }

                // --- 4. содержимое папок + лог
                report.AppendLine();
                report.AppendLine("Mods: " + ListDlls(mods));
                report.AppendLine("Plugins: " + ListDlls(plugins));
                report.AppendLine();

                summary.AddRange(ReadLog(game, report));
            }
            catch (Exception e)
            {
                report.AppendLine();
                report.AppendLine("ОШИБКА: " + e);
                summary.Add("Ошибка: " + e.Message);
            }

            // --- 5. отчёт и итог
            string reportPath = WriteReport(report);

            UnityEngine.Debug.Log("[AuroraRP] отчёт: " + reportPath + "\n" + report);

            string text = string.Join("\n", summary) +
                          "\n\nПолный отчёт: " + reportPath +
                          "\n\nДальше: зайди в уровень игры и нажми Y + A (или F8).";

            EditorUtility.DisplayDialog("AuroraRP", text, "Ок");
            EditorUtility.RevealInFinder(reportPath);
        }

        // ------------------------------------------------------------------ MelonLoader

        private static bool InstallMelonLoader(string game, StringBuilder report)
        {
            string temp = Path.Combine(Path.GetTempPath(), "AuroraRP-MelonLoader");

            try
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, true);
                }

                Directory.CreateDirectory(temp);

                string zip = Path.Combine(temp, "MelonLoader.x64.zip");
                Download(MelonLoaderUrl, zip);

                string unpacked = Path.Combine(temp, "unpacked");
                ExtractZip(zip, unpacked);

                // В архиве MelonLoader может быть как «плоская» раскладка, так и с папкой сверху:
                // находим version.dll и копируем всё из его папки в корень игры.
                string root = Directory.GetFiles(unpacked, "version.dll", SearchOption.AllDirectories).Length > 0
                    ? Path.GetDirectoryName(Directory.GetFiles(unpacked, "version.dll", SearchOption.AllDirectories)[0])
                    : unpacked;

                CopyTree(root, game);

                report.AppendLine("MelonLoader: скачан и распакован из " + MelonLoaderUrl);

                return File.Exists(Path.Combine(game, "version.dll"));
            }
            catch (Exception e)
            {
                report.AppendLine("MelonLoader: ошибка " + e.Message);

                if (e is UnauthorizedAccessException)
                {
                    report.AppendLine("Похоже, папка игры защищена. Запусти Unity «от имени администратора» и повтори.");
                }

                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                    {
                        Directory.Delete(temp, true);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        // ------------------------------------------------------------------ зависимости

        private static void EnsureBoneLib(string game, string mods, StringBuilder report, List<string> summary)
        {
            if (File.Exists(Path.Combine(mods, "BoneLib.dll")))
            {
                summary.Add("BoneLib: уже стоит ✓");
                return;
            }

            string temp = Path.Combine(Path.GetTempPath(), "AuroraRP-BoneLib");

            try
            {
                Directory.CreateDirectory(temp);

                string zip = Path.Combine(temp, "BoneLib.zip");
                Download(BoneLibUrl, zip);

                string unpacked = Path.Combine(temp, "unpacked");
                ExtractZip(zip, unpacked);

                int copied = 0;

                foreach (string file in Directory.GetFiles(unpacked, "*.dll", SearchOption.AllDirectories))
                {
                    File.Copy(file, Path.Combine(mods, Path.GetFileName(file)), true);
                    copied++;
                }

                summary.Add(copied > 0 ? "BoneLib: скачан и поставлен ✓ (" + copied + " dll)" : "BoneLib: в архиве нет dll ✗");
                report.AppendLine("BoneLib: " + copied + " dll из " + BoneLibUrl);
            }
            catch (Exception e)
            {
                summary.Add("BoneLib: НЕ удалось поставить ✗");
                report.AppendLine("BoneLib: ошибка " + e.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                    {
                        Directory.Delete(temp, true);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static void EnsureFusion(string game, string mods, string plugins, StringBuilder report, List<string> summary)
        {
            try
            {
                string dst = Path.Combine(mods, "LabFusion.dll");

                if (File.Exists(dst))
                {
                    summary.Add("LabFusion: уже стоит ✓");
                    return;
                }

                Download(FusionUrl, dst);
                Download(FusionUpdaterUrl, Path.Combine(plugins, "LabFusionUpdater.dll"));

                summary.Add("LabFusion: скачан и поставлен ✓ (мультиплеер)");
                report.AppendLine("LabFusion: " + FusionUrl);
            }
            catch (Exception e)
            {
                summary.Add("LabFusion: не поставился (мультиплеера не будет)");
                report.AppendLine("LabFusion: ошибка " + e.Message);
            }
        }

        // ------------------------------------------------------------------ чтение лога

        private static List<string> ReadLog(string game, StringBuilder report)
        {
            var summary = new List<string>();

            string log = Path.Combine(game, "MelonLoader", "Latest.log");

            if (!File.Exists(log))
            {
                string alt = Path.Combine(game, "Latest.log");

                if (File.Exists(alt))
                {
                    log = alt;
                }
            }

            report.AppendLine();
            report.AppendLine("Лог MelonLoader: " + log);

            if (!File.Exists(log))
            {
                summary.Add("Лога MelonLoader нет — игра с модом ещё не запускалась ✗");
                report.AppendLine("(файла нет: MelonLoader не запускался)");
                return summary;
            }

            string[] lines;

            try
            {
                lines = File.ReadAllLines(log);
            }
            catch (Exception e)
            {
                report.AppendLine("лог не прочитался: " + e.Message);
                summary.Add("Лог не читается: " + e.Message);
                return summary;
            }

            int from = Math.Max(0, lines.Length - 400);

            for (int i = from; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.IndexOf("AuroraRP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("MelonLoader v", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("BoneLib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("LabFusion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    report.AppendLine(line);
                }
            }

            bool loaded = false;
            bool ready = false;

            for (int i = from; i < lines.Length; i++)
            {
                if (lines[i].IndexOf("AuroraRP", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    loaded = true;
                }

                if (lines[i].IndexOf("Меню создано", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ready = true;
                }
            }

            if (ready)
            {
                summary.Add("Лог: мод загрузился и меню создано ✓");
            }
            else if (loaded)
            {
                summary.Add("Лог: мод загрузился, но меню ещё не создано (выйди в уровень и нажми Y + A / F8)");
            }
            else
            {
                summary.Add("Лог: про мод в логе ничего нет ✗ — пришли отчёт, разберу");
            }

            return summary;
        }

        // ------------------------------------------------------------------ файловая система

        private static string DataDir
        {
            get { return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "AuroraRP-Files"); }
        }

        private static string ListDlls(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return "(папки нет)";
            }

            var names = new List<string>();

            foreach (string file in Directory.GetFiles(dir, "*.dll"))
            {
                names.Add(Path.GetFileName(file));
            }

            return names.Count == 0 ? "(пусто)" : string.Join(", ", names.ToArray());
        }

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(to, dir.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            }

            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dst = Path.Combine(to, rel);

                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(file, dst, true);
            }
        }

        private static string WriteReport(StringBuilder report)
        {
            try
            {
                string dir = EditorPrefs.GetString(PrefKey, string.Empty);
                string path = !string.IsNullOrEmpty(dir) && Directory.Exists(dir)
                    ? Path.Combine(dir, ReportName)
                    : Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ReportName);

                File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
                return path;
            }
            catch (Exception)
            {
                string path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ReportName);
                File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
                return path;
            }
        }

        // ------------------------------------------------------------------ поиск игры

        private static string FindGame(bool askIfMissing)
        {
            string cached = EditorPrefs.GetString(PrefKey, string.Empty);

            if (LooksLikeGame(cached))
            {
                return cached;
            }

            foreach (string candidate in SteamCandidates())
            {
                if (LooksLikeGame(candidate))
                {
                    EditorPrefs.SetString(PrefKey, candidate);
                    return candidate;
                }
            }

            if (askIfMissing)
            {
                string picked = EditorUtility.OpenFolderPanel("Выбери папку BONELAB (где лежит BONELAB.exe)", string.Empty, string.Empty);

                if (LooksLikeGame(picked))
                {
                    EditorPrefs.SetString(PrefKey, picked);
                    return picked;
                }
            }

            return null;
        }

        private static bool LooksLikeGame(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return false;
            }

            return File.Exists(Path.Combine(dir, "BONELAB.exe")) ||
                   File.Exists(Path.Combine(dir, "BoneLab.exe")) ||
                   Directory.Exists(Path.Combine(dir, "BONELAB_Data"));
        }

        /// <summary>Возможные пути BONELAB: реестр Steam, все стим-библиотеки, типовые папки.</summary>
        private static List<string> SteamCandidates()
        {
            var result = new List<string>();
            var libraries = new List<string>();

            try
            {
                object steam = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);

                if (steam == null)
                {
                    steam = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null);
                }

                if (steam is string s && !string.IsNullOrEmpty(s))
                {
                    libraries.Add(s);
                }
            }
            catch (Exception)
            {
            }

            // Стандартные места, если реестр недоступен.
            libraries.Add(@"C:\Program Files (x86)\Steam");
            libraries.Add(@"C:\Program Files\Steam");

            for (int i = 0; i < libraries.Count; i++)
            {
                string library = libraries[i];
                string vdf = Path.Combine(library, "steamapps", "libraryfolders.vdf");

                result.Add(Path.Combine(library, "steamapps", "common", "BONELAB"));

                if (!File.Exists(vdf))
                {
                    continue;
                }

                try
                {
                    foreach (string raw in File.ReadAllLines(vdf))
                    {
                        string line = raw.Trim();
                        int q1 = line.IndexOf('"');

                        if (q1 < 0)
                        {
                            continue;
                        }

                        // Формат: "path"		"D:\\SteamLibrary"  или  "1"  "D:\\SteamLibrary"
                        int q2 = line.IndexOf('"', q1 + 1);
                        int q3 = q2 < 0 ? -1 : line.IndexOf('"', q2 + 1);
                        int q4 = q3 < 0 ? -1 : line.IndexOf('"', q3 + 1);

                        if (q3 < 0 || q4 < 0)
                        {
                            continue;
                        }

                        string value = line.Substring(q3 + 1, q4 - q3 - 1).Replace("\\\\", "\\");

                        if (value.Length > 2 && (value.Contains(":\\") || value.StartsWith("/")))
                        {
                            result.Add(Path.Combine(value, "steamapps", "common", "BONELAB"));
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            // Oculus / Meta и просто диск D.
            result.Add(@"C:\Program Files\Oculus\Software\Software\stress-level-zero-inc-bonelab");
            result.Add(@"D:\SteamLibrary\steamapps\common\BONELAB");
            result.Add(@"E:\SteamLibrary\steamapps\common\BONELAB");

            return result;
        }

        // ------------------------------------------------------------------ сеть и zip

        private static void Download(string url, string path)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
            catch (Exception)
            {
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "AuroraRP-Installer");

                EditorUtility.DisplayProgressBar("AuroraRP", "Скачиваю " + Path.GetFileName(path), 0.5f);

                try
                {
                    client.DownloadFile(url, path);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private static void ExtractZip(string zipPath, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue; // папка
                    }

                    string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string dst = Path.Combine(targetDir, rel);

                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    entry.ExtractToFile(dst, true);
                }
            }
        }
    }
}
#endif
