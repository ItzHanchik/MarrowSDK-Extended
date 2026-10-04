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

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            AuroraUtils.CaptureMainThread();
            AuroraRuntime.Driver = this;
            AuroraRuntime.Initialize();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            AuroraRuntime.Tick(dt);

            // Раз в секунду проверяем, что ссылки на риг игрока валидны (после загрузки уровня они новые).
            if (Time.realtimeSinceStartup - _lastLevelCheck > 1f)
            {
                _lastLevelCheck = Time.realtimeSinceStartup;
                GameHooks.ValidatePlayerReferences();
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
