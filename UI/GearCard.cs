using UnityEngine;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Карточка шлема или комплекта одежды в магазине: та же доска, что у байков (картинка, цена, КУПИТЬ / ВЫБРАТЬ / ВЫБРАНО),
    /// без названия. Создаёт GearShop (копией карточки байка).
    /// </summary>
    public class GearCard : MonoBehaviour
    {
        public string id;
        public int price;
        public Image card, button, picture, priceCoin;
        public Text priceText;
        [HideInInspector] public GearShop shop;

        public bool Owned => PlayerGear.Owns(id, price);

        private float _shake, _pop;
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePos;
        private Color _priceColor = Color.white;

        public void Init()
        {
            Button b = button != null ? button.GetComponent<Button>() : null;
            if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(() => { if (shop != null) shop.CardButton(this); }); }
            Button cb = card != null ? card.GetComponent<Button>() : null;
            if (cb != null) { cb.onClick.RemoveAllListeners(); cb.onClick.AddListener(() => { if (shop != null && Owned) shop.Pick(this); }); }
            _baseScale = transform.localScale;
            if (priceText != null) _priceColor = priceText.color;
            LayoutPrice();
        }

        /// <summary>Место карточки в ленте (запоминаем — к нему возвращается тряска).</summary>
        public void Place(Vector2 pos)
        {
            ((RectTransform)transform).anchoredPosition = pos;
            _basePos = pos;
        }

        public void LayoutPrice()
        {
            if (priceText == null) return;
            bool free = price <= 0;
            priceText.text = free ? "FREE" : PlayerWallet.Format(price);
            priceText.color = free ? new Color(1f, 0.93f, 0.82f) : new Color(1f, 0.85f, 0.38f);
            if (priceCoin != null) priceCoin.gameObject.SetActive(!free);
            float coin = free || priceCoin == null ? 0f : priceCoin.rectTransform.sizeDelta.x;
            float gap = coin > 0f ? 10f : 0f;
            float w = priceText.preferredWidth;
            float total = coin + gap + w;
            priceText.rectTransform.anchoredPosition = new Vector2(-total / 2f + coin + gap + w / 2f, priceText.rectTransform.anchoredPosition.y);
            if (priceCoin != null) priceCoin.rectTransform.anchoredPosition = new Vector2(-total / 2f + coin / 2f, priceCoin.rectTransform.anchoredPosition.y);
        }

        public void Refresh(bool selected, MainMenu m)
        {
            if (card != null && m.cardSprite != null) card.sprite = selected ? m.cardSelectedSprite : m.cardSprite;
            if (button == null) return;
            Sprite s = selected ? m.selectedSprite : Owned ? m.selectSprite : (m.buySprite != null ? m.buySprite : m.selectSprite);
            if (s != null) button.sprite = s;
        }

        public void Denied() => _shake = 1f;
        public void Purchased() => _pop = 1f;

        private void Update()
        {
            if (_shake <= 0f && _pop <= 0f) return;
            float dt = Time.unscaledDeltaTime;
            var rt = (RectTransform)transform;
            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - dt / 0.45f);
                rt.anchoredPosition = _basePos + new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 14f * _shake, 0f);
                if (priceText != null) priceText.color = Color.Lerp(_priceColor, new Color(1f, 0.3f, 0.25f), Mathf.Clamp01(_shake * 1.6f));
                if (_shake <= 0f) { rt.anchoredPosition = _basePos; if (priceText != null) priceText.color = _priceColor; }
            }
            if (_pop > 0f)
            {
                _pop = Mathf.Max(0f, _pop - dt / 0.5f);
                transform.localScale = _baseScale * (1f + Mathf.Sin(_pop * Mathf.PI) * 0.08f);
                if (_pop <= 0f) transform.localScale = _baseScale;
            }
        }
    }
}
