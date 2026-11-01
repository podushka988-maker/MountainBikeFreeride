using System.Collections.Generic;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Передний план: у самой камеры, по нижнему краю кадра, изредка проплывают тёмные размытые силуэты травы,
    /// папоротников, сухих стеблей (своих у каждой локации) — будто снимаем из травы у трассы. Они ближе к зрителю,
    /// чем трасса, поэтому движутся быстрее неё (параллакс > 1) — сразу видна глубина и скорость.
    /// Стоят по одному-два, с большими промежутками, и закрывают только низ кадра (до ~15% высоты), байк не загораживают.
    /// Ночью — почти чёрные, днём — с лёгкой дымкой. Висит на камере; картинки — Art/FX/Foreground/&lt;локация&gt;
    /// (Tools/art/environment/foreground.py).
    /// </summary>
    [DefaultExecutionOrder(116)]
    public class ForegroundLayer : MonoBehaviour
    {
        [System.Serializable]
        public class Set
        {
            public string location;
            public Sprite[] sprites;
        }

        public Set[] sets;
        public Material material;
        [Tooltip("Поверх трассы, травы перед колёсами (61) и частиц — под интерфейсом.")]
        public int sortingOrder = 70;
        [Tooltip("Во сколько раз быстрее трассы движется передний план (ближе к камере — больше).")]
        public float parallax = 1.7f;
        [Tooltip("Промежуток между силуэтами (в своих метрах переднего плана).")]
        public Vector2 gap = new Vector2(9f, 24f);
        [Tooltip("Высота силуэта относительно половины высоты кадра (0.4 — 20% кадра).")]
        public float heightOfHalfScreen = 0.62f;
        [Tooltip("Насколько силуэт уходит за нижний край кадра (доля его высоты).")]
        [Range(0f, 0.8f)] public float sink = 0.25f;
        [Range(0f, 1f)] public float opacity = 0.92f;

        private class Item { public SpriteRenderer sr; public float u, scale; }

        private readonly List<Item> _items = new List<Item>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private Sprite[] _sprites;
        private Camera _cam;
        private float _nextU;
        private System.Random _rnd;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            string loc = BackgroundThemes.Current;
            if (sets != null)
                foreach (Set s in sets)
                    if (s != null && s.location == loc && s.sprites != null && s.sprites.Length > 0) _sprites = s.sprites;
            if (_sprites == null && sets != null && sets.Length > 0) _sprites = sets[0].sprites;
            if (_sprites == null || _sprites.Length == 0) { enabled = false; return; }
            _rnd = new System.Random(System.Environment.TickCount);
            // первый силуэт — не сразу у старта, а через полэкрана
            _nextU = _cam.transform.position.x * parallax + _cam.orthographicSize * _cam.aspect * 0.8f;
        }

        private float Rand(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

        private void LateUpdate()
        {
            if (_cam == null) return;
            float halfH = _cam.orthographicSize, halfW = halfH * _cam.aspect;
            Vector3 c = _cam.transform.position;
            float camU = c.x * parallax;                       // «камера» переднего плана
            float zoom = halfH / 4.2f;                         // при отдалении силуэты на экране того же размера
            float margin = halfW * 0.6f + 2f;

            // новые силуэты справа (едем вправо) — и слева, если откатились назад
            while (_nextU - camU < halfW + margin)
            {
                Spawn(_nextU);
                _nextU += Rand(gap.x, gap.y) * zoom;
            }
            float night = AtmosphereGrade.Night(DayNightCycle.Hour);
            Color col = Color.Lerp(new Color(1f, 1f, 1f, opacity), new Color(0.55f, 0.6f, 0.72f, opacity), night);
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Item it = _items[i];
                float dx = it.u - camU;                                    // смещение от центра кадра (в мире — так же)
                if (dx < -halfW - margin * 2f) { Recycle(i); continue; }
                float s = it.scale * zoom;
                float h = it.sr.sprite.bounds.size.y * s;
                it.sr.transform.position = new Vector3(c.x + dx, c.y - halfH - h * sink, 0f);
                it.sr.transform.localScale = new Vector3(Mathf.Sign(it.sr.transform.localScale.x) * s, s, 1f);
                it.sr.color = col;
            }
        }

        private void Spawn(float u)
        {
            SpriteRenderer sr = _pool.Count > 0 ? _pool.Pop() : MakeRenderer();
            sr.gameObject.SetActive(true);
            sr.sprite = _sprites[_rnd.Next(_sprites.Length)];
            float baseH = Mathf.Max(0.01f, sr.sprite.bounds.size.y);
            float scale = heightOfHalfScreen * 4.2f / baseH * Rand(0.8f, 1.2f);
            sr.transform.localScale = new Vector3(_rnd.NextDouble() < 0.5 ? -1f : 1f, 1f, 1f);
            _items.Add(new Item { sr = sr, u = u, scale = scale });
        }

        private SpriteRenderer MakeRenderer()
        {
            var go = new GameObject("Foreground");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        private void Recycle(int i)
        {
            SpriteRenderer sr = _items[i].sr;
            sr.gameObject.SetActive(false);
            _pool.Push(sr);
            _items.RemoveAt(i);
        }

        private void OnDestroy()
        {
            foreach (Item it in _items) if (it.sr != null) Destroy(it.sr.gameObject);
            foreach (SpriteRenderer sr in _pool) if (sr != null) Destroy(sr.gameObject);
        }
    }
}
