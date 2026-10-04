using System.Collections.Generic;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Звуки мода. Все клипы синтезируются в коде (никаких внешних файлов и паков не нужно):
    /// приятный «звон монет» при переводе денег, щелчки меню, касса при покупке и т.д.
    /// </summary>
    public class AuroraSfx
    {
        public enum Kind
        {
            Hover = 0,
            Click = 1,
            Open = 2,
            Close = 3,
            SendMoney = 4,
            ReceiveMoney = 5,
            Error = 6,
            HoldStart = 7,
            DoorBuy = 8,
            DoorSell = 9,
            Purchase = 10,
            Contract = 11,
            Success = 12
        }

        private const int SampleRate = 44100;

        private static readonly Dictionary<Kind, AudioClip> Clips = new Dictionary<Kind, AudioClip>();

        private AudioSource _source;
        private int _lastHoverFrame = -10;

        public static void Initialize()
        {
            if (Clips.Count > 0)
            {
                return;
            }

            Clips[Kind.Hover] = Build("aurora_hover", 0.06f, t => Sine(t, 1180f, 0.35f) * Env(t, 0.06f, 26f) * 0.45f);
            Clips[Kind.Click] = Build("aurora_click", 0.09f, t => (Sine(t, 880f, 0.25f) + Sine(t, 1320f, 0.15f)) * Env(t, 0.09f, 30f));
            Clips[Kind.Open] = Build("aurora_open", 0.32f, t => Sweep(t, 0.32f, 320f, 900f, 0.6f) * Env(t, 0.32f, 7f) * 0.7f);
            Clips[Kind.Close] = Build("aurora_close", 0.24f, t => Sweep(t, 0.24f, 820f, 300f, 0.6f) * Env(t, 0.24f, 9f) * 0.6f);
            Clips[Kind.SendMoney] = BuildCoins("aurora_send", true);
            Clips[Kind.ReceiveMoney] = BuildCoins("aurora_receive", false);
            Clips[Kind.Error] = Build("aurora_error", 0.22f, t => (Square(t, 190f) + Sine(t, 96f, 0.5f)) * Env(t, 0.22f, 11f) * 0.55f);
            Clips[Kind.HoldStart] = Build("aurora_hold", 0.14f, t => Sine(t, 620f, 0.3f) * Env(t, 0.14f, 16f) * 0.5f);
            Clips[Kind.DoorBuy] = Build("aurora_door_buy", 0.5f, t => (Sine(t, 520f, 0.4f) + Sine(t, 780f, 0.3f)) * Env(t, 0.5f, 6f) * 0.7f);
            Clips[Kind.DoorSell] = Build("aurora_door_sell", 0.42f, t => (Sine(t, 400f, 0.4f) + Sine(t, 600f, 0.3f)) * Env(t, 0.42f, 7f) * 0.65f);
            Clips[Kind.Purchase] = BuildCoins("aurora_purchase", true, 1.15f);
            Clips[Kind.Contract] = Build("aurora_contract", 0.7f, t => Sweep(t, 0.7f, 200f, 120f, 0.4f) * Env(t, 0.7f, 4.5f) * 0.7f + Noise(t, 0.7f) * Env(t, 0.7f, 14f) * 0.12f);
            Clips[Kind.Success] = Build("aurora_success", 0.4f, t => (Sine(t, 660f, 0.4f) + Sine(t, 990f, 0.25f) * Step(t, 0.14f)) * Env(t, 0.4f, 7f) * 0.65f);
        }

        public void Play(Kind kind, float delay = 0f, float volumeScale = 1f)
        {
            if (AuroraConfig.Current.sfxVolume <= 0.001f)
            {
                return;
            }

            if (kind == Kind.Hover)
            {
                if (Time.frameCount - _lastHoverFrame < 6)
                {
                    return;
                }

                _lastHoverFrame = Time.frameCount;
            }

            AudioClip clip = Get(kind);
            if (clip == null)
            {
                return;
            }

            EnsureSource();
            if (_source == null)
            {
                return;
            }

            float vol = Mathf.Clamp01(AuroraConfig.Current.sfxVolume * volumeScale);
            if (delay <= 0f)
            {
                _source.PlayOneShot(clip, vol);
            }
            else
            {
                AuroraUtils.RunCoroutine(PlayDelayed(clip, delay, vol));
            }
        }

        private System.Collections.IEnumerator PlayDelayed(AudioClip clip, float delay, float volume)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (_source != null)
            {
                _source.PlayOneShot(clip, volume);
            }
        }

        private static AudioClip Get(Kind kind)
        {
            // Красота из палета: Sfx_SendMoney, Sfx_Click и т.д. Нет — играем синтезированный.
            var fromPallet = AuroraVisuals.GetSound("Sfx_" + kind);
            if (fromPallet != null)
            {
                return fromPallet;
            }

            if (Clips.Count == 0)
            {
                Initialize();
            }

            Clips.TryGetValue(kind, out var clip);
            return clip;
        }

        private void EnsureSource()
        {
            if (_source != null)
            {
                return;
            }

            var driver = AuroraDriver.Instance;
            if (driver == null)
            {
                return;
            }

            _source = driver.gameObject.GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = driver.gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f; // 2D: звук банка всегда слышно
                _source.volume = 1f;
            }
        }

        // ------------------------------------------------------------ генераторы

        private static AudioClip Build(string name, float seconds, System.Func<float, float> generator)
        {
            int samples = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(generator(t), -1f, 1f);
            }

            SoftClip(data);

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>«Звон монет»: несколько затухающих металлических нот подряд.</summary>
        private static AudioClip BuildCoins(string name, bool up, float pitchScale = 1f)
        {
            float seconds = 0.85f;
            int samples = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[samples];

            float[] notes = up
                ? new[] { 1318.5f, 1567.9f, 1975.5f, 2349.3f }
                : new[] { 1975.5f, 1567.9f, 1318.5f, 1046.5f };

            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.075f;
                float freq = notes[n] * pitchScale;

                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    if (t < start)
                    {
                        continue;
                    }

                    float lt = t - start;
                    float env = Mathf.Exp(-6.5f * lt);
                    if (env < 0.0005f)
                    {
                        continue;
                    }

                    // Металлический тембр: основной тон + негармонические обертоны.
                    float v = Mathf.Sin(2f * Mathf.PI * freq * lt)
                              + 0.45f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * lt)
                              + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * lt);

                    data[i] += v * env * 0.22f;
                }
            }

            SoftClip(data);

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sine(float t, float freq, float amp = 1f) => Mathf.Sin(2f * Mathf.PI * freq * t) * amp;

        private static float Square(float t, float freq) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.6f;

        /// <summary>Скользящий тон (для анимации открытия меню).</summary>
        private static float Sweep(float t, float duration, float from, float to, float amp)
        {
            float k = Mathf.Clamp01(t / duration);
            float freq = Mathf.Lerp(from, to, k);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * amp;
        }

        private static float Noise(float t, float duration)
        {
            float k = Mathf.Clamp01(t / duration);
            return Mathf.PerlinNoise(t * 24000f, 0.5f) * 2f - 1f;
        }

        private static float Step(float t, float from) => t >= from ? 1f : 0f;

        private static float Env(float t, float duration, float decay)
        {
            if (t > duration)
            {
                return 0f;
            }

            float attack = Mathf.Clamp01(t / 0.004f);
            return attack * Mathf.Exp(-decay * t);
        }

        private static void SoftClip(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                data[i] = x / (1f + Mathf.Abs(x)); // мягкое ограничение — убирает щелчки
            }
        }
    }
}
