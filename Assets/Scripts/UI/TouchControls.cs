using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// M0 触屏控制层：左半屏动态摇杆移动，右半屏拖动视角，
/// 右下角开火 / 拾取按钮。全部用 uGUI 指针事件，屏蔽新旧输入系统差异。
/// </summary>
public sealed class TouchControls : MonoBehaviour
{
    private const float JoystickRadius = 90f;

    private static Sprite _circleSprite;

    private Vector2 _lookDelta;
    private VirtualJoystick _joystick;
    private HoldButton _fire;
    private TapButton _interact;

    public Vector2 Move => _joystick != null ? _joystick.Value : Vector2.zero;
    public bool FireHeld => _fire != null && _fire.Held;

    public static TouchControls Create()
    {
        var canvasGo = new GameObject("TouchControls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();

        var controls = canvasGo.AddComponent<TouchControls>();
        controls.Build(canvasGo.transform);
        return controls;
    }

    public Vector2 ConsumeLookDelta()
    {
        var value = _lookDelta;
        _lookDelta = Vector2.zero;
        return value;
    }

    public bool ConsumeInteract()
    {
        return _interact != null && _interact.Consume();
    }

    internal void AddLook(Vector2 delta)
    {
        _lookDelta += delta;
    }

    private static void EnsureEventSystem()
    {
        var existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            // 默认阈值 10px：手指移动的前 10px 完全没反应，手机上就是"滑一下没动、再滑才动"。
            existing.pixelDragThreshold = 1;
            return;
        }

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemGo.AddComponent<StandaloneInputModule>();
#endif

        var eventSystem = eventSystemGo.GetComponent<EventSystem>();
        if (eventSystem != null)
        {
            eventSystem.pixelDragThreshold = 1;
        }
    }

    private void Build(Transform root)
    {
        // 层级顺序决定点击优先级：后添加的在上层。
        var lookArea = CreateArea("LookPad", root, new Vector2(0.42f, 0f), Vector2.one);
        lookArea.gameObject.AddComponent<LookPad>().Owner = this;

        var moveArea = CreateArea("MovePad", root, Vector2.zero, new Vector2(0.42f, 1f));
        _joystick = moveArea.gameObject.AddComponent<VirtualJoystick>();
        _joystick.Initialize(moveArea, JoystickRadius, CircleSprite());

        _fire = CreateButton<HoldButton>(root, "FireButton", "开火",
            new Vector2(-210f, 230f), 190f, new Color(0.85f, 0.2f, 0.2f, 0.5f));

        _interact = CreateButton<TapButton>(root, "InteractButton", "拿",
            new Vector2(-210f, 450f), 130f, new Color(0.2f, 0.6f, 0.9f, 0.5f));
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

    private static T CreateButton<T>(Transform root, string name, string label,
        Vector2 anchoredPosition, float size, Color color) where T : MonoBehaviour
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = anchoredPosition;

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
        text.font = BuiltinFont();
        text.fontSize = Mathf.RoundToInt(size * 0.3f);
        text.color = new Color(1f, 1f, 1f, 0.9f);
        text.raycastTarget = false;

        return go.AddComponent<T>();
    }

    private static Font BuiltinFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static Sprite CircleSprite()
    {
        if (_circleSprite != null)
        {
            return _circleSprite;
        }

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

    /// <summary>
    /// 右半屏视角区。只让第一根手指驱动视角：
    /// 之前没有绑定 pointerId，两根手指同时拖动时 delta 会叠加，视角直接飞出去。
    /// </summary>
    private sealed class LookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public TouchControls Owner;

        private int _pointerId = -1;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId == -1)
            {
                _pointerId = eventData.pointerId;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            Owner.AddLook(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
            {
                _pointerId = -1;
            }
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
            if (_pointerId != -1)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _origin = LocalPoint(eventData);
            _base.gameObject.SetActive(true);
            _base.anchoredPosition = _origin;
            _knob.anchoredPosition = Vector2.zero;
            Value = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            var offset = Vector2.ClampMagnitude(LocalPoint(eventData) - _origin, _radius);
            _knob.anchoredPosition = offset;
            Value = offset / _radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

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
        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            Held = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Held = false;
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