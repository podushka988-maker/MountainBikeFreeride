using System;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Экипировка гонщика из магазина: шлемы (3D-модели в Resources/RiderGear) и комплекты одежды (цвета футболки, шорт,
    /// носков, перчаток и кед). Что куплено — PlayerWallet (как байки), что выбрано — PlayerPrefs. Действует и в меню, и в игре:
    /// RiderGear надевает выбранное на 3D-гонщика (и красит нарисованного).
    /// </summary>
    public static class PlayerGear
    {
        public class Helmet
        {
            public string id, name, model;     // model — имя FBX в Resources/RiderGear (пусто — родной шлем модели)
            public int price;
            public Color main;                 // основной цвет — для нарисованного гонщика
        }

        public class Outfit
        {
            public string id, name;
            public int price;
            public Color shirt, shorts, socks, gloves, shoes;
        }

        private static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        public static readonly Helmet[] Helmets =
        {
            new Helmet { id = "helmet_stock", name = "Stock", model = "helmet_stock", price = 0, main = C(40, 41, 45) },
            new Helmet { id = "helmet_enduro", name = "Vent Red", model = "helmet_enduro", price = 100, main = C(196, 28, 30) },
            new Helmet { id = "helmet_shard", name = "Shard MX", model = "helmet_shard", price = 400, main = C(200, 200, 204) },
            new Helmet { id = "helmet_arctic", name = "Arctic", model = "helmet_arctic", price = 550, main = C(196, 199, 203) },
            new Helmet { id = "helmet_venom", name = "Venom", model = "helmet_venom", price = 900, main = C(214, 236, 24) },
            new Helmet { id = "helmet_violet", name = "Violet", model = "helmet_violet", price = 1600, main = C(96, 50, 146) },
            new Helmet { id = "helmet_storm", name = "Storm", model = "helmet_storm", price = 2500, main = C(26, 44, 82) },
            new Helmet { id = "helmet_flame", name = "Flame", model = "helmet_flame", price = 3000, main = C(170, 30, 120) },
            new Helmet { id = "helmet_splash", name = "Red Splash", model = "helmet_splash", price = 3500, main = C(214, 30, 32) },
        };

        public static readonly Outfit[] Outfits =
        {
            new Outfit { id = "outfit_classic", name = "Classic", price = 0,
                         shirt = C(208, 206, 200), shorts = C(34, 34, 38), socks = C(24, 24, 27), gloves = C(22, 22, 25), shoes = C(44, 44, 49) },
            new Outfit { id = "outfit_trail", name = "Trail", price = 150,
                         shirt = C(84, 112, 64), shorts = C(66, 60, 50), socks = C(40, 40, 40), gloves = C(32, 32, 32), shoes = C(70, 58, 46) },
            new Outfit { id = "outfit_fire", name = "Fire", price = 300,
                         shirt = C(190, 34, 32), shorts = C(28, 28, 30), socks = C(190, 34, 32), gloves = C(28, 28, 30), shoes = C(40, 40, 44) },
            new Outfit { id = "outfit_ocean", name = "Ocean", price = 500,
                         shirt = C(36, 74, 136), shorts = C(118, 122, 130), socks = C(228, 228, 228), gloves = C(36, 74, 136), shoes = C(228, 228, 228) },
            new Outfit { id = "outfit_sunset", name = "Sunset", price = 600,
                         shirt = C(240, 120, 36), shorts = C(70, 40, 112), socks = C(252, 206, 48), gloves = C(70, 40, 112), shoes = C(240, 120, 36) },
            new Outfit { id = "outfit_neon", name = "Neon", price = 800,
                         shirt = C(206, 230, 30), shorts = C(24, 24, 26), socks = C(206, 230, 30), gloves = C(24, 24, 26), shoes = C(24, 24, 26) },
            new Outfit { id = "outfit_shard", name = "Pink Shard", price = 1000,
                         shirt = C(62, 62, 66), shorts = C(24, 24, 26), socks = C(226, 44, 150), gloves = C(226, 44, 150), shoes = C(236, 236, 236) },
            new Outfit { id = "outfit_violet", name = "Royal Violet", price = 1200,
                         shirt = C(100, 54, 154), shorts = C(24, 24, 28), socks = C(236, 236, 236), gloves = C(100, 54, 154), shoes = C(236, 236, 236) },
            new Outfit { id = "outfit_arctic", name = "Arctic", price = 1500,
                         shirt = C(226, 228, 231), shorts = C(166, 170, 177), socks = C(240, 240, 240), gloves = C(198, 201, 206), shoes = C(240, 240, 240) },
            new Outfit { id = "outfit_inferno", name = "Inferno", price = 2000,
                         shirt = C(18, 18, 20), shorts = C(176, 26, 30), socks = C(246, 124, 30), gloves = C(250, 206, 56), shoes = C(176, 26, 30) },
        };

        public const string HelmetKey = "gear_helmet", OutfitKey = "gear_outfit";

        /// <summary>Выбрали другой шлем или одежду (гонщик в меню переодевается сразу).</summary>
        public static event Action Changed;

        public static bool Owns(string id, int price) => price <= 0 || PlayerWallet.Owns(id);

        public static Helmet CurrentHelmet
        {
            get
            {
                string id = PlayerPrefs.GetString(HelmetKey, "");
                foreach (Helmet h in Helmets) if (h.id == id && Owns(h.id, h.price)) return h;
                return Helmets[0];
            }
        }

        public static Outfit CurrentOutfit
        {
            get
            {
                string id = PlayerPrefs.GetString(OutfitKey, "");
                foreach (Outfit o in Outfits) if (o.id == id && Owns(o.id, o.price)) return o;
                return Outfits[0];
            }
        }

        public static void Select(string id)
        {
            bool helmet = id.StartsWith("helmet_");
            PlayerPrefs.SetString(helmet ? HelmetKey : OutfitKey, id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
