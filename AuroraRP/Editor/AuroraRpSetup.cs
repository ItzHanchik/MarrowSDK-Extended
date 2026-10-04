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
            string defines = hasFusion ? "<DefineConstants>$(DefineConstants);AURORA_FUSION</DefineConstants>" : "";

            string csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <!--
    AuroraRP {AuroraRpPaths.ModVersion} — проект сборки мода.
    Файл генерируется автоматически (меню AuroraRP в Unity). Правьте исходники в Mod~/Runtime.
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
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <DebugType>none</DebugType>
    <NoWarn>CS0436;CS0169;CS0649;CS0162;CS0414</NoWarn>
    <AssemblySearchPaths>$(AssemblySearchPaths)</AssemblySearchPaths>
    {defines}
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""Runtime\**\*.cs"" />
  </ItemGroup>

  <ItemGroup>
{references}
  </ItemGroup>

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

            sb.AppendLine("    <!-- Все interop-сборки игры -->");
            sb.AppendLine("    <GameAssemblies Include=\"" + Escape(Path.Combine(il2cpp, "*.dll")) + "\" />");

            if (Directory.Exists(melon1))
            {
                sb.AppendLine("    <GameAssemblies Include=\"" + Escape(Path.Combine(melon1, "*.dll")) + "\" />");
            }

            if (Directory.Exists(melon2))
            {
                sb.AppendLine("    <GameAssemblies Include=\"" + Escape(Path.Combine(melon2, "*.dll")) + "\" />");
            }

            if (!string.IsNullOrEmpty(boneLib) && File.Exists(boneLib))
            {
                sb.AppendLine("    <GameAssemblies Include=\"" + Escape(boneLib) + "\" />");
            }

            if (!string.IsNullOrEmpty(labFusion) && File.Exists(labFusion))
            {
                sb.AppendLine("    <GameAssemblies Include=\"" + Escape(labFusion) + "\" />");
            }

            sb.AppendLine();
            sb.AppendLine("    <Reference Include=\"@(GameAssemblies)\">");
            sb.AppendLine("      <Private>false</Private>");
            sb.AppendLine("    </Reference>");

            return sb.ToString();
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

        /// <summary>Копирует DLL и палет в папку MODS игры.</summary>
        public static bool InstallToMods(string palletFolder, out string message)
        {
            message = string.Empty;
            string mods = AuroraRpPaths.ModsFolder;

            if (string.IsNullOrEmpty(mods))
            {
                message = "Папка MODS не найдена. Скопируйте файлы вручную.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(mods);

                string dll = AuroraRpPaths.DllOutput;
                if (File.Exists(dll))
                {
                    File.Copy(dll, Path.Combine(mods, "AuroraRP.dll"), true);
                }

                if (!string.IsNullOrEmpty(palletFolder) && Directory.Exists(palletFolder))
                {
                    string target = Path.Combine(mods, Path.GetFileName(palletFolder));
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }

                    CopyDirectory(palletFolder, target);
                }

                message = "Установлено в: " + mods;
                return true;
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
