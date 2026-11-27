using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>内置字体。Android 上没有 Arial.ttf，只有 LegacyRuntime.ttf。</summary>
public static class UiFont
{
    private static Font _default;

    public static Font Default
    {
        get
        {
            if (_default != null) return _default;

            _default = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_default == null)
            {
                _default = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return _default;
        }
    }
}

/// <summary>保证场景里有 EventSystem，并把拖动阈值降到 1px。</summary>
public static class UiEventSystem
{
    public static void Ensure()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            existing.pixelDragThreshold = 1;
            return;
        }

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif

        var eventSystem = go.GetComponent<EventSystem>();
        if (eventSystem != null)
        {
            eventSystem.pixelDragThreshold = 1;
        }
    }
}

/// <summary>按钮按下时轻微缩小 + 咔哒声。</summary>
public sealed class UiPressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.localScale = _baseScale * 0.97f;
        Fx.Play2D(Fx.Click, 0.22f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.localScale = _baseScale;
    }
}

/// <summary>整个界面淡入 + 轻微放大。</summary>
public sealed class UiFade : MonoBehaviour
{
    private CanvasGroup _group;
    private float _time;

    private void Awake()
    {
        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
    }

    private void Update()
    {
        _time += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(_time / 0.22f);
        _group.alpha = t;

        // 只淡入，不缩放：ScreenSpaceOverlay 的 Canvas 根节点缩放行为不可靠
        if (t >= 1f) enabled = false;
    }
}

/// <summary>代码搭 uGUI 的小工具。按 1920×1080 参考分辨率排像素。</summary>
public static class UiKit
{
    private static Sprite _rounded;
    private static Sprite _roundedSmall;
    private static Sprite _gradient;

    /// <summary>圆角矩形（9 宫格）。没有图片资源，运行时生成。</summary>
    public static Sprite Rounded
    {
        get
        {
            if (_rounded == null) _rounded = BuildRounded(64, 16);
            return _rounded;
        }
    }

    public static Sprite RoundedSmall
    {
        get
        {
            if (_roundedSmall == null) _roundedSmall = BuildRounded(32, 8);
            return _roundedSmall;
        }
    }

    /// <summary>竖直渐变背景。</summary>
    public static Sprite Gradient
    {
        get
        {
            if (_gradient != null) return _gradient;

            const int height = 128;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1);
                texture.SetPixel(0, y, Color.Lerp(new Color(0.045f, 0.05f, 0.062f),
                    new Color(0.115f, 0.12f, 0.145f), t));
            }

            texture.Apply();
            _gradient = Sprite.Create(texture, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), 100f);
            return _gradient;
        }
    }

    private static Sprite BuildRounded(int size, int radius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x, radius, size - 1 - radius);
                var cy = Mathf.Clamp(y, radius, size - 1 - radius);
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UiEventSystem.Ensure();
        return canvas;
    }

    /// <summary>面板。anchor/pivot 决定它是贴哪条边排的。</summary>
    public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color color, bool rounded)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (rounded)
        {
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
        }

        return rect;
    }

    /// <summary>铺满父级的背景图。</summary>
    public static Image Background(Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    public static Text Label(Transform parent, string name, string text, int fontSize, TextAnchor align,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return Style(go.GetComponent<Text>(), text, fontSize, align, color, bold);
    }

    /// <summary>铺满父面板并留内边距的文字。</summary>
    public static Text LabelIn(RectTransform parent, string name, string text, int fontSize, TextAnchor align,
        float left, float bottom, float right, float top, Color color, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);

        return Style(go.GetComponent<Text>(), text, fontSize, align, color, bold);
    }

    private static Text Style(Text label, string text, int fontSize, TextAnchor align, Color color, bool bold)
    {
        label.text = text;
        label.font = UiFont.Default;
        label.fontSize = fontSize;
        label.alignment = align;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        if (bold) label.fontStyle = FontStyle.Bold;
        return label;
    }

    public static Button TextButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color background, Color foreground, int fontSize, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = background;
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        if (onClick != null) button.onClick.AddListener(onClick);
        go.AddComponent<UiPressFx>();

        LabelIn(rect, "Label", text, fontSize, TextAnchor.MiddleCenter, 8f, 4f, 8f, 4f, foreground, true);
        return button;
    }

    public static void Clear(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}