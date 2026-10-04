using System;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Ввод с контроллеров: Y+A (открытие меню), кнопка B (двери),
    /// кнопка X/A (панель перевода). Читает и MarrowSDK (BaseController), и голый Unity XR —
    /// поэтому комбинация Y+A работает на любых контроллерах.
    /// </summary>
    public class AuroraInput
    {
        /// <summary>Y+A (или другой жест из конфига) — открыть/закрыть меню.</summary>
        public event Action OnMenuGesture;

        /// <summary>Кнопка B нажата (любая рука) — двери.</summary>
        public event Action<Handedness> OnBPressed;

        /// <summary>Кнопка X (A на левом контроллере) — панель сумм перевода.</summary>
        public event Action OnXPressed;

        public bool Enabled = true;

        // --- Y + A: ждём нажатие двумя руками почти одновременно
        private const float ComboWindow = 0.35f;
        private float _comboLeftDown = -10f;
        private float _comboRightDown = -10f;
        private float _lastComboFire = -10f;

        // --- двойной щелчок (запасной жест)
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
            HandleKeyboard();
            HandleButtonB(left, right);
            HandleButtonX(left);
            TickDiagnostics(left, right);
        }

        private void HandleMenuGesture(BaseController left, BaseController right)
        {
            string gesture = AuroraConfig.Current.menuOpenGesture ?? "";

            // 1) Основной жест: Y на левой руке + A на правой (или наоборот по железу).
            if (gesture == "y_and_a" || gesture == "both")
            {
                if (ComboYPlusA())
                {
                    AuroraLog.Info("Жест меню: Y + A");
                    FireMenuGesture();
                    return;
                }
            }

            // 2) Кнопки-одиночки из конфига.
            if (gesture == "thumbstick" && (left.GetThumbStickDown() || right.GetThumbStickDown()))
            {
                FireMenuGesture();
                return;
            }

            if (gesture == "menu_tap" && left.GetMenuButtonDown())
            {
                FireMenuGesture();
                return;
            }

            // 3) Запасной жест: двойной щелчок обоих курков.
            if (gesture != "both_triggers_double" && gesture != "both")
            {
                return;
            }

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
                FireMenuGesture();
            }
        }

        /// <summary>
        /// Клавиатура — страховка: F8 открывает/закрывает меню всегда, даже если
        /// жест на контроллерах не срабатывает. В VR за ПК это самый надёжный путь.
        /// </summary>
        private void HandleKeyboard()
        {
            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
                {
                    AuroraLog.Info("Меню: нажата F8 (клавиатура).");
                    FireMenuGesture();
                }
            }
            catch (Exception)
            {
            }
        }

        // --- диагностика: пишем в лог, когда меняется состояние любой из кнопок жеста
        private bool _dbgY;
        private bool _dbgA;
        private bool _dbgBLeft;
        private bool _dbgARight;
        private bool _dbgTriggerLeft;
        private bool _dbgTriggerRight;

        private void TickDiagnostics(BaseController left, BaseController right)
        {
            if (!AuroraConfig.Current.debugInput)
            {
                return;
            }

            bool y = LeftYDown();
            bool a = RightADown();
            bool bLeft = false;
            bool aRight = false;

            try
            {
                bLeft = left != null && left.GetBButtonDown();
                aRight = right != null && right.GetAButtonDown();
            }
            catch (Exception)
            {
            }

            bool triggerLeft = IsTriggerDown(BoneLib.Player.LeftHand);
            bool triggerRight = IsTriggerDown(BoneLib.Player.RightHand);

            if (y == _dbgY && a == _dbgA && bLeft == _dbgBLeft && aRight == _dbgARight &&
                triggerLeft == _dbgTriggerLeft && triggerRight == _dbgTriggerRight)
            {
                return;
            }

            _dbgY = y;
            _dbgA = a;
            _dbgBLeft = bLeft;
            _dbgARight = aRight;
            _dbgTriggerLeft = triggerLeft;
            _dbgTriggerRight = triggerRight;

            AuroraLog.Info("Ввод: Y-лев(raw)={0} A-прав(raw)={1} B-лев(Marrow)={2} A-прав(Marrow)={3} курок-лев={4} курок-прав={5}",
                y, a, bLeft, aRight, triggerLeft, triggerRight);
        }

        private void FireMenuGesture()
        {
            float now = Time.realtimeSinceStartup;

            // Защита от дребезга: одно открытие на полсекунды.
            if (now - _lastGestureTime < 0.4f)
            {
                return;
            }

            _lastGestureTime = now;
            OnMenuGesture?.Invoke();
        }

        /// <summary>
        /// Y (левая рука) + A (правая рука). Нажатия засчитываются, если разошлись не больше
        /// чем на 0.35 с — двумя руками идеально одновременно не нажать.
        /// </summary>
        private bool ComboYPlusA()
        {
            float now = Time.realtimeSinceStartup;

            if (LeftYDown())
            {
                _comboLeftDown = now;
            }

            if (RightADown())
            {
                _comboRightDown = now;
            }

            if (now - _comboLeftDown > ComboWindow || now - _comboRightDown > ComboWindow)
            {
                return false;
            }

            if (now - _lastComboFire < 0.5f)
            {
                return false;
            }

            // Гасим пару, чтобы одно нажатие не сработало дважды.
            _comboLeftDown = -10f;
            _comboRightDown = -10f;
            _lastComboFire = now;
            return true;
        }

        /// <summary>Y на левом контроллере: сначала сырой Unity XR, потом MarrowSDK.</summary>
        private static bool LeftYDown()
        {
#if AURORA_XR
            if (RawButton(UnityEngine.XR.XRNode.LeftHand, false))
            {
                return true;
            }
#endif
            try
            {
                var left = BoneLib.Player.LeftController;
                return left != null && left.GetBButtonDown();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A на правом контроллере: сначала сырой Unity XR, потом MarrowSDK.</summary>
        private static bool RightADown()
        {
#if AURORA_XR
            if (RawButton(UnityEngine.XR.XRNode.RightHand, true))
            {
                return true;
            }
#endif
            try
            {
                var right = BoneLib.Player.RightController;
                return right != null && right.GetAButtonDown();
            }
            catch (Exception)
            {
                return false;
            }
        }

#if AURORA_XR
        /// <summary>Сырое состояние кнопки: primary = A/X, secondary = B/Y.</summary>
        private static bool RawButton(UnityEngine.XR.XRNode node, bool primary)
        {
            try
            {
                var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);

                if (!device.isValid)
                {
                    return false;
                }

                bool pressed;
                var usage = primary ? UnityEngine.XR.CommonUsages.primaryButton : UnityEngine.XR.CommonUsages.secondaryButton;

                return device.TryGetFeatureValue(usage, out pressed) && pressed;
            }
            catch (Exception)
            {
                return false;
            }
        }
#endif

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
