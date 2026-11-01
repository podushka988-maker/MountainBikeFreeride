using UnityEngine;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Надпись трюка с анимацией (как всплывающие надписи в Bike Mayhem):
    ///   появление — «выпрыгивает» из маленькой с перелётом (0.4 → 1.15 → 1), проявляется, чуть поднимается и качнётся;
    ///   новый трюк, пока надпись на экране, — она «подпрыгивает» (пульс) и держится дольше;
    ///   исчезновение — уплывает вверх, уменьшается и гаснет.
    /// Время — реальное (не зависит от замедления). Прозрачность — через CanvasGroup (вместе с контуром и тенью).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class TrickLabel : MonoBehaviour
    {
        [Tooltip("Появление, с.")]
        public float popTime = 0.38f;
        [Tooltip("Исчезновение, с.")]
        public float fadeTime = 0.45f;
        [Tooltip("Насколько надпись поднимается при появлении (снизу) и при исчезновении (вверх), пикс.")]
        public float rise = 26f, riseOut = 60f;
        [Tooltip("Наклон при появлении, ° (качнётся и выпрямится).")]
        public float wobble = 7f;
        [Tooltip("Пульс при добавлении трюка к надписи.")]
        public float punch = 0.22f;

        private enum State { Hidden, In, Hold, Out }

        private Text _text;
        private CanvasGroup _group;
        private RectTransform _rt;
        private Vector2 _base;
        private State _state = State.Hidden;
        private float _age, _hold, _pulse;
        private float _side = 1f;                     // в какую сторону качнуться (по очереди)

        public bool Showing => _state != State.Hidden;

        private void Awake()
        {
            _text = GetComponent<Text>();
            _rt = (RectTransform)transform;
            _base = _rt.anchoredPosition;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; _group.interactable = false;
            HideNow();
        }

        /// <summary>Показать надпись на hold секунд (без появления и исчезновения). Уже на экране — сменить текст с пульсом.</summary>
        public void Show(string text, float hold)
        {
            if (_text == null) Awake();
            _text.text = text;
            _hold = Mathf.Max(0.2f, hold);
            if (_state == State.In || _state == State.Hold)
            {
                _pulse = 1f;                              // уже видно — «подпрыгнуть» и держать заново
                if (_state == State.Hold) _age = 0f;
                return;
            }
            // с нуля (или во время исчезновения — заново, из текущей прозрачности)
            _state = State.In;
            _age = 0f;
            _pulse = 0f;
            _side = -_side;
        }

        public void HideNow()
        {
            _state = State.Hidden;
            if (_text != null) _text.text = "";
            if (_group != null) _group.alpha = 0f;
            if (_rt != null) { _rt.anchoredPosition = _base; _rt.localScale = Vector3.one; _rt.localRotation = Quaternion.identity; }
        }

        private void Update()
        {
            if (_state == State.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            _age += dt;
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt / 0.3f);
            float pulse = 1f + punch * Mathf.Sin(_pulse * Mathf.PI) * _pulse;   // быстрый подскок и мягкий возврат

            float scale = 1f, alpha = 1f, y = 0f, rot = 0f;
            switch (_state)
            {
                case State.In:
                {
                    float t = Mathf.Clamp01(_age / popTime);
                    scale = Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(t, 2.2f));
                    alpha = Mathf.Clamp01(t * 3f);
                    y = -rise * (1f - EaseOutCubic(t));
                    rot = _side * wobble * (1f - t) * Mathf.Cos(t * Mathf.PI * 1.5f);
                    if (t >= 1f) { _state = State.Hold; _age = 0f; }
                    break;
                }
                case State.Hold:
                {
                    // еле заметно «дышит», пока висит
                    scale = 1f + 0.02f * Mathf.Sin(_age * 6f);
                    if (_age >= _hold) { _state = State.Out; _age = 0f; }
                    break;
                }
                case State.Out:
                {
                    float t = Mathf.Clamp01(_age / fadeTime);
                    float e = t * t;
                    scale = Mathf.Lerp(1f, 0.82f, e);
                    alpha = 1f - EaseOutCubic(t);
                    y = riseOut * e;
                    if (t >= 1f) { HideNow(); return; }
                    break;
                }
            }
            _group.alpha = alpha;
            _rt.anchoredPosition = _base + new Vector2(0f, y);
            _rt.localScale = Vector3.one * (scale * pulse);
            _rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        private static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }

        private static float EaseOutBack(float t, float s)
        {
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
