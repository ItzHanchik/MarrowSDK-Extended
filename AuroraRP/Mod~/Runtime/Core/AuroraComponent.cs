using UnityEngine;

namespace AuroraRP
{
    /// <summary>Простые интерфейсы для «висящих» на объектах обработчиков (чтобы не создавать по типу на класс).</summary>
    public interface IAuroraTickable
    {
        void Tick(AuroraComponent component, float deltaTime);
    }

    public interface IAuroraLateTickable
    {
        void LateTick(AuroraComponent component);
    }

    public interface IAuroraDisposable
    {
        void Dispose();
    }

    /// <summary>
    /// Единственный MonoBehaviour мода (кроме драйвера), который регистрируется в IL2CPP.
    /// Логика живёт в обычных C# обработчиках — так проще и безопаснее с Il2CppInterop.
    /// </summary>
    public class AuroraComponent : MonoBehaviour
    {
        public string Kind = "generic";
        public object Handler;

        public T GetHandler<T>() where T : class => Handler as T;

        private void Update()
        {
            if (Handler is IAuroraTickable tickable)
            {
                tickable.Tick(this, Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (Handler is IAuroraLateTickable late)
            {
                late.LateTick(this);
            }
        }

        private void OnDestroy()
        {
            if (Handler is IAuroraDisposable disposable)
            {
                disposable.Dispose();
            }

            UiHitRegistry.Unregister(this);
        }

        /// <summary>Создаёт (или находит) компонент на объекте и вешает обработчик.</summary>
        public static AuroraComponent Attach(GameObject go, string kind, object handler)
        {
            if (go == null)
            {
                return null;
            }

            var component = go.GetComponent<AuroraComponent>();
            if (component == null)
            {
                component = go.AddComponent<AuroraComponent>();
            }

            component.Kind = kind;
            component.Handler = handler;
            return component;
        }
    }
}
