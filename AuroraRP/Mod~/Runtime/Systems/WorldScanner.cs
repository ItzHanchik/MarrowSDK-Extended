using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Поиск объектов мода в мире: двери из паков, терминалы AuroraRP из палета, принтеры денег.
    /// Логика вешается на объекты уже в рантайме — так палет не зависит от кода мода.
    /// </summary>
    public class WorldScanner
    {
        private readonly Dictionary<int, AuroraComponent> _tracked = new Dictionary<int, AuroraComponent>();
        private float _nextScan;
        private bool _scanRequested = true;

        public void RequestScan()
        {
            _scanRequested = true;
        }

        public void Tick(float dt)
        {
            if (!GameHooks.PlayerReady)
            {
                return;
            }

            if (!_scanRequested && Time.realtimeSinceStartup < _nextScan)
            {
                return;
            }

            _scanRequested = false;
            _nextScan = Time.realtimeSinceStartup + 6f;
            Scan();
        }

        private void Scan()
        {
            AuroraRuntime.Doors?.Scan();
            ScanTerminals();
            SweepDead();
        }

        private void SweepDead()
        {
            List<int> dead = null;

            foreach (var pair in _tracked)
            {
                if (pair.Value == null || pair.Value.gameObject == null)
                {
                    (dead ??= new List<int>()).Add(pair.Key);
                }
            }

            if (dead != null)
            {
                foreach (var id in dead)
                {
                    _tracked.Remove(id);
                }
            }
        }

        /// <summary>Терминалы AuroraRP из нашего палета (по имени объекта или barcode).</summary>
        private void ScanTerminals()
        {
            try
            {
                var hosts = UnityEngine.Object.FindObjectsOfType<InteractableHost>();
                if (hosts == null)
                {
                    return;
                }

                for (int i = 0; i < hosts.Length; i++)
                {
                    var host = hosts[i];
                    if (host == null)
                    {
                        continue;
                    }

                    var go = host.gameObject;
                    if (!IsTerminal(go))
                    {
                        continue;
                    }

                    int id = go.GetInstanceID();
                    if (_tracked.ContainsKey(id))
                    {
                        continue;
                    }

                    var component = AuroraComponent.Attach(go, "terminal", new TerminalBehaviour(go, this));
                    if (component != null)
                    {
                        _tracked[id] = component;
                        AuroraLog.Info("Найден терминал AuroraRP: {0}", go.name);
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "terminal scan");
            }
        }

        private static bool IsTerminal(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            string name = go.name;
            if (name.StartsWith("AuroraRP", StringComparison.OrdinalIgnoreCase))
            {
                return name.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0
                       || name.IndexOf("ATM", StringComparison.OrdinalIgnoreCase) >= 0
                       || name.IndexOf("Bank", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        /// <summary>Игрок взял предмет — если это объект AuroraRP, вешаем логику.</summary>
        public void InspectGrabbed(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (IsTerminal(go))
            {
                int id = go.GetInstanceID();
                if (!_tracked.ContainsKey(id))
                {
                    var component = AuroraComponent.Attach(go, "terminal", new TerminalBehaviour(go, this));
                    if (component != null)
                    {
                        _tracked[id] = component;
                    }
                }

                AuroraNotifications.Send(AuroraL.Get("menu.hint.open"), UiTheme.Accent);
            }
        }
    }

    /// <summary>
    /// Банковский терминал из палета AuroraRP: берёшь в руку и нажимаешь B —
    /// открывается меню (кошелёк, роли, магазин, двери, контракты).
    /// </summary>
    public class TerminalBehaviour : IAuroraTickable, IAuroraDisposable
    {
        private readonly GameObject _root;
        private readonly WorldScanner _scanner;
        private bool _held;
        private bool _subscribed;
        private float _glow;

        public TerminalBehaviour(GameObject root, WorldScanner scanner)
        {
            _root = root;
            _scanner = scanner;
        }

        public void Tick(AuroraComponent component, float dt)
        {
            if (!_subscribed && AuroraRuntime.Input != null)
            {
                AuroraRuntime.Input.OnBPressed += OnButton;
                _subscribed = true;
            }

            // Мягкое свечение: экран терминала «дышит», когда игрок рядом.
            _glow = (Mathf.Sin(Time.time * 1.6f) + 1f) * 0.5f;

            var head = BoneLib.Player.Head;
            if (head == null || _root == null)
            {
                return;
            }

            bool near = (_root.transform.position - head.position).sqrMagnitude < 9f;
            _held = _held || near;
        }

        private void OnButton(Il2CppSLZ.Marrow.Interaction.Handedness hand)
        {
            if (!_held)
            {
                return;
            }

            var head = BoneLib.Player.Head;
            if (head == null || _root == null || (_root.transform.position - head.position).sqrMagnitude > 9f)
            {
                return;
            }

            AuroraRuntime.Menu.Open(MenuPage.Main);
        }

        public void Dispose()
        {
            if (_subscribed && AuroraRuntime.Input != null)
            {
                AuroraRuntime.Input.OnBPressed -= OnButton;
            }
        }
    }
}
