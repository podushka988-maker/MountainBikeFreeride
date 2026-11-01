using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BikeGame
{
    /// <summary>
    /// Надевает на 3D-гонщика выбранную в магазине экипировку (PlayerGear): шлем — отдельная модель из Resources/RiderGear
    /// (сетка висит на кости Head гонщика и поворачивается вместе с головой; окраска — текстура &lt;модель&gt;_paint.png; родной шлем прячется), одежда — цвета материалов футболки,
    /// шорт, носков, перчаток и кед. Шлем висит на кости Head — поворачивается с головой в езде, трюках (360) и при падении.
    /// Вешается сам на каждого Rider3D при загрузке сцены — в сцену ничего добавлять не нужно.
    /// </summary>
    public class RiderGear : MonoBehaviour
    {
        private const string ShaderName = "MountainBike/RiderGear";      // Resources/RiderGear/RiderGear.shader

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            AttachAll();
            SceneManager.sceneLoaded += (s, m) => AttachAll();
        }

        private static void AttachAll()
        {
#if UNITY_2023_1_OR_NEWER
            Rider3D[] riders = FindObjectsByType<Rider3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            Rider3D[] riders = FindObjectsOfType<Rider3D>(true);
#endif
            foreach (Rider3D r in riders)
                if (r.GetComponent<RiderGear>() == null) r.gameObject.AddComponent<RiderGear>();
        }

        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        private readonly List<(SkinnedMeshRenderer r, int slot, Material m)> _stockHelmet = new List<(SkinnedMeshRenderer, int, Material)>();
        private readonly Dictionary<string, Material> _clothes = new Dictionary<string, Material>();
        private SkinnedMeshRenderer _near;
        private GameObject _helmet;
        private string _helmetModel = "";
        private Material _hidden;
        private BikeVisual _vis;
        private Transform _model;
        private bool _ready;

        private void Start()
        {
            _vis = GetComponentInParent<BikeVisual>();
            Transform model = transform.Find("Model");
            if (model == null) return;
            _model = model;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (!_bones.ContainsKey(t.name)) _bones[t.name] = t;
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.name.Contains("Far") || _near == null) _near = r;       // ближняя сетка (с головой) — её порядок отрисовки
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    string key = Key(m.name);
                    if (key.StartsWith("helmet_")) _stockHelmet.Add((r, i, m));
                    else if (IsCloth(key) && !_clothes.ContainsKey(key)) _clothes[key] = m;
                }
            }
            Shader gs = Shader.Find(ShaderName);
            if (gs == null) Debug.LogWarning("[RiderGear] Не найден шейдер " + ShaderName + " (Resources/RiderGear/RiderGear.shader).");
            else if (_stockHelmet.Count > 0)
            {
                _hidden = new Material(gs) { name = "RiderGear_hidden" };
                _hidden.SetFloat("_Hide", 1f);
            }
            _ready = true;
            PlayerGear.Changed += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            PlayerGear.Changed -= Apply;
            if (_hidden != null) Destroy(_hidden);
        }

        private static string Key(string matName)
        {
            string n = matName.Replace(" (Instance)", "");
            return n.StartsWith("Rider3D_") ? n.Substring(8) : n;
        }

        private static bool IsCloth(string key) =>
            key.StartsWith("tshirt") || key.StartsWith("shorts") || key.StartsWith("socks") || key.StartsWith("gloves") || key == "shoes";

        /// <summary>Надеть выбранное (вызывается при старте и при выборе в магазине).</summary>
        public void Apply()
        {
            if (!_ready) return;
            PlayerGear.Outfit o = PlayerGear.CurrentOutfit;
            PlayerGear.Helmet h = PlayerGear.CurrentHelmet;
            foreach (var kv in _clothes)
            {
                if (!kv.Value.HasProperty("_BaseColor")) continue;
                Color c = kv.Key.StartsWith("tshirt") ? o.shirt : kv.Key.StartsWith("shorts") ? o.shorts : kv.Key.StartsWith("socks") ? o.socks
                        : kv.Key.StartsWith("gloves") ? o.gloves : o.shoes;
                kv.Value.SetColor("_BaseColor", c);
            }
            SetHelmet(h.model);
            if (_vis != null) _vis.ApplyColors(o.shirt, o.shorts, h.main);
        }

        private void SetHelmet(string model)
        {
            if (model == _helmetModel && (_helmet != null || string.IsNullOrEmpty(model))) return;
            if (_helmet != null) Destroy(_helmet);
            _helmet = null;
            _helmetModel = model ?? "";
            GameObject prefab = string.IsNullOrEmpty(model) ? null : Resources.Load<GameObject>("RiderGear/" + model);
            if (!string.IsNullOrEmpty(model) && prefab == null)
                Debug.LogWarning("[RiderGear] Нет модели шлема Resources/RiderGear/" + model + " — родной шлем.");
            bool custom = prefab != null && Mount(prefab);
            // родной шлем: спрятать, если надет другой
            foreach (var (r, slot, m) in _stockHelmet)
            {
                Material[] mats = r.sharedMaterials;
                mats[slot] = custom && _hidden != null ? _hidden : m;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>
        /// Шлем из FBX (скелет RiderRig + сетка на кости Head): сетку вешаем обычным объектом прямо на кость Head нашего
        /// гонщика — шлем поворачивается ровно с головой в любой позе и трюке (360, тейлвип…), без скиннинга.
        /// </summary>
        private bool Mount(GameObject prefab)
        {
            if (!_bones.TryGetValue("Head", out Transform head)) return false;
            GameObject inst = Instantiate(prefab);
            SkinnedMeshRenderer src = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (src == null || src.sharedMesh == null) { Destroy(inst); return false; }
            Mesh mesh = src.sharedMesh;
            // положение сетки относительно кости Head — из позы привязки (bindpose: сетка → кость)
            Matrix4x4 bind = Matrix4x4.identity;
            Transform[] fbxBones = src.bones;
            Matrix4x4[] bp = mesh.bindposes;
            for (int i = 0; i < fbxBones.Length && i < bp.Length; i++)
                if (fbxBones[i] != null && fbxBones[i].name == "Head") { bind = bp[i]; break; }
            Material[] srcMats = src.sharedMaterials;
            Destroy(inst);

            var go = new GameObject("Helmet " + prefab.name);
            go.transform.SetParent(head, false);
            go.transform.localPosition = bind.GetColumn(3);
            go.transform.localRotation = bind.rotation;
            go.transform.localScale = bind.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (_near != null) { mr.sortingLayerID = _near.sortingLayerID; mr.sortingOrder = _near.sortingOrder; }

            Shader sh = Shader.Find(ShaderName);
            Texture2D paint = Resources.Load<Texture2D>("RiderGear/" + prefab.name + "_paint");   // окраска шлема (рисунок)
            if (paint == null) Debug.LogWarning("[RiderGear] Нет окраски Resources/RiderGear/" + prefab.name + "_paint.png");
            Material[] mats = new Material[srcMats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                bool glass = srcMats[i] != null && srcMats[i].name.Contains("glass");
                if (sh == null) { mats[i] = srcMats[i]; continue; }
                var m = new Material(sh) { name = "RiderGear_" + (glass ? "glass" : "paint") };
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_VColor", 1f);
                if (paint != null) m.SetTexture("_PaintTex", paint);
                m.SetFloat("_Spec", glass ? 0.8f : 0.22f);
                m.SetFloat("_Gloss", glass ? 100f : 28f);
                m.SetFloat("_Chrome", 0f);
                m.SetFloat("_Rim", glass ? 0.22f : 0.15f);
                mats[i] = m;
            }
            mr.sharedMaterials = mats;
            _helmet = go;
            return true;
        }
    }
}