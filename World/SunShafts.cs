using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Лучи света сквозь лес: мягкий веер полос из-за края кадра — оттуда, где сейчас солнце (утром слева, вечером справа,
    /// ночью — еле заметный холодный лунный). Висят между дальним лесом с туманом и ближним лесом: ближние ели
    /// заслоняют лучи — видна глубина. Ярче всего в «золотой час», днём слабее; в тумане — гуще и холоднее,
    /// в каньоне — реже (открытое небо). Сам диск солнца не рисуется. Полосы медленно колышутся и «дышат».
    /// Висит на камере; текстура лучей строится из кода (картинка не нужна).
    /// </summary>
    [DefaultExecutionOrder(115)]   // после камеры, параллакса и времени суток
    public class SunShafts : MonoBehaviour
    {
        public Material material;
        [Tooltip("Порядок отрисовки: между туманом (−65) и ближним лесом (−60).")]
        public int sortingOrder = -62;
        [Tooltip("Общая яркость лучей.")]
        [Range(0f, 2f)] public float intensity = 1f;
        [Tooltip("Яркость: в золотой час, днём, ночью (луна).")]
        public float goldenAlpha = 0.5f, dayAlpha = 0.2f, nightAlpha = 0.07f;
        [Tooltip("Размер веера относительно высоты кадра.")]
        public float size = 2.1f;

        private Camera _cam;
        private SpriteRenderer _a, _b;
        private float _locK = 1f;
        private Color _locTint = Color.white;
        private float _side = -1f;           // −1 — солнце слева, +1 — справа (плавно)

        private void Start()
        {
            _cam = GetComponent<Camera>();
            _a = Make("Sun Shafts A", BuildTexture(11, 9), sortingOrder);
            _b = Make("Sun Shafts B", BuildTexture(23, 6), sortingOrder);
            switch (BackgroundThemes.Current)
            {
                case "misty": _locK = 1.45f; _locTint = new Color(0.9f, 0.95f, 1f); break;
                case "canyon": _locK = 0.6f; _locTint = new Color(1f, 0.9f, 0.78f); break;
                case "autumn": _locK = 1.15f; _locTint = new Color(1f, 0.92f, 0.76f); break;
                case "winter": _locK = 0.85f; _locTint = new Color(0.92f, 0.96f, 1f); break;
                default: _locK = 1f; _locTint = Color.white; break;
            }
            _side = LeftSide(DayNightCycle.Hour) ? -1f : 1f;
        }

        private SpriteRenderer Make(string name, Sprite sp, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.sortingOrder = order; sr.color = Color.clear;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        /// <summary>
        /// Веер лучей: источник — левый верхний угол картинки, полосы расходятся вниз-вправо. Каждая полоса — своя ширина
        /// и яркость, у источника мягко проявляются, дальше гаснут; у краёв веера — тише.
        /// </summary>
        private static Sprite BuildTexture(int seed, int beams)
        {
            const int w = 256, h = 192;
            var rnd = new System.Random(seed);
            var ang = new float[beams]; var wid = new float[beams]; var amp = new float[beams];
            for (int i = 0; i < beams; i++)
            {
                ang[i] = Mathf.Lerp(-62f, -18f, (i + 0.5f) / beams) + (float)(rnd.NextDouble() - 0.5) * 6f;
                wid[i] = 1.2f + (float)rnd.NextDouble() * 3.2f;
                amp[i] = 0.35f + (float)rnd.NextDouble() * 0.65f;
            }
            var px = new Color32[w * h];
            float diag = Mathf.Sqrt(w * w + h * h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 8f, dy = y - (h + 8f);                       // источник чуть за углом картинки
                    float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, d = Mathf.Sqrt(dx * dx + dy * dy) / diag;
                    float v = 0f;
                    for (int i = 0; i < beams; i++)
                    {
                        float t = (a - ang[i]) / wid[i];
                        v += amp[i] * Mathf.Exp(-t * t);
                    }
                    float spread = Mathf.Clamp01(1f - Mathf.Abs(a + 40f) / 34f);   // края веера тише
                    float along = Mathf.SmoothStep(0f, 1f, d / 0.12f) * Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                    float al = Mathf.Clamp01(v * 0.7f * spread * along);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(al * 255f));
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 1f), h);   // 1 м высоты, опора — угол-источник
        }

        private void LateUpdate()
        {
            if (_cam == null || _a == null) return;
            float hour = DayNightCycle.Hour;
            float g = AtmosphereGrade.Golden(hour), n = AtmosphereGrade.Night(hour);
            float alpha = (Mathf.Lerp(dayAlpha, goldenAlpha, g) * (1f - n) + nightAlpha * n) * intensity * _locK;
            Color col = Color.Lerp(Color.Lerp(new Color(1f, 0.97f, 0.9f), new Color(1f, 0.82f, 0.58f), g), new Color(0.7f, 0.8f, 1f), n) * _locTint;

            float target = LeftSide(hour) ? -1f : 1f;
            _side = Mathf.MoveTowards(_side, target, Time.deltaTime * 0.25f);
            float fade = Mathf.Abs(_side);                                  // во время смены стороны лучи гаснут
            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
            Vector3 c = _cam.transform.position; c.z = 0f;
            float s = size * 2f * halfH;
            float t = Time.time;
            Place(_a, c, halfW, halfH, s, Mathf.Sign(_side), Mathf.Sin(t * 0.17f) * 2.2f + Mathf.Sin(t * 0.41f) * 0.8f,
                  col, alpha * fade * (0.82f + 0.18f * Mathf.Sin(t * 0.33f)));
            Place(_b, c, halfW, halfH, s * 1.12f, Mathf.Sign(_side), Mathf.Sin(t * 0.13f + 2f) * 2.8f - 3f,
                  col, alpha * fade * 0.7f * (0.75f + 0.25f * Mathf.Sin(t * 0.27f + 1.3f)));
        }

        /// <summary>
        /// С какой стороны свет: солнце утром слева, после полудня справа (до конца заката); ночью луна вечером слева,
        /// под утро справа — как свет на гонщике (DayNightCycle). Стороны меняются, когда лучи и так слабые.
        /// </summary>
        private static bool LeftSide(float h) => h < 0f || (h >= 5.5f && h < 12.5f) || h >= 19.5f;

        private static void Place(SpriteRenderer sr, Vector3 c, float halfW, float halfH, float size, float side, float sway, Color col, float a)
        {
            if (a < 0.002f) { if (sr.enabled) sr.enabled = false; return; }
            if (!sr.enabled) sr.enabled = true;
            // источник — над верхним углом кадра со стороны солнца; веер падает внутрь кадра
            sr.transform.position = c + new Vector3(side * (halfW + halfH * 0.15f), halfH * 1.12f, 0f);
            sr.transform.localScale = new Vector3(-side * size, size, 1f);   // слева — как нарисован, справа — зеркально
            sr.transform.rotation = Quaternion.Euler(0f, 0f, sway * -side);
            col.a = a;
            sr.color = col;
        }
    }
}
