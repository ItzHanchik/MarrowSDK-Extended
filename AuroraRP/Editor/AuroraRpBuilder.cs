using System;
using System.IO;
using System.IO.Compression;
using SLZ.Marrow.Warehouse;
using SLZ.MarrowEditor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AuroraRP.EditorTools
{
    /// <summary>
    /// Сборка «в один клик»: DLL мода, палет с контентом, установка в BONELAB и zip-архив для релиза.
    /// </summary>
    public static class AuroraRpBuilder
    {
        public const string ZipName = "AuroraRP_v" + AuroraRpPaths.ModVersion;

        [MenuItem("AuroraRP/3. СОБРАТЬ ВСЁ (мод + палет + установка)", false, 3)]
        public static void BuildAll()
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("AuroraRP", "Unity ещё компилирует скрипты. Подождите пару секунд.", "Ок");
                return;
            }

            var report = new System.Text.StringBuilder();

            try
            {
                // ---- 1. Файлы сборки DLL -------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Готовлю файлы сборки мода...", 0.05f);
                string error = AuroraRpSetup.GenerateProjectFiles(out _);
                if (error != null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("AuroraRP", error, "Ок");
                    return;
                }

                // ---- 2. Контент палета ----------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Создаю контент палета...", 0.1f);
                AuroraRpContent.CreateContent();
                var pallet = AuroraRpContent.GetOrCreatePallet();
                report.AppendLine("• Палет: " + AssetDatabase.GetAssetPath(pallet));

                // ---- 3. Упаковка палета (Addressables) ------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Упаковываю контент (Addressables)...", 0.25f);
                bool packed = PalletPackerEditor.PackPallet(pallet, out var packResult, false, false);

                string packError = packResult != null ? packResult.Error : null;
                if (!packed || !string.IsNullOrEmpty(packError))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("AuroraRP",
                        "Контент не упаковался.\n\n" + (packError ?? "Неизвестная ошибка упаковки."),
                        "Ок");
                    return;
                }

                string palletFolder = AddressablesManager.EvaluateProfileValueBuildPathForPallet(pallet, AddressablesManager.ProfilePalletID);
                report.AppendLine("• Собранный контент: " + palletFolder);

                // Кладём конфиг внутрь контента: настройки едут вместе с палетом (необязательный путь).
                if (CopyPalletConfig(palletFolder, out string configMessage))
                {
                    report.AppendLine("• " + configMessage);
                }
                else
                {
                    report.AppendLine("• ВНИМАНИЕ: " + configMessage);
                }

                // ---- 4. Пак «вся красота» внутрь DLL --------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Вшиваю красоту в DLL (пак)...", 0.45f);
                string packPath = AuroraRpPack.CreatePack(palletFolder, out string packMessage);

                if (packPath != null)
                {
                    long packSize = 0;

                    try
                    {
                        packSize = new FileInfo(packPath).Length;
                    }
                    catch
                    {
                        // размер не критичен
                    }

                    report.AppendLine("• Пак в DLL: " + (packSize / 1024) + " КБ (" + packPath + ")");
                }
                else
                {
                    report.AppendLine("• Пак в DLL: НЕ собран — " + packMessage);
                    Debug.LogWarning("[AuroraRP] " + packMessage);
                }

                // ---- 5. DLL (с красотой внутри) -------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Собираю AuroraRP.dll (dotnet build)...", 0.6f);
                bool dllOk = AuroraRpSetup.BuildModDll(out string buildLog);
                report.AppendLine(dllOk ? "• DLL: собрана (красота внутри)" : "• DLL: НЕ собрана");

                if (!dllOk)
                {
                    Debug.LogWarning("[AuroraRP] Сборка DLL не удалась:\n" + buildLog);
                    bool continueAnyway = EditorUtility.DisplayDialog("AuroraRP",
                        "Не удалось собрать DLL мода.\n\n" +
                        "Без AuroraRP.dll роли/деньги/двери не заработают.\n\n" +
                        "Лог:\n" + Trim(buildLog, 900) + "\n\nПродолжить сборку архивов?",
                        "Продолжить", "Отмена");

                    if (!continueAnyway)
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }

                // ---- 6. Апдейтер (плагин автообновления) ---------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Собираю AuroraRPUpdater.dll (автообновление)...", 0.7f);
                bool updaterOk = AuroraRpSetup.BuildUpdaterDll(out string updaterLog);
                report.AppendLine(updaterOk ? "• Апдейтер: собран" : "• Апдейтер: НЕ собран");

                if (!updaterOk)
                {
                    Debug.LogWarning("[AuroraRP] Сборка апдейтера не удалась:\n" + updaterLog);
                }

                // ---- 7. Установка в игру --------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Устанавливаю в BONELAB...", 0.8f);
                AuroraRpSetup.InstallToMods(palletFolder, out string installMessage);
                report.AppendLine("• " + installMessage);

                string dllSource = AuroraRpPaths.DllOutput;
                if (File.Exists(dllSource))
                {
                    string gameMods = Path.Combine(AuroraRpPaths.BonelabPath, "Mods");
                    if (Directory.Exists(gameMods) || Directory.Exists(AuroraRpPaths.BonelabPath))
                    {
                        Directory.CreateDirectory(gameMods);
                        File.Copy(dllSource, Path.Combine(gameMods, "AuroraRP.dll"), true);
                        report.AppendLine("• DLL → " + gameMods);
                    }
                }

                // ---- 8. Архивы для релиза -------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Собираю архивы...", 0.9f);
                string zipPath = CreateReleaseZip(palletFolder, dllSource);
                report.AppendLine("• Архив для игроков: " + zipPath);

                string palletZip = CreatePalletZip(palletFolder);
                if (palletZip != null)
                {
                    report.AppendLine("• Палет для mod.io/автообновления: " + palletZip);
                }

                EditorUtility.ClearProgressBar();

                Debug.Log("[AuroraRP] Сборка завершена:\n" + report);
                EditorUtility.DisplayDialog("AuroraRP", "Готово!\n\n" + report, "Круто");

                if (File.Exists(zipPath))
                {
                    EditorUtility.RevealInFinder(zipPath);
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[AuroraRP] Ошибка сборки: " + e);
                EditorUtility.DisplayDialog("AuroraRP", "Ошибка сборки:\n" + e.Message + "\n\nПодробности в Console.", "Ок");
            }
        }

        [MenuItem("AuroraRP/4. Открыть папку сборки", false, 20)]
        public static void OpenBuildFolder()
        {
            string output = AuroraRpPaths.BuildOutput;
            Directory.CreateDirectory(output);
            EditorUtility.RevealInFinder(output);
        }

        [MenuItem("AuroraRP/5. Установить последнюю сборку в BONELAB", false, 21)]
        public static void InstallLastBuild()
        {
            string palletFolder = Path.Combine(AuroraRpPaths.PalletRoot, "ServerData", "StandaloneWindows64");

            // Палет не обязателен: красота уже внутри DLL, палет нужен только для игроков без мода.
            if (!Directory.Exists(palletFolder))
            {
                palletFolder = null;
                Debug.Log("[AuroraRP] Собранного палета нет — ставлю только DLL и апдейтер (этого достаточно).");
            }

            AuroraRpSetup.InstallToMods(palletFolder, out string message);
            EditorUtility.DisplayDialog("AuroraRP", message, "Ок");
        }

        // ------------------------------------------------------------------ архив

        private static string CreateReleaseZip(string palletFolder, string dllPath)
        {
            string output = AuroraRpPaths.BuildOutput;
            Directory.CreateDirectory(output);

            string zipPath = Path.Combine(output, ZipName + ".zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            string staging = Path.Combine(output, "staging_" + ZipName);
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }

            Directory.CreateDirectory(staging);

            // Архив содержит: AuroraRP.dll + палет + апдейтер + инструкцию.
            if (File.Exists(dllPath))
            {
                File.Copy(dllPath, Path.Combine(staging, "AuroraRP.dll"), true);
            }

            string updaterDll = AuroraRpPaths.UpdaterDllOutput;
            if (File.Exists(updaterDll))
            {
                string plugins = Path.Combine(staging, "Plugins");
                Directory.CreateDirectory(plugins);
                File.Copy(updaterDll, Path.Combine(plugins, "AuroraRPUpdater.dll"), true);
            }

            if (Directory.Exists(palletFolder))
            {
                string target = Path.Combine(staging, "Mods", Path.GetFileName(palletFolder));
                AuroraRpSetup.CopyDirectory(palletFolder, target);
            }

            File.WriteAllText(Path.Combine(staging, "ПРОЧТИ_МЕНЯ.txt"),
                "AuroraRP " + AuroraRpPaths.ModVersion + " — установка:\r\n" +
                "\r\n" +
                "ВСЁ В ОДНОМ ПЛАГИНЕ: красота (меню, иконки, анимации, эффекты, звуки) уже внутри AuroraRP.dll.\r\n" +
                "\r\n" +
                "1) AuroraRP.dll                -> в  <BONELAB>\\Mods\r\n" +
                "2) Plugins\\AuroraRPUpdater.dll -> в  <BONELAB>\\Plugins   (один раз!)\r\n" +
                "   Дальше сам следит за обновлениями AuroraRP.dll.\r\n" +
                "3) Палет (папка из Mods\\...) копировать НЕ обязательно — он нужен только тем,\r\n" +
                "   у кого нет мода, чтобы они видели терминал/принтер/двери.\r\n" +
                "\r\n" +
                "ИГРА: двойное нажатие обоих триггеров — меню. B — двери. X — перевод денег.\r\n" +
                "\r\n" +
                "Требуется: MelonLoader 0.6.x и BoneLib.\r\n" +
                "Мультиплеер (LabFusion) поддерживается, если LabFusion установлен.\r\n",
                System.Text.Encoding.UTF8);

            ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, false);
            Directory.Delete(staging, true);

            return zipPath;
        }

        /// <summary>
        /// Копирует Assets/AuroraRP/Pallet/config.json в собранный палет.
        /// Этот файл — «мозг» системы: роли, цены, лимиты, стартовые наборы, магазин.
        /// </summary>
        public static bool CopyPalletConfig(string palletFolder, out string message)
        {
            string source = Path.Combine(AuroraRpPaths.PalletRoot, "config.json");

            if (!File.Exists(source))
            {
                message = "нет файла Assets/AuroraRP/Pallet/config.json — палет поедет без конфига.";
                return false;
            }

            if (string.IsNullOrEmpty(palletFolder) || !Directory.Exists(palletFolder))
            {
                message = "не нашёл папку собранного палета: " + palletFolder;
                return false;
            }

            try
            {
                File.Copy(source, Path.Combine(palletFolder, "config.json"), true);
                message = "config.json положен в палет (настройки поедут к игрокам).";
                return true;
            }
            catch (Exception e)
            {
                message = "не удалось положить config.json в палет: " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Палет-архив для загрузки на mod.io и для автообновления (pallet.json — в корне архива).
        /// </summary>
        public static string CreatePalletZip(string palletFolder)
        {
            if (string.IsNullOrEmpty(palletFolder) || !Directory.Exists(palletFolder))
            {
                return null;
            }

            string output = AuroraRpPaths.BuildOutput;
            Directory.CreateDirectory(output);

            string name = AuroraRpPaths.PalletTitle + "-pallet-" + AuroraRpPaths.ModVersion;
            string zipPath = Path.Combine(output, name + ".zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            try
            {
                ZipFile.CreateFromDirectory(palletFolder, zipPath, CompressionLevel.Optimal, false);
                return zipPath;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AuroraRP] Не удалось собрать палет-архив: " + e.Message);
                return null;
            }
        }

        [MenuItem("AuroraRP/6. Собрать палет-архив (для mod.io и автообновления)", false, 22)]
        public static void BuildPalletZipOnly()
        {
            string palletFolder = Path.Combine(AuroraRpPaths.PalletRoot, "ServerData", "StandaloneWindows64");

            if (!Directory.Exists(palletFolder))
            {
                EditorUtility.DisplayDialog("AuroraRP",
                    "Не нашёл собранный палет: " + palletFolder + "\nСначала нажмите «СОБРАТЬ ВСЁ».", "Ок");
                return;
            }

            CopyPalletConfig(palletFolder, out _);

            string zip = CreatePalletZip(palletFolder);
            EditorUtility.DisplayDialog("AuroraRP",
                zip != null ? ("Готово:\n" + zip + "\n\nЗагрузите его на mod.io и приложите к GitHub-релизу.") : "Не удалось собрать архив.",
                "Ок");
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "(пусто)";
            }

            return text.Length <= max ? text : text.Substring(text.Length - max);
        }
    }
}
