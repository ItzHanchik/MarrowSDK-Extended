using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;

[assembly: AssemblyTitle("AuroraRP Updater")]
[assembly: AssemblyProduct("AuroraRP Updater")]
[assembly: AssemblyVersion(AuroraRPUpdater.AuroraRpUpdaterPlugin.PluginVersion)]
[assembly: AssemblyFileVersion(AuroraRPUpdater.AuroraRpUpdaterPlugin.PluginVersion)]
[assembly: MelonInfo(typeof(AuroraRPUpdater.AuroraRpUpdaterPlugin), "AuroraRP Updater",
    AuroraRPUpdater.AuroraRpUpdaterPlugin.PluginVersion, "ItzHanchik", null)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace AuroraRPUpdater
{
    /// <summary>
    /// MelonLoader-ПЛАГИН (папка Plugins) — загружается раньше модов, поэтому
    /// успевает положить свежий AuroraRP.dll в папку Mods до его загрузки.
    /// Игроку достаточно один раз поставить этот файл: дальше код мода, палет
    /// с контентом и моды-зависимости (BoneLib, LabFusion) ставятся и обновляются сами.
    /// </summary>
    public class AuroraRpUpdaterPlugin : MelonPlugin
    {
        public const string PluginVersion = "1.0.0";

        public const string ModName = "AuroraRP";
        public const string ModFileName = "AuroraRP.dll";

        /// <summary>
        /// Манифест обновлений: лежит в свежем релизе, поэтому не зависит от ветки.
        /// Правится в UserData/AuroraRPUpdater.cfg.
        /// </summary>
        public const string DefaultManifestUrl =
            "https://github.com/ItzHanchik/MarrowSDK-Extended/releases/download/aurorarp-v1.0.0/update.json";

        /// <summary>Резервный адрес манифеста, если релизный недоступен.</summary>
        public const string FallbackManifestUrl =
            "https://raw.githubusercontent.com/ItzHanchik/MarrowSDK-Extended/main/AuroraRP/update.json";

        public static AuroraRpUpdaterPlugin Instance { get; private set; }
        public static MelonLogger.Instance Log { get; private set; }

        /// <summary>Полный путь к AuroraRP.dll (папка Mods игры).</summary>
        public static string ModAssemblyPath => Path.Combine(MelonEnvironment.ModsDirectory, ModFileName);

        private MelonPreferences_Category _prefs;
        private MelonPreferences_Entry<bool> _autoUpdate;
        private MelonPreferences_Entry<string> _manifestUrl;
        private MelonPreferences_Entry<bool> _installPallet;
        private MelonPreferences_Entry<bool> _installDependencies;

        public override void OnPreInitialization()
        {
            Instance = this;
            Log = LoggerInstance;

            _prefs = MelonPreferences.CreateCategory("AuroraRPUpdater");
            _autoUpdate = _prefs.CreateEntry("AutoUpdate", true);
            _manifestUrl = _prefs.CreateEntry("ManifestUrl", DefaultManifestUrl);
            // Палет больше не нужен: вся красота внутри AuroraRP.dll.
            _installPallet = _prefs.CreateEntry("InstallPallet", false);
            _installDependencies = _prefs.CreateEntry("InstallDependencies", true);
            _prefs.SaveToFile(false);

            Log.Msg("AuroraRP Updater v{0}. Папка Mods: {1}", PluginVersion, MelonEnvironment.ModsDirectory);

            if (!_autoUpdate.Value)
            {
                Log.Msg("Автообновление выключено (AutoUpdate = false). Пропускаю.");
                return;
            }

            try
            {
                var runner = new UpdateRunner(Log);
                runner.Run(_manifestUrl.Value, _installPallet.Value, _installDependencies.Value, FallbackManifestUrl);
            }
            catch (Exception e)
            {
                // Плагин не должен ломать запуск игры ни при каких условиях.
                Log.Error("Автообновление не удалось: " + e.Message);
                Log.Msg("Скачать мод вручную: https://github.com/ItzHanchik/MarrowSDK-Extended/releases");
            }
        }
    }
}
