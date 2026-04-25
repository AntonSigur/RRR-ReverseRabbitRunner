using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Tiny on-the-fly synth used to build short SFX clips at runtime so the
    /// game ships with juicy feedback (combo tier-up sting, magnet whoosh,
    /// near-miss whoosh, game-over fanfare) without shipping audio assets.
    ///
    /// All clips are mono, 22050Hz, hard-cached after first build.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 22050;

        private static AudioClip cachedNearMiss;
        private static AudioClip cachedMagnet;
        private static AudioClip cachedFanfare;
        private static readonly AudioClip[] cachedTierStings = new AudioClip[6];

        /// <summary>Sweeping whoosh, descending then rising — used for near-miss.</summary>
        public static AudioClip NearMissWhoosh()
        {
            if (cachedNearMiss != null) return cachedNearMiss;
            const float duration = 0.30f;
            int n = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                // Filtered noise + sine sweep down 1200→300, up to 800Hz at end
                float freq = Mathf.Lerp(1200f, 300f, Mathf.Pow(t, 0.6f));
                if (t > 0.7f) freq = Mathf.Lerp(300f, 800f, (t - 0.7f) / 0.3f);
                float sine = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate));
                float noise = (Random.value * 2f - 1f) * 0.55f;
                float env = Mathf.Sin(Mathf.PI * t); // bell envelope
                data[i] = (sine * 0.55f + noise * 0.45f) * env * 0.6f;
            }
            cachedNearMiss = AudioClip.Create("NearMissWhoosh", n, 1, SampleRate, false);
            cachedNearMiss.SetData(data, 0);
            return cachedNearMiss;
        }

        /// <summary>Magnet pickup: shimmery rising chord with a metallic ping.</summary>
        public static AudioClip MagnetWhoosh()
        {
            if (cachedMagnet != null) return cachedMagnet;
            const float duration = 0.45f;
            int n = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[n];
            // Rising glissando + perfect-fifth
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float baseF = Mathf.Lerp(440f, 880f, t);
                float fifth = baseF * 1.5f;
                float ping = Mathf.Sin(2f * Mathf.PI * baseF * 4f * (i / (float)SampleRate))
                             * Mathf.Exp(-t * 8f) * 0.25f;
                float a = Mathf.Sin(2f * Mathf.PI * baseF * (i / (float)SampleRate));
                float b = Mathf.Sin(2f * Mathf.PI * fifth * (i / (float)SampleRate));
                float env = Mathf.Sin(Mathf.PI * t);
                data[i] = ((a * 0.5f + b * 0.4f) * env + ping) * 0.55f;
            }
            cachedMagnet = AudioClip.Create("MagnetWhoosh", n, 1, SampleRate, false);
            cachedMagnet.SetData(data, 0);
            return cachedMagnet;
        }

        /// <summary>Short triumphant sting — pitch climbs with combo tier (0..5).</summary>
        public static AudioClip ComboTierSting(int tierIndex)
        {
            tierIndex = Mathf.Clamp(tierIndex, 0, cachedTierStings.Length - 1);
            if (cachedTierStings[tierIndex] != null) return cachedTierStings[tierIndex];

            // Major-third stack: each tier shifts up a major third (1.26x)
            float root = 330f * Mathf.Pow(1.26f, tierIndex);
            float duration = 0.26f;
            int n = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float a = Mathf.Sin(2f * Mathf.PI * root * (i / (float)SampleRate));
                float b = Mathf.Sin(2f * Mathf.PI * root * 1.26f * (i / (float)SampleRate));
                float c = Mathf.Sin(2f * Mathf.PI * root * 1.5f * (i / (float)SampleRate));
                float env = Mathf.Exp(-t * 5f) * (1f - Mathf.Exp(-t * 60f)); // pluck
                data[i] = (a * 0.5f + b * 0.35f + c * 0.3f) * env * 0.55f;
            }
            var clip = AudioClip.Create($"ComboSting_{tierIndex}", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            cachedTierStings[tierIndex] = clip;
            return clip;
        }

        /// <summary>Two-bar mournful descending fanfare for game-over screen.</summary>
        public static AudioClip GameOverFanfare()
        {
            if (cachedFanfare != null) return cachedFanfare;
            // Notes: G4, E4, C4, A3 (descending sigh) - 0.35s each
            float[] freqs = { 392f, 330f, 262f, 220f };
            float noteDur = 0.32f;
            int notes = freqs.Length;
            int total = Mathf.RoundToInt(noteDur * notes * SampleRate);
            var data = new float[total];
            int notLen = Mathf.RoundToInt(noteDur * SampleRate);
            for (int n_i = 0; n_i < notes; n_i++)
            {
                float f = freqs[n_i];
                int offset = n_i * notLen;
                for (int i = 0; i < notLen; i++)
                {
                    float t = i / (float)notLen;
                    float s = Mathf.Sin(2f * Mathf.PI * f * (i / (float)SampleRate));
                    float oct = Mathf.Sin(2f * Mathf.PI * f * 0.5f * (i / (float)SampleRate));
                    float env = Mathf.Exp(-t * 3.2f) * (1f - Mathf.Exp(-t * 50f));
                    data[offset + i] = (s * 0.55f + oct * 0.4f) * env * 0.55f;
                }
            }
            cachedFanfare = AudioClip.Create("GameOverFanfare", total, 1, SampleRate, false);
            cachedFanfare.SetData(data, 0);
            return cachedFanfare;
        }
    }
}
