using System;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Ввод с контроллеров: двойной щелчок обоих триггеров (открытие меню),
    /// кнопка B (двери), кнопка X/A (панель перевода). Использует API MarrowSDK (BaseController).
    /// </summary>
    public class AuroraInput
    {
        /// <summary>Двойной щелчок обоих триггеров — открыть/закрыть меню.</summary>
        public event Action OnMenuGesture;

        /// <summary>Кнопка B нажата (любая рука) — двери.</summary>
        public event Action<Handedness> OnBPressed;

        /// <summary>Кнопка X (A на левом контроллере) — панель сумм перевода.</summary>
        public event Action OnXPressed;

        public bool Enabled = true;

        // --- двойной щелчок
        private float _leftFirstClick = -10f;
        private float _rightFirstClick = -10f;
        private int _leftClicks;
        private int _rightClicks;
        private float _lastGestureTime = -10f;

        public void Tick(float dt)
        {
            if (!Enabled || !GameHooks.PlayerReady)
            {
                return;
            }

            var left = BoneLib.Player.LeftController;
            var right = BoneLib.Player.RightController;

            if (left == null || right == null)
            {
                return;
            }

            HandleMenuGesture(left, right);
            HandleButtonB(left, right);
            HandleButtonX(left);
        }

        private void HandleMenuGesture(BaseController left, BaseController right)
        {
            float window = AuroraConfig.Current.doubleClickWindow;
            float now = Time.realtimeSinceStartup;

            bool leftDown = IsTriggerDown(BoneLib.Player.LeftHand);
            bool rightDown = IsTriggerDown(BoneLib.Player.RightHand);

            if (leftDown)
            {
                if (now - _leftFirstClick <= window)
                {
                    _leftClicks++;
                }
                else
                {
                    _leftClicks = 1;
                    _leftFirstClick = now;
                }
            }

            if (rightDown)
            {
                if (now - _rightFirstClick <= window)
                {
                    _rightClicks++;
                }
                else
                {
                    _rightClicks = 1;
                    _rightFirstClick = now;
                }
            }

            // сбрасываем счётчики, если окно прошло
            if (now - _leftFirstClick > window)
            {
                _leftClicks = 0;
            }

            if (now - _rightFirstClick > window)
            {
                _rightClicks = 0;
            }

            if (_leftClicks >= 2 && _rightClicks >= 2)
            {
                _leftClicks = 0;
                _rightClicks = 0;

                if (now - _lastGestureTime > 0.4f)
                {
                    _lastGestureTime = now;
                    OnMenuGesture?.Invoke();
                }
            }

            // Альтернативные жесты из конфига
            string gesture = AuroraConfig.Current.menuOpenGesture;
            if (gesture == "thumbstick" && (left.GetThumbStickDown() || right.GetThumbStickDown()))
            {
                OnMenuGesture?.Invoke();
            }
            else if (gesture == "menu_tap" && left.GetMenuButtonDown())
            {
                OnMenuGesture?.Invoke();
            }
        }

        private static bool IsTriggerDown(Hand hand)
        {
            // «Триггер» = индексный курок (как в ванильном BONELAB: GetIndexButtonDown).
            return hand != null && hand.GetIndexButtonDown();
        }

        private void HandleButtonB(BaseController left, BaseController right)
        {
            if (left.GetBButtonDown())
            {
                OnBPressed?.Invoke(Handedness.LEFT);
            }

            if (right.GetBButtonDown())
            {
                OnBPressed?.Invoke(Handedness.RIGHT);
            }
        }

        private void HandleButtonX(BaseController left)
        {
            // На левом контроллере кнопка X/ю — это A/B в терминах Marrow.
            if (left.GetAButtonDown() || left.GetMenuButtonDown())
            {
                OnXPressed?.Invoke();
            }
        }

        // ------------------------------------------------------------------ вибрация

        public void HapticBoth(float amplitude, float duration)
        {
            if (!AuroraConfig.Current.haptics)
            {
                return;
            }

            try
            {
                BoneLib.Player.LeftController?.HapticAction(0f, duration, 0.5f, Mathf.Clamp01(amplitude));
                BoneLib.Player.RightController?.HapticAction(0f, duration, 0.5f, Mathf.Clamp01(amplitude));
            }
            catch (Exception)
            {
                // HapticAction есть не на всех контроллерах — игнорируем.
            }
        }

        public void Haptic(Handedness hand, float amplitude, float duration)
        {
            if (!AuroraConfig.Current.haptics)
            {
                return;
            }

            try
            {
                var controller = hand == Handedness.LEFT ? BoneLib.Player.LeftController : BoneLib.Player.RightController;
                controller?.HapticAction(0f, duration, 0.5f, Mathf.Clamp01(amplitude));
            }
            catch (Exception)
            {
            }
        }
    }
}
