using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// «Красота» из палета. Вся визуальная часть (иконки, скин меню, партиклы перевода денег,
    /// модели купюр/монет, звуки) лежит в палете AuroraRP в спавнабле
    /// «ItzHanchik.AuroraRP.Spawnable.VisualSet» и ищется по именам объектов.
    ///
    /// Имена, которые понимает код:
    ///   MenuSkin      — спрайт/материал скина меню
    ///   Icon_&lt;имя&gt;   — иконки (coin, wallet, gun, police, ... — те же имена, что рисует UiTheme)
    ///   Fx_Transfer   — эффект передачи денег (ParticleSystem)
    ///   Fx_Receive    — эффект получения денег
    ///   Prop_Cash     — предмет, который летит из руки в руку (пачка купюр)
    ///   Sfx_&lt;Kind&gt;    — звук вместо синтезированного (Sfx_SendMoney, Sfx_Click, ...)
    ///
    /// Если палета нет или он пустой — всё работает как раньше: код рисует иконки сам,
    /// звуки синтезируются, перевод просто проходит без анимации.
    /// </summary>
    public static class AuroraVisuals
    {
        public const string DefaultVisualSetBarcode = "ItzHanchik.AuroraRP.Spawnable.VisualSet";

        /// <summary>Событие «получателю прилетели деньги» (рассылку делает хост).</summary>
        public const string FxMoneyReceive = "money.receive";

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RuntimeAnimatorController> Animators = new Dictionary<string, RuntimeAnimatorController>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, GameObject> Parts = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private sealed class Flying
        {
            public GameObject Go;
            public Vector3 From;
            public Vector3 To;
            public float Time;
            public float Duration;
            public bool Arc;
        }

        private static readonly List<Flying> Flights = new List<Flying>();
        private static readonly List<KeyValuePair<GameObject, float>> Temp = new List<KeyValuePair<GameObject, float>>();

        private static GameObject _holder;
        private static bool _loading;
        private static bool _packFailed;
        private static int _attempts;
        private static float _lastAttempt;

        public static bool Ready { get; private set; }

        public static string VisualSetBarcode =>
            string.IsNullOrWhiteSpace(AuroraConfig.Current.visualSetBarcode)
                ? DefaultVisualSetBarcode
                : AuroraConfig.Current.visualSetBarcode;

        // ------------------------------------------------------------------ загрузка

        public static void Tick(float dt)
        {
            AdvanceFlights(dt);
            AdvanceTemp(dt);

            var cfg = AuroraConfig.Current;

            if (!cfg.useEmbeddedVisuals && !cfg.usePalletVisuals)
            {
                return;
            }

            // Палет мог выгрузиться вместе с уровнем — попробуем собрать заново.
            if (Ready && _holder == null)
            {
                Reset();
            }

            if (Ready || _loading)
            {
                return;
            }

            // 1) Красота, зашитая в DLL (основной путь).
            if (cfg.useEmbeddedVisuals && !_packFailed && AuroraPack.IsAvailable)
            {
                if (TryLoadEmbedded())
                {
                    return;
                }

                _packFailed = true;
                AuroraLog.Warn("Пак в DLL не разобрался — пробую палет и кодовые заглушки.");
            }

            // 2) Палет (необязательный запасной путь).
            if (!cfg.usePalletVisuals)
            {
                return;
            }

            if (_attempts >= 40 || Time.realtimeSinceStartup - _lastAttempt < 4f)
            {
                return;
            }

            TryLoad();
        }

        /// <summary>
        /// Красота из DLL: бандлы, зашитые в AuroraRP.dll (ресурс «aurorarp.pack»).
        /// Палет для этого не нужен.
        /// </summary>
        private static bool TryLoadEmbedded()
        {
            if (!AuroraPack.EnsureLoaded())
            {
                return false;
            }

            GameObject holder = null;

            try
            {
                holder = AuroraPack.Instantiate(AuroraPack.VisualSetPrefab, new Vector3(0f, -900f, 0f), Quaternion.identity);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "pack visual set");
            }

            if (holder != null)
            {
                _holder = holder;
                holder.SetActive(false);

                var body = holder.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.useGravity = false;
                    body.isKinematic = true;
                }

                Harvest(holder.transform);
            }

            HarvestPack();

            Ready = Parts.Count > 0 || Sprites.Count > 0 || Materials.Count > 0 || Sounds.Count > 0;

            if (Ready)
            {
                AuroraLog.Info("Красота из DLL загружена: {0} объектов, {1} иконок, {2} звуков",
                    Parts.Count, Sprites.Count, Sounds.Count);
            }

            return Ready;
        }

        /// <summary>Ассеты пака, лежащие в бандле отдельно от префаба (иконки, звуки, анимации).</summary>
        private static void HarvestPack()
        {
            foreach (var pair in AuroraPack.AllSprites)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null && !Sprites.ContainsKey(pair.Key))
                {
                    Sprites[pair.Key] = pair.Value;
                }
            }

            foreach (var pair in AuroraPack.AllClips)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null && !Sounds.ContainsKey(pair.Key))
                {
                    Sounds[pair.Key] = pair.Value;
                }
            }

            foreach (var pair in AuroraPack.AllMaterials)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null && !Materials.ContainsKey(pair.Key))
                {
                    Materials[pair.Key] = pair.Value;
                }
            }

            foreach (var pair in AuroraPack.AllControllers)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null && !Animators.ContainsKey(pair.Key))
                {
                    Animators[pair.Key] = pair.Value;
                }
            }
        }

        private static void TryLoad()
        {
            var spawn = AuroraRuntime.Spawn;

            if (spawn == null || !spawn.IsReady)
            {
                return;
            }

            string barcode = VisualSetBarcode;

            if (string.IsNullOrWhiteSpace(barcode) || !spawn.Exists(barcode, out _))
            {
                return;
            }

            _attempts++;
            _lastAttempt = Time.realtimeSinceStartup;
            _loading = true;

            // Спавним далеко под уровнем: объект нужен только как «контейнер красоты».
            var position = new Vector3(0f, -900f, 0f);

            if (!spawn.TrySpawnLocal(barcode, position, Quaternion.identity, out string error, OnSpawned))
            {
                _loading = false;
                AuroraLog.Warn("VisualSet не заспавнился: {0}", error);
            }
        }

        private static void OnSpawned(GameObject go)
        {
            _loading = false;

            if (go == null)
            {
                return;
            }

            try
            {
                _holder = go;
                go.transform.position = new Vector3(0f, -900f, 0f);
                go.transform.rotation = Quaternion.identity;
                go.SetActive(false);

                var body = go.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.useGravity = false;
                    body.isKinematic = true;
                }

                Harvest(go.transform);
                Ready = Parts.Count > 0 || Sprites.Count > 0 || Materials.Count > 0 || Sounds.Count > 0;

                AuroraLog.Info("Красота из палета загружена: {0} объектов, {1} иконок, {2} звуков",
                    Parts.Count, Sprites.Count, Sounds.Count);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "visual set harvest");
            }
        }

        private static void Harvest(Transform root)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part == null)
                {
                    continue;
                }

                string name = part.name;

                if (string.IsNullOrEmpty(name) || Parts.ContainsKey(name))
                {
                    continue;
                }

                try
                {
                    Parts[name] = part.gameObject;

                    // Спрайты: и как SpriteRenderer в мире, и как Image в UI-префабах.
                    var spriteRenderer = part.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null && spriteRenderer.sprite != null)
                    {
                        Sprites[name] = spriteRenderer.sprite;
                    }

                    var image = part.GetComponent<Image>();
                    if (image != null && image.sprite != null)
                    {
                        Sprites[name] = image.sprite;
                    }

                    var renderer = part.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        Materials[name] = renderer.sharedMaterial;
                    }

                    var source = part.GetComponent<AudioSource>();
                    if (source != null && source.clip != null)
                    {
                        Sounds[name] = source.clip;
                    }

                    var animator = part.GetComponent<Animator>();
                    if (animator != null && animator.runtimeAnimatorController != null)
                    {
                        Animators[name] = animator.runtimeAnimatorController;
                    }
                }
                catch (Exception e)
                {
                    AuroraLog.Exception(e, "harvest " + name);
                }
            }
        }

        private static void Reset()
        {
            Ready = false;
            _holder = null;
            _attempts = 0;
            _lastAttempt = Time.realtimeSinceStartup;
        }

        public static void Shutdown()
        {
            Flights.Clear();
            Temp.Clear();
            AuroraPack.Shutdown();

            Sprites.Clear();
            Materials.Clear();
            Sounds.Clear();
            Animators.Clear();
            Parts.Clear();

            Ready = false;
            _holder = null;
            _packFailed = false;
        }

        // ------------------------------------------------------------------- доступ

        public static Sprite GetSprite(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // Icon_coin, Icon_wallet и т.п.
            if (Sprites.TryGetValue("Icon_" + name, out var icon))
            {
                return icon;
            }

            return Sprites.TryGetValue(name, out var sprite) ? sprite : null;
        }

        public static Material GetMaterial(string name)
        {
            return !string.IsNullOrEmpty(name) && Materials.TryGetValue(name, out var material) ? material : null;
        }

        public static AudioClip GetSound(string name)
        {
            return !string.IsNullOrEmpty(name) && Sounds.TryGetValue(name, out var clip) ? clip : null;
        }

        public static RuntimeAnimatorController GetAnimator(string name)
        {
            return !string.IsNullOrEmpty(name) && Animators.TryGetValue(name, out var animator) ? animator : null;
        }

        public static GameObject GetPart(string name)
        {
            return !string.IsNullOrEmpty(name) && Parts.TryGetValue(name, out var part) ? part : null;
        }

        public static bool Has(string name)
        {
            return Parts.ContainsKey(name) || Sprites.ContainsKey("Icon_" + name) || Sounds.ContainsKey(name);
        }

        // ------------------------------------------------------------------- эффекты

        /// <summary>Перевод денег: пачка купюр летит из руки в руку, на месте — партиклы.</summary>
        public static void PlayTransferFx(Vector3 from, Vector3 to)
        {
            if (!Ready || !AuroraConfig.Current.transferFxEnabled)
            {
                return;
            }

            SpawnTemp("Fx_Transfer", from, 3f);

            var cash = InstantiatePart("Prop_Cash", from, 2.5f);

            if (cash != null)
            {
                Flights.Add(new Flying
                {
                    Go = cash,
                    From = from,
                    To = to,
                    Duration = Mathf.Max(0.1f, AuroraConfig.Current.transferFxSeconds),
                    Arc = true
                });
            }
            else
            {
                SpawnTemp("Fx_Receive", to, 2f);
            }
        }

        /// <summary>Событие из сети: проиграть эффект у себя.</summary>
        public static void PlayFxEvent(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (string.Equals(name, FxMoneyReceive, StringComparison.OrdinalIgnoreCase))
            {
                PlayReceiveFx(LocalHandPosition());
                AuroraRuntime.Sfx?.Play(AuroraSfx.Kind.ReceiveMoney, 0.05f);
            }
        }

        private static Vector3 LocalHandPosition()
        {
            try
            {
                var hand = BoneLib.Player.RightHand;

                if (hand != null)
                {
                    return hand.transform.position;
                }

                var head = BoneLib.Player.Head;
                return head != null ? head.position : Vector3.zero;
            }
            catch (Exception)
            {
                return Vector3.zero;
            }
        }

        /// <summary>Получение денег: вспышка у получателя.</summary>
        public static void PlayReceiveFx(Vector3 at)
        {
            if (!Ready || !AuroraConfig.Current.transferFxEnabled)
            {
                return;
            }

            SpawnTemp("Fx_Receive", at, 2f);
        }

        private static GameObject InstantiatePart(string name, Vector3 position, float life)
        {
            var part = GetPart(name);

            if (part == null)
            {
                return null;
            }

            try
            {
                var copy = UnityEngine.Object.Instantiate(part, position, Quaternion.identity);
                copy.name = name + "_Fx";
                copy.SetActive(true);

                try
                {
                    var particles = copy.GetComponentInChildren<ParticleSystem>();

                    if (particles != null)
                    {
                        particles.Play(true);
                    }
                }
                catch (Exception)
                {
                }

                Temp.Add(new KeyValuePair<GameObject, float>(copy, Time.realtimeSinceStartup + life));
                return copy;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "instantiate " + name);
                return null;
            }
        }

        private static void SpawnTemp(string name, Vector3 position, float life)
        {
            InstantiatePart(name, position, life);
        }

        private static void AdvanceFlights(float dt)
        {
            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                var flight = Flights[i];

                if (flight.Go == null)
                {
                    Flights.RemoveAt(i);
                    continue;
                }

                flight.Time += dt;
                float k = Mathf.Clamp01(flight.Time / flight.Duration);

                Vector3 position = Vector3.Lerp(flight.From, flight.To, k);

                if (flight.Arc)
                {
                    position += Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.22f);
                }

                try
                {
                    flight.Go.transform.position = position;
                    flight.Go.transform.Rotate(Vector3.up, 360f * dt, Space.World);
                }
                catch (Exception)
                {
                    Flights.RemoveAt(i);
                    continue;
                }

                if (k >= 1f)
                {
                    Flights.RemoveAt(i);
                    PlayReceiveFx(flight.To);
                    Release(flight.Go);
                }
            }
        }

        private static void AdvanceTemp(float dt)
        {
            float now = Time.realtimeSinceStartup;

            for (int i = Temp.Count - 1; i >= 0; i--)
            {
                var item = Temp[i];

                if (item.Key == null)
                {
                    Temp.RemoveAt(i);
                    continue;
                }

                if (now < item.Value)
                {
                    continue;
                }

                Temp.RemoveAt(i);

                // Летящую купюру убираем в конце полёта, а не здесь.
                bool flying = false;
                for (int j = 0; j < Flights.Count; j++)
                {
                    if (Flights[j].Go == item.Key)
                    {
                        flying = true;
                        break;
                    }
                }

                if (!flying)
                {
                    Release(item.Key);
                }
            }
        }

        private static void Release(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(go);
            }
            catch (Exception)
            {
            }
        }
    }
}
