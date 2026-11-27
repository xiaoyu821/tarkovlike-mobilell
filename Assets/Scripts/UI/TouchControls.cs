using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 触屏控制层：左半屏动态摇杆移动，右半屏拖动视角，
/// 右下角开火/开镜/搜刮/换弹/背包，左下角冲刺/治疗。
/// 按钮位置和大小可以在"按键布局"里拖拽调整，存到 TouchLayout。
/// </summary>
public sealed class TouchControls : MonoBehaviour
{
    private const float JoystickRadius = 90f;

    private static Sprite _circleSprite;

    private Vector2 _lookDelta;
    private VirtualJoystick _joystick;
    private HoldButton _fire;
    private AimButton _aim;
    private HoldButton _sprint;
    private TapButton _interact;
    private TapButton _heal;
    private TapButton _reload;
    private TapButton _backpack;
    private bool _inputEnabled = true;

    public Vector2 Move => _inputEnabled && _joystick != null ? _joystick.Value : Vector2.zero;
    public bool FireHeld => _inputEnabled && _fire != null && _fire.Held;
    public bool AimHeld => _inputEnabled && _aim != null && _aim.Held;
    public bool SprintHeld => _inputEnabled && _sprint != null && _sprint.Held;

    public static TouchControls Create()
    {
        return Create(10, false);
    }

    public static TouchControls Create(int sortingOrder, bool editMode)
    {
        var canvasGo = new GameObject("TouchControls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UiEventSystem.Ensure();

        var controls = canvasGo.AddComponent<TouchControls>();
        controls.Build(canvasGo.transform, editMode);
        return controls;
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        _lookDelta = Vector2.zero;
    }

    public Vector2 ConsumeLookDelta()
    {
        var value = _lookDelta;
        _lookDelta = Vector2.zero;
        return value;
    }

    public bool ConsumeInteract() => _inputEnabled && _interact != null && _interact.Consume();
    public bool ConsumeHeal() => _inputEnabled && _heal != null && _heal.Consume();
    public bool ConsumeReload() => _inputEnabled && _reload != null && _reload.Consume();
    public bool ConsumeBackpack() => _inputEnabled && _backpack != null && _backpack.Consume();
    public bool ConsumeAimPress() => _inputEnabled && _aim != null && _aim.ConsumePress();

    internal void AddLook(Vector2 delta)
    {
        if (!_inputEnabled) return;
        _lookDelta += delta;
    }

    private void Build(Transform root, bool editMode)
    {
        if (!editMode)
        {
            // 层级顺序决定点击优先级：后添加的在上层。
            var lookArea = CreateArea("LookPad", root, new Vector2(0.42f, 0f), Vector2.one);
            lookArea.gameObject.AddComponent<LookPad>().Owner = this;

            var moveArea = CreateArea("MovePad", root, Vector2.zero, new Vector2(0.42f, 1f));
            _joystick = moveArea.gameObject.AddComponent<VirtualJoystick>();
            _joystick.Initialize(moveArea, JoystickRadius, CircleSprite());
        }

        var scale = TouchLayout.Scale;

        _fire = CreateButton<HoldButton>(root, "fire", "开火", new Vector2(1f, 0f),
            new Vector2(-230f, 250f), 200f * scale, new Color(0.85f, 0.2f, 0.2f, 0.5f), editMode);

        _aim = CreateButton<AimButton>(root, "aim", "镜", new Vector2(1f, 0f),
            new Vector2(-230f, 480f), 140f * scale, new Color(0.9f, 0.75f, 0.25f, 0.4f), editMode);
        _aim.Owner = this;

        _interact = CreateButton<TapButton>(root, "search", "搜", new Vector2(1f, 0f),
            new Vector2(-440f, 300f), 140f * scale, new Color(0.2f, 0.6f, 0.9f, 0.45f), editMode);

        _reload = CreateButton<TapButton>(root, "reload", "换", new Vector2(1f, 0f),
            new Vector2(-440f, 510f), 130f * scale, new Color(0.75f, 0.75f, 0.8f, 0.4f), editMode);

        _backpack = CreateButton<TapButton>(root, "backpack", "包", new Vector2(1f, 0f),
            new Vector2(-600f, 300f), 120f * scale, new Color(0.55f, 0.45f, 0.75f, 0.42f), editMode);

        _sprint = CreateButton<HoldButton>(root, "sprint", "跑", new Vector2(0f, 0f),
            new Vector2(150f, 150f), 130f * scale, new Color(0.3f, 0.85f, 0.4f, 0.4f), editMode);

        _heal = CreateButton<TapButton>(root, "heal", "医", new Vector2(0f, 0f),
            new Vector2(150f, 330f), 130f * scale, new Color(0.9f, 0.45f, 0.5f, 0.4f), editMode);
    }

    private static RectTransform CreateArea(string name, Transform root, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        return rect;
    }

    private static T CreateButton<T>(Transform root, string key, string label, Vector2 anchor,
        Vector2 defaultPosition, float size, Color color, bool editMode) where T : MonoBehaviour
    {
        var go = new GameObject("Btn_" + key, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = TouchLayout.TryGet(key, out var saved) ? saved : defaultPosition;

        var image = go.GetComponent<Image>();
        image.sprite = CircleSprite();
        image.color = color;
        image.raycastTarget = true;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var textRect = (RectTransform)textGo.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = UiFont.Default;
        text.fontSize = Mathf.RoundToInt(size * 0.3f);
        text.color = new Color(1f, 1f, 1f, 0.9f);
        text.raycastTarget = false;

        if (editMode)
        {
            var drag = go.AddComponent<DragMove>();
            drag.Key = key;
        }

        return go.AddComponent<T>();
    }

    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        var center = (size - 1) * 0.5f;
        var radius = size * 0.5f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                var alpha = Mathf.Clamp01(radius - distance);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        _circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

    /// <summary>只让第一根手指驱动视角，双指不会叠加导致视角乱飞。</summary>
    private sealed class LookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public TouchControls Owner;

        private int _pointerId = -1;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId == -1) _pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Owner.AddLook(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) _pointerId = -1;
        }
    }

    private sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _area;
        private RectTransform _base;
        private RectTransform _knob;
        private float _radius;
        private int _pointerId = -1;
        private Vector2 _origin;

        public Vector2 Value { get; private set; }

        public void Initialize(RectTransform area, float radius, Sprite circle)
        {
            _area = area;
            _radius = radius;

            _base = CreateCircle("Base", area, radius * 2.6f, circle, new Color(1f, 1f, 1f, 0.15f));
            _knob = CreateCircle("Knob", _base, radius * 1.1f, circle, new Color(1f, 1f, 1f, 0.45f));
            _base.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != -1) return;

            _pointerId = eventData.pointerId;
            _origin = LocalPoint(eventData);
            _base.gameObject.SetActive(true);
            _base.anchoredPosition = _origin;
            _knob.anchoredPosition = Vector2.zero;
            Value = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            var offset = Vector2.ClampMagnitude(LocalPoint(eventData) - _origin, _radius);
            _knob.anchoredPosition = offset;
            Value = offset / _radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            _pointerId = -1;
            _base.gameObject.SetActive(false);
            Value = Vector2.zero;
        }

        private Vector2 LocalPoint(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _area, eventData.position, eventData.pressEventCamera, out var point);
            return point;
        }

        private static RectTransform CreateCircle(string name, Transform parent, float size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }

    private sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private bool _pressed;

        public bool Held { get; private set; }

        public bool ConsumePress()
        {
            var value = _pressed;
            _pressed = false;
            return value;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
        }
    }

    /// <summary>
    /// 开镜键。除了按住开镜，按住后拖动还能直接转视角 ——
    /// 右手拇指按住镜就能一边瞄一边跟枪，不用再腾第三根手指。
    /// </summary>
    private sealed class AimButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public TouchControls Owner;

        private bool _pressed;

        public bool Held { get; private set; }

        public bool ConsumePress()
        {
            var value = _pressed;
            _pressed = false;
            return value;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Owner != null) Owner.AddLook(eventData.delta);
        }
    }

    private sealed class TapButton : MonoBehaviour, IPointerDownHandler
    {
        private bool _pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public bool Consume()
        {
            var value = _pressed;
            _pressed = false;
            return value;
        }
    }
}