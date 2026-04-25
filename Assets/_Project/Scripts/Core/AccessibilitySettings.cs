using UnityEngine;

namespace ReverseRabbitRunner.Core
{
    /// <summary>
    /// Player-tuned accessibility and motion settings. Values are cached in
    /// memory and persisted to PlayerPrefs with a dirty-flag + explicit flush
    /// on app lifecycle events (see <see cref="Flush"/>).
    /// </summary>
    public static class AccessibilitySettings
    {
        private const string ShakeScaleKey = "Access.ShakeScale";
        private const string ReduceMotionKey = "Access.ReduceMotion";

        private static float shakeScale = 1f;
        private static bool reduceMotion;
        private static bool loaded;
        private static bool dirty;

        /// <summary>Multiplier applied to all screen-shake intensities (0 = off).</summary>
        public static float ShakeScale
        {
            get { EnsureLoaded(); return reduceMotion ? 0f : shakeScale; }
            set
            {
                EnsureLoaded();
                float v = Mathf.Clamp(value, 0f, 1.5f);
                if (Mathf.Approximately(v, shakeScale)) return;
                shakeScale = v;
                dirty = true;
            }
        }

        /// <summary>When true, shake-like motion is disabled entirely.</summary>
        public static bool ReduceMotion
        {
            get { EnsureLoaded(); return reduceMotion; }
            set
            {
                EnsureLoaded();
                if (reduceMotion == value) return;
                reduceMotion = value;
                dirty = true;
            }
        }

        /// <summary>Raw shake-scale value without the reduce-motion override. Use for UI display.</summary>
        public static float RawShakeScale
        {
            get { EnsureLoaded(); return shakeScale; }
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            shakeScale = Mathf.Clamp(PlayerPrefs.GetFloat(ShakeScaleKey, 1f), 0f, 1.5f);
            reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) != 0;
            loaded = true;
        }

        /// <summary>Write pending changes to PlayerPrefs. No-op if nothing changed.</summary>
        public static void Flush()
        {
            if (!loaded || !dirty) return;
            PlayerPrefs.SetFloat(ShakeScaleKey, shakeScale);
            PlayerPrefs.SetInt(ReduceMotionKey, reduceMotion ? 1 : 0);
            PlayerPrefs.Save();
            dirty = false;
        }
    }
}
