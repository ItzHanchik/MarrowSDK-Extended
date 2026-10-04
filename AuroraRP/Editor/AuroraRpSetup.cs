using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AuroraRP.EditorTools
{
    /// <summary>
    /// Настройка и сборка DLL мода. Генерирует csproj и скрипты, компилирует через dotnet,
    /// кладёт готовый AuroraRP.dll в папку MODS.
    /// </summary>
    public static class AuroraRpSetup
    {
        [MenuItem("AuroraRP/0. Проверить окружение", false, 0)]
        public static void ShowEnvironmentWindow()
        {
            AuroraRpEnvironmentWindow.ShowWindow();
        }

        [MenuItem("AuroraRP/1. Настроить сборку мода (csproj + скрипты)", false, 1)]
        public static void GenerateBuildFiles()
        {
            string error = GenerateProjectFiles(out string csproj);

            if (error != null)
            {
                EditorUtility.DisplayDialog("AuroraRP", error, "Ок");
                return;
            }

            Debug.Log("[AuroraRP] Файлы сборки готовы:\n" + csproj);
            EditorUtility.DisplayDialog("AuroraRP",
                "Файлы сборки созданы:\n\n" + csproj + "\n\nТеперь нажмите «Собрать всё» или запустите build_mod.cmd рядом с ним.",
                "Ок");
        }

        /// <summary>Создаёт csproj и вспомогательные скрипты. Возвращает текст ошибки или null.</summary>
        public static string GenerateProjectFiles(out string csprojPath)
        {
            csprojPath = AuroraRpPaths.CsprojPath;

            if (!Directory.Exists(AuroraRpPaths.ModRoot))
            {
                return "Не найден исходный код мода: " + AuroraRpPaths.ModRoot +
                       "\nСкопируйте папку AuroraRP из архива в Assets вашего Unity-проекта.";
            }

            string il2cpp = AuroraRpPaths.Il2CppAssembliesDir;
            if (string.IsNullOrEmpty(il2cpp))
            {
                return "Не найдена папка Il2CppAssemblies.\n\nУкажите путь к BONELAB: меню AuroraRP → «Путь к BONELAB».\n" +
                       "Обычно это <Steam>/steamapps/common/BONELAB";
            }

            string game = AuroraRpPaths.BonelabPath;
            string boneLib = AuroraRpPaths.BoneLibPath;
            string labFusion = AuroraRpPaths.LabFusionPath;

            bool hasFusion = !string.IsNullOrEmpty(labFusion);

            string melonDir1 = Path.Combine(game, "MelonLoader");
            string melonDir2 = Path.Combine(game, "MelonLoader", "net6");

            string references = BuildReferences(il2cpp, melonDir1, melonDir2, boneLib, labFusion);

            string definesBlock = hasFusion
                ? "    <DefineConstants>$(DefineConstants);AURORA_FUSION</DefineConstants>\n    <HasFusion>true</HasFusion>"
                : "    <HasFusion>false</HasFusion>";

            string csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <!--
    AuroraRP {AuroraRpPaths.ModVersion} — проект сборки мода (MelonLoader + BoneLib).
    Файл сгенерирован автоматически из Unity (меню AuroraRP). Исходники — в Mod~/Runtime.

    Сборка вручную:
        dotnet build AuroraRP.csproj -c Release -o bin
    Если LabFusion был установлен на момент генерации, мультиплеер включён (AURORA_FUSION).
  -->

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>AuroraRP</AssemblyName>
    <RootNamespace>AuroraRP</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <DebugType>none</DebugType>
    <NoWarn>CS0436;CS0169;CS0649;CS0162;CS0414;CS0108;CS0114</NoWarn>
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
{definesBlock}
    <BonelabDir>{Escape(game)}</BonelabDir>
    <Il2CppDir>{Escape(il2cpp)}</Il2CppDir>
    <ModsDir>{Escape(Path.Combine(game, "Mods"))}</ModsDir>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""Runtime\**\*.cs"" />
  </ItemGroup>

  <ItemGroup>
{references}
  </ItemGroup>

  <Target Name=""AuroraCheckPaths"" BeforeTargets=""ResolveAssemblyReferences"">
    <Error Condition=""!Exists('$(Il2CppDir)')""
           Text=""Не найдена папка IL2CPP-сборок: $(Il2CppDir). Перегенерируйте проект: Unity → AuroraRP → 0. Проверить окружение."" />
    <Message Importance=""high"" Text=""AuroraRP: LabFusion включён — мод соберётся с мультиплеером."" Condition=""'$(HasFusion)' == 'true'"" />
    <Message Importance=""high"" Text=""AuroraRP: LabFusion не найден — только одиночная игра."" Condition=""'$(HasFusion)' != 'true'"" />
  </Target>

</Project>
";

            try
            {
                Directory.CreateDirectory(AuroraRpPaths.ModRoot);
                File.WriteAllText(csprojPath, csproj, new UTF8Encoding(false));
                WriteBuildScripts(game, hasFusion);
            }
            catch (Exception e)
            {
                return "Не удалось записать проект сборки: " + e.Message;
            }

            return null;
        }

        private static string BuildReferences(string il2cpp, string melon1, string melon2, string boneLib, string labFusion)
        {
            var sb = new StringBuilder();

            AddReference(sb, Path.Combine(il2cpp, "*.dll"), "IL2CPP-сборки игры (из папки BONELAB_Data)");

            if (Directory.Exists(melon1))
            {
                AddReference(sb, Path.Combine(melon1, "*.dll"), "MelonLoader");
            }

            if (Directory.Exists(melon2))
            {
                AddReference(sb, Path.Combine(melon2, "*.dll"), "MelonLoader (net6)");
            }

            if (!string.IsNullOrEmpty(boneLib) && File.Exists(boneLib))
            {
                AddReference(sb, boneLib, "BoneLib (зависимость)");
            }

            if (!string.IsNullOrEmpty(labFusion) && File.Exists(labFusion))
            {
                AddReference(sb, labFusion, "LabFusion (мультиплеер)");
            }

            return sb.ToString();
        }

        private static void AddReference(StringBuilder sb, string path, string comment)
        {
            sb.AppendLine("    <!-- " + comment + " -->");
            sb.AppendLine("    <Reference Include=\"" + Escape(path) + "\">");
            sb.AppendLine("      <Private>false</Private>");
            sb.AppendLine("    </Reference>");
        }

        private static string Escape(string path) => path.Replace("&", "&amp;").Replace("\"", "&quot;");

        private static void WriteBuildScripts(string game, bool hasFusion)
        {
            string modRoot = AuroraRpPaths.ModRoot;

            string cmd = $@"@echo off
chcp 65001 >nul
echo === AuroraRP {AuroraRpPaths.ModVersion}: сборка мода ===
cd /d ""%~dp0""
dotnet build AuroraRP.csproj -c Release -o bin
if errorlevel 1 (
  echo.
  echo [ОШИБКА] Сборка не удалась. Проверьте, что установлен .NET SDK 6+ (https://dotnet.microsoft.com/download)
  pause
  exit /b 1
)
if exist ""..\Updater\AuroraRPUpdater.csproj"" (
  pushd ""..\Updater""
  echo === AuroraRPUpdater: сборка автообновления ===
  dotnet build AuroraRPUpdater.csproj -c Release -o bin
  popd
)
echo.
echo Готово: {Path.Combine(modRoot, "bin", "AuroraRP.dll")}
echo Скопируйте AuroraRP.dll в папку MODS игры (меню AuroraRP в Unity сделает это сам).
pause
";

            string sh = $@"#!/usr/bin/env bash
# AuroraRP {AuroraRpPaths.ModVersion}: сборка мода
set -e
cd ""$(dirname ""$0"")""
dotnet build AuroraRP.csproj -c Release -o bin
if [ -f ../Updater/AuroraRPUpdater.csproj ]; then
  echo ""=== AuroraRPUpdater: сборка автообновления ===""
  (cd ../Updater && dotnet build AuroraRPUpdater.csproj -c Release -o bin)
fi
echo ""Готово: $(pwd)/bin/AuroraRP.dll""
";

            File.WriteAllText(Path.Combine(modRoot, "build_mod.cmd"), cmd, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(modRoot, "build_mod.sh"), sh.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        /// <summary>Запускает dotnet build и возвращает true при успехе.</summary>
        public static bool BuildModDll(out string log)
        {
            log = string.Empty;

            if (GenerateProjectFiles(out string csproj) is string error && error != null)
            {
                log = error;
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = AuroraRpPaths.DotnetExecutable,
                    Arguments = "-c Release -o bin \"AuroraRP.csproj\"",
                    WorkingDirectory = AuroraRpPaths.ModRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    log = "Не удалось запустить dotnet. Установите .NET SDK 6+.";
                    return false;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                log = stdout + "\n" + stderr;
                return process.ExitCode == 0 && File.Exists(AuroraRpPaths.DllOutput);
            }
            catch (Exception e)
            {
                log = "Ошибка сборки: " + e.Message + "\n\nУбедитесь, что установлен .NET SDK 6 или новее.";
                return false;
            }
        }

        /// <summary>Собирает AuroraRPUpdater.dll (MelonLoader-плагин автообновления).</summary>
        public static bool BuildUpdaterDll(out string log)
        {
            log = string.Empty;

            if (!Directory.Exists(AuroraRpPaths.UpdaterRoot) || !File.Exists(AuroraRpPaths.UpdaterCsproj))
            {
                log = "Не найден проект апдейтера: " + AuroraRpPaths.UpdaterCsproj;
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = AuroraRpPaths.DotnetExecutable,
                    Arguments = "-c Release -o bin \"AuroraRPUpdater.csproj\" -p:BonelabDir=\"" + AuroraRpPaths.BonelabPath + "\"",
                    WorkingDirectory = AuroraRpPaths.UpdaterRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    log = "Не удалось запустить dotnet. Установите .NET SDK 6+.";
                    return false;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                log = stdout + "\n" + stderr;
                return process.ExitCode == 0 && File.Exists(AuroraRpPaths.UpdaterDllOutput);
            }
            catch (Exception e)
            {
                log = "Ошибка сборки апдейтера: " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Устанавливает мод в игру: палет — в MODS (контент), DLL — в &lt;BONELAB&gt;\Mods (код),
        /// апдейтер — в &lt;BONELAB&gt;\Plugins.
        /// </summary>
        public static bool InstallToMods(string palletFolder, out string message)
        {
            message = string.Empty;
            string palletMods = AuroraRpPaths.ModsFolder;   // LocalLow\...\BONELAB\MODS — контент
            string gameMods = AuroraRpPaths.GameModsFolder; // <BONELAB>\Mods — code-моды

            if (string.IsNullOrEmpty(palletMods) && string.IsNullOrEmpty(gameMods))
            {
                message = "Папки игры не найдены. Скопируйте файлы вручную.";
                return false;
            }

            try
            {
                var report = new System.Text.StringBuilder();

                // 1) DLL мода — только в папку code-модов игры.
                string dll = AuroraRpPaths.DllOutput;
                if (File.Exists(dll) && !string.IsNullOrEmpty(gameMods))
                {
                    Directory.CreateDirectory(gameMods);
                    File.Copy(dll, Path.Combine(gameMods, "AuroraRP.dll"), true);
                    report.Append("DLL → " + gameMods + "; ");
                }

                // 2) Апдейтер — в Plugins (грузятся раньше модов).
                string updater = AuroraRpPaths.UpdaterDllOutput;
                string plugins = AuroraRpPaths.PluginsFolder;
                if (File.Exists(updater) && !string.IsNullOrEmpty(plugins))
                {
                    Directory.CreateDirectory(plugins);
                    File.Copy(updater, Path.Combine(plugins, "AuroraRPUpdater.dll"), true);
                    report.Append("апдейтер → " + plugins + "; ");
                }

                // 3) Палет — в MODS (контент).
                if (!string.IsNullOrEmpty(palletFolder) && Directory.Exists(palletFolder) && !string.IsNullOrEmpty(palletMods))
                {
                    Directory.CreateDirectory(palletMods);
                    string target = Path.Combine(palletMods, Path.GetFileName(palletFolder));
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }

                    CopyDirectory(palletFolder, target);
                    report.Append("палет → " + palletMods + "; ");
                }

                message = report.Length > 0 ? ("Установлено: " + report.ToString().TrimEnd(' ', ';')) : "Нечего устанавливать.";
                return report.Length > 0;
            }
            catch (Exception e)
            {
                message = "Ошибка установки: " + e.Message;
                return false;
            }
        }

        public static void CopyDirectory(string source, string target)
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
    }

    /// <summary>Окно проверки окружения.</summary>
    public class AuroraRpEnvironmentWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<AuroraRpEnvironmentWindow>("AuroraRP: окружение");
            window.minSize = new Vector2(560f, 420f);
        }

        private void OnGUI()
        {
            GUILayout.Label("AuroraRP " + AuroraRpPaths.ModVersion, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawRow("Папка мода", AuroraRpPaths.ModRoot, Directory.Exists(AuroraRpPaths.ModRoot));
            DrawRow("BONELAB", AuroraRpPaths.BonelabPath, !string.IsNullOrEmpty(AuroraRpPaths.BonelabPath));
            DrawRow("Il2CppAssemblies", AuroraRpPaths.Il2CppAssembliesDir, AuroraRpPaths.HasIl2CppAssemblies);
            DrawRow("BoneLib.dll", AuroraRpPaths.BoneLibPath, !string.IsNullOrEmpty(AuroraRpPaths.BoneLibPath));

            bool fusion = !string.IsNullOrEmpty(AuroraRpPaths.LabFusionPath);
            DrawRow("LabFusion.dll", fusion ? AuroraRpPaths.LabFusionPath : "не найден (играем в одиночку)", fusion);

            DrawRow("Папка MODS", AuroraRpPaths.ModsFolder, Directory.Exists(AuroraRpPaths.ModsFolder));
            DrawRow("dotnet SDK", AuroraRpPaths.DotnetExecutable, true);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Порядок сборки:\n" +
                "1) Убедитесь, что BoneLib установлен (иначе мод не загрузится).\n" +
                "2) Нажмите «Создать контент палета», затем «СОБРАТЬ ВСЁ».\n" +
                "3) Мод скопируется в MODS вместе с палетом.",
                MessageType.Info);

            if (GUILayout.Button("Указать папку BONELAB..."))
            {
                string selected = EditorUtility.OpenFolderPanel("Папка с BONELAB.exe", AuroraRpPaths.BonelabPath, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    AuroraRpPaths.BonelabPath = selected;
                }
            }

            if (GUILayout.Button("Сгенерировать файлы сборки"))
            {
                AuroraRpSetup.GenerateBuildFiles();
            }
        }

        private static void DrawRow(string label, string value, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(140f));
            var previous = GUI.color;
            GUI.color = ok ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.8f, 0.6f);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "—" : value, EditorStyles.wordWrappedLabel);
            GUI.color = previous;
            EditorGUILayout.EndHorizontal();
        }
    }
}
