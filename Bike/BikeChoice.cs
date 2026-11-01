using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Байки магазина: у каждого свои настройки физики (BikeConfig), внешний вид (BikeSkin) и цена в монетах.
    /// До того, как байк соберёт физику и спрайты, подставляет выбранный (PlayerBike), если он куплен; иначе — первый
    /// (бесплатный). Висит на объекте байка; заполняет сборщик сцены. Порядок в списке = порядок карточек в магазине.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // раньше BikeController2D / RiderBody / BikeVisual
    public class BikeChoice : MonoBehaviour
    {
        [System.Serializable]
        public class Model
        {
            [Tooltip("Постоянный идентификатор (по нему хранится выбор и покупка) — не меняйте после выхода игры.")]
            public string id = "bike";
            public string displayName = "Bike";
            [TextArea] public string description = "";
            [Tooltip("Цена в монетах. 0 — бесплатный (всегда есть у игрока).")]
            public int price;
            public BikeConfig config;
            public BikeSkin skin;
            [Tooltip("Картинка для карточки в магазине.")]
            public Sprite preview;
            [Header("Передачи (свои у каждого байка)")]
            [Tooltip("Зубьев на звезде у шатунов (0 — как в настройках Config).")]
            public int chainring;
            [Tooltip("Звёзды кассеты от самой лёгкой передачи к самой тяжёлой: сколько чисел — столько передач " +
                     "(пусто — как в настройках Config).")]
            public int[] cassette = new int[0];
            [Tooltip("С какой передачи начинать заезд (0 — как в настройках Config).")]
            public int startGear;

            public bool Owned => price <= 0 || PlayerWallet.Owns(id);
        }

        public Model[] models = new Model[0];

        /// <summary>Выбранная модель (если не куплена или не найдена — первая).</summary>
        public Model Current => Resolve(models);

        /// <summary>
        /// Настройки байка с его передачами: если у модели заданы свои передачи, а в ассете настроек другие — в игре
        /// используется копия настроек с передачами модели (ассет не меняется).
        /// </summary>
        public static BikeConfig WithGears(Model m)
        {
            BikeConfig c = m.config;
            if (c == null || m.cassette == null || m.cassette.Length == 0) return c;
            int ring = m.chainring > 0 ? m.chainring : c.chainring;
            int start = m.startGear > 0 ? m.startGear : c.startGear;
            bool same = c.cassette != null && c.cassette.Length == m.cassette.Length && c.chainring == ring && c.startGear == start;
            for (int i = 0; same && i < m.cassette.Length; i++) same = c.cassette[i] == m.cassette[i];
            if (same) return c;
            BikeConfig copy = Instantiate(c);
            copy.name = c.name;
            copy.chainring = ring;
            copy.cassette = (int[])m.cassette.Clone();
            copy.startGear = Mathf.Clamp(start, 1, m.cassette.Length);
            return copy;
        }

        public static Model Resolve(Model[] list)
        {
            if (list == null || list.Length == 0) return null;
            string id = PlayerBike.Id;
            foreach (Model m in list)
                if (m != null && m.id == id && m.Owned) return m;
            foreach (Model m in list)
                if (m != null && m.price <= 0) return m;
            return list[0];
        }

        private void Awake()
        {
            Model m = Current;
            if (m == null) return;
            var bike = GetComponent<BikeController2D>();
            var visual = GetComponent<BikeVisual>();
            if (bike != null && m.config != null) bike.config = WithGears(m);
            if (visual != null && m.skin != null) visual.skin = m.skin;
        }
    }
}
