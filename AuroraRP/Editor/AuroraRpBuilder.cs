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

                // ---- 2. DLL ---------------------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Собираю AuroraRP.dll (dotnet build)...", 0.2f);
                bool dllOk = AuroraRpSetup.BuildModDll(out string buildLog);
                report.AppendLine(dllOk ? "• DLL: собрана" : "• DLL: НЕ собрана");

                if (!dllOk)
                {
                    Debug.LogWarning("[AuroraRP] Сборка DLL не удалась:\n" + buildLog);
                    bool continueAnyway = EditorUtility.DisplayDialog("AuroraRP",
                        "Не удалось собрать DLL мода.\n\n" +
                        "Палет соберём, но роли/деньги/двери не заработают без AuroraRP.dll.\n\n" +
                        "Лог:\n" + Trim(buildLog, 900) + "\n\nПродолжить сборку палета?",
                        "Продолжить", "Отмена");

                    if (!continueAnyway)
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }

                // ---- 3. Контент палета ----------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Создаю контент палета...", 0.45f);
                AuroraRpContent.CreateContent();
                var pallet = AuroraRpContent.GetOrCreatePallet();
                report.AppendLine("• Палет: " + AssetDatabase.GetAssetPath(pallet));

                // ---- 4. Упаковка палета ---------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Упаковываю палет (Addressables)...", 0.6f);
                bool packed = PalletPackerEditor.PackPallet(pallet, out var packResult, false, false);

                string packError = packResult != null ? packResult.Error : null;
                if (!packed || !string.IsNullOrEmpty(packError))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("AuroraRP",
                        "Палет не упаковался.\n\n" + (packError ?? "Неизвестная ошибка упаковки."),
                        "Ок");
                    return;
                }

                string palletFolder = AddressablesManager.EvaluateProfileValueBuildPathForPallet(pallet, AddressablesManager.ProfilePalletID);
                report.AppendLine("• Собранный палет: " + palletFolder);

                // ---- 5. Установка в игру --------------------------------------------------
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

                // ---- 6. Архив для релиза --------------------------------------------------
                EditorUtility.DisplayProgressBar("AuroraRP", "Собираю архив...", 0.9f);
                string zipPath = CreateReleaseZip(palletFolder, dllSource);
                report.AppendLine("• Архив: " + zipPath);

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

            if (!Directory.Exists(palletFolder))
            {
                EditorUtility.DisplayDialog("AuroraRP",
                    "Не нашёл собранный палет: " + palletFolder + "\nСначала нажмите «СОБРАТЬ ВСЁ».", "Ок");
                return;
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

            // Архив содержит: AuroraRP.dll + палет + инструкцию.
            if (File.Exists(dllPath))
            {
                File.Copy(dllPath, Path.Combine(staging, "AuroraRP.dll"), true);
            }

            if (Directory.Exists(palletFolder))
            {
                string target = Path.Combine(staging, "Mods", Path.GetFileName(palletFolder));
                AuroraRpSetup.CopyDirectory(palletFolder, target);
            }

            File.WriteAllText(Path.Combine(staging, "ПРОЧТИ_МЕНЯ.txt"),
                "AuroraRP " + AuroraRpPaths.ModVersion + " — установка:\r\n" +
                "\r\n" +
                "1) AuroraRP.dll положить в  <BONELAB>\\Mods\r\n" +
                "2) папку из Mods\\... (палет) положить в  %USERPROFILE%\\AppData\\LocalLow\\Stress Level Zero\\BONELAB\\MODS\r\n" +
                "3) В игре: двойное нажатие обоих триггеров — меню. B — двери. X — перевод денег.\r\n" +
                "\r\n" +
                "Требуется: MelonLoader 0.6.x и BoneLib.\r\n" +
                "Мультиплеер (LabFusion) поддерживается, если LabFusion установлен.\r\n",
                System.Text.Encoding.UTF8);

            ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, false);
            Directory.Delete(staging, true);

            return zipPath;
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
