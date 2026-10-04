using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AuroraRP.EditorTools
{
    /// <summary>
    /// Пути и окружение: где лежит мод, где игра, где сборки IL2CPP и папка MODS.
    /// </summary>
    public static class AuroraRpPaths
    {
        public const string ModVersion = "1.0.0";
        public const string PalletTitle = "AuroraRP";
        public const string PalletAuthor = "ItzHanchik";

        public const string BonelabPathKey = "AuroraRP.BonelabPath";

        /// <summary>Абсолютный путь к папке с исходниками мода (Assets/AuroraRP/Mod~).</summary>
        public static string ModRoot
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Assets", "AuroraRP", "Mod~");
            }
        }

        public static string PalletRoot
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Assets", "AuroraRP", "Pallet");
            }
        }

        public static string BuildOutput
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Builds");
            }
        }

        // ------------------------------------------------------------- BONELAB

        public static string BonelabPath
        {
            get
            {
                string saved = EditorPrefs.GetString(BonelabPathKey, string.Empty);
                if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
                {
                    return saved;
                }

                return DetectBonelabPath();
            }
            set => EditorPrefs.SetString(BonelabPathKey, value);
        }

        private static string DetectBonelabPath()
        {
            var candidates = new List<string>();

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                    {
                        continue;
                    }

                    candidates.Add(Path.Combine(drive.Name, "Program Files (x86)", "Steam", "steamapps", "common", "BONELAB"));
                    candidates.Add(Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "BONELAB"));
                    candidates.Add(Path.Combine(drive.Name, "Steam", "steamapps", "common", "BONELAB"));
                    candidates.Add(Path.Combine(drive.Name, "Games", "Steam", "steamapps", "common", "BONELAB"));
                    candidates.Add(Path.Combine(drive.Name, "Oculus", "Software", "Software", "stress-level-zero-inc-bonelab"));
                }
            }
            catch (Exception)
            {
            }

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "BONELAB.exe")) || Directory.Exists(Path.Combine(candidate, "BONELAB_Data")))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        /// <summary>Папка с interop-сборками MelonLoader (Il2CppAssemblies).</summary>
        public static string Il2CppAssembliesDir
        {
            get
            {
                string game = BonelabPath;
                if (string.IsNullOrEmpty(game))
                {
                    return string.Empty;
                }

                string[] variants =
                {
                    Path.Combine(game, "BONELAB_Data", "il2cpp", "MelonLoader", "Il2CppAssemblies"),
                    Path.Combine(game, "MelonLoader", "Il2CppAssemblies"),
                    Path.Combine(game, "BONELAB_Data", "Managed")
                };

                foreach (var path in variants)
                {
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }

                return string.Empty;
            }
        }

        public static string ModsFolder
        {
            get
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(local))
                {
                    return string.Empty;
                }

                string candidate = Path.Combine(local, "..", "LocalLow", "Stress Level Zero", "BONELAB", "MODS");
                candidate = Path.GetFullPath(candidate);
                return Directory.Exists(candidate) ? candidate : candidate;
            }
        }

        public static string LabFusionPath
        {
            get
            {
                string mods = ModsFolder;
                if (string.IsNullOrEmpty(mods))
                {
                    return string.Empty;
                }

                string direct = Path.Combine(mods, "LabFusion.dll");
                if (File.Exists(direct))
                {
                    return direct;
                }

                // LabFusion может лежать в подпапке
                try
                {
                    var found = Directory.GetFiles(mods, "LabFusion*.dll", SearchOption.AllDirectories).FirstOrDefault();
                    return found ?? string.Empty;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }

        public static string BoneLibPath
        {
            get
            {
                string mods = ModsFolder;
                if (string.IsNullOrEmpty(mods))
                {
                    return string.Empty;
                }

                string direct = Path.Combine(mods, "BoneLib.dll");
                if (File.Exists(direct))
                {
                    return direct;
                }

                try
                {
                    return Directory.GetFiles(mods, "BoneLib*.dll", SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }

        public static string DotnetExecutable
        {
            get
            {
                string[] candidates = { "dotnet", "/usr/local/bin/dotnet", "/usr/bin/dotnet" };

                foreach (var candidate in candidates)
                {
                    if (candidate == "dotnet")
                    {
                        return candidate; // путь ищется через PATH
                    }

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return "dotnet";
            }
        }

        public static string CsprojPath => Path.Combine(ModRoot, "AuroraRP.csproj");
        public static string DllOutput => Path.Combine(ModRoot, "bin", "AuroraRP.dll");

        public static bool HasIl2CppAssemblies => !string.IsNullOrEmpty(Il2CppAssembliesDir);
    }
}
