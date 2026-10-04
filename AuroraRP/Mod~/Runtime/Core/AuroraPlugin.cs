using System;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(AuroraRP.AuroraPlugin), AuroraRP.AuroraRuntime.ModName, AuroraRP.AuroraRuntime.Version, AuroraRP.AuroraRuntime.ModAuthor)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]
[assembly: MelonOptionalDependencies("BoneLib.dll", "LabFusion.dll")]

namespace AuroraRP
{
    /// <summary>
    /// Точка входа мода AuroraRP для MelonLoader.
    /// Мод работает поверх BoneLib (обязательно) и LabFusion (опционально — для мультиплеера).
    /// </summary>
    public class AuroraPlugin : MelonMod
    {
        public static AuroraPlugin Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;

            try
            {
                AuroraUtils.CaptureMainThread();
                AuroraFileLog.Initialize();

                // Регистрируем MonoBehaviour'ы в IL2CPP — без этого AddComponent не сработает.
                ClassInjector.RegisterTypeInIl2Cpp<AuroraDriver>();
                ClassInjector.RegisterTypeInIl2Cpp<AuroraComponent>();

                var go = new GameObject("AuroraRP_Driver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AuroraDriver>();

                AuroraLog.Info("AuroraRP {0} загружен. Автор: {1}", AuroraRuntime.Version, AuroraRuntime.ModAuthor);
                AuroraLog.Info("Меню: двойной щелчок обоих триггеров · двери: B · перевод: X");
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnInitializeMelon");
            }
        }

        public override void OnUpdate()
        {
            // Основная работа идёт через AuroraDriver (MonoBehaviour), здесь только страховка.
            if (AuroraConfig.Current.discordBankEnabled && AuroraRuntime.Initialized)
            {
                AuroraFileLog.Flush();
            }
        }

        public override void OnApplicationQuit()
        {
            AuroraRuntime.Shutdown();
        }
    }
}
