using System;
using BoneLib;
using Il2CppSLZ.Marrow;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Подписка на события игры через BoneLib (уровни, захват предметов, смерть игрока).
    /// </summary>
    public static class GameHooks
    {
        private static bool _subscribed;

        public static void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            Hooking.OnLevelLoaded += LevelLoaded;
            Hooking.OnLevelUnloaded += LevelUnloaded;
            Hooking.OnGrabObject += OnGrab;
            Hooking.OnReleaseObject += OnRelease;
            Hooking.OnPlayerDeath += OnPlayerDeath;
            Hooking.OnPlayerResurrected += OnPlayerResurrected;
            Hooking.OnUIRigCreated += ValidatePlayerReferences;

            _subscribed = true;
            AuroraLog.Info("Подписка на события игры выполнена");
        }

        public static void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            Hooking.OnLevelLoaded -= LevelLoaded;
            Hooking.OnLevelUnloaded -= LevelUnloaded;
            Hooking.OnGrabObject -= OnGrab;
            Hooking.OnReleaseObject -= OnRelease;
            Hooking.OnPlayerDeath -= OnPlayerDeath;
            Hooking.OnPlayerResurrected -= OnPlayerResurrected;
            Hooking.OnUIRigCreated -= ValidatePlayerReferences;

            _subscribed = false;
        }

        // ------------------------------------------------------------------ уровни

        private static void LevelLoaded(LevelInfo info)
        {
            try
            {
                ValidatePlayerReferences();

                AuroraRuntime.Doors?.OnLevelLoaded();
                AuroraRuntime.Scanner?.RequestScan();

                // Роль применяется после загрузки уровня — выдаём стартовый сет.
                var role = AuroraRuntime.Roles.GetRole(AuroraRuntime.LocalId);
                if (role != AuroraRoleId.Citizen)
                {
                    AuroraRuntime.Roles.ApplyStarterKit(role);
                }

                AuroraLog.Info("Уровень загружен: {0} ({1})", info.title, info.barcode);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnLevelLoaded");
            }
        }

        private static void LevelUnloaded()
        {
            try
            {
                AuroraRuntime.Doors?.OnLevelUnloaded();
                AuroraRuntime.Craft?.OnLevelUnloaded();
                AuroraNotifications.Clear();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnLevelUnloaded");
            }
        }

        // ------------------------------------------------------------------ захват

        private static void OnGrab(GameObject go, Hand hand)
        {
            try
            {
                if (go == null || hand == null)
                {
                    return;
                }

                // Нас интересуют только руки локального игрока.
                if (!IsLocalHand(hand))
                {
                    return;
                }

                AuroraRuntime.Doors?.OnGrabbed(go, hand);
                AuroraRuntime.Craft?.OnGrabbed(go, hand);
                AuroraRuntime.Scanner?.InspectGrabbed(go);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnGrab");
            }
        }

        private static void OnRelease(Hand hand)
        {
            try
            {
                if (hand == null || !IsLocalHand(hand))
                {
                    return;
                }

                AuroraRuntime.Doors?.OnReleased(hand);
                AuroraRuntime.Craft?.OnReleased(hand);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnRelease");
            }
        }

        public static bool IsLocalHand(Hand hand)
        {
            if (hand == null)
            {
                return false;
            }

            if (Player.LeftHand != null && hand == Player.LeftHand)
            {
                return true;
            }

            return Player.RightHand != null && hand == Player.RightHand;
        }

        // ------------------------------------------------------------------ смерть

        private static void OnPlayerDeath(RigManager rig)
        {
            try
            {
                // Локальный игрок умер — сбрасываем удержание денег.
                AuroraRuntime.Transfers?.ResetPending();

                AuroraRuntime.Net?.NotifyLocalDeath();
                AuroraRuntime.Contracts?.OnLocalPlayerDied();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnPlayerDeath");
            }
        }

        private static void OnPlayerResurrected(RigManager rig)
        {
            try
            {
                AuroraRuntime.Contracts?.OnLocalPlayerRespawned();
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnPlayerResurrected");
            }
        }

        // ------------------------------------------------------------ ссылки игрока

        /// <summary>Проверяет, что BoneLib знает о риге игрока (после загрузки уровня он новый).</summary>
        public static void ValidatePlayerReferences()
        {
            try
            {
                if (!Player.HandsExist && Player.RigManager == null)
                {
                    var rig = UnityEngine.Object.FindObjectOfType<RigManager>();
                    if (rig != null)
                    {
                        AuroraLog.Info("Найден RigManager, ждём инициализацию BoneLib");
                    }
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "ValidatePlayerReferences");
            }
        }

        public static bool PlayerReady => Player.RigManager != null && Player.HandsExist && Player.ControllerRig != null;
    }
}
