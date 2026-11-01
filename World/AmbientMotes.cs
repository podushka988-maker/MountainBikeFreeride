using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Пылинки и пыльца, медленно плывущие в вечернем свете перед камерой — «воздух» в кадре. Ночью пылинок почти нет,
    /// зато в Альпах, осеннем лесу и в тумане летают светлячки: тёплые зелёно-жёлтые огоньки, медленно мигают и петляют.
    /// Висит на камере; система частиц создаётся из кода. Частицы живут в мире (не прилипают к экрану),
    /// поэтому при движении байка проплывают мимо — добавляет ощущение глубины и скорости.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class AmbientMotes : MonoBehaviour
    {
        [Tooltip("Материал частиц (Art/FX/mote.png).")]
        public Material moteMaterial;
        public Color color = new Color(1f, 0.92f, 0.72f, 0.55f);
        [Tooltip("Частиц в секунду.")]
        public float rate = 14f;
        public int sortingOrder = 9;
        [Tooltip("Светлячков в секунду глубокой ночью (0 — без светлячков). В каньоне и зимой их нет.")]
        public float fireflyRate = 5f;

        private ParticleSystem _ps, _ff;
        private Camera _cam;

        private void Start()
        {
            _cam = GetComponent<Camera>();
            var go = new GameObject("Ambient Motes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 10f);   // перед камерой, на плоскости игры
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = color;
            main.maxParticles = 300;
            main.prewarm = true;

            var em = _ps.emission;
            em.rateOverTime = rate;
            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_cam.orthographicSize * _cam.aspect * 2.6f, _cam.orthographicSize * 2.4f, 0.1f);

            // мерцают и плавают (лёгкий шум как ветерок)
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.2f;
            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);        // ветер слева направо
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sortingOrder = sortingOrder;
            if (moteMaterial != null) r.sharedMaterial = moteMaterial;
            _ps.Play();
            string loc = BackgroundThemes.Current;
            if (fireflyRate > 0f && loc != "canyon" && loc != "winter") _ff = MakeFireflies();
        }

        /// <summary>Светлячки: живут в мире, петляют (сильный медленный шум), мигают — то гаснут, то вспыхивают.</summary>
        private ParticleSystem MakeFireflies()
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 10f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.45f, 1f), new Color(1f, 0.92f, 0.45f, 1f));
            main.maxParticles = 80;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_cam.orthographicSize * _cam.aspect * 2.4f, _cam.orthographicSize * 1.4f, 0.1f);
            shape.position = new Vector3(0f, -_cam.orthographicSize * 0.35f, 0f);     // ниже — у травы и кустов
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.1f, 0.22f), new GradientAlphaKey(0.95f, 0.36f),
                              new GradientAlphaKey(0.15f, 0.5f), new GradientAlphaKey(1f, 0.63f), new GradientAlphaKey(0.05f, 0.77f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.25f; noise.scrollSpeed = 0.15f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sortingOrder = sortingOrder;
            if (moteMaterial != null) r.sharedMaterial = moteMaterial;
            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            if (_ps == null) return;
            // область появления следует за зумом камеры
            var shape = _ps.shape;
            shape.scale = new Vector3(_cam.orthographicSize * _cam.aspect * 2.6f, _cam.orthographicSize * 2.4f, 0.1f);
            // ночью пылинок в свете почти нет, светлячков — больше
            float n = AtmosphereGrade.Night(DayNightCycle.Hour);
            var em = _ps.emission;
            em.rateOverTime = rate * (1f - 0.8f * n);
            if (_ff != null)
            {
                var fe = _ff.emission;
                fe.rateOverTime = fireflyRate * n;
                var fs = _ff.shape;
                fs.scale = new Vector3(_cam.orthographicSize * _cam.aspect * 2.4f, _cam.orthographicSize * 1.4f, 0.1f);
                fs.position = new Vector3(0f, -_cam.orthographicSize * 0.35f, 0f);
            }
        }
    }
}
