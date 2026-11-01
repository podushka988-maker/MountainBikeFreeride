using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Внешний вид байка и гонщика из спрайтов BikeSkin. Физику не трогает — только читает положения рамы, колёс и тела.
    ///   • Байк: колёса крутятся, задний маятник качается за осью, вилка телескопически сжимается,
    ///     шатуны крутятся только при педалировании.
    ///   • Гонщик собран из частей (корпус, бедро, голень, ступня, плечо, предплечье, голова) и стоит в стойке райдера:
    ///     корпус наклонён вперёд, локти и колени согнуты. Тело рисуется ТАМ, ГДЕ ЕГО МАССА В ФИЗИКЕ: по центру масс
    ///     из RiderBody (перенос веса, присед на приземлении, отставание по инерции) подбирается положение таза так, чтобы
    ///     центр масс нарисованного тела (массы частей по Демпстеру) совпал с физическим. Поэтому присед, вынос таза назад,
    ///     бросок вперёд — следствие движения массы, а не отдельная анимация.
    ///   • Ноги крутят педали (IK), стопы «работают»: на педалировании носок опускается внизу хода, на накате пятки вниз.
    ///   • Голова держит взгляд по ходу движения, шея — пружина с гашением (на ударах голова чуть «кивает» и выравнивается).
    ///   • Падение: гонщик отцепляется от байка и падает куклой (RiderRagdoll) — руки и ноги в пределах суставов.
    /// Геометрия совпадает с нарисованными спрайтами и с Tools/art/rider_pose_new.py — меняйте вместе.
    /// Висит на корневом объекте Bike.
    /// </summary>
    public class BikeVisual : MonoBehaviour
    {
        public BikeController2D bike;
        public BikeInput input;
        public BikeSkin skin;
        [Tooltip("Unlit-материал для спрайтов (создаётся сборщиком сцены).")]
        public Material material;
        [Tooltip("Рама разломалась: насколько гонщика бросает через руль, м/с (вперёд по раме, вверх).")]
        public Vector2 brokenFling = new Vector2(3f, 2.2f);
        [Tooltip("Рама разломалась: кувырок гонщика через голову, °/с (минус — вперёд).")]
        public float brokenSpin = -380f;

        [Header("Геометрия байка (локально относительно рамы, метры)")]
        public Vector2 bottomBracket = new Vector2(-0.141f, -0.039f);
        [Tooltip("Нижняя ось маятника (за кареткой). Перья и верхние перья к качалке — одна жёсткая деталь.")]
        public Vector2 swingarmPivot = new Vector2(-0.115f, 0.028f);
        public Vector2 headTubeBottom = new Vector2(0.354f, 0.369f);
        public Vector2 grip = new Vector2(0.335f, 0.585f);
        public Vector2 saddle = new Vector2(-0.31f, 0.66f);
        public float crankLength = 0.17f;

        [Header("Скелет гонщика")]
        [Tooltip("Физическое тело гонщика: откуда брать центр масс, перенос веса и наклон тела.")]
        public RiderBody rider;
        [Tooltip("Длины частей тела, м (взрослый ~180 см): корпус — от таза до плеча.")]
        public float torsoLength = 0.48f;
        public float thigh = 0.46f, shin = 0.45f;
        public float upperArm = 0.30f, forearm = 0.29f;
        [Tooltip("Голеностоп относительно оси педали (ступня горизонтально).")]
        public Vector2 ankleOffset = new Vector2(-0.07f, 0.075f);
        [Tooltip("Голова от плеча (корпус под 40°): вперёд-вверх.")]
        public Vector2 neck = new Vector2(0.085f, 0.175f);
        [Tooltip("Дальнее плечо чуть сдвинуто — чтобы руки не сливались.")]
        public Vector2 farShoulderOffset = new Vector2(-0.025f, 0.01f);
        [Tooltip("Доля длины ноги, больше которой нога не разгибается.")]
        [Range(0.8f, 1f)] public float maxLegExtension = 0.97f;

        [Header("Стойка (как её рисуем)")]
        [Tooltip("Угол корпуса к раме, °: вес назад / нейтрально / вперёд.")]
        public float torsoBack = 36f, torsoNeutral = 40f, torsoForward = 26f;
        [Tooltip("Насколько выпрямлены руки (доля длины): назад / нейтрально / вперёд.")]
        public float armBack = 0.97f, armNeutral = 0.86f, armForward = 0.70f;
        [Tooltip("Руки: не сгибаются сильнее и не выпрямляются больше этих долей длины.")]
        public float armMin = 0.66f, armMax = 0.985f;
        [Tooltip("Угол корпуса к раме — пределы, °.")]
        public float torsoMin = 12f, torsoMax = 85f;
        [Tooltip("Угол корпуса к горизонту — пределы, °: нос байка вниз — гонщик выпрямляется, вверх — наклоняется.")]
        public float torsoWorldMin = 24f, torsoWorldMax = 72f;
        [Tooltip("Таз в нарисованных стойках (вес назад / нейтрально / вперёд). Разница центра масс такой фигуры и физической " +
                 "стойки добавляется к физическому центру масс: статичная поза — настоящая стойка райдера, а движения — из физики.")]
        public Vector2 hipVisBack = new Vector2(-0.50f, 0.54f), hipVisNeutral = new Vector2(-0.36f, 0.67f), hipVisForward = new Vector2(-0.13f, 0.62f);
        [Tooltip("Таз в обычной стойке не опускается ниже, м; в приседе на приземлении — ещё на Hip Floor Squat ниже (по силе удара).")]
        public float hipFloor = 0.56f, hipFloorSquat = 0.08f;
        [Tooltip("Присед на приземлении: насколько грудь опускается к рулю, ° и насколько сгибаются руки.")]
        public float compressTorso = 8f, compressArms = 0.08f;
        [Tooltip("Ступня на педалировании: средний угол, размах, фаза (°). На накате — пятки вниз на Foot Coast°.")]
        public float footMean = -6f, footSwing = 13f, footPhase = 45f, footCoast = 10f;

        [Header("Педалирование всем телом")]
        [Tooltip("Райдер крутит педали стоя, работая всем телом: на каждом рабочем ходе (педаль впереди) вес падает на педаль — " +
                 "таз чуть опускается и уходит вперёд (м), корпус почти неподвижен (°), голова смотрит вперёд по трассе (°); " +
                 "рука со стороны давящей ноги тянет руль на себя — плечо подходит к рулю, локоть сгибается (м), " +
                 "другая рука выпрямляется. Сильнее всего — в разгоне с места, на скорости и сидя — мягче, на каденсе 90+ — только ноги.")]
        public float pumpHipDrop = 0.012f, pumpHipForward = 0.012f, pumpTorso = 2f, pumpHead = 0f, pumpShoulder = 0.03f;
        [Tooltip("За сколько секунд «раскачка» телом нарастает и затихает, с.")]
        public float pumpResponse = 0.25f;
        [Tooltip("Скорость (м/с), на которой раскачка телом слабее всего (доля Pump At Speed от полной).")]
        public float pumpFadeSpeed = 14f;
        [Range(0f, 1f)] public float pumpAtSpeed = 0.4f;
        [Tooltip("Каденс, об/мин: до Pump Cadence Full тело работает в полную силу, к Pump Cadence Still раскачка плавно гаснет, " +
                 "а с него и выше (быстрое верчение, 90+) работают только ноги — корпус, руки и голова неподвижны.")]
        public float pumpCadenceFull = 70f, pumpCadenceStill = 90f;

        [Header("Посадка (по видео-образцу)")]
        [Tooltip("По ровному и в подъём гонщик едет СИДЯ: таз на седле, корпус почти вертикален, верх тела неподвижен — " +
                 "крутят только ноги (как в видео-образце). Встаёт на спуске круче Sit Max Descent°, при переносе веса (A/D), " +
                 "в приседе на приземлении, в прыжке и в полёте.")]
        public bool sitWhenRiding = true;
        public float sitMaxDescent = 5f;
        [Tooltip("Угол корпуса к раме сидя, ° (стоя — Torso Neutral).")]
        public float torsoSeated = 60f;
        [Tooltip("За сколько секунд садится и встаёт.")]
        public float sitTime = 0.35f;

        [Header("Рельеф: подъём и спуск (как настоящий райдер)")]
        [Tooltip("Уклон земли (°), с которого гонщик начинает менять стойку, и при котором — полностью.")]
        public float terrainFrom = 4f, terrainFull = 14f;
        [Tooltip("В подъём: грудь ниже к рулю (°) и таз вперёд (м) — вес на переднее колесо, чтобы перед не задирался.")]
        public float climbChest = 8f, climbHipForward = 0.05f;
        [Tooltip("На спуске: грудь ниже (°) и локти согнуты сильнее (доля длины руки) — вес на ногах, руки «лёгкие», " +
                 "таз назад (его уводит физика RiderBody).")]
        public float descentChest = 10f, descentElbows = 0.1f;

        [Header("Вилли")]
        [Tooltip("На заднем колесе руки выпрямляются (тело откидывается назад и висит на руле), корпус может отклониться " +
                 "от вертикали назад сильнее обычного предела на столько градусов.")]
        public float wheelieTorsoBack = 40f;
        private float _wheelie, _armWeight = 400f;                     // 0…1 — сейчас вилли; вес «прямых рук» в решателе плеча

        [Header("Голова и шея")]
        [Tooltip("Насколько голова поворачивается относительно рамы, °.")]
        public float headMin = -40f, headMax = 35f;
        [Tooltip("Пружина шеи: частота, рад/с, и гашение.")]
        public float neckFrequency = 9f, neckDamping = 0.75f;

        [Header("Суставы")]
        [Tooltip("Ноги и руки плавно гнутся в коленях и локтях (цельные картинки ноги и руки на сетке, SkinnedLimb), " +
                 "ягодица и дельта частично идут за корпусом. Выключите — будут отдельные части, как раньше.")]
        public bool bendJoints = true;

        [Header("Свет")]
        [Tooltip("Откуда светит солнце (в мире): тёплый свет на обращённой к нему стороне тела, холодная тень на другой.")]
        public Vector2 sunDirection = new Vector2(-0.45f, 0.89f);
        [Range(0f, 1.5f)] public float sunStrength = 1f;

        [Tooltip("Датчик падения (голова) — двигается вместе с нарисованной головой.")]
        public Transform headTrigger;

        [Header("Трюки")]
        [Tooltip("Трюки (BikeTricks): поза гонщика и вращение байка в трюке. У бота в меню не назначено.")]
        public BikeTricks tricks;
        [Tooltip("360 и вип: гонщик поворачивается вокруг вертикали через свой таз — верх тела (плечи, руки, голова) и низ " +
                 "(таз, ноги) отдельно; спереди тело не уже этой доли (у человека есть плечи), байк — не уже ширины покрышки.")]
        [Range(0.1f, 1f)] public float riderMinWidth = 0.3f;
        [Tooltip("Вип: вокруг какой высоты (м) байк «кладётся» набок. Позы и повороты всех трюков (кроме барспина) — " +
                 "в TrickChoreo (Tools/art/tricks_choreo.py).")]
        public float tablePivotY = 0.3f;
        [Tooltip("Барспин: насколько байк задирает нос в трюке, °.")]
        public float barspinPitch = 8f;
        [Tooltip("Ось рулевой: верх рулевого стакана (низ — Head Tube Bottom), как в Tools/art/bike_v3.py. Кадры руля барспина " +
                 "(BikeSkin.barSpin) нарисованы с центром в этой точке.")]
        public Vector2 headTubeTop = new Vector2(0.306f, 0.503f);
        [Tooltip("Середина грипсы в 3D-модели руля (Tools/art/barspin_frames.py), м: вперёд поперёк оси рулевой, вверх по оси, " +
                 "вбок от середины руля. За ней тянутся руки.")]
        public Vector3 barGrip = new Vector3(0.0538f, 0.0615f, 0.2575f);

        [Header("Сортировка")]
        [Tooltip("С этого Order in Layer начинаются слои байка и гонщика (всего ~35 слоёв).")]
        public int startOrder = 10;

        [Header("3D-гонщик (Resources/Rider3D)")]
        [Tooltip("Вместо нарисованного гонщика — 3D-модель (в езде, в трюках и при падении — кукла)")]
        public bool model3D = true;

        /// <summary>
        /// Суставы гонщика в мировых координатах — для 3D-модели (Rider3D).
        /// Езда и падение: точки в плоскости байка, вбок модель разносит их сама (full3D = false).
        /// Трюк: точки уже в 3D (с глубиной, повёрнуты на углы верха и низа тела), full3D = true.
        /// </summary>
        public struct RiderPose3D
        {
            public bool valid;                                         // есть поза (езда, трюк или кукла)
            public bool full3D;                                        // трюк: суставы уже в 3D (вбок не разносить)
            public Vector3 hip, sh, head, headUp, shN, shF, elbowN, elbowF, handN, handF, kneeN, kneeF, ankleN, ankleF;
            public Vector3 footDirN, footDirF, forward, up;
            public Vector3 fwdU, fwdL, fwdH;                           // full3D: куда смотрят грудь, таз и голова
            public float gripZ;
            public float seated;                                       // 0…1: сидит на седле
            public bool backTurned;                                    // гонщик спиной к нам — ближняя/дальняя стороны меняются
            public bool farLegFront;                                   // кэн-кэн: дальняя нога перекинута на нашу сторону
        }
        public RiderPose3D Pose3D;
        public int RiderFarOrder { get; private set; }
        public int RiderNearOrder { get; private set; }
        public int RiderSortingLayer { get; private set; }
        private Renderer[] _rider2D;
        private bool _rider2DOn = true;

        // массы частей тела (доли, Демпстер) и где центр масс части (доля длины от проксимального сустава)
        private const float MTrunk = 0.497f, MHead = 0.081f, MUArm = 0.028f, MFArm = 0.022f, MThigh = 0.100f, MShank = 0.0465f, MFoot = 0.0145f;

        private class Limb { public Transform upper, lower, foot; }
        private class Lit { public Transform node; public SpriteRenderer up, down; public float k; }
        private readonly System.Collections.Generic.List<Lit> _lit = new System.Collections.Generic.List<Lit>();
        private struct Skel
        {
            public Vector2 hip, sh, shN, shF, head, kneeN, kneeF, ankleN, ankleF, elbowN, elbowF;
            public float theta, arm;
        }

        private Transform _swingarm, _forkUpper, _forkLower, _crankNear, _crankFar;
        private Transform _torso, _head, _pelvis;
        private Transform _upperArmNear, _forearmNear, _upperArmFar, _forearmFar;
        private Limb _legNear, _legFar;

        private Vector2 _restRearAxle;
        private int _order;
        private SpriteRenderer _crack;
        private Transform _frameNode, _frameRear, _frameFront;   // рама целиком и её половины (видны, когда рама разломалась)
        private Vector2 _visN, _visB, _visF;          // поправка центра масс стоек (нарисованная − физическая)
        private Vector2 _hip;                         // таз с прошлого кадра (для решателя)
        private bool _hasHip;
        private float _headAngle, _headVel;
        private bool _hasHead;
        private RiderRagdoll _ragdoll;
        private float _pump, _pullN, _pullF, _climb, _descent, _sit;          // раскачка телом при педалировании 0…1; плечи к рулю (м): ближнее, дальнее
        /// <summary>Кукла гонщика после падения (null, пока он на байке).</summary>
        public RiderRagdoll Ragdoll => _ragdoll;
        /// <summary>Центр тела гонщика в мире (середина корпуса) — за ним следит камера.</summary>
        public Vector2 RiderCenter
        {
            get
            {
                if (_ragdoll != null && _ragdoll.Torso != null) return _ragdoll.Torso.position + (Vector2)_ragdoll.Torso.transform.right * (torsoLength * 0.5f);
                if (_torso != null) return (Vector2)_torso.position + (Vector2)_torso.right * (torsoLength * 0.5f);
                return bike.frame.position;
            }
        }
        private SkinnedLimb _legSkinN, _legSkinF, _armSkinN, _armSkinF;
        private Transform _vroot, _bikeRoot, _rearRoot, _frontRoot, _wheelRear, _wheelFront;   // узлы для трюков (см. Awake)
        private Vector2 _gripNow;                                     // руль сейчас (в випе/360 байк повёрнут)
        private float _cWhip = 1f, _sBar = 1f, _sTab = 1f, _cB = 1f;  // «сжатие» в трюке: 1 — как обычно, −1 — развёрнут
        private float _cU = 1f, _cL = 1f, _yawPivot = -0.36f;          // поворот верха и низа тела вокруг вертикали через таз
        private Vector2 _bikeOff;                                      // байк относительно гонщика (толкает / подтягивает)
        private Transform _upperRoot, _lowerRoot;                     // верх тела (корпус, голова, руки) и низ (ноги)
        private readonly System.Collections.Generic.List<Renderer> _farLeg = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<int> _farLegOrder = new System.Collections.Generic.List<int>();
        private int _farLegLift;                                       // насколько поднять дальнюю ногу, чтобы она была перед рамой
        private bool _farLegFront;
        // 360 (как в Bike Mayhem): гонщик с байком поворачивается вокруг вертикали как одно целое — суставы тела в 3D
        // (плечи, таз, колени, ступни, кисти разнесены вбок) и проецируются: спиной к нам — плечи и ноги во всю ширину
        private bool _rider3D;
        private float _aU, _aL, _aB, _aH;                              // угол поворота верха и низа тела, байка и головы, °
        private float _visPitch;                                       // байк с гонщиком повёрнут в трюке носом вверх, °
        // 360: корпус «сжимается» по ширине вокруг таза (узел TorsoSpin, не уже Torso Min Width, спиной к нам — шире в плечах),
        // шлем — кадры 3D-шлема (BikeSkin.helmetYaw*), ближняя и дальняя руки меняются местами, когда гонщик спиной к нам
        private Transform _torsoSpin, _headYaw;
        private SpriteRenderer _headYawPaint, _headYawTop;
        private SpriteRenderer[] _headSr;
        private int _headYawOrder;
        private float _headFade;                                       // нарисованный шлем прозрачнее (вместо него — 3D-кадры)
        private Color _helmetColor = Color.white;
        private readonly System.Collections.Generic.List<Renderer> _armN = new System.Collections.Generic.List<Renderer>(),
                                                                    _armF = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<int> _armNOrder = new System.Collections.Generic.List<int>(),
                                                               _armFOrder = new System.Collections.Generic.List<int>();
        private bool _armsSwapped;
        [Tooltip("360: корпус спереди/сзади не уже этой доли (ширина плеч).")]
        [Range(0.2f, 1f)] public float torsoMinWidth = 0.5f;
        private float _armSpread;                                      // руки разведены в стороны (к нам / от нас), 0…1
        private const float HipW = 0.09f, KneeW = 0.11f, AnkleW = 0.13f, PedalW = 0.16f, ShoulderW = 0.19f, ElbowW = 0.24f;
        // вип (по кадрам Bike Mayhem): байк положен набок на угол spin (до 75°, почти плашмя) и чуть повёрнут (0.2·spin);
        // руль — вместе с байком. Так же отрисованы кадры руля whip_00…12 (Tools/art/barspin_frames.py)
        private const float WhipYaw = 0.2f, WhipRoll = 1f, WhipSteer = 0f, WhipMax = 75f;
        private const float ArmReach = 0.95f;                           // рука до руля — не длиннее этой доли (плечо + предплечье)
        private const float CanCanReach = 0.86f;                        // кэн-кэн: нога к зрителю — доля длины (почти прямая)
        private Transform _rearAxis, _rearScale;                      // тейлвип: задняя часть — вокруг оси рулевой
        private Transform _steerAxis, _steerScale, _frontContent;     // барспин: вилка «поворачивается» поперёк линии вилки
        private Transform _wheelAxis, _wheelScale, _wheelContent;     // барспин: переднее колесо — так же, но может стать совсем узким
        private Transform _cpAxis, _cpScale, _cpContent, _cockpit;    // барспин: кокпит (вынос, руль) — вокруг оси рулевой
        private Transform _barSpin;                                   // 3D-руль в трюках: кадры (BikeSkin.barSpin / barYaw / barWhip)
        private SpriteRenderer _barSr;
        private SpriteRenderer[] _cockpitSr;                          // слои нарисованного кокпита и их цвета (плавная смена на 3D-руль)
        private Color[] _cockpitCol;
        private float _barPhi;                                        // угол поворота руля в барспине, °
        private Vector2 _barEndN, _barEndF;                           // где сейчас концы руля (ближний, дальний) — за ними руки
        private bool _wheelsOnBodies;                                 // после падения картинки колёс снова на самих колёсах
        // для 3D-гонщика в трюке: что считает TrickPose (в координатах тела, до «сжатия»), углы поворота, педали в раме
        private struct TrickRig
        {
            public float wN, wF, kN, kF, sp, xf;                       // руки с руля, ноги с педалей, руки в стороны, кэн-кэн
            public Vector2 hTgtN, hTgtF, aTgtN, aTgtF;                 // цели кистей и голеностопов
            public bool bar;                                           // барспин: кулаки к груди
            public float bN, bF;
            public Vector2 chestN, chestF, push;
        }
        private TrickRig _tr;
        private float _yU, _yL, _yH;                                   // углы верха, низа тела и головы в трюке, °
        private float _spinNow;                                        // канал spin трюка сейчас, °
        private Vector2 _pedalBikeN, _pedalBikeF;                      // педали в координатах рамы (без трюковых поворотов)
        // 360: последняя поза в плоскости тела (без проекции поворота) — из неё кукла при падении посреди трюка
        private bool _hasPlanar;
        private Skel _planS;
        private Vector2 _planHandN, _planHandF, _planFootN, _planFootF;
        private float _planFootAN, _planFootAF, _planHead;
        private readonly System.Collections.Generic.List<SkinnedLimb> _skins = new System.Collections.Generic.List<SkinnedLimb>();

        private void Awake()
        {
            if (skin == null)
            {
                Debug.LogWarning("[BikeVisual] Не назначен BikeSkin — байк не будет нарисован.", this);
                enabled = false;
                return;
            }

            if (skin.wheel == null || skin.frame.baseLayer == null)
                Debug.LogError("[BikeVisual] В BikeSkin нет спрайтов — байк будет невидимым. Выделите папку Assets/_Project/Art → ПКМ → Reimport, " +
                               "затем Bike → Создать сцену Free Ride.", this);

            Transform f = bike.frame.transform;
            // Всё нарисованное — в узле Visual на раме: трюки вращают байк и гонщика, не трогая физику.
            //   Visual (360 — разворот всего) → BikeParts (вип — байк поворачивается и кладётся набок) → Rear (рама, заднее колесо,
            //   маятник, шатуны — тейлвип) и Front (вилка и переднее колесо — барспин).
            _vroot = MakeNode(f, "Visual");
            _bikeRoot = MakeNode(_vroot, "BikeParts");
            _rearAxis = MakeNode(_bikeRoot, "RearAxis");                  // тейлвип: задняя часть крутится вокруг оси рулевой
            _rearScale = MakeNode(_rearAxis, "RearSpin");
            _rearRoot = MakeNode(_rearScale, "Rear");
            _frontRoot = MakeNode(_bikeRoot, "Front");
            // ось рулевой (наклонена, как у настоящего байка): вилка с колесом «сжимаются» поперёк неё — колесо в барспине
            // становится узким эллипсом вдоль вилки, как в Bike Mayhem
            _steerAxis = MakeNode(_frontRoot, "SteerAxis");
            _steerScale = MakeNode(_steerAxis, "SteerSpin");
            _frontContent = MakeNode(_steerScale, "FrontContent");
            _wheelAxis = MakeNode(_frontRoot, "WheelAxis");
            _wheelScale = MakeNode(_wheelAxis, "WheelSpin");
            _wheelContent = MakeNode(_wheelScale, "WheelContent");
            _cpAxis = MakeNode(_frontRoot, "CockpitAxis");
            _cpScale = MakeNode(_cpAxis, "CockpitSpin");
            _cpContent = MakeNode(_cpScale, "CockpitContent");
            _gripNow = grip;
            Transform r = _vroot;
            _restRearAxle = f.InverseTransformPoint(bike.rearWheel.transform.position);
            _order = startOrder;
            float k = skin.farSideShade;
            Color far = new Color(k, k, k, 1f);

            // Порядок создания = порядок отрисовки (от дальнего к ближнему).
            bool bend = bendJoints && HasLayers(skin.leg) && HasLayers(skin.arm);
            int farLegStart = _order, farLegEnd = _order;
            // --- дальняя сторона гонщика ---
            if (bend)
            {
                // кости (без картинок) и цельные нога/рука на сетке; корпус как кость нужен сразу — картинки ему добавим позже
                _torso = MakeNode(r, "Torso");
                _legFar = new Limb { upper = MakeNode(r, "ThighFar"), lower = MakeNode(r, "ShinFar") };
                _legSkinF = MakeSkin(r, "LegFar", skin.leg, _legFar.upper, _legFar.lower, thigh, -1f, far);
                _legSkinF.AddSegment(-0.2f, 9f, 0f, 0f, ref _order);
                _legFar.foot = MakePart(r, "ShoeFar", skin.shoe, far, far);
                farLegEnd = _order;
                _upperArmFar = MakeNode(r, "UpperArmFar"); _forearmFar = MakeNode(r, "ForearmFar");
                _armSkinF = MakeSkin(r, "ArmFar", skin.arm, _upperArmFar, _forearmFar, upperArm, +1f, far);
                _armSkinF.AddSegment(-0.2f, 9f, -0.1f, -0.03f, ref _order);    // у плеча — плавно в корпус
            }
            else
            {
                _legFar = MakeLeg(r, "Far", far);
                farLegEnd = _order;
                _upperArmFar = MakePart(r, "UpperArmFar", skin.upperArm, far, Mul(skin.jerseyColor, k));
                _forearmFar = MakePart(r, "ForearmFar", skin.forearm, far, Mul(skin.jerseyColor, k));
            }
            _crankFar = MakeNode(_rearRoot, "CrankFar");
            AddSprite(_crankFar, skin.crankFar, Color.white);

            // --- байк ---
            // колёса: картинка на узле, который каждый кадр ставится на ось колеса (после падения — снова на самом колесе)
            _wheelRear = MakeNode(_rearRoot, "WheelRear");
            AddSprite(_wheelRear, skin.wheel, Color.white);
            _wheelFront = MakeNode(_wheelContent, "WheelFront");
            AddSprite(_wheelFront, skin.wheel, Color.white);
            _swingarm = MakePart(_rearRoot, "Swingarm", skin.swingarm, Color.white, skin.frameColor);
            _swingarm.localPosition = swingarmPivot;
            _forkLower = MakeNode(_frontContent, "ForkLower");
            AddSprite(_forkLower, skin.forkLower, Color.white);
            _frameNode = MakePart(_rearRoot, "FrameSprites", skin.frame, Color.white, skin.frameColor);
            if (HasLayers(skin.cockpit))                                     // руль отдельно от рамы (в барспине крутится)
                _cockpit = MakePart(_cpContent, "Cockpit", skin.cockpit, Color.white, skin.frameColor);
            if (HasLayers(skin.frameRear) && HasLayers(skin.frameFront))
            {
                // половины разломанной рамы — на месте рамы, пока скрыты
                _frameRear = MakePart(_rearRoot, "FrameRear", skin.frameRear, Color.white, skin.frameColor);
                _frameFront = MakePart(_rearRoot, "FrameFront", skin.frameFront, Color.white, skin.frameColor);
                _frameRear.gameObject.SetActive(false);
                _frameFront.gameObject.SetActive(false);
            }
            else
            {
                _crack = AddSprite(_frameNode, skin.frameCrack, Color.white);   // нет картинок разлома — трещины
                if (_crack != null) _crack.enabled = false;
            }
            _forkUpper = MakeNode(_frontContent, "ForkUpper");
            _forkUpper.localPosition = headTubeBottom;
            AddSprite(_forkUpper, skin.forkUpper, Color.white);
            // 3D-руль (кадры поворота: барспин, 360, вип) — на раме, а не в «сжатом» узле: поворот уже нарисован
            _barSpin = MakeNode(f, "BarSpin");
            _barSpin.localPosition = headTubeTop;
            _barSr = AddSprite(_barSpin, FirstFrame(skin.barSpin) ?? FirstFrame(skin.barYaw) ?? FirstFrame(skin.barWhip), Color.white);
            _barSpin.gameObject.SetActive(false);
            if (_cockpit != null)
            {
                _cockpitSr = _cockpit.GetComponentsInChildren<SpriteRenderer>(true);
                _cockpitCol = new Color[_cockpitSr.Length];
                for (int i = 0; i < _cockpitSr.Length; i++) _cockpitCol[i] = _cockpitSr[i].color;
            }
            _crankNear = MakeNode(_rearRoot, "CrankNear");
            AddSprite(_crankNear, skin.crankNear, Color.white);
            _crankNear.localPosition = _crankFar.localPosition = bottomBracket;
            // место между байком и ближней ногой — для дальней ноги, перекинутой на нашу сторону (кэн-кэн)
            _farLegLift = _order - farLegStart;
            _order += farLegEnd - farLegStart;

            // --- ближняя сторона гонщика: бедро под корпусом (джерси закрывает пояс штанов), голень и стопа — поверх ---
            if (bend)
            {
                _legNear = new Limb { upper = MakeNode(r, "ThighNear"), lower = MakeNode(r, "ShinNear") };
                _legSkinN = MakeSkin(r, "LegNear", skin.leg, _legNear.upper, _legNear.lower, thigh, -1f, Color.white);
                _legSkinN.AddSegment(-0.2f, thigh + 0.1f, 0f, 0f, ref _order);                       // бедро — под корпусом
                AddPelvisAndTorso();
                _legSkinN.AddSegment(thigh - 0.1f, 9f, thigh - 0.1f, thigh - 0.04f, ref _order);   // голень — поверх, без шва
                _legNear.foot = MakePart(r, "ShoeNear", skin.shoe, Color.white, Color.white);
                _upperArmNear = MakeNode(r, "UpperArmNear"); _forearmNear = MakeNode(r, "ForearmNear");
                _armSkinN = MakeSkin(r, "ArmNear", skin.arm, _upperArmNear, _forearmNear, upperArm, +1f, Color.white);
                _armSkinN.AddSegment(-0.2f, 9f, -0.1f, -0.03f, ref _order);
            }
            else
            {
                _legNear = new Limb { upper = MakePart(r, "ThighNear", skin.thigh, Color.white, skin.shortsColor) };
                _torso = MakeNode(r, "Torso");
                AddPelvisAndTorso();
                _legNear.lower = MakePart(r, "ShinNear", skin.shin, Color.white, skin.shortsColor);
                _legNear.foot = MakePart(r, "ShoeNear", skin.shoe, Color.white, Color.white);
                _upperArmNear = MakePart(r, "UpperArmNear", skin.upperArm, Color.white, skin.jerseyColor);
                _forearmNear = MakePart(r, "ForearmNear", skin.forearm, Color.white, skin.jerseyColor);
            }
            _headYawOrder = _order; _order += 2;                       // 3D-шлем (360) — сразу под нарисованным шлемом
            _head = MakePart(r, "Head", skin.head, Color.white, skin.helmetColor);
            // верх и низ тела — свои узлы: в 360 и випе плечи ведут поворот, таз и байк догоняют
            _upperRoot = MakeNode(_vroot, "UpperBody");
            _lowerRoot = MakeNode(_vroot, "LowerBody");
            _torsoSpin = MakeNode(_upperRoot, "TorsoSpin");
            foreach (Transform part in new[] { _head, _upperArmNear, _forearmNear, _upperArmFar, _forearmFar })
                if (part != null) part.SetParent(_upperRoot, false);
            _torso.SetParent(_torsoSpin, false);
            // 3D-шлем для 360: кадры поворота (оболочка перекрашивается, детали — свои). Рисуется под нарисованным шлемом:
            // при смене нарисованный становится прозрачным поверх уже непрозрачного 3D — без «призрака» и просвета
            _headSr = _head.GetComponentsInChildren<SpriteRenderer>(true);
            _headYaw = MakeNode(_upperRoot, "HeadYaw");
            _headYawPaint = HeadYawLayer("Paint", _headYawOrder);
            _headYawTop = HeadYawLayer("Top", _headYawOrder + 1);
            _headYaw.gameObject.SetActive(false);
            // руки: рендереры и порядок — когда гонщик спиной к нам, дальняя рука ближе (рисуется поверх корпуса)
            CollectArm(_armSkinN, _upperArmNear, _forearmNear, _armN, _armNOrder);
            CollectArm(_armSkinF, _upperArmFar, _forearmFar, _armF, _armFOrder);
            foreach (Transform part in new[] { _legNear.upper, _legNear.lower, _legNear.foot, _legFar.upper, _legFar.lower, _legFar.foot })
                if (part != null) part.SetParent(_lowerRoot, false);

            // рендереры дальней ноги и их порядок — чтобы перекинуть ногу перед рамой
            if (_legSkinF != null) foreach (Renderer rr in _legSkinF.Renderers) _farLeg.Add(rr);
            foreach (Transform part in new[] { _legFar.upper, _legFar.lower, _legFar.foot })
                if (part != null) foreach (Renderer rr in part.GetComponentsInChildren<Renderer>(true)) if (!_farLeg.Contains(rr)) _farLeg.Add(rr);
            foreach (Renderer rr in _farLeg) _farLegOrder.Add(rr.sortingOrder);

            // нарисованный гонщик: все рендереры в Visual, кроме байка — их прячет 3D-модель
            var riderR = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer rr in _vroot.GetComponentsInChildren<Renderer>(true))
                if (!rr.transform.IsChildOf(_bikeRoot)) riderR.Add(rr);
            _rider2D = riderR.ToArray();
            RiderFarOrder = farLegStart;
            RiderNearOrder = _order;
            RiderSortingLayer = _headSr != null && _headSr.Length > 0 ? _headSr[0].sortingLayerID : 0;
            ComputeStanceOffsets();
            ComputeRestAngles();
            // цвета, выбранные в магазине (если выбирали)
            ApplyColors(PlayerLook.Get(PlayerLook.JerseyKey, skin.jerseyColor), PlayerLook.Get(PlayerLook.PantsKey, skin.shortsColor),
                        PlayerLook.Get(PlayerLook.HelmetKey, skin.helmetColor));
            if (model3D) Rider3D.Attach(this);
            LateUpdate();
        }

        private SpriteRenderer HeadYawLayer(string name, int order)
        {
            var go = new GameObject("HeadYaw" + name);
            go.transform.SetParent(_headYaw, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        private static void CollectArm(SkinnedLimb skinL, Transform upper, Transform lower,
                                       System.Collections.Generic.List<Renderer> list, System.Collections.Generic.List<int> orders)
        {
            if (skinL != null) list.AddRange(skinL.Renderers);
            else foreach (Transform t in new[] { upper, lower }) if (t != null) list.AddRange(t.GetComponentsInChildren<Renderer>(true));
            foreach (Renderer r in list) orders.Add(r.sortingOrder);
        }

        /// <summary>Спиной к нам: ближняя и дальняя руки меняются порядком отрисовки (и оттенком дальней стороны).</summary>
        private void SwapArms(bool swap)
        {
            if (swap == _armsSwapped || _armN.Count != _armF.Count) return;
            _armsSwapped = swap;
            for (int i = 0; i < _armN.Count; i++)
            {
                if (_armN[i] != null) _armN[i].sortingOrder = swap ? _armFOrder[i] : _armNOrder[i];
                if (_armF[i] != null) _armF[i].sortingOrder = swap ? _armNOrder[i] : _armFOrder[i];
            }
        }

        /// <summary>Перекрасить экипировку: джерси, штаны, шлем (дальние рука и нога — темнее, как в BikeSkin).</summary>
        public void ApplyColors(Color jersey, Color pants, Color helmet)
        {
            Paint(_torso, jersey); Paint(_upperArmNear, jersey); Paint(_forearmNear, jersey); Paint(_upperArmFar, jersey); Paint(_forearmFar, jersey);
            Paint(_legNear.upper, pants); Paint(_legNear.lower, pants); Paint(_legFar.upper, pants); Paint(_legFar.lower, pants);
            Paint(_head, helmet);
            _helmetColor = helmet;
            if (_headYawPaint != null) _headYawPaint.color = helmet;
            Paint(_pelvis, pants);
            float k = skin != null ? skin.farSideShade : 0.58f;
            if (_legSkinN != null) _legSkinN.paint = pants;
            if (_legSkinF != null) _legSkinF.paint = Mul(pants, k);
            if (_armSkinN != null) _armSkinN.paint = jersey;
            if (_armSkinF != null) _armSkinF.paint = Mul(jersey, k);
        }

        private static void Paint(Transform node, Color c)
        {
            if (node == null) return;
            // только свои слои (прямые дети узла): у корпуса есть ребёнок-таз со своим цветом (штаны)
            float shade = 1f;
            foreach (Transform ch in node)
            {
                var sr = ch.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null && sr.sprite.name.EndsWith("_base")) { shade = sr.color.r; break; }
            }
            foreach (Transform ch in node)
            {
                var sr = ch.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null && sr.sprite.name.EndsWith("_paint")) sr.color = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
            }
        }

        private void Start()
        {
            if (bike != null) { bike.Crashed += OnCrashed; bike.Broken += OnBroken; }
        }

        /// <summary>
        /// Рама разломалась: вместо целой рамы — две половины; передняя (рулевой стакан с обломками труб) и вилка
        /// переходят к отдельному телу передней части (BikeController2D.FrontPiece) и улетают вместе с передним колесом.
        /// </summary>
        private void OnBroken()
        {
            ResetTrickRoots();
            Rigidbody2D front = bike.FrontPiece;
            if (_frameRear != null && front != null)
            {
                _frameNode.gameObject.SetActive(false);
                _frameRear.gameObject.SetActive(true);
                _frameFront.gameObject.SetActive(true);
                if (_cockpit != null) _cockpit.gameObject.SetActive(false);   // руль нарисован на передней половине
                Transform ff = front.transform;                                // в момент разлома — в той же позе, что рама
                _frameFront.SetParent(ff, true);
                _forkUpper.SetParent(ff, true);
                _forkLower.SetParent(ff, true);
            }
            else if (_crack != null) _crack.enabled = true;
        }

        private void OnDestroy()
        {
            if (bike != null) { bike.Crashed -= OnCrashed; bike.Broken -= OnBroken; }
            foreach (SkinnedLimb sl in _skins) sl.Dispose();
            _skins.Clear();
        }

        /// <summary>
        /// Падение посреди 360: нарисованные части тела стоят в проекции поворота (повёрнутая к нам нога видна короче,
        /// плечи и колени сдвинуты вбок). Кукла взяла бы тела с этих мест, но с полной длиной костей — шарниры рывком
        /// стянули бы части, и тело вывернуло бы. Поэтому перед куклой части ставятся в позу трюка в плоскости тела.
        /// </summary>
        private void RestorePlanarPose()
        {
            _hasPlanar = false;
            Skel s = _planS;
            if (_torso != null) { Place(_torso, s.hip, s.sh); _torso.localScale = Vector3.one; }
            if (_head != null)
            {
                _head.localPosition = s.head;
                _head.localRotation = Quaternion.Euler(0f, 0f, _planHead);
                _head.localScale = Vector3.one;
            }
            if (_legNear != null && _legNear.upper != null) PlaceLeg(_legNear, s.hip, s.kneeN, s.ankleN, _planFootN, _planFootAN);
            if (_legFar != null && _legFar.upper != null) PlaceLeg(_legFar, s.hip, s.kneeF, s.ankleF, _planFootF, _planFootAF);
            if (_upperArmNear != null) { Place(_upperArmNear, s.shN, s.elbowN); Place(_forearmNear, s.elbowN, _planHandN); }
            if (_upperArmFar != null) { Place(_upperArmFar, s.shF, s.elbowF); Place(_forearmFar, s.elbowF, _planHandF); }
        }

        /// <summary>Падение: гонщик отцепляется от байка и падает куклой.</summary>
        private void OnCrashed()
        {
            ResetTrickRoots();                                             // кукла — в позе трюка, но байк и тело без «поворота»
            if (_hasPlanar) RestorePlanarPose();                           // 360: части тела — без проекции, с настоящими длинами
            if (_ragdoll != null || rider == null) return;
            _ragdoll = RiderRagdoll.Create(this, bike, rider,
                _torso, _head, _legNear.upper, _legNear.lower, _legFar.upper, _legFar.lower,
                _upperArmNear, _forearmNear, _upperArmFar, _forearmFar, _legNear.foot, _legFar.foot);
            // рама разломалась: гонщика бросает через руль — вперёд и вверх, кувырком через голову, и он врезается в землю
            if (bike.IsBroken)
                _ragdoll.Fling((Vector2)bike.frame.transform.right * brokenFling.x + Vector2.up * brokenFling.y, brokenSpin);
        }

        // ------------------------------------------------------------------ создание

        private static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        private static Transform MakeNode(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private Limb MakeLeg(Transform parent, string side, Color shade)
        {
            return new Limb
            {
                upper = MakePart(parent, "Thigh" + side, skin.thigh, shade, Mul(skin.shortsColor, shade.r)),
                lower = MakePart(parent, "Shin" + side, skin.shin, shade, Mul(skin.shortsColor, shade.r)),
                foot = MakePart(parent, "Shoe" + side, skin.shoe, shade, shade),
            };
        }

        /// <summary>Узел со слоями: основа (tint), краска (paint), детали (tint) и свет солнца (две стороны).</summary>
        private Transform MakePart(Transform parent, string name, BikeSkin.Layered layers, Color tint, Color paint)
        {
            var node = MakeNode(parent, name);
            AddLayers(node, layers, tint, paint);
            return node;
        }

        /// <summary>
        /// Таз в штанах (ягодицы, низ живота) — ребёнок корпуса, рисуется под джерси и поверх ближнего бедра; туда, где выходит
        /// бедро, картинка таза плавно прозрачна — бедро продолжается из него без шва. Затем — картинки корпуса.
        /// </summary>
        private void AddPelvisAndTorso()
        {
            if (HasLayers(skin.pelvis)) _pelvis = MakePart(_torso, "Pelvis", skin.pelvis, Color.white, skin.shortsColor);
            else _order += 5;
            AddLayers(_torso, skin.torso, Color.white, skin.jerseyColor);
        }

        private static bool HasLayers(BikeSkin.Layered l) => l != null && l.paintLayer != null && l.paintLayer.rect.width > 8f;

        /// <summary>Нога/рука, которая гнётся в суставах: корень — корпус, кости — b1, b2 (см. SkinnedLimb).</summary>
        private SkinnedLimb MakeSkin(Transform parent, string name, BikeSkin.Layered layers, Transform b1, Transform b2,
                                     float len1, float innerSign, Color tint)
        {
            var sl = new SkinnedLimb(parent, name, layers, material, _torso, b1, b2, len1, innerSign) { tint = tint, lightK = tint.r };
            _skins.Add(sl);
            return sl;
        }

        /// <summary>Угол бедра и плеча к корпусу в нейтральной стойке — в ней у таза и плеча нет изгиба.</summary>
        private void ComputeRestAngles()
        {
            if (_skins.Count == 0) return;
            Vector2 pn = bottomBracket + new Vector2(crankLength, 0f), pf = bottomBracket - new Vector2(crankLength, 0f);
            Pref(0f, out float th, out float e);
            Skel s = Skeleton(hipVisNeutral, th, e, pn, pf, footCoast, footCoast);
            float leg = Angle(s.kneeN - s.hip) - s.theta, arm = Angle(s.elbowN - s.sh) - s.theta;
            if (_legSkinN != null) _legSkinN.rest = leg;
            if (_legSkinF != null) _legSkinF.rest = leg;
            if (_armSkinN != null) _armSkinN.rest = arm;
            if (_armSkinF != null) _armSkinF.rest = arm;
        }

        private void UpdateSkins()
        {
            if (_skins.Count == 0) return;
            Vector2 sun = sunDirection.sqrMagnitude > 1e-6f ? sunDirection.normalized : Vector2.up;
            foreach (SkinnedLimb sl in _skins) sl.Update(sun, sunStrength);
        }

        /// <summary>Слои картинки (основа, краска, детали, свет солнца) на уже созданный узел.</summary>
        private void AddLayers(Transform node, BikeSkin.Layered layers, Color tint, Color paint)
        {
            AddSprite(node, layers.baseLayer, tint);
            AddSprite(node, layers.paintLayer, paint);
            AddSprite(node, layers.topLayer, tint);
            if (layers.lightUp != null || layers.lightDown != null)
                _lit.Add(new Lit { node = node, up = AddSprite(node, layers.lightUp, Color.clear), down = AddSprite(node, layers.lightDown, Color.clear), k = tint.r });
        }

        private SpriteRenderer AddSprite(Transform parent, Sprite sprite, Color color)
        {
            int order = _order++;
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        /// <summary>
        /// Свет солнца на теле: у каждой части два слоя — освещена сторона +Y или −Y спрайта. Смешиваем по тому, как часть
        /// повёрнута к солнцу в мире, — рука, нога, корпус, шлем вращаются, а свет остаётся с одной стороны (как у 3D-модели).
        /// </summary>
        private void UpdateLight()
        {
            Vector2 sun = sunDirection.sqrMagnitude > 1e-6f ? sunDirection.normalized : Vector2.up;
            foreach (Lit l in _lit)
            {
                if (l.node == null) continue;
                float w = Vector2.Dot(l.node.up, sun) * sunStrength * l.k;
                if (l.node == _head) w *= 1f - _headFade;                  // в 360 вместо нарисованного шлема — 3D-кадры
                if (l.up != null) l.up.color = new Color(1f, 1f, 1f, Mathf.Clamp01(w));
                if (l.down != null) l.down.color = new Color(1f, 1f, 1f, Mathf.Clamp01(-w));
            }
        }

        // ------------------------------------------------------------------ каждый кадр

        private void LateUpdate()
        {
            Transform f = bike.frame.transform;
            Transform ff = bike.FrontPiece != null && _forkUpper.parent != f ? bike.FrontPiece.transform : f;   // вилка — на передней части рамы
            Vector2 rearAxle = f.InverseTransformPoint(bike.rearWheel.transform.position);
            Vector2 frontAxle = ff.InverseTransformPoint(bike.frontWheel.transform.position);
            if (!bike.IsCrashed) UpdateTrickRoots(rearAxle, frontAxle);
            if (!_wheelsOnBodies) { PlaceWheel(_wheelRear, bike.rearWheel.transform, f); PlaceWheel(_wheelFront, bike.frontWheel.transform, f); }

            // маятник поворачивается вокруг оси так, чтобы дропаут оставался на оси колеса
            float rest = Angle(_restRearAxle - swingarmPivot);
            float now = Angle(rearAxle - swingarmPivot);
            _swingarm.localRotation = Quaternion.Euler(0f, 0f, now - rest);

            // вилка: верх смотрит от короны на ось, низ — от оси на корону
            Vector2 forkDir = frontAxle - headTubeBottom;
            _forkUpper.localRotation = Quaternion.Euler(0f, 0f, Angle(forkDir) + 90f);
            _forkLower.localPosition = frontAxle;
            _forkLower.localRotation = Quaternion.Euler(0f, 0f, Angle(-forkDir) - 90f);

            // шатуны: угол считает физика (ритм педалей через передачу, выравнивание на накате)
            float crankAngle = bike.CrankAngle;
            _crankNear.localRotation = Quaternion.Euler(0f, 0f, crankAngle);
            _crankFar.localRotation = Quaternion.Euler(0f, 0f, crankAngle + 180f);
            if (_ragdoll != null) { ExportRagdoll3D(); UpdateLight(); UpdateSkins(); return; }   // гонщик упал — его двигает кукла
            float ca = crankAngle * Mathf.Deg2Rad;
            Vector2 crank = new Vector2(Mathf.Cos(ca), Mathf.Sin(ca)) * crankLength;
            _pedalBikeN = bottomBracket + crank; _pedalBikeF = bottomBracket - crank;
            Vector2 pedalNear = BikeToVis(bottomBracket + crank, false, !_rider3D);  // в трюке байк повёрнут — педали вместе с ним
            Vector2 pedalFar = BikeToVis(bottomBracket - crank, false, !_rider3D);   // (в 360 — поворот потом, в 3D)

            PoseRider(pedalNear, pedalFar, crankAngle, Time.deltaTime);
            UpdateLight();
            UpdateSkins();
        }

        /// <summary>Стойка и положение тела гонщика (повторяет Tools/art/rider_pose_new.py).</summary>
        private void PoseRider(Vector2 pedalN, Vector2 pedalF, float crankAngle, float dt)
        {
            float lean = 0f, tilt = 0f, seat = 0f, compress = 0f;
            Vector2 C = new Vector2(-0.106f, 0.757f);
            if (rider != null)
            {
                C = rider.RenderCom(out compress);
                lean = rider.PoseLean; tilt = rider.BodyTilt; seat = rider.Seat;
            }
            tilt -= bike.AirTwist;                                         // в воздухе гонщик повернул байк относительно тела
            // центр масс нарисованной стойки (поправка вращается вместе с телом, которое держится по гравитации)
            C += (Vector2)(Quaternion.Euler(0f, 0f, tilt * (1f - seat)) * (Vector3)StanceOffset(lean));

            // педалирование всем телом: ритм — от шатунов. Рабочий ход ближней ноги — педаль впереди (угол 0°), дальней — 180°:
            // stroke = 1 на каждом рабочем ходе (дважды за оборот), side = +1 — давит ближняя нога, −1 — дальняя
            bool pedalingNow = input != null && input.Pedal > 0f && !bike.IsCrashed && bike.IsOnGround;
            // посадка: по ровному и в подъём — сидя (как в видео-образце), на спуске, с переносом веса, в прыжке и в полёте — стоя
            float slopeNow = rider != null ? rider.SlopeAngle : 0f;
            bool sitWanted = sitWhenRiding && !bike.IsCrashed && bike.IsGrounded && slopeNow > -sitMaxDescent
                             && Mathf.Abs(lean) < 0.35f && compress < 0.2f && bike.HopCrouch < 0.05f;
            _sit = Mathf.MoveTowards(_sit, sitWanted ? 1f : 0f, dt / Mathf.Max(0.05f, sitTime));
            float sitK = SmoothStep(_sit);
            float seated = Mathf.Max(seat, sitK);
            float spin = 1f - SmoothStep((bike.Cadence - pumpCadenceFull) / Mathf.Max(1f, pumpCadenceStill - pumpCadenceFull));   // 90+ об/мин — только ноги
            float pumpTarget = pedalingNow
                ? Mathf.Clamp01(input.Pedal) * Mathf.Lerp(1f, pumpAtSpeed, Mathf.Clamp01(bike.Speed / Mathf.Max(1f, pumpFadeSpeed))) * (1f - 0.9f * seated) * spin   // сидя верх тела неподвижен
                : 0f;
            _pump += (pumpTarget - _pump) * Mathf.Min(1f, dt / Mathf.Max(0.02f, pumpResponse));
            float ca = crankAngle * Mathf.Deg2Rad;
            float stroke = 0.5f + 0.5f * Mathf.Cos(2f * ca), side = Mathf.Cos(ca);
            C += new Vector2(pumpHipForward * (2f * stroke - 1f), -pumpHipDrop * stroke) * _pump;   // вес падает на педаль
            // рельеф: в подъём — таз вперёд и грудь к рулю, на спуске — грудь ниже и локти согнуты (уклон — по земле, в полёте — плавно к нулю)
            float slope = rider != null && bike.IsOnGround ? rider.SlopeAngle : 0f;
            float k = Mathf.Min(1f, dt / 0.3f);
            _climb += (SmoothStep((slope - terrainFrom) / Mathf.Max(0.1f, terrainFull - terrainFrom)) - _climb) * k;
            _descent += (SmoothStep((-slope - terrainFrom) / Mathf.Max(0.1f, terrainFull - terrainFrom)) - _descent) * k;
            C += new Vector2(climbHipForward * _climb, 0f);
            _pullN = pumpShoulder * _pump * side;                         // рука давящей стороны тянет руль
            _pullF = -pumpShoulder * _pump * side;

            float framePitch = Mathf.DeltaAngle(0f, bike.frame.rotation);
            bool wheelieNow = !bike.IsCrashed && bike.RearGrounded && !bike.FrontGrounded && framePitch > 6f;
            _wheelie = Mathf.MoveTowards(_wheelie, wheelieNow ? SmoothStep((framePitch - 6f) / 14f) : 0f, dt / 0.25f);
            Pref(lean, out float th0, out float e0);
            th0 = Mathf.Lerp(th0, torsoSeated, sitK);                      // сидя — корпус почти вертикален
            th0 += tilt * 0.8f;                                            // тело частично держится по гравитации (как в физике)
            th0 -= pumpTorso * stroke * _pump;                             // корпус почти неподвижен: работают ноги и руки
            th0 -= climbChest * _climb + descentChest * _descent;         // подъём и спуск — грудь ниже
            e0 -= descentElbows * _descent;                                // на спуске локти согнуты — руки работают как подвеска
            th0 -= compressTorso * compress;                               // присед: грудь к рулю
            e0 -= compressArms * compress;
            float fade = 1f - SmoothStep((Mathf.Abs(framePitch) - 60f) / 50f);   // в сальто — вместе с байком
            // вилли: руки прямые — гонщик откидывается и висит на руле (как на фото-образце), корпус может уйти назад
            e0 = Mathf.Lerp(e0, armMax, _wheelie);
            _armWeight = Mathf.Lerp(400f, 6000f, _wheelie);
            float worldMax = torsoWorldMax + wheelieTorsoBack * _wheelie;
            th0 += (Mathf.Max(0f, torsoWorldMin - framePitch - th0) - Mathf.Max(0f, th0 + framePitch - worldMax)) * fade;

            bool pedaling = input != null && input.Pedal > 0f && !bike.IsCrashed;
            float footN = FootAngle(crankAngle, pedaling, compress), footF = FootAngle(crankAngle + 180f, pedaling, compress);

            // таз: чтобы центр масс нарисованного тела совпал с физическим (но в обычной стойке не ниже Hip Floor)
            float floor = hipFloor - hipFloorSquat * compress;
            Vector2 hip = _hasHip ? _hip : new Vector2(C.x - 0.22f, C.y - 0.02f);
            for (int i = 0; i < 10; i++)
            {
                hip = ClampHip(hip, pedalN, pedalF, footN, footF, floor);
                Skel sk = Skeleton(hip, th0, e0, pedalN, pedalF, footN, footF);
                hip += (C - ComOf(sk, pedalN, pedalF)) / 0.72f;
            }
            hip = ClampHip(hip, pedalN, pedalF, footN, footF, floor);
            if (seated > 0f) hip = Vector2.Lerp(hip, saddle, seated);     // сидя (и вилли сидя) — таз на седле
            _hip = hip; _hasHip = true;
            Skel s = Skeleton(hip, th0, e0, pedalN, pedalF, footN, footF);

            // голова: взгляд по ходу движения (почти горизонт), пружина шеи, пределы поворота
            Vector2 v = bike.Velocity;
            float look = Mathf.Atan2(v.y, Mathf.Max(1f, Mathf.Abs(v.x))) * Mathf.Rad2Deg * 0.4f;
            float want = Mathf.Clamp(Mathf.DeltaAngle(0f, look - framePitch), headMin, headMax) - pumpHead * stroke * _pump;   // кивок в такт
            if (!_hasHead) { _headAngle = want; _headVel = 0f; _hasHead = true; }
            float w = neckFrequency;
            for (int i = 0; i < 4; i++)
            {
                float h = dt / 4f;
                _headVel += (w * w * (want - _headAngle) - 2f * neckDamping * w * _headVel) * h;
                _headAngle += _headVel * h;
            }
            _headAngle = Mathf.Clamp(_headAngle, headMin - 10f, headMax + 10f);

            // трюк: поза поверх обычной стойки (руки, ноги, корпус плавно уходят в позу трюка и возвращаются)
            Vector2 footPosN = pedalN, footPosF = pedalF, handN = _gripNow, handF = _gripNow;
            float headAdd = 0f;
            _cU = _cL = 1f; _yawPivot = s.hip.x; _armSpread = 0f;
            bool farFront = false;
            bool trickNow = tricks != null && tricks.Current >= 0 && !bike.IsCrashed;
            Skel body3D = s;                                                // поза трюка в координатах тела — для 3D-гонщика
            if (trickNow)
            {
                headAdd = TrickPose(ref s, tricks.Current, tricks.Phase, ref footN, ref footF, ref footPosN, ref footPosF, ref handN, ref handF);
                farFront = TrickChoreo.Get(tricks.Current, TrickChoreo.Ch.Xf, tricks.Phase) > 0.5f;   // кэн-кэн: нога через раму
                body3D = s;
            }
            // верх и низ тела поворачиваются вокруг вертикали через таз (вип, кэн-кэн…), иначе — как есть
            SquashX(_upperRoot, _cU, _yawPivot);
            SquashX(_lowerRoot, _cL, _yawPivot);

            // 360: суставы в 3D, поворот на свой угол верха и низа тела, проекция на экран
            Vector2 hipN = s.hip, hipF = s.hip;
            float sgU = 1f, sgL = 1f;
            bool r3 = _rider3D && tricks != null && tricks.Current >= 0 && !bike.IsCrashed;
            // 360: поза в плоскости тела (до проекции) — если гонщик упадёт посреди трюка, кукла строится из неё
            _hasPlanar = r3;
            if (r3)
            {
                _planS = s; _planHandN = handN; _planHandF = handF; _planFootN = footPosN; _planFootF = footPosF;
                _planFootAN = footN; _planFootAF = footF; _planHead = _headAngle + headAdd;
            }
            Vector2 hipSide = s.hip, shSide = s.sh, headSide = s.head;          // корпус и голова в плоскости байка (до поворота)
            float cuC = 1f, thick = 1f;
            if (r3)
            {
                float cu = Mathf.Cos(_aU * Mathf.Deg2Rad), su = Mathf.Sin(_aU * Mathf.Deg2Rad);
                float cl = Mathf.Cos(_aL * Mathf.Deg2Rad), sl = Mathf.Sin(_aL * Mathf.Deg2Rad);
                cuC = AtLeast(cu, torsoMinWidth);                               // корпус по ширине — не уже плеч
                thick = 1f + 0.35f * Mathf.Abs(su);                              // спиной/лицом к нам — шире
                float P = _yawPivot;
                // верх тела: середина корпуса и голова — с той же «шириной», что картинка корпуса (шея не отрывается
                // от шлема); плечи, локти — разнесены вбок на свою ширину
                Vector2 PU(Vector2 p, float z) => new Vector2(P + (p.x - P) * cuC - z * su, p.y);
                Vector2 PL(Vector2 p, float z) => new Vector2(P + (p.x - P) * cl - z * sl, p.y);
                float cb = Mathf.Cos(_aB * Mathf.Deg2Rad), sb = Mathf.Sin(_aB * Mathf.Deg2Rad);
                Vector2 PB(Vector2 p, float z) => new Vector2(P + (p.x - P) * cb - z * sb, p.y);   // байк (руль, педали)
                // таз и колени — на угол низа тела, кисти и ступни — на руле и педалях байка (его угол);
                // локти и колени сдвигаются на полпути за кистью/ступнёй (рука и нога скручиваются между ними)
                hipN = PL(s.hip, HipW); hipF = PL(s.hip, -HipW);
                Vector2 ankN = PB(s.ankleN, AnkleW), ankF = PB(s.ankleF, -AnkleW);
                s.kneeN = PL(s.kneeN, KneeW) + 0.5f * (ankN - PL(s.ankleN, AnkleW));
                s.kneeF = PL(s.kneeF, -KneeW) + 0.5f * (ankF - PL(s.ankleF, -AnkleW));
                s.ankleN = ankN; s.ankleF = ankF;
                footPosN = PB(footPosN, PedalW); footPosF = PB(footPosF, -PedalW);
                Vector2 hN3 = PB(_barEndN, barGrip.z), hF3 = PB(_barEndF, -barGrip.z);    // кисти — на руле (всегда)
                s.elbowN = PU(s.elbowN, ElbowW) + 0.5f * (hN3 - PU(handN, barGrip.z));
                s.elbowF = PU(s.elbowF, -ElbowW) + 0.5f * (hF3 - PU(handF, -barGrip.z));
                handN = hN3; handF = hF3;
                s.shN = PU(s.shN, ShoulderW); s.shF = PU(s.shF, -ShoulderW);
                s.head = PU(s.head, 0f);
                s.sh = PU(s.sh, 0f);
                s.hip = PL(s.hip, 0f);
                sgU = cu < 0f ? -1f : 1f; sgL = cl < 0f ? -1f : 1f;
            }
            SetFarLegFront(farFront || sgL < 0f);                                // спиной к нам — дальняя нога ближе
            SwapArms(r3 && sgU < 0f);

            // корпус: в плоскости байка, а узел TorsoSpin «сжимает» его по ширине вокруг таза (спиной к нам — зеркально)
            SquashX(_torsoSpin, cuC, _yawPivot);
            Place(_torso, hipSide, shSide);
            _torso.localScale = new Vector3(1f, thick, 1f);
            _head.localPosition = s.head;
            _head.localRotation = Quaternion.Euler(0f, 0f, sgU * (_headAngle + headAdd));
            _head.localScale = new Vector3(sgU, 1f, 1f);
            // 3D-шлем: кадр по углу головы; у бока (0°/360°) плавно сменяется нарисованным
            Sprite[] hp = skin.helmetYawPaint, ht = skin.helmetYawTop;
            bool yawOk = r3 && hp != null && ht != null && hp.Length > 0 && hp.Length == ht.Length;
            float aH = Mathf.Repeat(_aH, 360f);
            _headFade = yawOk ? SmoothStep(Mathf.Min(aH, 360f - aH) / 25f) : 0f;
            if (_headYaw.gameObject.activeSelf != _headFade > 0.001f) _headYaw.gameObject.SetActive(_headFade > 0.001f);
            if (_headFade > 0.001f)
            {
                int n = hp.Length, i = ((Mathf.RoundToInt(aH / 360f * n) % n) + n) % n;
                _headYawPaint.sprite = hp[i]; _headYawTop.sprite = ht[i];
                float a3 = Mathf.Clamp01(_headFade * 4f);                    // 3D проявляется быстрее, чем гаснет нарисованный
                Color hc = _helmetColor; hc.a = a3; _headYawPaint.color = hc;
                _headYawTop.color = new Color(1f, 1f, 1f, a3);
                _headYaw.localPosition = s.head;
                // кивок головы виден сбоку как поворот, спереди/сзади — почти нет
                _headYaw.localRotation = Quaternion.Euler(0f, 0f, (_headAngle + headAdd) * Mathf.Cos(aH * Mathf.Deg2Rad));
            }
            foreach (SpriteRenderer sr in _headSr)
            {
                if (sr == null) continue;
                bool lit = sr.sprite != null && (sr.sprite.name.EndsWith("_lu") || sr.sprite.name.EndsWith("_ld"));
                if (lit) continue;                                              // свет — в UpdateLight
                Color c = sr.color; c.a = 1f - _headFade; sr.color = c;
            }
            if (headTrigger != null) headTrigger.position = _upperRoot.TransformPoint(s.head);
            PlaceLeg(_legNear, hipN, s.kneeN, s.ankleN, footPosN, footN);
            PlaceLeg(_legFar, hipF, s.kneeF, s.ankleF, footPosF, footF);
            Place(_upperArmNear, s.shN, s.elbowN); Place(_forearmNear, s.elbowN, handN);
            Place(_upperArmFar, s.shF, s.elbowF); Place(_forearmFar, s.elbowF, handF);
            // кости видны короче (повёрнуты к нам), картинки зеркально, когда гонщик развёрнут
            float cl2 = r3 ? Mathf.Abs(Mathf.Cos(_aL * Mathf.Deg2Rad)) : 1f;
            foreach (Limb leg in new[] { _legNear, _legFar })
                if (leg.foot != null)
                {
                    leg.foot.localRotation = Quaternion.Euler(0f, 0f, leg.foot.localEulerAngles.z * sgL);
                    leg.foot.localScale = new Vector3(sgL * (r3 ? Mathf.Max(0.45f, cl2) : 1f), 1f, 1f);
                }
            Foreshorten(_legSkinN, _legNear.upper, _legNear.lower, hipN, s.kneeN, s.ankleN, thigh, shin, sgL, r3);
            Foreshorten(_legSkinF, _legFar.upper, _legFar.lower, hipF, s.kneeF, s.ankleF, thigh, shin, sgL, r3);
            bool spread = _armSpread > 0.001f && tricks != null && tricks.Current >= 0 && !bike.IsCrashed;
            Foreshorten(_armSkinN, _upperArmNear, _forearmNear, s.shN, s.elbowN, handN, upperArm, forearm, sgU, r3 || spread);
            Foreshorten(_armSkinF, _upperArmFar, _forearmFar, s.shF, s.elbowF, handF, upperArm, forearm, sgU, r3 || spread);
            // корпус «сжат» узлом TorsoSpin — угол корпуса для сгиба у таза и плеча берём по точкам, а не по кости
            float rootA = r3 ? Angle((Vector2)_upperRoot.TransformPoint(s.sh) - (Vector2)_upperRoot.TransformPoint(s.hip)) : float.NaN;
            foreach (SkinnedLimb sl in _skins) sl.rootAngle = rootA;
            if (trickNow) ExportTrick3D(body3D, pedalN, pedalF, footN, footF, headAdd, r3);
            else ExportPose3D(s, handN, handF);
        }

        /// <summary>Суставы для 3D-гонщика: те же точки, что у нарисованного, в мировых координатах.</summary>
        private void ExportPose3D(Skel s, Vector2 handN, Vector2 handF)
        {
            Pose3D.valid = !bike.IsCrashed && _ragdoll == null;
            Pose3D.full3D = false; Pose3D.backTurned = false; Pose3D.farLegFront = false;
            Pose3D.fwdH = Vector3.zero;                                         // голова смотрит по раме
            if (!Pose3D.valid) return;
            Vector3 L(Vector2 p) => _lowerRoot.TransformPoint(p);
            Vector3 U(Vector2 p) => _upperRoot.TransformPoint(p);
            Transform f = bike.frame.transform;
            Pose3D.forward = f.right; Pose3D.up = f.up;
            Pose3D.hip = L(s.hip); Pose3D.sh = U(s.sh); Pose3D.head = U(s.head); Pose3D.headUp = _head.up;
            Pose3D.shN = U(s.shN); Pose3D.shF = U(s.shF);
            Pose3D.elbowN = U(s.elbowN); Pose3D.elbowF = U(s.elbowF);
            Pose3D.handN = U(handN); Pose3D.handF = U(handF);
            Pose3D.kneeN = L(s.kneeN); Pose3D.kneeF = L(s.kneeF);
            Pose3D.ankleN = L(s.ankleN); Pose3D.ankleF = L(s.ankleF);
            Pose3D.footDirN = _legNear.foot != null ? _legNear.foot.right : f.right;
            Pose3D.footDirF = _legFar.foot != null ? _legFar.foot.right : f.right;
            Pose3D.gripZ = barGrip.z;
            Pose3D.seated = SmoothStep(_sit);
        }

        /// <summary>
        /// Трюк для 3D-гонщика: поза из TrickPose (каналы TrickChoreo) — в 3D, до проекции 360. Каждая точка тела — в плоскости
        /// байка плюс глубина (к зрителю — плюс), затем поворот вокруг вертикали через таз: верх тела (плечи, локти, кисти
        /// у цели) — на угол Yu, низ (таз, колени, голеностопы у цели) — на Yl, голова — на Yh. Кисти на руле и стопы на
        /// педалях — там, где руль и педали нарисованы сейчас, с глубиной по 3D-модели байка (360 — поворот _aB, вип —
        /// набок и чуть вокруг вертикали, барспин — концы руля gripZ·cos φ, тейлвип — задняя часть вокруг рулевой).
        /// Кэн-кэн (канал Xf) — дальняя нога на нашу сторону; спиной к нам — Rider3D меняет порядок Near/Far.
        /// </summary>
        private void ExportTrick3D(Skel b, Vector2 pedalN, Vector2 pedalF, float footN, float footF, float headAdd, bool r3)
        {
            Pose3D.valid = _ragdoll == null;
            if (!Pose3D.valid) return;
            Pose3D.full3D = true;
            int id = tricks.Current;
            bool whip = id == (int)BikeTricks.Id.Whip, tail = id == (int)BikeTricks.Id.Tailwhip;
            float P = _yawPivot;
            float aU = _yU, aL = _yL, aH = _yH;

            // точка тела (плоскость байка + глубина z к зрителю) → поворот вокруг вертикали через таз на угол a
            Vector3 Yaw(Vector2 p, float z, float aDeg)
            {
                float a = aDeg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a), x = p.x - P;
                return new Vector3(P + x * c - z * sn, p.y, x * sn + z * c);
            }
            Vector3 YawDir(Vector2 d, float aDeg)
            {
                float a = aDeg * Mathf.Deg2Rad;
                return new Vector3(d.x * Mathf.Cos(a), d.y, d.x * Mathf.Sin(a));
            }
            // глубина точки байка (координаты рамы) с трюковыми поворотами: вип — набок (вокруг продольной оси на высоте
            // Table Pivot Y) и чуть вокруг вертикали; тейлвип — задняя часть вокруг оси рулевой
            float BikeDepth(Vector2 pb, float z, bool rear)
            {
                if (whip)
                {
                    float ya = WhipYaw * _spinNow * Mathf.Deg2Rad, ro = WhipRoll * _spinNow * Mathf.Deg2Rad;
                    float z1 = (pb.x - P) * Mathf.Sin(ya) + z * Mathf.Cos(ya);
                    // педали (стопы): низ байка — к зрителю, а не за спину. Иначе ноги тянутся назад сквозь таз
                    // и футболка их закрывает; так обе ноги выходят из-под корпуса вперёд, к камере, и видны
                    if (rear) return z1 * Mathf.Cos(ro) + (tablePivotY - pb.y) * Mathf.Sin(ro);
                    return (pb.y - tablePivotY) * Mathf.Sin(ro) + z1 * Mathf.Cos(ro);
                }
                if (tail && rear)
                {
                    float st = Angle(headTubeTop - headTubeBottom) - 90f, sp = _spinNow * Mathf.Deg2Rad;
                    Vector2 q = Rotate(pb - headTubeBottom, -st);
                    return q.x * Mathf.Sin(sp) + z * Mathf.Cos(sp);
                }
                return z;
            }
            // точка байка: x, y — как нарисована сейчас (vis), глубина — по 3D; 360 — точка в плоскости байка, поворот здесь
            Vector3 BikePt(Vector2 vis, Vector2 pb, float z, bool rear) =>
                r3 ? Yaw(vis, z, _aB) : new Vector3(vis.x, vis.y, BikeDepth(pb, z, rear));

            // руль: концы (середины грипс)
            Vector2 ax = (headTubeTop - headTubeBottom).normalized, nf = new Vector2(ax.y, -ax.x);
            Vector2 gripBike = headTubeTop + nf * barGrip.x + ax * barGrip.y;
            Vector3 barN, barF;
            if (_tr.bar)
            {
                float c = Mathf.Cos(_barPhi * Mathf.Deg2Rad) * barGrip.z;
                barN = new Vector3(_barEndN.x, _barEndN.y, c);
                barF = new Vector3(_barEndF.x, _barEndF.y, -c);
            }
            else
            {
                barN = BikePt(_barEndN, gripBike, barGrip.z, false);
                barF = BikePt(_barEndF, gripBike, -barGrip.z, false);
            }

            // плечи и руки
            Vector3 shN = Yaw(b.shN, ShoulderW, aU), shF = Yaw(b.shF, -ShoulderW, aU);
            Vector3 handN, handF;
            if (_tr.bar)
            {
                // барспин: ближняя рука толкает руль (глубина — та же доля, что видна на экране), кулаки к груди
                Vector2 dv = _barEndN - _gripNow;
                float frac = dv.magnitude > 0.17f ? 0.17f / dv.magnitude : 1f;
                Vector3 push = new Vector3(_tr.push.x, _tr.push.y, barN.z * frac);
                handN = Vector3.LerpUnclamped(push, Yaw(_tr.chestN, 0.10f, aU), _tr.bN);
                handF = Vector3.LerpUnclamped(barF, Yaw(_tr.chestF, -0.10f, aU), _tr.bF);
            }
            else
            {
                // рука с руля — к цели от плеча; разведённые руки (Sp) прямые и уходят вбок: к нам / от нас
                float R = 0.98f * (upperArm + forearm);
                float Out(Vector2 tgt, Vector2 sh) => _tr.sp * Mathf.Sqrt(Mathf.Max(0f, R * R - (tgt - sh).sqrMagnitude));
                handN = Vector3.LerpUnclamped(barN, Yaw(_tr.hTgtN, ShoulderW + Out(_tr.hTgtN, b.shN), aU), _tr.wN);
                handF = Vector3.LerpUnclamped(barF, Yaw(_tr.hTgtF, -ShoulderW - Out(_tr.hTgtF, b.shF), aU), _tr.wF);
            }
            Vector3 elbN = Yaw(b.elbowN, ElbowW, aU), elbF = Yaw(b.elbowF, -ElbowW, aU);

            // ноги: с педалей к цели от таза; кэн-кэн — дальняя нога через раму на нашу сторону (ближняя чуть отходит)
            float xf = Mathf.Clamp01(_tr.xf);
            float zAnkN = AnkleW + 0.06f * xf, zAnkF = Mathf.Lerp(-AnkleW, 0.06f, xf);
            float zKneeN = KneeW + 0.06f * xf, zKneeF = Mathf.Lerp(-KneeW, 0.08f, xf);
            float aPedal = r3 ? _aB : whip ? WhipYaw * _spinNow : 0f;
            Vector3 hipN = Yaw(b.hip, HipW, aL), hipF = Yaw(b.hip, -HipW, aL);
            float legMax = maxLegExtension * (thigh + shin);
            Vector3 Ankle(Vector2 pedal, Vector2 pb, float foot, float zPed, Vector2 tgt, float zTgt, float k, Vector3 hj)
            {
                Vector2 off = Rotate(ankleOffset, foot);
                Vector3 onPedal = BikePt(pedal + off, pb + off, zPed, true);
                Vector3 a = Vector3.LerpUnclamped(onPedal, Yaw(tgt, zTgt, aL), k);
                Vector3 d = a - hj;
                return d.magnitude > legMax ? hj + d.normalized * legMax : a;
            }
            Vector3 ankN = Ankle(pedalN, _pedalBikeN, footN, AnkleW, _tr.aTgtN, zAnkN, _tr.kN, hipN);
            Vector3 ankF = Ankle(pedalF, _pedalBikeF, footF, -AnkleW, _tr.aTgtF, zAnkF, _tr.kF, hipF);
            // глубокий присед (тейлвип): колени расходятся в стороны — бедро идёт мимо корпуса, а не в грудь.
            // Вип — наоборот: ноги вместе, колени вперёд, к рулю (как на фото-образце), стопы на педалях сбоку
            float wl = whip ? SmoothStep(_spinNow / 40f) : 0f;
            float Fold(Vector3 a, Vector3 hj) => SmoothStep((0.75f * (thigh + shin) - (a - hj).magnitude) / (0.35f * (thigh + shin)));
            zKneeN += 0.16f * Fold(ankN, hipN) * (1f - wl);
            zKneeF -= 0.16f * Fold(ankF, hipF) * (1f - xf) * (1f - wl);
            Vector3 kneeN = Yaw(b.kneeN, zKneeN, aL), kneeF = Yaw(b.kneeF, zKneeF, aL);
            if (wl > 0f)
            {
                Vector3 fwdKnee = new Vector3(0.30f, 0.12f, 0f);
                kneeN = Vector3.Lerp(kneeN, (hipN + ankN) * 0.5f + fwdKnee, wl);
                kneeF = Vector3.Lerp(kneeF, (hipF + ankF) * 0.5f + fwdKnee, wl);
            }
            // стопа: на педали — повёрнута с байком, у цели — с тазом
            Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            Vector3 fdN = YawDir(Dir(footN), Mathf.Lerp(aPedal, aL, Mathf.Clamp01(_tr.kN)));
            Vector3 fdF = YawDir(Dir(footF), Mathf.Lerp(aPedal, aL, Mathf.Clamp01(_tr.kF)));

            // кэн-кэн: дальняя нога перекинута через раму, и обе ноги разгибаются вбок — к зрителю (ближняя чуть выше
            // и впереди, дальняя ниже), подошвы смотрят в камеру; к концу трюка ноги так же возвращаются на педали
            if (id == (int)BikeTricks.Id.CanCan)
            {
                float t = tricks.Phase;
                float side = SmoothStep((t - 0.24f) / 0.14f) * SmoothStep((0.74f - t) / 0.14f);
                if (side > 0f)
                {
                    float reach = CanCanReach * (thigh + shin);
                    Vector3 outN = hipN + new Vector3(0.12f, -0.10f, 1f).normalized * reach;
                    Vector3 outF = hipF + new Vector3(0.04f, -0.32f, 1f).normalized * reach;
                    ankN = Vector3.Lerp(ankN, outN, side);
                    ankF = Vector3.Lerp(ankF, outF, side);
                    // колено чуть вверх — нога почти прямая, слегка согнута
                    kneeN = Vector3.Lerp(kneeN, (hipN + ankN) * 0.5f + new Vector3(0.03f, 0.06f, 0f), side);
                    kneeF = Vector3.Lerp(kneeF, (hipF + ankF) * 0.5f + new Vector3(0.03f, 0.06f, 0f), side);
                    // стопа поперёк ноги носком вверх — подошва точно в камеру. Поворот — всё время поперёк ноги (стопа
                    // не проходит через вывернутые положения, пока нога разгибается к зрителю)
                    Vector3 Perp(Vector3 v, Vector3 leg, Vector3 alt)
                    {
                        Vector3 u = v - leg * Vector3.Dot(v, leg);
                        return u.sqrMagnitude > 1e-6f ? u.normalized : alt;
                    }
                    Vector3 legN = (ankN - hipN).normalized, legF = (ankF - hipF).normalized;
                    Vector3 soleN = Perp(Vector3.up, legN, Vector3.right), soleF = Perp(Vector3.up, legF, Vector3.right);
                    fdN = Vector3.Slerp(Perp(fdN, legN, soleN), soleN, side).normalized;
                    fdF = Vector3.Slerp(Perp(fdF, legF, soleF), soleF, side).normalized;
                }
            }

            // голова: кивок (как у нарисованной) в плоскости, повёрнутой на угол головы
            Vector3 headUp = YawDir(Rotate(Vector2.up, _headAngle + headAdd), aH);

            // руки на руле не длиннее руки: если руль дальше (поза таза в игре от физики, байк повёрнут и сдвинут),
            // корпус сам наклоняется к рулю вокруг таза — кисти никогда не слезают с грипс
            Vector3 hipC = Yaw(b.hip, 0f, aL), shC = Yaw(b.sh, 0f, aU), headC = Yaw(b.head, 0f, aU);
            float holdN = 1f - Mathf.Clamp01(_tr.bar ? _tr.bN : _tr.wN), holdF = 1f - Mathf.Clamp01(_tr.bar ? _tr.bF : _tr.wF);
            float reachMax = ArmReach * (upperArm + forearm);
            for (int it = 0; it < 4; it++)
            {
                Vector3 corr = Vector3.zero; int n = 0;
                Vector3 dN = handN - shN, dF = handF - shF;
                if (holdN > 0f && dN.magnitude > reachMax) { corr += dN.normalized * (dN.magnitude - reachMax) * holdN; n++; }
                if (holdF > 0f && dF.magnitude > reachMax) { corr += dF.normalized * (dF.magnitude - reachMax) * holdF; n++; }
                if (n == 0) break;
                Vector3 v0 = shC - hipC;
                Quaternion q = Quaternion.FromToRotation(v0, v0 + corr / n);
                Vector3 R(Vector3 v) => hipC + q * (v - hipC);
                shC = R(shC); headC = R(headC); shN = R(shN); shF = R(shF); elbN = R(elbN); elbF = R(elbF);
                headUp = q * headUp;
            }

            Vector3 W(Vector3 v) => _vroot.TransformPoint(new Vector3(v.x, v.y, -v.z));   // глубина к зрителю — это −Z
            Vector3 WD(Vector3 v) => _vroot.TransformDirection(new Vector3(v.x, v.y, -v.z));
            Pose3D.forward = _vroot.right; Pose3D.up = _vroot.up;
            Pose3D.fwdU = WD(YawDir(Vector2.right, aU));
            Pose3D.fwdL = WD(YawDir(Vector2.right, aL));
            Pose3D.fwdH = WD(YawDir(Vector2.right, aH));
            Pose3D.hip = W(hipC); Pose3D.sh = W(shC); Pose3D.head = W(headC);
            Pose3D.headUp = WD(headUp);
            Pose3D.shN = W(shN); Pose3D.shF = W(shF);
            Pose3D.elbowN = W(elbN); Pose3D.elbowF = W(elbF);
            Pose3D.handN = W(handN); Pose3D.handF = W(handF);
            Pose3D.kneeN = W(kneeN); Pose3D.kneeF = W(kneeF);
            Pose3D.ankleN = W(ankN); Pose3D.ankleF = W(ankF);
            Pose3D.footDirN = WD(fdN); Pose3D.footDirF = WD(fdF);
            Pose3D.gripZ = barGrip.z;
            Pose3D.seated = SmoothStep(_sit);
            Pose3D.backTurned = Mathf.Cos(aL * Mathf.Deg2Rad) < 0f;
            Pose3D.farLegFront = xf > 0.5f || (whip && _spinNow > 20f);          // вип: дальняя нога перед байком
        }

        /// <summary>
        /// Падение: кости 3D-модели — по телам куклы RiderRagdoll (к ним перешли нарисованные части: корпус, голова,
        /// бёдра, голени со ступнями, плечи, предплечья). Точки в плоскости; «вперёд» — от спины к груди корпуса куклы.
        /// </summary>
        private void ExportRagdoll3D()
        {
            Pose3D.valid = _torso != null && _head != null && _legNear?.lower != null && _legFar?.lower != null
                           && _upperArmNear != null && _forearmNear != null && _upperArmFar != null && _forearmFar != null;
            Pose3D.full3D = false; Pose3D.backTurned = false; Pose3D.farLegFront = false;
            if (!Pose3D.valid) return;
            Vector3 hip = _torso.position, spine = _torso.right;
            spine.z = 0f; spine.Normalize();
            Pose3D.up = spine;
            Pose3D.forward = new Vector3(spine.y, -spine.x, 0f);                // грудь: корпус повёрнут на −90°
            Pose3D.hip = hip;
            Pose3D.sh = hip + spine * torsoLength;
            Pose3D.head = _head.position; Pose3D.headUp = _head.up;
            Pose3D.fwdH = _head.right;                                          // лицо — по самой голове куклы
            Pose3D.shN = _upperArmNear.position; Pose3D.shF = _upperArmFar.position;
            Pose3D.elbowN = _forearmNear.position; Pose3D.elbowF = _forearmFar.position;
            Pose3D.handN = _forearmNear.position + _forearmNear.right * forearm;
            Pose3D.handF = _forearmFar.position + _forearmFar.right * forearm;
            Pose3D.kneeN = _legNear.lower.position; Pose3D.kneeF = _legFar.lower.position;
            Pose3D.ankleN = _legNear.foot != null ? _legNear.foot.TransformPoint(ankleOffset) : _legNear.lower.position + _legNear.lower.right * shin;
            Pose3D.ankleF = _legFar.foot != null ? _legFar.foot.TransformPoint(ankleOffset) : _legFar.lower.position + _legFar.lower.right * shin;
            Pose3D.footDirN = _legNear.foot != null ? _legNear.foot.right : (Vector3)Pose3D.forward;
            Pose3D.footDirF = _legFar.foot != null ? _legFar.foot.right : (Vector3)Pose3D.forward;
            Pose3D.gripZ = ShoulderW + 0.03f;                                   // кисти чуть шире плеч
            Pose3D.seated = 0f;
        }

        /// <summary>Показать/спрятать нарисованного гонщика (байк остаётся).</summary>
        public void SetRider2DVisible(bool on)
        {
            if (_rider2D == null || on == _rider2DOn) return;
            _rider2DOn = on;
            foreach (Renderer r in _rider2D) if (r != null) r.forceRenderingOff = !on;
        }

        /// <summary>360: нога/рука, повёрнутая к нам, видна короче; спиной к нам — зеркально по толщине.</summary>
        private static void Foreshorten(SkinnedLimb skin, Transform upper, Transform lower, Vector2 a, Vector2 b, Vector2 c,
                                        float l1, float l2, float sg, bool on)
        {
            float k1 = on ? Mathf.Clamp(Vector2.Distance(a, b) / l1, 0.05f, 1.15f) : 1f;   // скручивание чуть тянет руку/ногу
            float k2 = on ? Mathf.Clamp(Vector2.Distance(b, c) / l2, 0.05f, 1.15f) : 1f;
            if (!on) sg = 1f;
            if (skin != null) { skin.k1 = k1; skin.k2 = k2; skin.flipY = sg; return; }
            if (upper != null) upper.localScale = new Vector3(k1, sg, 1f);       // отдельные картинки (без сгибов)
            if (lower != null) lower.localScale = new Vector3(k2, sg, 1f);
        }

        // ------------------------------------------------------------------ трюки

        /// <summary>
        /// Поза трюка (ход t 0…1) поверх обычной стойки — гонщик сам делает трюк: замах, толчок, поза, возврат к приземлению.
        /// Хореография всех трюков, кроме барспина, — ключи в TrickChoreo (Tools/art/tricks_choreo.py): сдвиг таза, поворот
        /// корпуса и головы, руки (с руля к цели от плеча), ноги (с педалей к цели от таза), углы стоп. В начале и в конце
        /// все каналы — ноль: трюк начинается и кончается в обычной стойке. Возвращает поворот головы, °.
        /// </summary>
        private float TrickPose(ref Skel s, int id, float t, ref float footN, ref float footF,
                                ref Vector2 posN, ref Vector2 posF, ref Vector2 handN, ref Vector2 handF)
        {
            bool barspin = id == (int)BikeTricks.Id.Barspin;
            _tr = default;
            _yU = _yL = _yH = 0f;
            if (!barspin && !TrickChoreo.Has(id)) return 0f;
            Vector2 dHip, hN = Vector2.zero, hF = Vector2.zero, aN = Vector2.zero, aF = Vector2.zero;
            float dTh, head, wN = 0f, wF = 0f, kN = 0f, kF = 0f, fN = footN, fF = footF;
            if (barspin)
            {
                // по кадрам Bike Mayhem: «поп» — таз чуть вниз и вверх, байк задирает нос, гонщик выпрямляется
                // и смотрит на руль; руки — см. BarspinHands
                float e = Env(t, 0.14f, 0.8f), dip = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.16f));
                dip *= dip;                                                     // «поп» без рывка: из покоя и в покой
                dHip = new Vector2(0.015f * e, 0.045f * e - 0.025f * dip);
                dTh = 9f * e;
                head = -16f * e;
            }
            else
            {
                dHip = new Vector2(Ch(id, TrickChoreo.Ch.Hx, t), Ch(id, TrickChoreo.Ch.Hy, t));
                dTh = Ch(id, TrickChoreo.Ch.Th, t);
                head = Ch(id, TrickChoreo.Ch.Hd, t);
                wN = Ch(id, TrickChoreo.Ch.Wn, t); wF = Ch(id, TrickChoreo.Ch.Wf, t);
                hN = new Vector2(Ch(id, TrickChoreo.Ch.Nx, t), Ch(id, TrickChoreo.Ch.Ny, t));
                hF = new Vector2(Ch(id, TrickChoreo.Ch.Fx, t), Ch(id, TrickChoreo.Ch.Fy, t));
                kN = Ch(id, TrickChoreo.Ch.Kn, t); kF = Ch(id, TrickChoreo.Ch.Kf, t);
                aN = new Vector2(Ch(id, TrickChoreo.Ch.Ax, t), Ch(id, TrickChoreo.Ch.Ay, t));
                aF = new Vector2(Ch(id, TrickChoreo.Ch.Bx, t), Ch(id, TrickChoreo.Ch.By, t));
                fN = Ch(id, TrickChoreo.Ch.An, t); fF = Ch(id, TrickChoreo.Ch.Af, t);
            }

            // корпус: таз сдвигается, корпус поворачивается вокруг таза от своей стойки
            Vector2 hip = s.hip + dHip;
            // поворот тела вокруг вертикали через таз: верх (плечи, руки, голова) и низ (таз, ноги) — каждый на свой угол;
            // руки и ноги тянутся к рулю и педалям байка, повёрнутого на свой угол (тело скручивается)
            if (!barspin)
            {
                // углы для 3D-гонщика (все трюки): верх, низ тела и голова
                _yU = Ch(id, TrickChoreo.Ch.Yu, t);
                _yL = Ch(id, TrickChoreo.Ch.Yl, t);
                _yH = TrickChoreo.HasChannel(id, TrickChoreo.Ch.Yh) ? Ch(id, TrickChoreo.Ch.Yh, t) : _yU;
                _tr.xf = Ch(id, TrickChoreo.Ch.Xf, t);
            }
            if (_rider3D)
            {
                // 360: тело поворачивается в 3D (см. PoseRider) — здесь только углы
                _aU = Ch(id, TrickChoreo.Ch.Yu, t);
                _aL = Ch(id, TrickChoreo.Ch.Yl, t);
                _aH = TrickChoreo.HasChannel(id, TrickChoreo.Ch.Yh) ? Ch(id, TrickChoreo.Ch.Yh, t) : _aU;   // голова ведёт поворот
            }
            else if (!barspin)
            {
                _cU = AtLeast(Mathf.Cos(Ch(id, TrickChoreo.Ch.Yu, t) * Mathf.Deg2Rad), riderMinWidth);
                _cL = AtLeast(Mathf.Cos(Ch(id, TrickChoreo.Ch.Yl, t) * Mathf.Deg2Rad), riderMinWidth);
            }
            _yawPivot = hip.x;
            posN = ToBody(posN, _cL); posF = ToBody(posF, _cL);
            Vector2 sh = hip + Rotate(s.sh - s.hip, dTh);
            Vector2 neckNow = s.head - s.sh;                                     // голова на шее — поворачивается с корпусом
            s.hip = hip; s.sh = sh;
            s.theta = Angle(sh - hip);
            s.head = sh + Rotate(neckNow, dTh);
            s.shN = sh; s.shF = sh + farShoulderOffset;

            // руки: с руля (или с грипс повёрнутого руля) к цели от плеча и обратно, не длиннее руки;
            // локоть всегда снизу: рука вперёд — локоть вниз-назад, рука за спину — вниз-вперёд
            float armLen = 0.98f * (upperArm + forearm);
            if (barspin) BarspinHands(t, s.shN, s.shF, out handN, out handF);
            else
            {
                handN = Vector2.LerpUnclamped(ToBody(_barEndN, _cU), s.shN + hN, wN);
                handF = Vector2.LerpUnclamped(ToBody(_barEndF, _cU), s.shF + hF, wF);
            }
            handN = KeepWithin(handN, s.shN, armLen);
            handF = KeepWithin(handF, s.shF, armLen);
            // локоть всегда вниз (как у живого человека), без «выворачивания»; руки, разведённые в стороны (к нам и от нас),
            // почти прямые и видны короче (SkinnedLimb укорачивает картинку — см. Foreshorten)
            _armSpread = barspin ? 0f : Mathf.Clamp01(Ch(id, TrickChoreo.Ch.Sp, t));
            s.elbowN = SpreadElbow(s.shN, handN, ElbowDown(s.shN, handN), _armSpread);
            s.elbowF = SpreadElbow(s.shF, handF, ElbowDown(s.shF, handF), _armSpread);

            // ноги: с педалей к цели от таза и обратно, не длиннее ноги; стопа — от голеностопа
            float legMax = maxLegExtension * (thigh + shin);
            footN = Mathf.Lerp(footN, fN, Mathf.Clamp01(kN));
            footF = Mathf.Lerp(footF, fF, Mathf.Clamp01(kF));
            s.ankleN = KeepWithin(Vector2.LerpUnclamped(posN + Rotate(ankleOffset, footN), hip + aN, kN), hip, legMax);
            s.ankleF = KeepWithin(Vector2.LerpUnclamped(posF + Rotate(ankleOffset, footF), hip + aF, kF), hip, legMax);
            posN = s.ankleN - Rotate(ankleOffset, footN);
            posF = s.ankleF - Rotate(ankleOffset, footF);
            s.kneeN = TwoBone(hip, s.ankleN, thigh, shin, +1f);
            s.kneeF = TwoBone(hip, s.ankleF, thigh, shin, +1f);
            // для 3D-гонщика: веса и цели в координатах тела (руль и педали он берёт сам — с глубиной)
            _tr.wN = wN; _tr.wF = wF; _tr.kN = kN; _tr.kF = kF; _tr.sp = _armSpread;
            _tr.hTgtN = s.shN + hN; _tr.hTgtF = s.shF + hF;
            _tr.aTgtN = hip + aN; _tr.aTgtF = hip + aF;
            return head + 0.5f * dTh;                                            // голова наклоняется с корпусом наполовину
        }

        private static float Ch(int id, TrickChoreo.Ch ch, float t) => TrickChoreo.Get(id, ch, t);

        /// <summary>Локоть: из двух решений — то, что ниже (по телу гонщика, с учётом наклона байка в трюке).</summary>
        private Vector2 ElbowDown(Vector2 sh, Vector2 hand)
        {
            Vector2 a = TwoBone(sh, hand, upperArm, forearm, -1f), b = TwoBone(sh, hand, upperArm, forearm, +1f);
            Vector2 down = Rotate(Vector2.down, -_visPitch);
            return Vector2.Dot(a - sh, down) >= Vector2.Dot(b - sh, down) ? a : b;
        }

        /// <summary>Рука разведена в сторону на долю k: локоть — к середине прямой плечо–кисть (рука прямая, видна короче).</summary>
        private static Vector2 SpreadElbow(Vector2 sh, Vector2 hand, Vector2 bent, float k) =>
            k <= 0f ? bent : Vector2.Lerp(bent, Vector2.Lerp(sh, hand, 0.5f), SmoothStep(k));

        /// <summary>Дальняя нога перекинута через раму на нашу сторону — рисуется перед байком (но за ближней ногой).</summary>
        private void SetFarLegFront(bool front)
        {
            if (front == _farLegFront) return;
            _farLegFront = front;
            for (int i = 0; i < _farLeg.Count; i++)
                if (_farLeg[i] != null) _farLeg[i].sortingOrder = _farLegOrder[i] + (front ? _farLegLift : 0);
        }

        /// <summary>Точка байка (как он нарисован) → в узел части тела, повёрнутой на «сжатие» c вокруг таза.</summary>
        private Vector2 ToBody(Vector2 q, float c) => c == 1f ? q : new Vector2(_yawPivot + (q.x - _yawPivot) / c, q.y);

        private static Sprite FirstFrame(Sprite[] frames) => frames != null && frames.Length > 0 ? frames[0] : null;

        // барспин: на каком отрезке хода трюка крутится руль
        private const float BarSpinFrom = 0.08f, BarSpinTo = 0.84f;

        /// <summary>
        /// Руки в барспине (по кадрам Bike Mayhem, ход трюка 0…1, весь трюк ≈0.6 с):
        ///   • дальняя рука отпускает руль первой (0.10) и рывком уходит кулаком к груди, хватает руль последней (0.80→0.96);
        ///   • ближняя рука толкает руль — держит ближний конец, пока он уходит назад к гонщику (около четверти оборота),
        ///     отпускает его (0.22) и тоже кулак к груди; хватает руль первой (0.72→0.86), когда он докручивается;
        ///   • кулаки перед грудью, локти согнуты и отведены назад, чуть «пружинят».
        /// </summary>
        private void BarspinHands(float t, Vector2 shN, Vector2 shF, out Vector2 handN, out Vector2 handF)
        {
            float bob = 0.012f * Mathf.Sin(t * Mathf.PI * 4f);
            Vector2 chestN = shN + new Vector2(0.27f, -0.27f + bob);
            Vector2 chestF = shF + new Vector2(0.22f, -0.21f - bob);
            // ближняя рука толкает ближний конец руля (он уходит назад, к гонщику), но тянется за ним не дальше 17 см
            Vector2 push = _gripNow + Vector2.ClampMagnitude(_barEndN - _gripNow, 0.17f);
            float kN = HandsOff(t, 0.22f, 0.40f, 0.72f, 0.86f);
            float kF = HandsOff(t, 0.10f, 0.26f, 0.80f, 0.96f);
            handN = Vector2.LerpUnclamped(push, chestN, kN);
            handF = Vector2.LerpUnclamped(_barEndF, chestF, kF);
            _tr.bar = true; _tr.bN = kN; _tr.bF = kF;                            // для 3D-гонщика
            _tr.chestN = chestN; _tr.chestF = chestF; _tr.push = push;
        }

        /// <summary>
        /// Повороты байка в трюке — «сжатием» узлов поперёк оси вращения (в 2D так выглядит поворот вокруг вертикали или
        /// продольной оси), с шириной не меньше настоящей (покрышка, плечи гонщика), и кадрами 3D-руля:
        ///   тейлвип — задняя часть (рама, заднее колесо, маятник, шатуны) вокруг оси рулевой; руль и вилка на месте;
        ///   360 — байк с гонщиком вокруг вертикали, руль — кадры поворота (BikeSkin.barYaw);
        ///   вип — бёдра гонщика разворачивают байк вбок (до 60°) и кладут почти плашмя, руль повёрнут к приземлению (barWhip);
        ///   в 360 и випе верх и низ тела гонщика поворачиваются каждый на свой угол (TrickPose, каналы yu, yl);
        ///   барспин — вилка, колесо и кокпит вокруг оси рулевой, руль — кадры (barSpin).
        /// Угол поворота и «нос вверх» — из TrickChoreo (каналы spin, pitch). Физика не меняется.
        /// </summary>
        private void UpdateTrickRoots(Vector2 rearAxle, Vector2 frontAxle)
        {
            int id = tricks != null ? tricks.Current : -1;
            float t = id >= 0 ? tricks.Phase : 0f;
            bool bar = id == (int)BikeTricks.Id.Barspin;
            bool cho = id >= 0 && TrickChoreo.Has(id);
            float spin = cho ? Ch(id, TrickChoreo.Ch.Spin, t) : 0f;
            float pitch = cho ? Ch(id, TrickChoreo.Ch.Pitch, t) : bar ? barspinPitch * Env(t, 0.18f, 0.7f) : 0f;
            float cs = Mathf.Cos(spin * Mathf.Deg2Rad);
            bool s360 = id == (int)BikeTricks.Id.Spin360, whip = id == (int)BikeTricks.Id.Whip;
            _spinNow = spin;
            _rider3D = s360;
            _aB = s360 ? spin : 0f;
            _cWhip = id == (int)BikeTricks.Id.Tailwhip ? AtLeast(cs, 0.08f) : 1f;          // тейлвип: задняя часть
            _cB = s360 ? AtLeast(cs, 0.08f) : whip ? AtLeast(Mathf.Cos(WhipYaw * spin * Mathf.Deg2Rad), 0.08f) : 1f;   // 360, вип
            _sTab = whip ? AtLeast(Mathf.Cos(WhipRoll * spin * Mathf.Deg2Rad), 0.25f) : 1f;  // вип: байк набок
            _bikeOff = cho ? new Vector2(Ch(id, TrickChoreo.Ch.Ox, t), Ch(id, TrickChoreo.Ch.Oy, t)) : Vector2.zero;
            // руль: барспин — оборот (по кадрам Bike Mayhem: толчок — быстро пол-оборота, дальше по инерции медленнее,
            // ≈0.46 с); вип — повёрнут к приземлению
            float v = Mathf.InverseLerp(BarSpinFrom, BarSpinTo, t);
            _barPhi = bar ? 360f * BarSpinEase(v) : whip ? WhipSteer * spin : 0f;
            float c = Mathf.Cos(_barPhi * Mathf.Deg2Rad);
            _sBar = AtLeast(c, 0.06f);

            float steer = Angle(headTubeTop - headTubeBottom);
            SquashAcross(_rearAxis, _rearScale, _rearRoot, headTubeBottom, steer, _cWhip);
            // байк: 360 и вип — по длине вокруг таза гонщика; вип — ещё и по высоте (набок); плюс толчок/подтягивание гонщиком
            _bikeRoot.localPosition = new Vector3(_yawPivot * (1f - _cB) + _bikeOff.x, tablePivotY * (1f - _sTab) + _bikeOff.y, 0f);
            _bikeRoot.localScale = new Vector3(_cB, _sTab, 1f);
            // всё (байк и гонщик): нос вверх вокруг задней оси
            _visPitch = pitch;
            Quaternion rot = Quaternion.Euler(0f, 0f, pitch);
            _vroot.localScale = Vector3.one;
            _vroot.localRotation = rot;
            _vroot.localPosition = (Vector3)rearAxle - rot * (Vector3)rearAxle;

            // вилка с колесом — поперёк линии вилки (колесо остаётся на оси и становится узким эллипсом вдоль вилки);
            // кокпит — поперёк настоящей оси рулевой (низ — верх рулевого стакана). p' = h + R(θ)·S·R(−θ)·(p − h)
            Vector2 forkLine = headTubeBottom - frontAxle;
            float forkAng = forkLine.sqrMagnitude > 1e-6f ? Angle(forkLine) : 110f;
            // колесо ребром — узкое (ширина покрышки ~8% диаметра); вилку спереди видно двумя ногами — не уже ~30%
            if (whip)
            {
                // вип: переднее колесо повёрнуто к приземлению — в мире на 0.45 поворота байка, поэтому шире заднего:
                // дополнительно «разжато» по длине вокруг своей оси (вилка остаётся с рамой)
                float cf = Mathf.Clamp(AtLeast(Mathf.Cos((WhipYaw + WhipSteer) * spin * Mathf.Deg2Rad), 0.08f) / _cB, 0.3f, 4f);
                SquashAcross(_wheelAxis, _wheelScale, _wheelContent, frontAxle, 90f, cf);
                SquashAcross(_steerAxis, _steerScale, _frontContent, headTubeBottom, forkAng, 1f);
            }
            else
            {
                SquashAcross(_wheelAxis, _wheelScale, _wheelContent, headTubeBottom, forkAng, AtLeast(c, 0.08f));
                SquashAcross(_steerAxis, _steerScale, _frontContent, headTubeBottom, forkAng, AtLeast(c, 0.3f));
            }
            SquashAcross(_cpAxis, _cpScale, _cpContent, headTubeBottom, steer, _sBar);

            // концы руля (середины грипс) — по той же 3D-модели, что и кадры руля (Tools/art/barspin_frames.py)
            Vector2 ax = (headTubeTop - headTubeBottom).normalized, nf = new Vector2(ax.y, -ax.x);
            Vector2 fit = grip - (headTubeTop + nf * barGrip.x + ax * barGrip.y);   // модель и Grip могут разойтись на мм
            Sprite[] frames = null;
            float frameAng = 0f, k3D = 0f;
            Vector2 htVis = headTubeTop;
            if (bar)
            {
                // барспин: поворот на φ вокруг оси рулевой; ближний конец в первой половине оборота уходит назад — к гонщику
                float cphi = Mathf.Cos(_barPhi * Mathf.Deg2Rad), sphi = Mathf.Sin(_barPhi * Mathf.Deg2Rad);
                Vector2 mid = headTubeTop + nf * (barGrip.x * cphi) + ax * barGrip.y + fit;
                _barEndN = mid - nf * (barGrip.z * sphi);
                _barEndF = mid + nf * (barGrip.z * sphi);
                _gripNow = mid;
                frames = skin.barSpin; frameAng = _barPhi;
                k3D = SmoothStep(Mathf.Min(_barPhi, 360f - _barPhi) / 20f);
            }
            else if (s360 || whip)
            {
                // 360 и вип: руль повёрнут вокруг вертикали (вип — ещё и положен набок); концы руля — по 3D-модели
                float yaw = s360 ? spin : (WhipYaw + WhipSteer) * spin;
                float roll = whip ? WhipRoll * spin : 0f;
                htVis = BikeToVis(headTubeTop, true);
                Vector2 q = nf * barGrip.x + ax * barGrip.y + fit;
                if (s360)
                    _gripNow = _barEndN = _barEndF = BikeToVis(grip, true, false);   // 360: руки — в плоскости байка, поворот в 3D потом
                else
                {
                    _gripNow = htVis + BarPoint(q, 0f, yaw, roll);
                    _barEndN = htVis + BarPoint(q, barGrip.z, yaw, roll);
                    _barEndF = htVis + BarPoint(q, -barGrip.z, yaw, roll);
                }
                if (s360)
                {
                    float a360 = Mathf.Repeat(spin, 360f);
                    frames = skin.barYaw; frameAng = a360;
                    k3D = SmoothStep(Mathf.Min(a360, 360f - a360) / 20f);
                }
                else
                {
                    frames = skin.barWhip; frameAng = Mathf.Clamp(spin, 0f, WhipMax);
                    k3D = SmoothStep(spin / 12f);
                }
            }
            else _gripNow = _barEndN = _barEndF = BikeToVis(grip, true);

            // 3D-руль: кадр по углу; в начале и в конце поворота плавно сменяется нарисованным кокпитом
            bool show3D = frames != null && frames.Length > 0 && _barSr != null && k3D > 0.001f;
            if (!show3D) k3D = 0f;
            if (_barSpin.gameObject.activeSelf != show3D) _barSpin.gameObject.SetActive(show3D);
            if (show3D)
            {
                int n = frames.Length;
                int idx = id == (int)BikeTricks.Id.Whip
                    ? Mathf.Clamp(Mathf.RoundToInt(frameAng / WhipMax * (n - 1)), 0, n - 1)    // вип: 0…75° наклона байка
                    : ((Mathf.RoundToInt(frameAng / 360f * n) % n) + n) % n;                   // кадры поворота: 0…360°
                _barSr.sprite = frames[idx];
                _barSr.color = new Color(1f, 1f, 1f, k3D);
                // на раме — там, где сейчас нарисован верх рулевого стакана; без «сжатия» (поворот уже в картинке)
                Transform f = bike.frame.transform;
                _barSpin.localPosition = (Vector2)f.InverseTransformPoint(_vroot.TransformPoint(htVis));
                _barSpin.localRotation = _vroot.localRotation;
                _barSpin.localScale = Vector3.one;
            }
            if (_cockpitSr != null)
                for (int i = 0; i < _cockpitSr.Length; i++)
                {
                    Color cc = _cockpitCol[i];
                    cc.a *= 1f - k3D;
                    _cockpitSr[i].color = cc;
                }
        }

        /// <summary>
        /// Точка руля (q — от верха рулевого стакана в плоскости байка, z — вбок к зрителю) после поворота руля вокруг
        /// вертикали на yaw и наклона байка набок на roll — как в кадрах Tools/art/barspin_frames.py (yaw, whip).
        /// </summary>
        private static Vector2 BarPoint(Vector2 q, float z, float yawDeg, float rollDeg)
        {
            float a = yawDeg * Mathf.Deg2Rad, r = rollDeg * Mathf.Deg2Rad;
            float x1 = q.x * Mathf.Cos(a) - z * Mathf.Sin(a);
            float z1 = q.x * Mathf.Sin(a) + z * Mathf.Cos(a);
            return new Vector2(x1, q.y * Mathf.Cos(r) - z1 * Mathf.Sin(r));
        }

        private static float AtLeast(float c, float min) => Mathf.Abs(c) < min ? (c < 0f ? -min : min) : c;

        /// <summary>Узлы axis → scale → content: содержимое «сжато» по c поперёк линии через pivot под углом thetaDeg.</summary>
        private static void SquashAcross(Transform axis, Transform scale, Transform content, Vector2 pivot, float thetaDeg, float c)
        {
            if (axis == null) return;
            float a = thetaDeg - 90f;                                       // локальная +Y узла — вдоль линии
            axis.localPosition = pivot;
            axis.localRotation = Quaternion.Euler(0f, 0f, a);
            scale.localScale = new Vector3(c, 1f, 1f);
            content.localRotation = Quaternion.Euler(0f, 0f, -a);
            content.localPosition = Rotate(-pivot, -a);
        }

        /// <summary>Руки отпускают руль: быстро, но плавно (a0→a1), держатся, хватают руль (b0→b1).</summary>
        private static float HandsOff(float t, float a0, float a1, float b0, float b1)
        {
            if (t <= a0 || t >= b1) return 0f;
            if (t < a1) return Smoother((t - a0) / (a1 - a0));                   // отпускает руль: разгон и торможение плавные
            if (t <= b0) return 1f;
            return 1f - Smoother((t - b0) / (b1 - b0));                           // хватает руль: мягко, без удара
        }

        /// <summary>Плавный шаг 0→1 без скачков скорости и ускорения (6u⁵ − 15u⁴ + 10u³).</summary>
        private static float Smoother(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u * (u * (6f * u - 15f) + 10f);
        }

        /// <summary>
        /// Оборот руля в барспине 0…1: толчок быстро раскручивает руль (но не мгновенно — из покоя), дальше он крутится
        /// по инерции всё медленнее и мягко встаёт в руки.
        /// </summary>
        private static float BarSpinEase(float v) => Smoother(Mathf.Pow(Mathf.Clamp01(v), 0.72f));

        /// <summary>
        /// Точка байка (в координатах рамы) — где она нарисована сейчас в пространстве гонщика (узел Visual): тейлвип
        /// (задняя часть вокруг оси рулевой), вип (поперёк и набок вокруг таза гонщика), 360 (вокруг таза), толчок гонщиком (ox, oy). Руль в барспине —
        /// отдельно (концы руля по 3D-модели).
        /// </summary>
        private Vector2 BikeToVis(Vector2 p, bool front, bool yaw = true)
        {
            if (!front && _cWhip != 1f)
            {
                float a = Angle(headTubeTop - headTubeBottom) - 90f;
                Vector2 q = Rotate(p - headTubeBottom, -a);
                q.x *= _cWhip;
                p = headTubeBottom + Rotate(q, a);
            }
            p.y = tablePivotY + (p.y - tablePivotY) * _sTab;
            if (yaw) p.x = _yawPivot + (p.x - _yawPivot) * _cB;
            return p + _bikeOff;
        }

        /// <summary>Байк и гонщик без трюковых поворотов; картинки колёс — снова на самих колёсах (падение, разлом).</summary>
        private void ResetTrickRoots()
        {
            _cWhip = _sBar = _sTab = _cB = _cU = _cL = 1f;
            _bikeOff = Vector2.zero;
            _visPitch = 0f; _armSpread = 0f;
            SetFarLegFront(false);
            SwapArms(false);
            _rider3D = false;
            if (_torsoSpin != null) { _torsoSpin.localPosition = Vector3.zero; _torsoSpin.localScale = Vector3.one; }
            _headFade = 0f;
            if (_headYaw != null) _headYaw.gameObject.SetActive(false);
            if (_headSr != null) foreach (SpriteRenderer sr in _headSr) if (sr != null && !(sr.sprite != null && (sr.sprite.name.EndsWith("_lu") || sr.sprite.name.EndsWith("_ld")))) { Color c = sr.color; c.a = 1f; sr.color = c; }
            foreach (SkinnedLimb sl in _skins) sl.rootAngle = float.NaN;
            // 360: части тела снова без укорочения и зеркала (кукла при падении — из обычных картинок)
            if (_torso != null) _torso.localScale = Vector3.one;
            if (_head != null) _head.localScale = Vector3.one;
            foreach (Limb leg in new[] { _legNear, _legFar })
                if (leg != null && leg.foot != null) leg.foot.localScale = Vector3.one;
            Foreshorten(_legSkinN, _legNear?.upper, _legNear?.lower, Vector2.zero, Vector2.zero, Vector2.zero, thigh, shin, 1f, false);
            Foreshorten(_legSkinF, _legFar?.upper, _legFar?.lower, Vector2.zero, Vector2.zero, Vector2.zero, thigh, shin, 1f, false);
            Foreshorten(_armSkinN, _upperArmNear, _forearmNear, Vector2.zero, Vector2.zero, Vector2.zero, upperArm, forearm, 1f, false);
            Foreshorten(_armSkinF, _upperArmFar, _forearmFar, Vector2.zero, Vector2.zero, Vector2.zero, upperArm, forearm, 1f, false);
            // узлы осей (Rear, Cockpit, Steer, Wheel) остаются как есть: без «сжатия» они ничего не меняют
            foreach (Transform t in new[] { _vroot, _bikeRoot, _frontRoot, _upperRoot, _lowerRoot })
                if (t != null) { t.localPosition = Vector3.zero; t.localScale = Vector3.one; t.localRotation = Quaternion.identity; }
            if (_steerScale != null) _steerScale.localScale = Vector3.one;       // без «сжатия» — вилка и руль как были
            if (_cpScale != null) _cpScale.localScale = Vector3.one;
            if (_wheelScale != null) _wheelScale.localScale = Vector3.one;
            if (_rearScale != null) _rearScale.localScale = Vector3.one;
            if (_barSpin != null) _barSpin.gameObject.SetActive(false);
            if (_cockpitSr != null) for (int i = 0; i < _cockpitSr.Length; i++) _cockpitSr[i].color = _cockpitCol[i];
            _barPhi = 0f;
            _barEndN = _barEndF = grip;
            _gripNow = grip;
            if (!_wheelsOnBodies && _wheelRear != null && _wheelFront != null)
            {
                _wheelsOnBodies = true;
                AttachTo(_wheelRear, bike.rearWheel.transform);
                AttachTo(_wheelFront, bike.frontWheel.transform);
            }
        }

        private static void AttachTo(Transform node, Transform body)
        {
            node.SetParent(body, false);
            node.localPosition = Vector3.zero;
            node.localRotation = Quaternion.identity;
            node.localScale = Vector3.one;
        }

        private static void PlaceWheel(Transform node, Transform wheel, Transform frame)
        {
            if (node == null) return;
            node.localPosition = (Vector2)frame.InverseTransformPoint(wheel.position);
            node.localRotation = Quaternion.Euler(0f, 0f, wheel.eulerAngles.z - frame.eulerAngles.z);
        }

        private static void SquashX(Transform node, float sx, float pivot)
        {
            if (node == null) return;
            node.localPosition = new Vector3(pivot * (1f - sx), 0f, 0f);
            node.localScale = new Vector3(sx, 1f, 1f);
        }

        /// <summary>Вес позы: нарастает до доли хода a, держится, спадает от доли b к концу.</summary>
        private static float Env(float t, float a, float b) => SmoothStep(t / a) * SmoothStep((1f - t) / (1f - b));

        private void Pref(float lean, out float theta, out float arm)
        {
            if (lean < 0f) { theta = Mathf.Lerp(torsoNeutral, torsoBack, -lean); arm = Mathf.Lerp(armNeutral, armBack, -lean); }
            else { theta = Mathf.Lerp(torsoNeutral, torsoForward, lean); arm = Mathf.Lerp(armNeutral, armForward, lean); }
        }

        private float FootAngle(float crank, bool pedaling, float compress) =>
            pedaling ? footMean + footSwing * Mathf.Cos((crank - footPhase) * Mathf.Deg2Rad) : footCoast + 12f * compress;

        private Vector2 StanceOffset(float lean) =>
            lean < 0f ? Vector2.Lerp(_visN, _visB, -lean) : Vector2.Lerp(_visN, _visF, lean);

        /// <summary>Разница центра масс нарисованных стоек и физических (RiderBody) — считается один раз.</summary>
        private void ComputeStanceOffsets()
        {
            Vector2 pn = bottomBracket + new Vector2(crankLength, 0f), pf = bottomBracket - new Vector2(crankLength, 0f);
            Vector2 physN = new Vector2(-0.106f, 0.757f), physB = new Vector2(-0.22f, 0.599f), physF = new Vector2(0.112f, 0.644f);
            if (rider != null)
            {
                physN = rider.hipNeutral + rider.comFromHipNeutral;
                physB = rider.hipBack + rider.comFromHipBack;
                physF = rider.hipForward + rider.comFromHipForward;
            }
            _visN = StanceCom(hipVisNeutral, 0f, pn, pf) - physN;
            _visB = StanceCom(hipVisBack, -1f, pn, pf) - physB;
            _visF = StanceCom(hipVisForward, 1f, pn, pf) - physF;
        }

        private Vector2 StanceCom(Vector2 hip, float lean, Vector2 pn, Vector2 pf)
        {
            Pref(lean, out float th, out float e);
            return ComOf(Skeleton(hip, th, e, pn, pf, footCoast, footCoast), pn, pf);
        }

        /// <summary>Плечо: корпус как можно ближе к предпочтительному углу, руки в пределах [armMin, armMax] длины.</summary>
        private Vector2 Shoulder(Vector2 hip, float thetaPref, float armPref, out float armUsed)
        {
            float armLen = upperArm + forearm;
            float d = Vector2.Distance(_gripNow, hip);
            float bestCost = float.MaxValue; Vector2 best = Vector2.zero; armUsed = 1f;
            for (int i = 0; i < 25; i++)
            {
                float e = armMin + (armMax - armMin) * i / 24f;
                if (d > torsoLength + e * armLen || d < Mathf.Abs(torsoLength - e * armLen)) continue;
                Vector2 sh = CircleMeet(hip, torsoLength, _gripNow, e * armLen);
                float th = Angle(sh - hip);
                if (th < torsoMin || th > torsoMax) continue;
                float cost = (th - thetaPref) * (th - thetaPref) + _armWeight * (e - armPref) * (e - armPref);
                if (cost < bestCost) { bestCost = cost; best = sh; armUsed = e; }
            }
            if (bestCost < float.MaxValue) return best;
            // руль далеко или близко: корпус под ближайшим допустимым углом
            float a = Mathf.Clamp(Angle(_gripNow - hip), torsoMin, torsoMax) * Mathf.Deg2Rad;
            Vector2 s = hip + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * torsoLength;
            armUsed = Mathf.Clamp(Vector2.Distance(_gripNow, s) / armLen, armMin, 1f);
            return s;
        }

        private Skel Skeleton(Vector2 hip, float thetaPref, float armPref, Vector2 pedalN, Vector2 pedalF, float footN, float footF)
        {
            var s = new Skel { hip = hip };
            s.sh = Shoulder(hip, thetaPref, armPref, out s.arm);
            s.theta = Angle(s.sh - hip);
            s.head = s.sh + Rotate(neck, (s.theta - 40f) * 0.6f);
            s.shN = PullShoulder(s.sh, _pullN);                             // плечи двигаются за руками: тянет руль — к рулю
            s.shF = PullShoulder(s.sh + farShoulderOffset, _pullF);
            s.ankleN = pedalN + Rotate(ankleOffset, footN);
            s.ankleF = pedalF + Rotate(ankleOffset, footF);
            s.kneeN = TwoBone(hip, s.ankleN, thigh, shin, +1f);           // колено вперёд
            s.kneeF = TwoBone(hip, s.ankleF, thigh, shin, +1f);
            s.elbowN = TwoBone(s.shN, _gripNow, upperArm, forearm, -1f);       // локоть вниз-назад
            s.elbowF = TwoBone(s.shF, _gripNow, upperArm, forearm, -1f);
            return s;
        }

        /// <summary>Плечо сдвинуто к рулю на pull (м; минус — от руля), но рука не вытягивается длиннее 98% своей длины.</summary>
        private Vector2 PullShoulder(Vector2 sh, float pull)
        {
            if (Mathf.Abs(pull) < 1e-5f) return sh;
            Vector2 d = _gripNow - sh; float l = d.magnitude;
            if (l < 1e-4f) return sh;
            float maxL = 0.98f * (upperArm + forearm);
            float newL = Mathf.Min(l - pull, Mathf.Max(l, maxL));         // от руля — только пока рука не прямая
            return _gripNow - d / l * Mathf.Max(0.3f, newL);
        }

        private Vector2 ComOf(Skel s, Vector2 pedalN, Vector2 pedalF)
        {
            Vector2 c = MTrunk * Vector2.Lerp(s.hip, s.sh, 0.5f) + MHead * s.head
                      + MUArm * (Vector2.Lerp(s.shN, s.elbowN, 0.436f) + Vector2.Lerp(s.shF, s.elbowF, 0.436f))
                      + MFArm * (Vector2.Lerp(s.elbowN, _gripNow, 0.682f) + Vector2.Lerp(s.elbowF, _gripNow, 0.682f))
                      + MThigh * (Vector2.Lerp(s.hip, s.kneeN, 0.433f) + Vector2.Lerp(s.hip, s.kneeF, 0.433f))
                      + MShank * (Vector2.Lerp(s.kneeN, s.ankleN, 0.433f) + Vector2.Lerp(s.kneeF, s.ankleF, 0.433f))
                      + MFoot * (Vector2.Lerp(s.ankleN, pedalN, 0.6f) + Vector2.Lerp(s.ankleF, pedalF, 0.6f));
            return c / (MTrunk + MHead + 2f * (MUArm + MFArm + MThigh + MShank + MFoot));
        }

        private Vector2 ClampHip(Vector2 hip, Vector2 pedalN, Vector2 pedalF, float footN, float footF, float floor)
        {
            Vector2 d = hip - _gripNow;
            if (d.magnitude > 0.92f) hip = _gripNow + d.normalized * 0.92f;    // руки и корпус не растягиваются
            float legMax = maxLegExtension * (thigh + shin);
            hip = KeepWithin(hip, pedalN + Rotate(ankleOffset, footN), legMax);
            hip = KeepWithin(hip, pedalF + Rotate(ankleOffset, footF), legMax);
            hip.y = Mathf.Max(hip.y, floor);
            return hip;
        }

        private static Vector2 KeepWithin(Vector2 p, Vector2 c, float r)
        {
            Vector2 d = p - c; float l = d.magnitude;
            return l > r ? c + d * (r / l) : p;
        }

        private void PlaceLeg(Limb leg, Vector2 hip, Vector2 knee, Vector2 ankle, Vector2 pedal, float foot)
        {
            Place(leg.upper, hip, knee);
            Place(leg.lower, knee, ankle);
            leg.foot.localPosition = pedal;
            leg.foot.localRotation = Quaternion.Euler(0f, 0f, foot);       // стопа «работает» на педали
        }

        /// <summary>Верхняя точка пересечения двух окружностей (центр c1 радиус r1, центр c2 радиус r2).</summary>
        private static Vector2 CircleMeet(Vector2 c1, float r1, Vector2 c2, float r2)
        {
            Vector2 d = c2 - c1;
            float dist = d.magnitude;
            if (dist < 1e-4f) return c1 + Vector2.up * r1;
            Vector2 u = d / dist;
            if (dist >= r1 + r2) return c1 + u * r1;
            float a = (r1 * r1 - r2 * r2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, r1 * r1 - a * a));
            return c1 + u * a + new Vector2(-u.y, u.x) * h;                // решение сверху (плечи над линией таз–руль)
        }

        /// <summary>Ставит сегмент в точку from и поворачивает его на точку to (спрайт идёт вдоль +X).</summary>
        private static void Place(Transform t, Vector2 from, Vector2 to)
        {
            t.localPosition = from;
            t.localRotation = Quaternion.Euler(0f, 0f, Angle(to - from));
        }

        // ------------------------------------------------------------------ помощники

        private static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        private static float SmoothStep(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Двухзвенная IK: положение сустава (колена/локтя). bendSign: +1 — изгиб влево от направления, -1 — вправо.</summary>
        private static Vector2 TwoBone(Vector2 root, Vector2 target, float l1, float l2, float bendSign)
        {
            Vector2 d = target - root;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            float cos = (l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist);
            float a = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * bendSign;
            Vector2 dir = d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.right;
            Vector2 rotated = new Vector2(dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a), dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));
            return root + rotated * l1;
        }
    }
}