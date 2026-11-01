using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Окно (магазин, лента локаций) открывается не рывком: плавно проявляется и чуть «приближается» (1.04 → 1).
    /// Срабатывает каждый раз, когда окно включают. Время — реальное.
    /// </summary>
    public class PanelFade : MonoBehaviour
    {
        public float time = 0.28f;
        [Tooltip("С какого масштаба окно «приближается» к обычному.")]
        public float fromScale = 1.04f;

        private CanvasGroup _g;
        private float _t;

        private void Awake()
        {
            _g = GetComponent<CanvasGroup>();
            if (_g == null) _g = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            _t = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_t >= time) return;
            _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Apply(Mathf.Clamp01(_t / Mathf.Max(0.01f, time)));
        }

        private void Apply(float k)
        {
            float e = 1f - Mathf.Pow(1f - k, 3f);
            if (_g != null) _g.alpha = e;
            transform.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, e);
        }
    }
}
