using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BikeGame
{
    /// <summary>
    /// Цветокоррекция кадра — у каждой локации своё «кино», и оно меняется со временем суток (как цветокор в фильме):
    ///   АЛЬПЫ — чистый воздух: тёплые света, голубоватые тени;
    ///   КАНЬОН — жаркое солнце пустыни: тёплый баланс, сочная охра, фиолетовые тени;
    ///   ТУМАН — сырое утро: холодно, приглушённо, мягкое свечение светлого неба;
    ///   ОСЕНЬ — золото: тёплый баланс, медовые света, насыщенные кроны;
    ///   ЗИМА — мороз: холодный баланс, синеватые тени, чуть меньше цвета.
    /// Время суток: «золотой час» (рассвет и закат) — теплее и с большим свечением, ночью — холоднее, темнее и без цвета.
    /// Меняет копию профиля Volume (общий ассет не трогается). Висит на камере; профиль — Settings/PostFX_Atmosphere.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class AtmosphereGrade : MonoBehaviour
    {
        [Tooltip("Глобальный Volume с профилем атмосферы (если пусто — найдётся сам).")]
        public Volume volume;
        [Tooltip("Сила цветокоррекции локации и времени суток (0 — только общий профиль).")]
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("Насколько теплее и ярче «золотой час» (рассвет и закат).")]
        [Range(0f, 1.5f)] public float goldenHour = 1f;
        [Tooltip("Насколько ночь холоднее и бесцветнее.")]
        [Range(0f, 1.5f)] public float night = 1f;

        /// <summary>Настройки «плёнки» локации.</summary>
        public struct Look
        {
            public float temperature, tint, saturation, contrast, exposure, bloom, vignette;
            public Color filter, shadows, highlights, bloomTint;
            public float splitBalance;
        }

        private static Look Base() => new Look
        {
            temperature = 4f, tint = 0f, saturation = -6f, contrast = 12f, exposure = 0.05f, bloom = 0.4f, vignette = 0.3f,
            filter = Color.white, shadows = new Color(0.44f, 0.49f, 0.58f), highlights = new Color(0.58f, 0.52f, 0.45f),
            bloomTint = new Color(1f, 0.92f, 0.84f), splitBalance = 0f,
        };

        /// <summary>«Плёнка» локации по её id (Locations.Ids).</summary>
        public static Look For(string id)
        {
            Look l = Base();
            switch (id)
            {
                case "canyon":
                    l.temperature = 16f; l.tint = 4f; l.saturation = 2f; l.contrast = 14f; l.exposure = 0.08f;
                    l.filter = new Color(1f, 0.97f, 0.92f); l.shadows = new Color(0.5f, 0.45f, 0.58f); l.highlights = new Color(0.62f, 0.5f, 0.38f);
                    l.bloomTint = new Color(1f, 0.86f, 0.7f); l.bloom = 0.45f; break;
                case "misty":
                    l.temperature = -10f; l.tint = -2f; l.saturation = -20f; l.contrast = 4f; l.exposure = 0.1f;
                    l.filter = new Color(0.95f, 0.98f, 1f); l.shadows = new Color(0.42f, 0.5f, 0.56f); l.highlights = new Color(0.52f, 0.53f, 0.52f);
                    l.bloomTint = new Color(0.92f, 0.96f, 1f); l.bloom = 0.6f; l.vignette = 0.36f; break;
                case "autumn":
                    l.temperature = 12f; l.tint = 2f; l.saturation = 4f; l.contrast = 12f; l.exposure = 0.06f;
                    l.filter = new Color(1f, 0.97f, 0.9f); l.shadows = new Color(0.46f, 0.45f, 0.56f); l.highlights = new Color(0.64f, 0.52f, 0.36f);
                    l.bloomTint = new Color(1f, 0.85f, 0.62f); l.bloom = 0.45f; break;
                case "winter":
                    l.temperature = -14f; l.tint = -1f; l.saturation = -10f; l.contrast = 10f; l.exposure = 0.12f;
                    l.filter = new Color(0.95f, 0.98f, 1.02f); l.shadows = new Color(0.4f, 0.47f, 0.62f); l.highlights = new Color(0.55f, 0.54f, 0.52f);
                    l.bloomTint = new Color(0.9f, 0.95f, 1f); l.bloom = 0.5f; break;
                default:                                               // alpine
                    l.temperature = 6f; l.saturation = -4f;
                    l.highlights = new Color(0.6f, 0.52f, 0.44f); break;
            }
            return l;
        }

        private VolumeProfile _p;
        private ColorAdjustments _adj;
        private WhiteBalance _wb;
        private SplitToning _split;
        private Bloom _bloom;
        private Vignette _vig;
        private Look _look;

        private void Start()
        {
            if (volume == null)
            {
#if UNITY_2023_1_OR_NEWER
                volume = FindFirstObjectByType<Volume>();
#else
                volume = FindObjectOfType<Volume>();
#endif
            }
            if (volume == null) { enabled = false; return; }
            _p = volume.profile;                                   // своя копия: ассет профиля не меняется
            _adj = Get<ColorAdjustments>(); _wb = Get<WhiteBalance>(); _split = Get<SplitToning>();
            _bloom = Get<Bloom>(); _vig = Get<Vignette>();
            _look = For(BackgroundThemes.Current);
            Apply();
        }

        private T Get<T>() where T : VolumeComponent
        {
            if (!_p.TryGet(out T c)) c = _p.Add<T>(false);
            c.active = true;
            return c;
        }

        private void LateUpdate() => Apply();

        /// <summary>«Золотой час»: 0…1 вокруг рассвета (~6:50) и заката (~18:30).</summary>
        public static float Golden(float h)
        {
            if (h < 0f) return 0f;
            float a = Mathf.Exp(-Mathf.Pow((h - 6.9f) / 0.9f, 2f)), b = Mathf.Exp(-Mathf.Pow((h - 18.5f) / 0.9f, 2f));
            return Mathf.Clamp01(Mathf.Max(a, b));
        }

        /// <summary>Ночь: 0 днём, 1 глубокой ночью (плавно в сумерках 19:30–21:00 и 4:30–6:00).</summary>
        public static float Night(float h)
        {
            if (h < 0f) return 0f;
            float evening = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(19.3f, 21f, h));
            float morning = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.5f, 6.1f, h));
            return h >= 12f ? evening : morning;
        }

        private void Apply()
        {
            if (_p == null) return;
            float hour = DayNightCycle.Hour;
            float g = Golden(hour) * goldenHour * strength, n = Night(hour) * night * strength;
            Look b = Base(), l = _look;
            float k = strength;

            float temp = Mathf.Lerp(b.temperature, l.temperature, k) + 10f * g - 12f * n;
            float tint = Mathf.Lerp(b.tint, l.tint, k) + 3f * g + 2f * n;
            float sat = Mathf.Lerp(b.saturation, l.saturation, k) + 6f * g - 22f * n;
            float con = Mathf.Lerp(b.contrast, l.contrast, k) + 4f * g + 4f * n;
            float exp = Mathf.Lerp(b.exposure, l.exposure, k) - 0.12f * n;
            Color filter = Color.Lerp(b.filter, l.filter, k);
            filter = Color.Lerp(filter, new Color(0.86f, 0.92f, 1.04f), 0.5f * n);     // лунный свет — холоднее

            if (_wb != null) { _wb.temperature.Override(Mathf.Clamp(temp, -100f, 100f)); _wb.tint.Override(Mathf.Clamp(tint, -100f, 100f)); }
            if (_adj != null)
            {
                _adj.saturation.Override(Mathf.Clamp(sat, -100f, 100f));
                _adj.contrast.Override(Mathf.Clamp(con, -100f, 100f));
                _adj.postExposure.Override(exp);
                _adj.colorFilter.Override(filter);
            }
            if (_split != null)
            {
                _split.shadows.Override(Color.Lerp(b.shadows, l.shadows, k));
                _split.highlights.Override(Color.Lerp(Color.Lerp(b.highlights, l.highlights, k), new Color(0.66f, 0.5f, 0.36f), 0.5f * g));
                _split.balance.Override(Mathf.Lerp(b.splitBalance, l.splitBalance, k) - 15f * n);
            }
            if (_bloom != null)
            {
                _bloom.intensity.Override(Mathf.Lerp(b.bloom, l.bloom, k) + 0.25f * g);
                _bloom.tint.Override(Color.Lerp(Color.Lerp(b.bloomTint, l.bloomTint, k), new Color(1f, 0.82f, 0.6f), 0.4f * g));
            }
            if (_vig != null) _vig.intensity.Override(Mathf.Lerp(b.vignette, l.vignette, k) + 0.08f * n);
        }
    }
}
