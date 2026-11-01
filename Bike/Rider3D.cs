using System.Collections.Generic;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// 3D-гонщик (Resources/Rider3D/rider3d.fbx: скелет + две сетки — ближняя сторона и дальние рука с ногой).
    /// Позу не анимирует сам: каждый кадр берёт суставы из BikeVisual (таз, плечи, локти, кисти на руле, колени,
    /// лодыжки, стопы на педалях, наклон головы) и ставит кости в эти точки — поэтому 3D-модель крутит педали,
    /// приседает, переносит вес и кивает ровно так же, как нарисованный гонщик.
    /// Дальняя рука и нога рисуются под байком, остальное — над ним (порядок как у нарисованного гонщика).
    /// В трюках BikeVisual отдаёт суставы уже в 3D (с глубиной, верх и низ тела повёрнуты на свои углы), при падении —
    /// по телам куклы RiderRagdoll; нарисованный гонщик прячется всегда, пока есть поза.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class Rider3D : MonoBehaviour
    {
        private const string ModelPath = "Rider3D/rider3d";
        private const string ShaderName = "MountainBike/Rider3D";

        private BikeVisual _vis;
        private GameObject _model;
        private bool _shown, _ok;
        private readonly List<Renderer> _nearR = new List<Renderer>(), _farR = new List<Renderer>();   // RiderNear / RiderFar
        private int _sortMode = -1;                                     // 0 — обычно, 1 — RiderFar над байком (спиной к нам, кэн-кэн)

        private class Bone
        {
            public Transform t, tail;
            public Quaternion restRot, restFrameInv;
            public Vector3 restDir;
        }
        private readonly Dictionary<string, Bone> _bones = new Dictionary<string, Bone>();
        private readonly Dictionary<string, Vector2> _armLen = new Dictionary<string, Vector2>();   // плечо, предплечье (м)
        private readonly Dictionary<string, Vector2> _legLen = new Dictionary<string, Vector2>();   // бедро, голень (м)
        // помощники сгиба (есть в модели — используем): поворот на половину угла локтя/колена, держат объём на сгибе
        private readonly List<(Transform h, Quaternion rest, Bone a, Bone b)> _helpers = new List<(Transform, Quaternion, Bone, Bone)>();

        private static readonly string[] Order =
        {
            "Hips", "Chest", "Neck", "Head",
            "Thigh.R", "Shin.R", "Foot.R", "Thigh.L", "Shin.L", "Foot.L",
            "UpperArm.R", "Forearm.R", "Hand.R", "UpperArm.L", "Forearm.L", "Hand.L",
        };

        private static readonly Dictionary<string, string> Tail = new Dictionary<string, string>
        {
            { "Hips", "Chest" }, { "Chest", "Neck" }, { "Neck", "Head" }, { "Head", "Head_end" },
            { "UpperArm.L", "Forearm.L" }, { "Forearm.L", "Hand.L" }, { "Hand.L", "Hand.L_end" },
            { "UpperArm.R", "Forearm.R" }, { "Forearm.R", "Hand.R" }, { "Hand.R", "Hand.R_end" },
            { "Thigh.L", "Shin.L" }, { "Shin.L", "Foot.L" }, { "Foot.L", "Foot.L_end" },
            { "Thigh.R", "Shin.R" }, { "Shin.R", "Foot.R" }, { "Foot.R", "Foot.R_end" },
        };

        // поперечные смещения суставов от плоскости байка (м): ближняя сторона — к камере
        private const float HeadDown = 14f;                            // наклон головы к дороге, °
        private const float AnkleMin = 55f, AnkleMax = 110f;           // угол голень–стопа, °: меньше — носок вниз, больше — вверх
        private const float HipZ = 0.09f, KneeZ = 0.11f, AnkleZ = 0.12f, ShoulderZ = 0.19f, ElbowZ = 0.25f;

        public static Rider3D Attach(BikeVisual vis)
        {
            GameObject prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)                                           // файл переименован при скачивании — берём любую модель из папки
            {
                foreach (GameObject g in Resources.LoadAll<GameObject>("Rider3D"))
                    if (g.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) { prefab = g; break; }
            }
            if (prefab == null)
            {
                Debug.LogWarning("[Rider3D] Нет модели Resources/" + ModelPath + " — остаётся нарисованный гонщик.");
                return null;
            }
            var go = new GameObject("Rider3D");
            go.transform.SetParent(vis.transform, false);
            var r = go.AddComponent<Rider3D>();
            r.Init(vis, prefab);
            return r;
        }

        private void Init(BikeVisual vis, GameObject prefab)
        {
            _vis = vis;
            _model = Instantiate(prefab, transform);
            _model.name = "Model";
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            foreach (Animator a in _model.GetComponentsInChildren<Animator>(true)) Destroy(a);   // позу ставим сами

            var all = new Dictionary<string, Transform>();
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
                if (!all.ContainsKey(t.name)) all[t.name] = t;

            foreach (string n in Order)
            {
                if (!all.TryGetValue(n, out Transform t))
                {
                    Debug.LogWarning("[Rider3D] В модели нет кости " + n + " — остаётся нарисованный гонщик.");
                    Destroy(gameObject);
                    return;
                }
                all.TryGetValue(Tail[n], out Transform tail);
                _bones[n] = new Bone { t = t, tail = tail };
            }

            // направления модели в покое (A-поза): вверх, вправо (правая рука), вперёд
            Vector3 up0 = (_bones["Head"].t.position - _bones["Hips"].t.position).normalized;
            Vector3 right0 = (_bones["UpperArm.R"].t.position - _bones["UpperArm.L"].t.position).normalized;
            Vector3 fwd0 = Vector3.Cross(right0, up0).normalized;
            foreach (string n in Order)
            {
                Bone b = _bones[n];
                Vector3 dir = b.tail != null ? b.tail.position - b.t.position : Vector3.zero;
                if (dir.sqrMagnitude < 1e-8f && b.t.parent != null) dir = b.t.position - b.t.parent.position;
                b.restDir = dir.normalized;
                Vector3 pole = IsFoot(n) ? up0 : fwd0;
                b.restRot = b.t.rotation;
                b.restFrameInv = Quaternion.Inverse(Frame(b.restDir, pole));
            }

            // плечо и предплечье поворачиваются вокруг оси локтя: опора — «наружу из локтя» (от линии плечо–запястье к локтю).
            // Тогда обе кости скручены одинаково и на сгибе рука не сужается («фантик»)
            foreach (string sd in new[] { "L", "R" })
            {
                Vector3 S0 = _bones["UpperArm." + sd].t.position, E0 = _bones["Forearm." + sd].t.position, W0 = _bones["Hand." + sd].t.position;
                Vector3 eb = ElbowOut(S0, E0, W0, -up0);
                foreach (string n in new[] { "UpperArm." + sd, "Forearm." + sd, "Hand." + sd })
                    _bones[n].restFrameInv = Quaternion.Inverse(Frame(_bones[n].restDir, eb));
            }

            foreach (string sd in new[] { "L", "R" })
                foreach ((string h, string a, string b) in new[] { ("Elbow." + sd, "UpperArm." + sd, "Forearm." + sd),
                                                                  ("Knee." + sd, "Thigh." + sd, "Shin." + sd),
                                                                  ("HipBend." + sd, "Hips", "Thigh." + sd) })
                    if (all.TryGetValue(h, out Transform ht))
                        _helpers.Add((ht, ht.rotation, _bones[a], _bones[b]));

            foreach (string sd in new[] { "L", "R" })
                _legLen[sd] = new Vector2(Vector3.Distance(_bones["Thigh." + sd].t.position, _bones["Shin." + sd].t.position),
                                          Vector3.Distance(_bones["Shin." + sd].t.position, _bones["Foot." + sd].t.position));
            foreach (string sd in new[] { "L", "R" })
                _armLen[sd] = new Vector2(Vector3.Distance(_bones["UpperArm." + sd].t.position, _bones["Forearm." + sd].t.position),
                                          Vector3.Distance(_bones["Forearm." + sd].t.position, _bones["Hand." + sd].t.position));

            SetupRenderers();
            _model.SetActive(false);
            _shown = false;
            _ok = true;
        }

        private static bool IsFoot(string n) => n.StartsWith("Foot");

        /// <summary>Куда «смотрит» локоть: от середины плечо–запястье к локтю (рука почти прямая — запасное направление).</summary>
        /// Рука почти прямая — локоть плавно уходит вниз (down), без рывков и перекручивания: иначе на прямой руке
        /// направление «из локтя» скачет, кость поворачивается вокруг своей оси и рука сплющивается.
        private static Vector3 ElbowOut(Vector3 s, Vector3 e, Vector3 w, Vector3 down)
        {
            Vector3 axis = (w - s).normalized;
            Vector3 v = e - (s + w) * 0.5f;
            v -= axis * Vector3.Dot(v, axis);
            Vector3 d = down - axis * Vector3.Dot(down, axis);
            if (d.sqrMagnitude > 1e-6f) v += d.normalized * 0.04f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : down;
        }
        private static bool IsArm(string n) => n.StartsWith("UpperArm") || n.StartsWith("Forearm") || n.StartsWith("Hand");

        private static Quaternion Frame(Vector3 dir, Vector3 pole)
        {
            pole -= dir * Vector3.Dot(pole, dir);
            // опора вдоль кости (или нулевая) — запасная поперёк кости. Кость может смотреть прямо в камеру (кэн-кэн — ноги
            // к зрителю, вдоль Z): тогда «поперёк Z» вырождается, берём «поперёк вертикали»
            if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(dir, Vector3.forward);
            if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(dir, Vector3.up);
            return Quaternion.LookRotation(dir, pole.normalized);
        }

        // ------------------------------------------------------------------ материалы и порядок отрисовки

        private void SetupRenderers()
        {
            if (QualitySettings.skinWeights < SkinWeights.FourBones) QualitySettings.skinWeights = SkinWeights.FourBones;
            Shader sh = Shader.Find(ShaderName);
            var cache = new Dictionary<string, Material>();
            foreach (SkinnedMeshRenderer r in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.updateWhenOffscreen = true;                              // кости уходят далеко от позы покоя
                // все 4 влияния костей на вершину: в мобильном качестве Unity по умолчанию берёт только 2 — тогда у локтя
                // теряется кость-помощник сгиба, и чем сильнее сгиб, тем тоньше рука
                r.quality = SkinQuality.Bone4;
                r.sortingLayerID = _vis.RiderSortingLayer;
                bool far = r.name.Contains("Far");
                r.sortingOrder = far ? _vis.RiderFarOrder : _vis.RiderNearOrder;
                (far ? _farR : _nearR).Add(r);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (sh == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string key = mats[i] != null ? mats[i].name.Replace(" (Instance)", "") : "skin";
                    if (!cache.TryGetValue(key, out Material m)) cache[key] = m = MakeMaterial(sh, key);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            if (sh == null) Debug.LogWarning("[Rider3D] Не найден шейдер " + ShaderName + " — материалы из FBX.");
        }

        private static Material MakeMaterial(Shader sh, string name)
        {
            var m = new Material(sh) { name = "Rider3D_" + name };
            // цвет (sRGB), блик, резкость блика, хром, контур-подсветка
            Color c; float spec = 0.04f, gloss = 12f, chrome = 0f, rim = 0.12f;
            switch (name)
            {
                case "skin": c = C(214, 168, 138); spec = 0.06f; gloss = 10f; rim = 0.18f; break;
                case "tshirt_white": c = C(208, 206, 200); spec = 0.02f; rim = 0.06f; break;
                case "shorts_black": c = C(34, 34, 38); spec = 0.05f; rim = 0.16f; break;
                case "socks_black": c = C(24, 24, 27); break;
                case "gloves_black": c = C(22, 22, 25); spec = 0.10f; gloss = 18f; break;
                case "shoes": c = C(44, 44, 49); spec = 0.08f; gloss = 16f; break;
                case "shoe_sole": c = C(205, 203, 196); break;
                case "laces": c = C(34, 34, 38); break;
                case "helmet_shell": c = C(40, 41, 45); spec = 0.18f; gloss = 22f; rim = 0.18f; break;
                case "helmet_rim": c = C(24, 25, 28); spec = 0.30f; gloss = 30f; break;
                case "helmet_visor": c = C(22, 23, 26); spec = 0.55f; gloss = 70f; rim = 0.2f; break;
                case "helmet_shield_opaque": c = C(14, 15, 18); spec = 0.9f; gloss = 110f; rim = 0.25f; break;
                case "helmet_chrome": case "helmet_screw": c = C(225, 227, 232); spec = 0.8f; gloss = 90f; chrome = 1f; break;
                case "helmet_liner": c = C(90, 92, 98); break;
                case "helmet_vent_mesh": c = C(70, 72, 78); spec = 0.2f; break;
                case "helmet_dark": c = C(8, 8, 9); break;
                default: c = C(128, 128, 128); break;
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Spec", spec);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Chrome", chrome);
            m.SetFloat("_Rim", rim);
            return m;
        }

        private static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        // ------------------------------------------------------------------ поза

        private void LateUpdate()
        {
            if (_vis == null) { Destroy(gameObject); return; }
            if (!_ok) return;
            BikeVisual.RiderPose3D p = _vis.Pose3D;
            if (p.valid != _shown)
            {
                _shown = p.valid;
                _model.SetActive(_shown);
                _vis.SetRider2DVisible(!_shown);
            }
            if (!_shown) return;
            UpdateSorting(p.backTurned, p.farLegFront);
            Apply(p);
        }

        private void OnDisable()
        {
            if (_vis != null && _ok) _vis.SetRider2DVisible(true);
            _shown = false;
            if (_model != null) _model.SetActive(false);
        }

        /// <summary>
        /// Порядок отрисовки: обычно дальние рука и нога (RiderFar) — под байком, остальное — над ним. Спиной к нам
        /// (360) и в кэн-кэне (дальняя нога перекинута через раму) RiderFar тоже над байком — сразу под ближней стороной,
        /// а кто кого закрывает, решает глубина. Корпус (RiderNear) всегда над байком: если увести его под байк, трубы
        /// рамы и колесо рисуются поверх футболки, и она выглядит пустой.
        /// </summary>
        private void UpdateSorting(bool back, bool farFront)
        {
            int mode = back || farFront ? 1 : 0;
            if (mode == _sortMode) return;
            _sortMode = mode;
            int near = _vis.RiderNearOrder;
            int far = mode == 1 ? _vis.RiderNearOrder - 1 : _vis.RiderFarOrder;
            foreach (Renderer r in _nearR) if (r != null) r.sortingOrder = near;
            foreach (Renderer r in _farR) if (r != null) r.sortingOrder = far;
        }

        /// <summary>Оси части тела: «вперёд» (куда смотрит грудь/таз/голова) и «вправо» (правый бок — к нам в обычной езде).</summary>
        private struct Axes
        {
            public Vector3 fwd, right;
            public Axes(Vector3 up, Vector3 f) { fwd = f.normalized; right = Vector3.Cross(up, fwd).normalized; }
            /// <summary>Перпендикуляр к d в плоскости тела (поперёк «вправо»), в сторону toward.</summary>
            public Vector3 InPlane(Vector3 d, Vector3 toward)
            {
                Vector3 q = Vector3.Cross(right, d);
                return Vector3.Dot(q, toward) >= 0f ? q : -q;
            }
        }

        private void Apply(BikeVisual.RiderPose3D p)
        {
            Vector3 fwd = p.forward.normalized, up = p.up.normalized;
            bool f3 = p.full3D;                                             // трюк: суставы уже в 3D
            // езда и кукла — всё тело в плоскости байка; трюк — грудь, таз и голова повёрнуты каждый на свой угол
            Axes body = new Axes(up, fwd);
            Axes U = f3 ? new Axes(up, p.fwdU) : body, L = f3 ? new Axes(up, p.fwdL) : body;
            // голова: в трюке — свой угол, у куклы — по телу головы (fwdH задан), в езде — по раме
            Axes Hd = (f3 || p.fwdH.sqrMagnitude > 1e-6f) && Vector3.Cross(up, p.fwdH).sqrMagnitude > 1e-6f ? new Axes(up, p.fwdH) : body;

            // корпус. Точка таза у нарисованного гонщика сидя — на седле (там, где ягодицы), а у модели кость таза —
            // тазобедренный сустав, он выше седла и чуть впереди: поднимаем, иначе ягодицы и подол уходят под седло
            Vector3 spine0 = (p.sh - p.hip).normalized;
            float s = Mathf.Clamp01(p.seated);
            Vector3 hip = p.hip + spine0 * (0.035f + 0.05f * s) + L.InPlane(spine0, L.fwd) * (0.015f + 0.02f * s);
            Vector3 spine = (p.sh - hip).normalized;
            Vector3 neckBase = p.sh + spine * 0.045f;
            Set("Hips", hip, spine, L.InPlane(spine, L.fwd));
            Set("Chest", hip + (p.sh - hip) * 0.5f, spine, U.InPlane(spine, U.fwd));
            Vector3 neckDir = (p.head - neckBase).normalized;
            Set("Neck", neckBase, neckDir, U.InPlane(neckDir, U.fwd));
            // взгляд на дорогу: у модели большой козырёк и окно — ровно поставленная голова выглядит «в небо», поэтому
            // макушка чуть вперёд (HeadDown°); сидя — строго по раме, без кивков
            float hd = HeadDown * Mathf.Deg2Rad;
            Vector3 level = (up * Mathf.Cos(hd) + Hd.fwd * Mathf.Sin(hd)).normalized;
            Vector3 hu = p.headUp.normalized;
            Vector3 riding = (hu * Mathf.Cos(hd) + Hd.InPlane(hu, Hd.fwd).normalized * Mathf.Sin(hd)).normalized;
            Vector3 headUp = Vector3.Slerp(riding, level, s).normalized;
            Set("Head", p.head, headUp, Hd.InPlane(headUp, Hd.fwd));

            // ноги: ближняя — правая
            Leg("R", +1f, p.kneeN, p.ankleN, p.footDirN, hip, L, up, f3);
            Leg("L", -1f, p.kneeF, p.ankleF, p.footDirF, hip, L, up, f3);
            // руки
            Arm("R", +1f, p.shN, p.elbowN, p.handN, p.gripZ, U, up, hip, f3);
            Arm("L", -1f, p.shF, p.elbowF, p.handF, p.gripZ, U, up, hip, f3);
            foreach (var hp in _helpers)
            {
                Quaternion qa = hp.a.t.rotation * Quaternion.Inverse(hp.a.restRot);
                Quaternion qb = hp.b.t.rotation * Quaternion.Inverse(hp.b.restRot);
                hp.h.rotation = Quaternion.Slerp(qa, qb, 0.5f) * hp.rest;
                hp.h.position = hp.b.t.position;
            }
        }

        private void Leg(string s, float side, Vector3 knee, Vector3 ankle, Vector3 footDir, Vector3 hip, Axes L, Vector3 up, bool f3)
        {
            // езда: точки в плоскости байка — разносим вбок; трюк: колено и голеностоп уже в 3D, вбок — только тазобедренный
            Vector3 H = hip + L.right * (HipZ * side);
            Vector3 Kg = f3 ? knee : knee + L.right * (KneeZ * side);
            Vector3 A0 = f3 ? ankle : ankle + L.right * (AnkleZ * side);
            // колено — по длинам бедра и голени самой модели (в плоскости сгиба нарисованной ноги): кости не тянутся
            // и не сжимаются, голень всегда выходит из колена под тем же углом, на который повернулся сустав
            Vector3 K, A;
            TwoBone(H, A0, _legLen[s].x, _legLen[s].y, Kg - H, L.fwd, out K, out A);
            Vector3 d1 = (K - H).normalized, d2 = (A - K).normalized;
            // перед колена — туда, куда согнута нога (от линии таз–лодыжка к колену), а не «вперёд по раме»:
            // в вилли и в приседе бедро смотрит вперёд-вверх, и опора по раме переворачивала ногу коленом назад
            Vector3 bend = K - (H + A) * 0.5f;
            Vector3 kneeFront = bend.sqrMagnitude > 1e-6f ? bend : L.fwd;
            // трюк: нога может уйти из плоскости тела (кэн-кэн — через раму) — опора прямо «перед колена»
            Set("Thigh." + s, H, d1, f3 ? kneeFront : L.InPlane(d1, kneeFront));
            Set("Shin." + s, K, d2, f3 ? kneeFront : L.InPlane(d2, kneeFront));
            Vector3 fd = (footDir.normalized - up * 0.35f).normalized;       // носок чуть вниз — как у стопы модели
            // голеностоп как у человека: носок вниз не больше ~35°, вверх не больше ~20° от прямого угла к голени
            Vector3 ax = Vector3.Cross(d2, fd);
            if (ax.sqrMagnitude > 1e-6f)
            {
                float ang = Mathf.Clamp(Vector3.Angle(d2, fd), AnkleMin, AnkleMax);
                fd = (Quaternion.AngleAxis(ang, ax.normalized) * d2).normalized;
            }
            // верх стопы — к колену (в трюке стопа повёрнута вместе с ногой)
            Set("Foot." + s, A, fd, f3 ? -d2 : L.InPlane(fd, up));
        }

        /// <summary>Двухзвенник: из S к цели T с длинами L1, L2; сгиб — в сторону hint (запасное — fallback).</summary>
        private static void TwoBone(Vector3 S, Vector3 T, float L1, float L2, Vector3 hint, Vector3 fallback, out Vector3 mid, out Vector3 end)
        {
            Vector3 st = T - S;
            float d = Mathf.Clamp(st.magnitude, Mathf.Abs(L1 - L2) + 0.01f, (L1 + L2) * 0.999f);
            Vector3 u = st.sqrMagnitude > 1e-8f ? st.normalized : Vector3.down;
            float a = (L1 * L1 - L2 * L2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
            Vector3 bend = hint - u * Vector3.Dot(hint, u);
            if (bend.sqrMagnitude < 1e-8f) bend = fallback - u * Vector3.Dot(fallback, u);
            mid = S + u * a + bend.normalized * h;
            end = mid + (T - mid).normalized * L2;
        }

        private void Arm(string s, float side, Vector3 sh, Vector3 elbow, Vector3 hand, float gripZ, Axes U, Vector3 up, Vector3 hip, bool f3)
        {
            // езда: точки в плоскости байка — разносим вбок; трюк: плечо, локоть и кисть уже в 3D
            Vector3 S = f3 ? sh : sh + U.right * (ShoulderZ * side);
            Vector3 Eg = f3 ? elbow : elbow + U.right * (ElbowZ * side);
            Vector3 G = f3 ? hand : hand + U.right * (gripZ * side);
            Vector3 handDir = ((G - Eg).normalized - up * 0.35f).normalized;   // кисть обхватывает руль сверху
            Vector3 T = G - handDir * 0.075f;                                  // куда должно прийти запястье
            // локоть — своим решением с длинами костей модели (в плоскости сгиба нарисованной руки): плечо и предплечье
            // никогда не растягиваются и не сжимаются — рука одного размера и прямая, и в полном сгибе
            float Lu = _armLen[s].x, Lf = _armLen[s].y;
            Vector3 st = T - S;
            float d = Mathf.Clamp(st.magnitude, Mathf.Abs(Lu - Lf) + 0.01f, (Lu + Lf) * 0.999f);
            Vector3 u = st.sqrMagnitude > 1e-8f ? st.normalized : -up;
            float a = (Lu * Lu - Lf * Lf + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, Lu * Lu - a * a));
            Vector3 bend = (Eg - S) - u * Vector3.Dot(Eg - S, u);
            // запасное направление локтя (рука прямая) — в плоскости тела, к тазу: локоть всегда смотрит к телу и вниз,
            // при любом наклоне байка (в высоком вилли «вниз по раме» почти совпадало с рукой — рука перекручивалась в нитку)
            Vector3 toBody = Vector3.Cross(U.right, u);
            if (Vector3.Dot(toBody, hip - S) < 0f) toBody = -toBody;
            if (toBody.sqrMagnitude < 1e-8f) toBody = -up;
            // локоть гнётся только «по-человечески»: назад-вниз от груди. Когда нарисованная рука почти прямая (no-footer —
            // байк уходит вниз, руки вытянуты), нарисованный локоть лежит почти на линии плечо–кисть, и от малейшего сдвига
            // оказывался с другой стороны — локоть выгибался назад. Не та сторона — зеркалим; почти прямая — берём анатомию
            Vector3 spineA = (sh - hip).sqrMagnitude > 1e-8f ? (sh - hip).normalized : up;
            Vector3 chestFwd = Vector3.Cross(U.right, spineA);                 // куда смотрит грудь (в трюке — повёрнута с верхом тела)
            Vector3 anat = -chestFwd - spineA * 0.6f;                          // локоть — назад и вниз
            anat -= u * Vector3.Dot(anat, u);
            if (anat.sqrMagnitude < 1e-4f) anat = toBody;
            if (bend.magnitude < 0.03f) bend = anat;                           // рука почти прямая — сторона по анатомии
            else if (Vector3.Dot(bend, anat) < 0f) bend -= 2f * Vector3.Dot(bend, anat.normalized) * anat.normalized;   // зеркало
            if (bend.sqrMagnitude < 1e-8f) bend = toBody;
            Vector3 E = S + u * a + bend.normalized * h;
            Vector3 W = E + (T - E).normalized * Lf;
            Vector3 d1 = (E - S).normalized, d2 = (W - E).normalized;
            Vector3 eb = ElbowOut(S, E, W, toBody);
            // плечо, предплечье и кисть — с одной опорой: без скрутки между ними рука одной толщины в любой позе
            Set("UpperArm." + s, S, d1, eb);
            Set("Forearm." + s, E, d2, eb);
            Set("Hand." + s, W, handDir, eb);
        }

        private static bool Bad(Vector3 v) => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                                               float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);

        private void Set(string n, Vector3 pos, Vector3 dir, Vector3 pole)
        {
            Bone b = _bones[n];
            if (dir.sqrMagnitude < 1e-8f || Bad(dir) || Bad(pos)) return;     // вырожденная поза — кость остаётся как была
            if (Bad(pole)) pole = Vector3.zero;
            b.t.position = pos;
            b.t.rotation = Frame(dir.normalized, pole) * b.restFrameInv * b.restRot;
        }
    }
}