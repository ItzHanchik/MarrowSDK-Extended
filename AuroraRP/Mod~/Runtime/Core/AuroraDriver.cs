using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Главный MonoBehaviour мода (регистрируется в IL2CPP через ClassInjector).
    /// Тикает все сервисы и живёт между уровнями.
    /// </summary>
    public class AuroraDriver : MonoBehaviour
    {
        public static AuroraDriver Instance { get; private set; }

        private float _lastLevelCheck;
        private bool _updateErrorLogged;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            AuroraUtils.CaptureMainThread();
            AuroraRuntime.Driver = this;

            try
            {
                AuroraRuntime.Initialize();
            }
            catch (System.Exception e)
            {
                AuroraLog.Exception(e, "driver awake");
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            try
            {
                AuroraRuntime.Tick(dt);

                // Раз в секунду проверяем, что ссылки на риг игрока валидны (после загрузки уровня они новые).
                if (Time.realtimeSinceStartup - _lastLevelCheck > 1f)
                {
                    _lastLevelCheck = Time.realtimeSinceStartup;
                    GameHooks.ValidatePlayerReferences();
                }
            }
            catch (System.Exception e)
            {
                if (!_updateErrorLogged)
                {
                    _updateErrorLogged = true;
                    AuroraLog.Exception(e, "driver update");
                }
            }
        }

        private void OnApplicationQuit()
        {
            AuroraRuntime.Shutdown();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
