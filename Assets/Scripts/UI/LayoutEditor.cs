using UnityEngine;

/// <summary>
/// 按键布局编辑：把触屏按钮单独摆出来，手指拖着放，松手即存。
/// 打开时先关掉主菜单，退出再回去。
/// </summary>
public sealed class LayoutEditor : MonoBehaviour
{
    private static LayoutEditor _instance;

    private GameApp _app;
    private GameObject _controlsRoot;
    private TouchControls _controls;
    private UnityEngine.UI.Text _scaleText;

    public static bool IsOpen => _instance != null;

    public static void Show(GameApp app)
    {
        if (_instance != null) return;

        MenuUi.Close();

        var go = new GameObject("LayoutEditor");
        var editor = go.AddComponent<LayoutEditor>();
        editor._app = app;
        editor.Build();
        _instance = editor;
    }

    public static void Close()
    {
        if (_instance == null) return;

        var app = _instance._app;
        _instance.gameObject.SetActive(false);
        Destroy(_instance.gameObject);
        _instance = null;

        if (app != null) app.ShowMenu();
    }

    private void Build()
    {
        // 底板 + 说明（z 最低），按钮（25）在上面，页眉页脚（35）压最上面
        var canvas = UiKit.CreateCanvas("LayoutCanvas", 15);
        canvas.transform.SetParent(transform, false);

        var root = canvas.transform;
        UiKit.Background(root, UiKit.Gradient, Color.white);
        UiKit.Panel(root, "BottomBar", V(0f, 0f), V(0f, 0f), Vector2.zero, new Vector2(2600f, 160f),
            new Color(0f, 0f, 0f, 0.5f), false);

        UiKit.Label(root, "Title", "按键布局", 46, TextAnchor.UpperLeft, V(0f, 1f), V(0f, 1f),
            new Vector2(70f, -50f), new Vector2(700f, 60f), Color.white, true);

        UiKit.Label(root, "Hint", "拖动按钮调整位置，松手自动保存 · 摇杆区不在编辑范围", 22,
            TextAnchor.UpperLeft, V(0f, 1f), V(0f, 1f), new Vector2(72f, -118f), new Vector2(1200f, 34f),
            new Color(0.62f, 0.66f, 0.72f), false);

        RebuildControls();

        // 页脚单独一层，保证压在按钮上面
        var footerCanvas = UiKit.CreateCanvas("LayoutFooter", 35);
        footerCanvas.transform.SetParent(transform, false);
        var footer = footerCanvas.transform;

        UiKit.TextButton(footer, "Smaller", "按钮变小", V(0f, 0f), V(0f, 0f), new Vector2(70f, 46f),
            new Vector2(220f, 72f), new Color(1f, 1f, 1f, 0.1f), Color.white, 24, () => ChangeScale(-0.1f));

        _scaleText = UiKit.Label(footer, "ScaleValue", "×" + TouchLayout.Scale.ToString("0.0"), 26,
            TextAnchor.MiddleCenter, V(0f, 0f), V(0f, 0f), new Vector2(300f, 46f), new Vector2(110f, 72f),
            new Color(0.9f, 0.92f, 0.95f), true);

        UiKit.TextButton(footer, "Bigger", "按钮变大", V(0f, 0f), V(0f, 0f), new Vector2(420f, 46f),
            new Vector2(220f, 72f), new Color(1f, 1f, 1f, 0.1f), Color.white, 24, () => ChangeScale(0.1f));

        UiKit.TextButton(footer, "ResetLayout", "恢复默认", V(0f, 0f), V(0f, 0f), new Vector2(680f, 46f),
            new Vector2(220f, 72f), new Color(1f, 1f, 1f, 0.06f), new Color(0.9f, 0.6f, 0.55f), 24, ResetLayout);

        UiKit.TextButton(footer, "Done", "完 成", V(1f, 0f), V(1f, 0f), new Vector2(-70f, 40f),
            new Vector2(300f, 84f), new Color(0.78f, 0.31f, 0.15f, 1f), Color.white, 30, Close);
    }

    private void RebuildControls()
    {
        if (_controlsRoot != null)
        {
            Destroy(_controlsRoot);
            _controlsRoot = null;
        }

        _controls = TouchControls.Create(25, true);
        _controlsRoot = _controls != null ? _controls.gameObject : null;
    }

    private void ChangeScale(float delta)
    {
        TouchLayout.SetScale(TouchLayout.Scale + delta);
        RebuildControls();

        if (_scaleText != null) _scaleText.text = "×" + TouchLayout.Scale.ToString("0.0");
    }

    private void ResetLayout()
    {
        TouchLayout.ResetAll();
        RebuildControls();

        if (_scaleText != null) _scaleText.text = "×" + TouchLayout.Scale.ToString("0.0");
    }

    private static Vector2 V(float x, float y)
    {
        return new Vector2(x, y);
    }
}