using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Плавные переходы между сценами: экран мягко темнеет, сцена загружается, потом изображение плавно проявляется
    /// (вместо резкой смены кадра). При запуске игры первая сцена тоже проявляется из темноты.
    /// Затемнение — поверх всего (свой Canvas), живёт между сценами; время — реальное (замедление не мешает).
    /// Вызов: ScreenFader.LoadScene("FreeRide").
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        /// <summary>Цвет затемнения — тёплый почти чёрный (как тени в игре), не «мёртвый» чёрный.</summary>
        public static readonly Color FadeColor = new Color(0.04f, 0.03f, 0.035f, 1f);
        public static float FadeOutTime = 0.3f, FadeInTime = 0.5f;

        private static ScreenFader _i;
        private CanvasGroup _group;
        private bool _busy;

        /// <summary>Идёт переход (затемнение, загрузка или проявление).</summary>
        public static bool Busy => _i != null && _i._busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            ScreenFader f = Ensure();
            f._group.alpha = 1f;
            f.StartCoroutine(f.Fade(1f, 0f, FadeInTime * 1.4f));
        }

        private static ScreenFader Ensure()
        {
            if (_i != null) return _i;
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<ScreenFader>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;                        // поверх любого интерфейса
            _i._group = go.AddComponent<CanvasGroup>();
            _i._group.alpha = 0f;
            _i._group.blocksRaycasts = false;
            var img = new GameObject("Fade", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.color = FadeColor;
            img.raycastTarget = true;
            return _i;
        }

        /// <summary>Загрузить сцену с затемнением. Повторные вызовы во время перехода игнорируются.</summary>
        public static void LoadScene(string scene)
        {
            ScreenFader f = Ensure();
            if (f._busy) return;
            f.StartCoroutine(f.Go(scene));
        }

        private IEnumerator Go(string scene)
        {
            _busy = true;
            _group.blocksRaycasts = true;                       // во время перехода кнопки не нажимаются
            yield return Fade(_group.alpha, 1f, FadeOutTime);
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;                                  // кадр на запуск сцены (Start у объектов)
            yield return Fade(1f, 0f, FadeInTime);
            _busy = false;
        }

        private IEnumerator Fade(float from, float to, float time)
        {
            _group.blocksRaycasts = true;
            float t = 0f;
            while (t < time)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // долгий кадр загрузки не «съедает» анимацию
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, time));
                _group.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }
            _group.alpha = to;
            _group.blocksRaycasts = to > 0.5f;
        }
    }
}
