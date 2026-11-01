using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Шлемы и одежда в магазине главного меню. Над лентой карточек — три вкладки: BIKES · HELMETS · OUTFITS.
    /// Карточки шлемов и одежды — копии карточки байка (та же доска, цена, КУПИТЬ / ВЫБРАТЬ / ВЫБРАНО) с картинкой из
    /// Resources/RiderGear (icon_&lt;id&gt;.png), без названий; по возрастанию цены. Купить — монеты (PlayerWallet), выбрать — PlayerGear: гонщик на фоне
    /// переодевается сразу. Вешается сам на MainMenu при загрузке сцены — сцену пересобирать не нужно.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class GearShop : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            AttachAll();
            SceneManager.sceneLoaded += (s, m) => AttachAll();
        }

        private static void AttachAll()
        {
#if UNITY_2023_1_OR_NEWER
            MainMenu menu = FindAnyObjectByType<MainMenu>();
#else
            MainMenu menu = FindObjectOfType<MainMenu>();
#endif
            if (menu != null && menu.GetComponent<GearShop>() == null) menu.gameObject.AddComponent<GearShop>();
        }

        private enum Tab { Bikes, Helmets, Outfits }

        private MainMenu _menu;
        private RectTransform _content;
        private readonly List<BikeCard> _bikeCards = new List<BikeCard>();
        private readonly List<GearCard> _helmets = new List<GearCard>(), _outfits = new List<GearCard>();
        private readonly List<(Tab tab, Image img, Text label)> _tabs = new List<(Tab, Image, Text)>();
        private Tab _tab = Tab.Bikes;
        private float _bikesWidth, _firstX, _gap = 470f, _cardY;
        private AudioSource _sfx;
        private float _plateShake;
        private Vector2 _plateBase;
        private bool _wasOpen;

        private void Start()
        {
            _menu = GetComponent<MainMenu>();
            if (_menu == null || _menu.shopPanel == null || _menu.shopScroll == null || _menu.shopScroll.content == null) { Destroy(this); return; }
            _content = _menu.shopScroll.content;
            _content.GetComponentsInChildren(true, _bikeCards);
            if (_bikeCards.Count == 0) { Destroy(this); return; }
            _bikesWidth = _content.sizeDelta.x;
            Vector2 p0 = ((RectTransform)_bikeCards[0].transform).anchoredPosition;
            _firstX = p0.x; _cardY = p0.y;
            if (_bikeCards.Count > 1) _gap = ((RectTransform)_bikeCards[1].transform).anchoredPosition.x - p0.x;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false; _sfx.spatialBlend = 0f;
            if (_menu.coinsPlate != null) _plateBase = _menu.coinsPlate.anchoredPosition;

            BikeCard template = _bikeCards[0];
            Font font = template.priceText != null ? template.priceText.font : null;
            foreach (PlayerGear.Helmet h in PlayerGear.Helmets) _helmets.Add(MakeCard(template, h.id, h.price));
            foreach (PlayerGear.Outfit o in PlayerGear.Outfits) _outfits.Add(MakeCard(template, o.id, o.price));
            _helmets.Sort((a, b) => a.price.CompareTo(b.price));                 // по возрастанию цены
            _outfits.Sort((a, b) => a.price.CompareTo(b.price));
            MakeTabs(font);
            Show(Tab.Bikes);
            _wasOpen = _menu.shopPanel.activeSelf;
        }

        // ------------------------------------------------------------------ карточки

        private GearCard MakeCard(BikeCard template, string id, int price)
        {
            var holder = new GameObject("holder"); holder.SetActive(false);               // копия без Awake карточки байка
            GameObject go = Instantiate(template.gameObject, holder.transform);
            go.name = "Gear " + id;
            BikeCard bc = go.GetComponent<BikeCard>();
            var gc = go.AddComponent<GearCard>();
            gc.id = id; gc.price = price; gc.shop = this;
            gc.card = go.GetComponent<Image>();
            gc.button = bc != null && bc.button != null ? bc.button : null;
            gc.priceText = bc != null ? bc.priceText : null;
            gc.priceCoin = bc != null ? bc.priceCoin : null;
            Transform pic = go.transform.Find("Bike");
            gc.picture = pic != null ? pic.GetComponent<Image>() : null;
            if (bc != null) DestroyImmediate(bc);
            if (gc.picture != null)
            {
                Texture2D tex = Resources.Load<Texture2D>("RiderGear/icon_" + id);
                if (tex != null) gc.picture.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                gc.picture.preserveAspect = true;
                gc.picture.color = tex != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
            go.SetActive(false);
            go.transform.SetParent(_content, false);
            Destroy(holder);
            gc.Init();
            return gc;
        }

        // ------------------------------------------------------------------ вкладки

        private void MakeTabs(Font font)
        {
            Image plateImg = _menu.coinsPlate != null ? _menu.coinsPlate.GetComponent<Image>() : null;
            Sprite plate = plateImg != null ? plateImg.sprite : null;
            var row = new GameObject("Tabs", typeof(RectTransform));
            var rrt = (RectTransform)row.transform;
            rrt.SetParent(_menu.shopPanel.transform, false);
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, 46f);
            rrt.sizeDelta = new Vector2(1100f, 90f);
            string[] names = { "BIKES", "HELMETS", "OUTFITS" };
            for (int i = 0; i < 3; i++)
            {
                Tab t = (Tab)i;
                var go = new GameObject("Tab " + names[i], typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(rrt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - 1) * 350f, 0f);
                rt.sizeDelta = new Vector2(320f, 80f);
                var img = go.AddComponent<Image>();
                img.sprite = plate;
                img.type = Image.Type.Simple;
                if (plate == null) img.color = new Color(0.45f, 0.3f, 0.2f, 0.95f);
                var b = go.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => Show(t));
                go.AddComponent<ButtonPress>();
                var lgo = new GameObject("Label", typeof(RectTransform));
                var lrt = (RectTransform)lgo.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                lrt.anchoredPosition = new Vector2(0f, 3f);
                var tx = lgo.AddComponent<Text>();
                tx.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tx.fontSize = 36; tx.alignment = TextAnchor.MiddleCenter; tx.text = names[i];
                tx.raycastTarget = false;
                var ol = lgo.AddComponent<Outline>(); ol.effectColor = new Color(0.16f, 0.09f, 0.05f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
                _tabs.Add((t, img, tx));
            }
        }

        private void Show(Tab tab)
        {
            _tab = tab;
            foreach (BikeCard c in _bikeCards) c.gameObject.SetActive(tab == Tab.Bikes);
            foreach (GearCard c in _helmets) c.gameObject.SetActive(tab == Tab.Helmets);
            foreach (GearCard c in _outfits) c.gameObject.SetActive(tab == Tab.Outfits);
            List<GearCard> list = tab == Tab.Helmets ? _helmets : tab == Tab.Outfits ? _outfits : null;
            if (list == null) _content.sizeDelta = new Vector2(_bikesWidth, _content.sizeDelta.y);
            else
            {
                for (int i = 0; i < list.Count; i++) list[i].Place(new Vector2(_firstX + i * _gap, _cardY));
                _content.sizeDelta = new Vector2(2f * _firstX + _gap * Mathf.Max(0, list.Count - 1), _content.sizeDelta.y);
            }
            foreach (var (t, img, label) in _tabs)
            {
                bool on = t == tab;
                img.color = on ? Color.white : new Color(0.62f, 0.58f, 0.55f, 1f);
                label.color = on ? new Color(1f, 0.85f, 0.38f) : new Color(0.92f, 0.86f, 0.78f);
                img.rectTransform.localScale = Vector3.one * (on ? 1.06f : 0.94f);
            }
            RefreshCards();
            CenterSelected(list);
        }

        private void CenterSelected(List<GearCard> list)
        {
            ScrollRect sr = _menu.shopScroll;
            sr.velocity = Vector2.zero;
            if (list == null) return;
            string sel = list == _helmets ? PlayerGear.CurrentHelmet.id : PlayerGear.CurrentOutfit.id;
            GearCard c = list.Find(g => g.id == sel);
            Canvas.ForceUpdateCanvases();
            float cw = _content.rect.width, vw = sr.viewport != null ? sr.viewport.rect.width : cw;
            if (c == null || cw <= vw) { sr.horizontalNormalizedPosition = 0f; return; }
            float x = ((RectTransform)c.transform).anchoredPosition.x;
            sr.horizontalNormalizedPosition = Mathf.Clamp01((x - vw / 2f) / (cw - vw));
        }

        private void RefreshCards()
        {
            string h = PlayerGear.CurrentHelmet.id, o = PlayerGear.CurrentOutfit.id;
            foreach (GearCard c in _helmets) c.Refresh(c.id == h, _menu);
            foreach (GearCard c in _outfits) c.Refresh(c.id == o, _menu);
        }

        // ------------------------------------------------------------------ купить / выбрать

        public void CardButton(GearCard card)
        {
            if (card.Owned) Pick(card);
            else Buy(card);
        }

        private void Buy(GearCard card)
        {
            if (card.Owned) return;
            if (!PlayerWallet.TrySpend(card.price))
            {
                card.Denied();
                _plateShake = 1f;
                PlaySfx(_menu.deniedSound);
                return;
            }
            PlayerWallet.SetOwned(card.id);
            card.Purchased();
            PlaySfx(_menu.buySound);
            RefreshCards();
        }

        public void Pick(GearCard card)
        {
            if (!card.Owned) return;
            PlayerGear.Select(card.id);
            RefreshCards();
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && _sfx != null) _sfx.PlayOneShot(clip);
        }

        private void Update()
        {
            // магазин открыли заново (MainMenu ставит ленту на выбранный байк) — показать текущую вкладку как надо
            bool open = _menu != null && _menu.shopPanel != null && _menu.shopPanel.activeSelf;
            if (open && !_wasOpen && _tab != Tab.Bikes) Show(_tab);
            _wasOpen = open;
            if (_plateShake > 0f && _menu != null && _menu.coinsPlate != null)
            {
                _plateShake = Mathf.Max(0f, _plateShake - Time.unscaledDeltaTime / 0.45f);
                _menu.coinsPlate.anchoredPosition = _plateBase + new Vector2(Mathf.Sin(Time.unscaledTime * 60f) * 10f * _plateShake, 0f);
            }
        }
    }
}
