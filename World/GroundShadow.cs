using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Мягкие тени на земле под колёсами и под гонщиком: «приклеивают» байк к трассе. Тень лежит вдоль склона, у земли —
    /// плотная и узкая, в прыжке — шире, светлее и пропадает метра через 2.5 (видно, как высоко летишь).
    /// После падения тень гонщика — под куклой. Днём тени плотнее, в сумерках и ночью — слабее.
    /// Висит на объекте Bike; текстура тени строится из кода.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class GroundShadow : MonoBehaviour
    {
        public BikeController2D bike;
        public TerrainGenerator terrain;
        public Material material;
        [Tooltip("Порядок отрисовки: поверх земли, под травой за байком (5).")]
        public int sortingOrder = 4;
        [Tooltip("Плотность тени у земли.")]
        [Range(0f, 1f)] public float opacity = 0.42f;
        [Tooltip("Выше этой высоты над землёй тени нет, м.")]
        public float fadeHeight = 2.5f;
        [Tooltip("Ширина тени колеса и гонщика у земли, м.")]
        public float wheelWidth = 0.75f, bodyWidth = 1.5f;

        private SpriteRenderer _rear, _front, _body;
        private BikeVisual _visual;
        private static Sprite _blob;

        private void Start()
        {
            if (bike == null) bike = GetComponent<BikeController2D>();
            _visual = bike != null ? bike.GetComponent<BikeVisual>() : null;
            if (_blob == null) _blob = BuildBlob();
            _body = Make("Shadow Body");
            _rear = Make("Shadow Rear");
            _front = Make("Shadow Front");
        }

        private SpriteRenderer Make(string name)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _blob; sr.sortingOrder = sortingOrder; sr.color = Color.clear;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        private void OnDestroy()
        {
            foreach (SpriteRenderer sr in new[] { _rear, _front, _body })
                if (sr != null) Destroy(sr.gameObject);
        }

        /// <summary>Мягкое пятно 1×1 м: плотное в середине, плавно гаснет к краям (по горизонтали дольше, чем по вертикали).</summary>
        private static Sprite BuildBlob()
        {
            const int w = 128, h = 32;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[y * w + x] = new Color32(16, 14, 20, (byte)(a * 255f));
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);   // 1 × 0.25 м
        }

        private void LateUpdate()
        {
            if (bike == null || terrain == null || _rear == null) return;
            float hour = DayNightCycle.Hour;
            float light = 1f - 0.55f * AtmosphereGrade.Night(hour);
            float r = bike.WheelRadius;
            Place(_rear, bike.rearWheel.position, r, wheelWidth, light);
            Place(_front, bike.frontWheel.position, r, wheelWidth, light);
            Vector2 body = _visual != null ? _visual.RiderCenter : (Vector2)bike.frame.position + Vector2.up * 0.7f;
            Place(_body, body, 0.85f, bodyWidth, light * 0.7f);
        }

        /// <summary>Тень точки p (на высоте lift над её «низом»): на земле под ней, вдоль склона.</summary>
        private void Place(SpriteRenderer sr, Vector2 p, float lift, float width, float light)
        {
            float g = terrain.HeightAt(p.x);
            float h = Mathf.Max(0f, p.y - lift - g);
            float k = Mathf.Clamp01(1f - h / Mathf.Max(0.1f, fadeHeight));
            if (k <= 0.001f) { if (sr.enabled) sr.enabled = false; return; }
            if (!sr.enabled) sr.enabled = true;
            float slope = (terrain.HeightAt(p.x + 0.25f) - terrain.HeightAt(p.x - 0.25f)) / 0.5f;
            float wide = width * (1f + 0.9f * (1f - k));                       // выше — шире и мягче
            sr.transform.position = new Vector3(p.x, g - 0.015f, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(wide, 0.55f + 0.4f * (1f - k), 1f);
            sr.color = new Color(1f, 1f, 1f, opacity * k * k * light);
        }
    }
}
