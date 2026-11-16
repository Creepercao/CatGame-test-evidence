using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CatGame
{
    /// <summary>
    /// 猫：世界里的实体（SpriteRenderer），不是 UI。
    /// 负责序列帧动画、强制动作、自动漫游、打瞌睡、朝向翻转、按 y 排序。
    ///
    /// 世界像素/画布/脚线来自 WorldArtSettings；原点表示脚点，屏幕大小由相机决定。
    /// </summary>
    public class CatActor : MonoBehaviour
#if UNITY_ANDROID && !UNITY_EDITOR
        , IPointerDownHandler
#endif
    {
        public const float ArtScale = 1f;          // 保留兼容常量；实例按帧素材尺寸决定实际缩放

        public static Vector3 LastPosition;

        public SpriteRenderer Renderer { get; private set; }

        CatBreed _breed;
        float _frameClock;
        int _frame;

        Vector3 _home;                              // 活动矩形中心
        Vector2 _halfAxes = new Vector2(8f, 2.6f);  // 活动矩形半宽/半高（世界单位）
        Vector3 _target;
        Vector3 _pos;
        float _pauseUntil;
        float _speed = 0.7f;                        // 世界单位/秒
        float _visualScale = ArtScale;
        bool _moving;
        float _facing = 1f;                         // 1 = 朝右，-1 = 朝左
        bool _forced;
        string _action = ActionNames.Idle;
        string _lastAction;
        float _sleepAmount;
        Transform _zzz;
        BoxCollider2D _clickCollider;
        bool _oneShot;
        bool _resting;
        public const float SleepDelay = 2f;
        Vector2 _visualPadding;
        float _visualCenterY;
        public RoomWalkSurface WalkSurface { get; private set; }
        public float GroundOffset { get; private set; }

        public void SetWalkSurface(RoomWalkSurface surface)
        {
            WalkSurface = surface;
            PickNewTarget(0.8f); // Surface attachment must preserve Setup's first-roam distance.
        }

        bool CanReach(Vector3 candidate)
        {
            if (WalkSurface == null) return true;
            var offset = Vector3.up * GroundOffset;
            return WalkSurface.AllowsSegment(_pos + offset, candidate + offset)
                && WalkSurface.AllowsSegment(SnapPosition(_pos) + offset, SnapPosition(candidate) + offset);
        }

        static Vector3 SnapPosition(Vector3 position)
        {
            position.z = 0;
            return WorldArtSettings.Current.Snap(position);
        }

        void MoveTo(Vector3 candidate)
        {
            if (CanReach(candidate)) _pos = candidate;
            else { _moving = false; PickNewTarget(); }
        }

        public event Action<CatActor> Licked;

        public string CurrentAction
        {
            get
            {
                if (_forced) return _action;
                if (_resting && !_moving && _sleepAmount >= SleepDelay &&
                    _breed != null && _breed.UseSleepWhileResting && HasClip(ActionNames.Sleep))
                    return ActionNames.Sleep;
                if (_moving && _breed != null && _breed.UseWalkWhileMoving && HasClip(ActionNames.Walk))
                    return ActionNames.Walk;
                return ActionNames.Idle;
            }
        }

        public bool IsForced { get { return _forced; } }
        public bool IsWalking { get { return _moving && !_forced; } }
        public Vector3 Position { get { return _pos; } }
        public Vector2 FloorHalfAxes { get { return _halfAxes; } }
        public CatBreed Breed { get { return _breed; } }
        public float VisualScale { get { return _visualScale; } }

        public void Setup(CatBreed breed, Vector2 halfAxes)
        {
            Setup(breed, Vector3.zero, halfAxes);
        }

        public void Setup(CatBreed breed, Vector3 floorCenter, Vector2 halfAxes)
        {
            Renderer = GetComponent<SpriteRenderer>();
            if (Renderer == null) Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.drawMode = SpriteDrawMode.Simple;
            _clickCollider = GetComponent<BoxCollider2D>();
            if (_clickCollider == null) _clickCollider = gameObject.AddComponent<BoxCollider2D>();
            _halfAxes = halfAxes;
            _home = floorCenter;
            _pos = _home;
            Bind(breed);
            _pos = SafeCenter();
            PickNewTarget(0.8f);
            _pauseUntil = Time.time + 0.6f;      // 出生先站一会儿，别一上来就走
            ApplyVisual();
        }

        /// <summary>把活动范围改成"以 center 为中心、halfAxes 为半宽半高"的矩形。</summary>
        public void SetFloor(Vector3 center, Vector2 halfAxes)
        {
            _home = center;
            _halfAxes = halfAxes;
            _pos = SafeCenter();
            PickNewTarget();
            ApplyVisual();
        }

        public void Bind(CatBreed breed)
        {
            _resting = false;
            _sleepAmount = 0f;
            _breed = breed;
            _visualScale = ScaleForBreed(breed);
            GroundOffset = breed != null && breed.Idle != null && breed.Idle.Length > 0
                ? (breed.Idle[0].bounds.min.y + breed.Idle[0].bounds.size.y * WorldArtSettings.Current.CatFootFraction) * _visualScale : 0;
            _visualPadding = Vector2.zero;
            float bottom = 0, top = 0;
            foreach (var action in ActionNames.All)
            {
                var frames = breed != null ? breed.Clip(action) : null;
                if (frames == null) continue;
                foreach (var frame in frames)
                {
                    if (frame == null) continue;
                    var bounds = frame.bounds;
                    _visualPadding.x = Mathf.Max(_visualPadding.x,
                        Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) * _visualScale);
                    bottom = Mathf.Min(bottom, bounds.min.y * _visualScale);
                    top = Mathf.Max(top, bounds.max.y * _visualScale);
                }
            }
            _visualPadding.y = (top - bottom) * .5f;
            _visualCenterY = (top + bottom) * .5f;
            _frame = 0;
            _frameClock = 0f;
            _lastAction = null;
            ApplyVisual();
        }

        /// <summary>强制循环播某个动作。返回 false 表示动作名非法或这个品种没有该帧。</summary>
        public bool PlayAction(string action)
        {
            if (!ActionNames.IsValid(action) || !HasClip(action)) return false;

            _forced = true;
            _resting = false;
            _oneShot = false;
            _sleepAmount = 0f;
            _action = action;

            // 走/跑：立刻朝新目标出发，别在原地等漫游的停顿
            if (action == ActionNames.Walk || action == ActionNames.Run)
            {
                _speed = action == ActionNames.Run ? 2f : 0.78f;
                PickNewTarget(1.5f);        // 目标必须够远，否则会原地停住
                _pauseUntil = Time.time;
            }
            else
            {
                _speed = 0.7f;
                _moving = false;
            }

            ApplyVisual();
            return true;
        }

        /// <summary>回到自动模式（自己漫游 + 长时间没人管会趴下睡）。</summary>
        public void ResumeAuto()
        {
            _resting = false;
            _sleepAmount = 0f;
            _forced = false;
            _oneShot = false;
            _moving = false;
            _speed = 0.7f;
            _pauseUntil = Time.time + UnityEngine.Random.Range(0.4f, 1.2f);
            ApplyVisual();
        }

        /// <summary>Handle one player click: play Jump once when available and notify the roster.</summary>
        public void OnLick()
        {
            if (CatWorld.I != null && CatWorld.I.Hud != null && CatWorld.I.Hud.Modals != null && CatWorld.I.Hud.Modals.InputBlocked) return;
            PlayOneShot(ActionNames.Jump);
            if (Licked != null) Licked(this);
        }

        public bool PlayOneShot(string action)
        {
            if (!ActionNames.IsValid(action) || !HasClip(action)) return false;
            _forced = true;
            _resting = false;
            _oneShot = true;
            _sleepAmount = 0f;
            _action = action;
            _frame = 0;
            _frameClock = 0f;
            _moving = false;
            ApplyVisual();
            return true;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnLick();
        }
#else
        void OnMouseDown()
        {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (PlayerVisualQA.IsScriptedFlow) return;
#endif
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            OnLick();
        }
#endif

        bool HasClip(string action)
        {
            if (_breed == null) return false;
            var clip = _breed.Clip(action);
            return clip != null && clip.Length > 0;
        }

        Sprite[] ActiveClip()
        {
            if (_breed == null) return null;
            if (_forced && HasClip(_action)) return _breed.Clip(_action);
            if (CurrentAction == ActionNames.Sleep && HasClip(ActionNames.Sleep)) return _breed.Sleep;
            if (CurrentAction == ActionNames.Walk && HasClip(ActionNames.Walk)) return _breed.Walk;
            return _breed.Idle;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (!_forced) Wander(dt);
            else if (_action == ActionNames.Walk || _action == ActionNames.Run) StepForced(dt);

            if (_moving || (_forced && _action != ActionNames.Idle)) _sleepAmount = 0f;
            else _sleepAmount += dt;

            Animate(dt);
            ApplyVisual();
            LastPosition = _pos;
        }

        /// <summary>强制走/跑时的移动：走到目标就立刻换一个新目标，不会停下来。</summary>
        void StepForced(float dt)
        {
            var step = _target - _pos;
            float dist = step.magnitude;
            if (dist < 0.05f)
            {
                PickNewTarget(1.5f);
                _moving = false;
                return;
            }

            _moving = true;
            MoveTo(_pos + step / dist * Mathf.Min(_speed * dt, dist));
            _facing = step.x >= 0f ? 1f : -1f;
        }

        void Wander(float dt)
        {
            if (Time.time < _pauseUntil)
            {
                _moving = false;
                return;
            }

            var step = _target - _pos;
            float dist = step.magnitude;
            if (dist < 0.05f)
            {
                _moving = false;
                _resting = _breed != null && _breed.UseSleepWhileResting && HasClip(ActionNames.Sleep)
                    && UnityEngine.Random.value < 0.2f;
                _sleepAmount = 0f;
                _pauseUntil = Time.time + (_resting ? UnityEngine.Random.Range(8f, 14f)
                    : UnityEngine.Random.Range(0.6f, 2.4f));
                PickNewTarget(0.8f);
                return;
            }

            _moving = true;
            _resting = false;
            MoveTo(_pos + step / dist * Mathf.Min(_speed * dt, dist));
            _facing = step.x >= 0f ? 1f : -1f;
        }

        void PickNewTarget(float minDist = 0f)
        {
            var safeHalf = SafeHalfAxes();
            var center = SafeCenter();
            for (int i = 0; i < 32; i++)
            {
                float x = center.x + UnityEngine.Random.Range(-safeHalf.x, safeHalf.x);
                float y = center.y + UnityEngine.Random.Range(-safeHalf.y, safeHalf.y);
                var candidate = new Vector3(x, y, 0f);
                if (((candidate - _pos).magnitude >= minDist || i >= 7) && CanReach(candidate))
                {
                    _target = candidate;
                    return;
                }
            }
            _target = _pos;
        }

        void Animate(float dt)
        {
            var clip = ActiveClip();
            if (clip == null || clip.Length == 0) return;

            // 换动作时从第一帧开始，别接着上一个动作的帧号跑
            if (_lastAction != CurrentAction)
            {
                _lastAction = CurrentAction;
                _frame = 0;
                _frameClock = 0f;
            }

            _frameClock += dt * FpsFor(CurrentAction);
            while (_frameClock >= 1f)
            {
                _frameClock -= 1f;
                if (_oneShot && _frame >= clip.Length - 1)
                {
                    _frame = 0;
                    _oneShot = false;
                    _forced = false;
                    _lastAction = null;
                    _pauseUntil = Time.time;
                    break;
                }
                _frame = (_frame + 1) % clip.Length;
            }
            if (_frame >= clip.Length) _frame = 0;
        }

        float FpsFor(string action)
        {
            switch (action)
            {
                case ActionNames.Walk: return 6f;
                case ActionNames.Run: return 8f;
                case ActionNames.Sleep: return 2.5f;
                case ActionNames.Jump: return 16f;
                case ActionNames.Eat: return 9f;
                case ActionNames.Attack: return 13f;
                default: return _breed != null && _breed.UseWalkWhileMoving ? 7f : (_sleepAmount > 0.55f ? 2.2f : 7f);
            }
        }

        void ApplyVisual()
        {
            if (Renderer == null) return;
            var safeHalf = SafeHalfAxes();
            var center = SafeCenter();
            _pos.x = Mathf.Clamp(_pos.x, center.x - safeHalf.x, center.x + safeHalf.x);
            _pos.y = Mathf.Clamp(_pos.y, center.y - safeHalf.y, center.y + safeHalf.y);

            var clip = ActiveClip();
            if (clip != null && clip.Length > 0)
                Renderer.sprite = clip[Mathf.Clamp(_frame, 0, clip.Length - 1)];

            // 同房间使用统一世界像素格；逻辑路径和吸附后的脚点都须可达。
            var snapped = SnapPosition(_pos);
            transform.position = snapped;

            transform.localScale = new Vector3(_facing * _visualScale, _visualScale, 1f);

            if (_clickCollider != null && Renderer.sprite != null)
            {
                _clickCollider.size = Renderer.sprite.bounds.size;
                _clickCollider.offset = Renderer.sprite.bounds.center;
            }

            // y 越小越靠前（离镜头越近）
            Renderer.sortingOrder = Mathf.RoundToInt((10f - snapped.y - (WalkSurface != null ? GroundOffset : 0f)) * 100f);

            UpdateZzz();
        }

        static float ScaleForBreed(CatBreed breed)
        {
            if (breed == null || breed.Idle == null || breed.Idle.Length == 0) return ArtScale;
            return WorldArtSettings.Current.CatScale(breed.Idle[0]);
        }

        Vector3 SafeCenter() { return _home - Vector3.up * _visualCenterY; }

        Vector2 SafeHalfAxes()
        {
            float pixel = WorldArtSettings.Current.UnitsPerPixel;
            return new Vector2(Mathf.Max(0, _halfAxes.x - _visualPadding.x - pixel),
                Mathf.Max(0, _halfAxes.y - _visualPadding.y - pixel));
        }

        void UpdateZzz()
        {
            bool show = _breed != null && _breed.UseSleepWhileResting
                ? CurrentAction == ActionNames.Sleep : _sleepAmount > 0.55f && !_forced;
            if (show && _zzz == null)
            {
                var go = new GameObject("Zzz");
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<TextMesh>();
                text.text = "z";
                text.fontSize = 64;
                text.characterSize = 0.08f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(0.35f, 0.28f, 0.22f, 0.85f);
                _zzz = go.transform;
            }
            if (_zzz != null)
            {
                _zzz.gameObject.SetActive(show);
                if (show)
                {
                    float t = Mathf.Repeat(Time.time * 0.5f, 1f);
                    float top = Renderer.sprite != null ? Renderer.sprite.bounds.max.y : 1f;
                    _zzz.localPosition = new Vector3(0.55f + t * 0.3f, top - .2f + t * .5f, 0f);
                }
            }
        }
    }
}
