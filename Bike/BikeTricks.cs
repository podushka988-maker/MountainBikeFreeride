using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BikeGame
{
    /// <summary>
    /// Трюки в полёте (как в Bike Mayhem): круг из 8 одинаковых кружков справа — какой кружок какой трюк, игрок запоминает сам.
    /// Нумерация кружков: 1 — нижний, дальше по кругу через левый край вверх и по правому вниз (2 — внизу слева, 3 — слева,
    /// 4 — вверху слева, 5 — верхний, 6 — вверху справа, 7 — справа, 8 — внизу справа). На клавиатуре — цифры 1–8.
    ///   1 Suicide no-hander (4 × время, с) · 2 No-footer (3 × время, с) · 3 Superman (5) · 4 Can-can (5) · 5 Tailwhip (10) · 6 360 (10) ·
    ///   7 Barspin (3) · 8 Whip (7).
    /// Нажал в воздухе — трюк длится свою длительность (позу рисует BikeVisual); доделал и приземлился — очки.
    /// Барспин можно и на заднем колесе (вилли): заднее колесо на земле, переднее — нет; коснулось переднее — падение.
    /// Во время барспина тормоз не работает.
    /// Suicide no-hander и No-footer — с удержанием: пока кружок (или цифра) зажат, гонщик держит позу (руки за спиной /
    /// ноги в стороны); отпустил — за recoverTime (0.25 с) возвращает руки на руль / ноги на педали. Коснулся земли,
    /// пока поза не собрана, — выбрасывает с байка; руки/ноги уже на месте — трюк засчитан.
    /// Коснулся земли, не доделав трюк, или нажал трюк на земле — гонщика выбрасывает с байка (падение куклой),
    /// через 2 с — итоги заезда. Висит на объекте Bike (только в игре; у бота в меню трюков нет).
    /// </summary>
    public class BikeTricks : MonoBehaviour
    {
        public enum Id { SuicideNoHander, NoFooter, Superman, CanCan, Tailwhip, Spin360, Barspin, Whip }

        [Serializable]
        public class Trick
        {
            public string name;
            public int points;
            [Tooltip("Сколько длится трюк, с (от отпускания руля/педалей до возврата).")]
            public float duration;
        }

        public BikeController2D bike;
        public BikeVisual visual;
        [Tooltip("Трюки по номерам кружков (1–8). Длительности — как у райдеров на видео: от «быстрого» барспина до 360.")]
        public Trick[] tricks =
        {
            new Trick { name = "Suicide No-Hander", points = 8, duration = 1.5f },
            new Trick { name = "No-Footer", points = 5, duration = 1.25f },
            new Trick { name = "Superman", points = 5, duration = 1.5f },
            new Trick { name = "Can-Can", points = 5, duration = 1.15f },
            new Trick { name = "Tailwhip", points = 10, duration = 1.1f },
            new Trick { name = "360", points = 10, duration = 1.0f },
            new Trick { name = "Barspin", points = 3, duration = 0.75f },
            new Trick { name = "Whip", points = 7, duration = 0.95f },
        };
        [Tooltip("Кружки на экране (по номерам 1–8).")]
        public TouchButton[] buttons;
        [Tooltip("Приземлился и столько секунд не упал — трюки засчитаны.")]
        public float landSettle = 0.35f;
        [Tooltip("Гонщика выбрасывает с байка: добавка скорости (вперёд по ходу, вверх), м/с, и кувырок, °/с.")]
        public Vector2 bailFling = new Vector2(1.5f, 1.4f);
        public float bailSpin = -220f;

        [Header("Удержание: Suicide No-Hander и No-Footer")]
        [Tooltip("Ход трюка (0…1), на котором поза раскрыта полностью: пока кнопку держат, трюк замирает здесь.")]
        public float noHanderHold = 0.54f;
        [Tooltip("Ход трюка, когда руки снова на руле (дальше земля уже не опасна).")]
        public float noHanderBack = 0.86f;
        public float noFooterHold = 0.46f;
        [Tooltip("Ход трюка, когда ноги снова на педалях.")]
        public float noFooterBack = 0.84f;
        [Tooltip("Отпустил кнопку — за столько секунд гонщик возвращает руки/ноги на место. Приземлился раньше — падение.")]
        public float recoverTime = 0.25f;
        [Tooltip("Очки за секунду трюка (от нажатия до возврата рук на руль): Suicide No-Hander.")]
        public float noHanderPointsPerSec = 4f;
        [Tooltip("Очки за секунду трюка (от нажатия до возврата ног на педали): No-Footer.")]
        public float noFooterPointsPerSec = 3f;

        /// <summary>Какой трюк сейчас выполняется (−1 — никакой) и его ход 0…1.</summary>
        public int Current { get; private set; } = -1;
        public float Phase { get; private set; }
        /// <summary>Гонщика выбросило с байка из-за трюка.</summary>
        public bool Bailed { get; private set; }
        /// <summary>Трюки засчитаны (приземлился): список сделанных за прыжок.</summary>
        public event Action<IReadOnlyList<Trick>> Landed;

        private readonly List<Trick> _done = new List<Trick>();   // доделаны в этом прыжке, ждут приземления
        private float _ground;
        private bool _released;                                    // кнопку трюка с удержанием уже отпустили
        private float _elapsed;                                    // сколько секунд идёт текущий трюк

#if ENABLE_INPUT_SYSTEM
        private static readonly Key[] Keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
        private static readonly Key[] Pad = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8 };
#endif

        private void Start()
        {
            if (bike == null) bike = GetComponent<BikeController2D>();
            if (visual == null) visual = GetComponent<BikeVisual>();
            if (buttons != null)
                for (int i = 0; i < buttons.Length; i++)
                {
                    int k = i;
                    if (buttons[i] != null) buttons[i].Pressed += () => Perform(k);
                }
        }

        private void Update()
        {
            int key = KeyPressed();
            if (key >= 0) Perform(key);
            if (bike == null || bike.IsCrashed) { Current = -1; _done.Clear(); LockBrake(false); return; }

            if (Current >= 0)
            {
                float dt = Time.deltaTime;
                _elapsed += dt;
                bool hold = HoldRange(Current, out float holdAt, out float backAt);
                if (Touching(Current))
                {
                    if (hold && Phase >= backAt) Finish();                  // руки/ноги уже на месте — засчитан
                    else { Bail(); return; }                                // не успел доделать — врезался
                }
                else if (hold && Phase < backAt)
                {
                    if (!IsHeld(Current)) _released = true;
                    if (Phase < holdAt)
                        Phase = Mathf.Min(holdAt, Phase + dt / Mathf.Max(0.1f, tricks[Current].duration));
                    else if (!_released)
                        Phase = holdAt;                                     // держит позу, пока зажата кнопка
                    else
                        Phase += dt * (backAt - holdAt) / Mathf.Max(0.05f, recoverTime);   // возвращает руки/ноги
                }
                else
                    Phase += dt / Mathf.Max(0.1f, tricks[Current].duration);
                if (Current >= 0 && Phase >= 1f) Finish();
            }
            LockBrake(Current == (int)Id.Barspin);

            if (!bike.IsGrounded) _ground = 0f;
            else if (_done.Count > 0 && Current < 0)
            {
                _ground += Time.deltaTime;
                if (_ground >= landSettle)
                {
                    var list = new List<Trick>(_done);
                    _done.Clear();
                    Landed?.Invoke(list);
                }
            }
        }

        /// <summary>
        /// Нажали кружок (номер с 0). В воздухе — трюк; на земле — гонщика выбрасывает. Барспин — ещё и на заднем колесе
        /// (переднее в воздухе); если переднее колесо на земле — выбрасывает.
        /// </summary>
        public void Perform(int index)
        {
            if (bike == null || bike.IsCrashed || Bailed || tricks == null || index < 0 || index >= tricks.Length) return;
            if (Touching(index)) { Bail(); return; }                         // нельзя делать на земле — выбрасывает
            if (Current >= 0) return;                                        // один трюк за раз
            Current = index;
            Phase = 0f;
            _released = false;
            _elapsed = 0f;
            LockBrake(index == (int)Id.Barspin);
        }

        private void Finish()
        {
            Trick t = tricks[Current];
            float perSec = Current == (int)Id.SuicideNoHander ? noHanderPointsPerSec
                         : Current == (int)Id.NoFooter ? noFooterPointsPerSec : 0f;
            if (perSec > 0f)                                            // очки = множитель × время трюка, с
                t = new Trick { name = t.name, duration = t.duration, points = Mathf.Max(1, Mathf.RoundToInt(perSec * _elapsed)) };
            _done.Add(t);
            Current = -1;
            Phase = 0f;
        }

        /// <summary>Трюк с удержанием: ход, где поза замирает, и ход, когда руки/ноги снова на месте.</summary>
        private bool HoldRange(int index, out float holdAt, out float backAt)
        {
            holdAt = backAt = 1f;
            if (index == (int)Id.SuicideNoHander) { holdAt = noHanderHold; backAt = noHanderBack; }
            else if (index == (int)Id.NoFooter) { holdAt = noFooterHold; backAt = noFooterBack; }
            else return false;
            backAt = Mathf.Max(backAt, holdAt + 0.01f);
            return true;
        }

        /// <summary>Зажат ли кружок трюка (или цифра на клавиатуре).</summary>
        private bool IsHeld(int index)
        {
            if (buttons != null && index < buttons.Length && buttons[index] != null && buttons[index].IsHeld) return true;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && index < 8 && (kb[Keys[index]].isPressed || kb[Pad[index]].isPressed);
#else
            return index < 8 && (Input.GetKey(KeyCode.Alpha1 + index) || Input.GetKey(KeyCode.Keypad1 + index));
#endif
        }

        private void LockBrake(bool on)
        {
            if (bike != null && bike.input != null) bike.input.BrakeLocked = on;
        }

        private void OnDisable() => LockBrake(false);

        /// <summary>
        /// Трюку «мешает земля»: барспин можно делать в полёте и на заднем колесе (вилли) — мешает только переднее колесо
        /// на земле; остальные трюки — только в полёте (мешает любое колесо на земле).
        /// </summary>
        private bool Touching(int index) =>
            index == (int)Id.Barspin ? bike.FrontGrounded : bike.IsGrounded;

        /// <summary>Гонщика выбрасывает с байка: падение, кукла летит по инерции вперёд-вверх и кувыркается.</summary>
        private void Bail()
        {
            if (Bailed || bike == null) return;
            Bailed = true;
            _done.Clear();
            bike.Crash();                                                   // BikeVisual создаёт куклу (в позе трюка)
            Current = -1;
            LockBrake(false);
            if (visual != null && visual.Ragdoll != null)
            {
                Vector2 fwd = bike.Velocity.x >= 0f ? Vector2.right : Vector2.left;
                visual.Ragdoll.Fling(fwd * bailFling.x + Vector2.up * bailFling.y, bailSpin);
            }
        }

        private static int KeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return -1;
            for (int i = 0; i < 8; i++)
                if (kb[Keys[i]].wasPressedThisFrame || kb[Pad[i]].wasPressedThisFrame) return i;
#else
            for (int i = 0; i < 8; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) return i;
#endif
            return -1;
        }
    }
}