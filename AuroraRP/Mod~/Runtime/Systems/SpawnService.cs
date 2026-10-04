using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow.Data;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.Warehouse;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Спавн предметов из паков по barcode. Работает через AssetSpawner —
    /// так же, как это делают LabFusion и ванильный Spawn Gun.
    /// </summary>
    public class SpawnService
    {
        /// <summary>Спавнили ли предмет за последние кадры (для отдельного звука).</summary>
        public event Action<GameObject, string> OnSpawned;

        /// <summary>Список barcode'ов, которых нет в текущей сборке паков (для диагностики в меню).</summary>
        public readonly HashSet<string> MissingBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private float _lastSpawnTime;
        private const float SpawnCooldown = 0.2f;

        public bool IsReady => AssetWarehouse.ready;

        /// <summary>Проверяет, что barcode существует в загруженных палетах.</summary>
        public bool Exists(string barcode, out string title)
        {
            title = null;

            if (string.IsNullOrWhiteSpace(barcode))
            {
                return false;
            }

            try
            {
                if (!AssetWarehouse.ready)
                {
                    return false;
                }

                var reference = new SpawnableCrateReference(barcode);
                if (reference.TryGetCrate(out var crate) && crate != null)
                {
                    title = crate.Title;
                    MissingBarcodes.Remove(barcode);
                    return true;
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "Exists(" + barcode + ")");
            }

            MissingBarcodes.Add(barcode);
            return false;
        }

        public static Spawnable CreateSpawnable(string barcode)
        {
            return new Spawnable
            {
                crateRef = new SpawnableCrateReference(barcode),
                policyData = null
            };
        }

        /// <summary>Заспавнить предмет. Ошибки не бросает — просто логирует.</summary>
        public bool TrySpawn(string barcode, Vector3 position, Quaternion rotation, out string error, Action<GameObject> callback = null)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(barcode))
            {
                error = "Пустой barcode";
                return false;
            }

            if (Time.realtimeSinceStartup - _lastSpawnTime < SpawnCooldown)
            {
                error = "Слишком часто";
                return false;
            }

            _lastSpawnTime = Time.realtimeSinceStartup;

            // В сети спавним через LabFusion: предмет становится сетевым и появляется у всех.
            // Важно: если у игрока нет палета мода, Fusion сам скачает его с mod.io.
            // Проверку Exists здесь пропускаем — локально палета может ещё не быть.
            var net = AuroraRuntime.Net;

            if (net != null && net.IsConnected && net.TryNetworkSpawn(barcode, position, rotation, callback))
            {
                AuroraLog.Info("Сетевой спавн {0}", barcode);
                return true;
            }

            if (!Exists(barcode, out _))
            {
                error = "Barcode не найден: " + barcode;
                AuroraLog.Warn(error);
                return false;
            }

            try
            {
                var spawnable = CreateSpawnable(barcode);

                // Так же, как в LabFusion: nullable-параметры Il2CPP передаём вручную,
                // иначе MelonLoader может не подставить значения по умолчанию.
                var scale = new Il2CppSystem.Nullable<Vector3>(Vector3.zero) { hasValue = false };
                var groupId = new Il2CppSystem.Nullable<int>(0) { hasValue = false };

                // Ровно как в LabFusion (9 аргументов): у IL2CPP-обёртки нет перегрузки с recycleCallback.
                var task = AssetSpawner.SpawnAsync(spawnable, position, rotation, scale, null, false, groupId, null, null);
                var awaiter = task.GetAwaiter();

                awaiter.OnCompleted(() =>
                {
                    Poolee poolee = null;
                    try
                    {
                        poolee = awaiter.GetResult();
                    }
                    catch (Exception e)
                    {
                        AuroraLog.Exception(e, "spawn await " + barcode);
                    }

                    if (poolee == null)
                    {
                        AuroraLog.Warn("Спавн не удался: {0}", barcode);
                        return;
                    }

                    var go = poolee.gameObject;
                    AuroraLog.Info("Заспавнен {0} ({1})", barcode, go.name);
                    OnSpawned?.Invoke(go, barcode);
                    callback?.Invoke(go);
                });

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                AuroraLog.Exception(e, "spawn " + barcode);
                return false;
            }
        }

        /// <summary>Позиция перед игроком: чуть ниже уровня глаз, чтобы предмет не падал с высоты.</summary>
        public Vector3 SpawnPositionInFrontOfLocalPlayer(float distance = 0.6f)
        {
            var head = BoneLib.Player.Head;
            var rig = BoneLib.Player.RigManager;

            Vector3 origin = head != null ? head.position : (rig != null ? rig.transform.position + Vector3.up * 1.4f : Vector3.zero);
            Vector3 forward = head != null ? head.forward : Vector3.forward;

            Vector3 pos = origin + forward * distance - Vector3.up * 0.25f;

            // Не даём предмету улететь в стену: проверяем луч.
            if (Physics.Raycast(origin, forward, out var hit, distance + 0.4f))
            {
                pos = hit.point + hit.normal * 0.05f;
            }

            return pos;
        }

        /// <summary>Выдать набор предметов локальному игроку (стартовый сет роли и т.п.).</summary>
        public void SpawnNearLocalPlayer(IEnumerable<string> barcodes, string reason)
        {
            if (barcodes == null)
            {
                return;
            }

            int spawned = 0;
            foreach (var barcode in barcodes)
            {
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    continue;
                }

                Vector3 pos = SpawnPositionInFrontOfLocalPlayer(0.7f + spawned * 0.15f);
                if (TrySpawn(barcode, pos, Quaternion.identity, out string error))
                {
                    spawned++;
                }
                else
                {
                    AuroraLog.Warn("Стартовый сет ({0}): {1}", reason, error);
                }
            }

            if (spawned > 0)
            {
                AuroraNotifications.Send(AuroraL.Get("notify.weapon.spawned"), AuroraUtils.Hex("#9AD1FF"));
            }
        }

        /// <summary>
        /// Выдать предмет другому игроку. Если игрок не локальный и мы не хост — просим хоста.
        /// </summary>
        public void SpawnForPlayer(byte playerId, string barcode)
        {
            if (playerId == AuroraRuntime.LocalId)
            {
                TrySpawn(barcode, SpawnPositionInFrontOfLocalPlayer(), Quaternion.identity, out _);
                return;
            }

            AuroraRuntime.Net?.SendSpawnForPlayer(playerId, barcode);
        }
    }
}
