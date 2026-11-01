using UnityEngine;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Появление главного меню: вывеска с названием опускается сверху на верёвках и пружинисто покачивается,
    /// доски-кнопки по очереди «выпрыгивают» (из маленьких, с перелётом), табличка монет выезжает справа, рекорд проявляется.
    /// Играет при запуске игры и при возврате из заезда — не каждый раз, когда бот на фоне падает и меню перезапускается.
    /// Висит на объекте MainMenu; ссылки задаёт сборщик сцены.
    /// </summary>
    public class MenuIntro : MonoBehaviour
    {
        public RectTransform title;
        public RectTransform[] buttons;
        public RectTransform coins;
        public Graphic record;
        [Tooltip("Насколько высоко над местом начинается вывеска, пикс.")]
        public float titleDrop = 460f;

        /// <summary>Сыграть появление при следующей загрузке меню (запуск игры, возврат из заезда).</summary>
        public static bool PlayNext = true;

        private float _t = -1f;
        private Vector2 _titleBase, _coinsBase;
        private Vector2[] _btnBase;
        private CanvasGroup[] _btnGroup;
        private ButtonPress[] _btnPress;
        private CanvasGroup _titleGroup, _coinsGroup;
        private Color _recordColor;

        private void Start()
        {
            if (!PlayNext) { enabled = false; return; }
            PlayNext = false;
            _t = 0f;
            if (title != null) { _titleBase = title.anchoredPosition; _titleGroup = Group(title); }
            if (coins != null) { _coinsBase = coins.anchoredPosition; _coinsGroup = Group(coins); }
            int n = buttons != null ? buttons.Length : 0;
            _btnBase = new Vector2[n]; _btnGroup = new CanvasGroup[n]; _btnPress = new ButtonPress[n];
            for (int i = 0; i < n; i++)
            {
                if (buttons[i] == null) continue;
                _btnBase[i] = buttons[i].anchoredPosition;
                _btnGroup[i] = Group(buttons[i]);
                _btnPress[i] = buttons[i].GetComponent<ButtonPress>();
            }
            if (record != null) _recordColor = record.color;
            Step(0f);
        }

        private static CanvasGroup Group(RectTransform rt)
        {
            var g = rt.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Step(_t);
            if (_t > 2.2f) { Step(99f); enabled = false; }
        }

        private void Step(float t)
        {
            // вывеска: падает и пружинит на верёвках (затухающие колебания)
            if (title != null)
            {
                float u = Mathf.Max(0f, t - 0.1f);
                float y = titleDrop * Mathf.Exp(-5.2f * u) * Mathf.Cos(9.5f * u);
                if (u <= 0f) y = titleDrop;
                title.anchoredPosition = _titleBase + new Vector2(0f, y);
                _titleGroup.alpha = Mathf.Clamp01(u / 0.2f);
            }
            // доски-кнопки: по очереди, из маленьких с перелётом, чуть снизу
            for (int i = 0; i < _btnBase.Length; i++)
            {
                if (buttons[i] == null) continue;
                float u = Mathf.Clamp01((t - 0.45f - 0.11f * i) / 0.42f);
                float k = EaseOutBack(u);
                buttons[i].anchoredPosition = _btnBase[i] + new Vector2(0f, -50f * (1f - k));
                if (_btnPress[i] != null) _btnPress[i].introScale = Mathf.LerpUnclamped(0.6f, 1f, k);
                else buttons[i].localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, k);
                _btnGroup[i].alpha = Mathf.Clamp01(u * 2.5f);
            }
            // монеты — выезжают справа
            if (coins != null)
            {
                float u = Mathf.Clamp01((t - 0.7f) / 0.5f);
                float e = 1f - Mathf.Pow(1f - u, 3f);
                coins.anchoredPosition = _coinsBase + new Vector2(420f * (1f - e), 0f);
                _coinsGroup.alpha = Mathf.Clamp01(u * 2f);
            }
            if (record != null)
            {
                Color c = _recordColor; c.a *= Mathf.Clamp01((t - 1.0f) / 0.5f);
                record.color = c;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float s = 1.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
