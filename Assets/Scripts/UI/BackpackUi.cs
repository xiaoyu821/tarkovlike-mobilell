using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包界面：暂停游戏，列出本局搜到的东西和总价值。
/// 打开时 Time.timeScale = 0，关闭恢复；关闭按钮和点背景都能关。
/// </summary>
public sealed class BackpackUi : MonoBehaviour
{
    private static BackpackUi _instance;

    private TouchControls _controls;

    public static bool IsOpen => _instance != null;

    public static void Show(GameApp app, PlayerRig player, TouchControls controls)
    {
        if (_instance != null) return;

        var go = new GameObject("BackpackUi");
        var ui = go.AddComponent<BackpackUi>();
        ui._controls = controls;
        ui.Build(player);
        _instance = ui;

        Time.timeScale = 0f;
        if (controls != null) controls.SetInputEnabled(false);
    }

    public static void Close()
    {
        if (_instance == null) return;

        if (_instance._controls != null) _instance._controls.SetInputEnabled(true);

        _instance.gameObject.SetActive(false);
        Destroy(_instance.gameObject);
        _instance = null;

        Time.timeScale = 1f;
    }

    private void Build(PlayerRig player)
    {
        var canvas = UiKit.CreateCanvas("BackpackCanvas", 15);
        canvas.transform.SetParent(transform, false);
        canvas.gameObject.AddComponent<UiFade>();

        var root = canvas.transform;

        // 点背景也能关
        var dim = UiKit.Background(root, null, new Color(0.02f, 0.025f, 0.03f, 0.78f));
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.onClick.AddListener(Close);

        var inventory = player != null ? player.Inventory : null;
        var items = inventory != null ? inventory.Items : null;
        var count = items != null ? items.Count : 0;
        var total = inventory != null ? inventory.TotalValue : 0;

        var card = UiKit.Panel(root, "Card", V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero,
            new Vector2(1240f, 760f), new Color(0.09f, 0.095f, 0.11f, 0.99f), true);
        card.GetComponent<Image>().raycastTarget = true;

        L(card, "Title", "背包", 42, 34f, 26f, 300f, 52f, Color.white, true);
        L(card, "Count", count + " / " + PlayerInventory.Capacity + " 格", 24, 900f, 34f, 280f, 34f,
            new Color(0.65f, 0.69f, 0.75f), false, TextAnchor.UpperRight);
        L(card, "Total", "总值 " + MetaProfile.FormatRoubles(total), 26, 640f, 34f, 250f, 34f,
            new Color(1f, 0.84f, 0.48f), true, TextAnchor.UpperRight);

        UiKit.Panel(card, "Divider", V(0f, 1f), V(0f, 1f), new Vector2(34f, -92f),
            new Vector2(1172f, 1.5f), new Color(1f, 1f, 1f, 0.1f), false);

        if (count == 0)
        {
            L(card, "Empty", "背包是空的 —— 去搜刮箱子，撤离成功才带得出去", 26, 34f, 140f, 1172f, 40f,
                new Color(0.55f, 0.58f, 0.64f), false, TextAnchor.UpperCenter);
        }
        else
        {
            var shown = Mathf.Min(count, 18);

            for (var i = 0; i < shown; i++)
            {
                var entry = items[i];
                var column = i / 9;
                var row = i % 9;
                var x = 34f + column * 596f;
                var y = 120f + row * 58f;

                L(card, "Name" + i, entry.DisplayName, 23, x, y, 330f, 30f,
                    new Color(0.88f, 0.9f, 0.94f), false);
                L(card, "Qty" + i, "×" + entry.Count, 21, x + 330f, y, 90f, 30f,
                    new Color(0.6f, 0.64f, 0.7f), false);
                L(card, "Val" + i, MetaProfile.FormatRoubles(entry.TotalValue), 21, x + 420f, y, 160f, 30f,
                    new Color(0.95f, 0.82f, 0.47f), false, TextAnchor.UpperRight);
            }

            if (count > shown)
            {
                L(card, "More", "…… 还有 " + (count - shown) + " 项", 20, 34f, 120f + shown * 58f, 600f, 28f,
                    new Color(0.55f, 0.58f, 0.64f), false);
            }
        }

        UiKit.Panel(card, "BottomDivider", V(0f, 0f), V(0f, 0f), new Vector2(34f, 96f),
            new Vector2(1172f, 1.5f), new Color(1f, 1f, 1f, 0.1f), false);

        L(card, "Hint", "阵亡会丢光背包，撤离成功才折算成卢布", 21, 34f, 56f, 760f, 30f,
            new Color(0.58f, 0.62f, 0.68f), false);

        UiKit.TextButton(card, "CloseButton", "关闭", V(1f, 0f), V(1f, 0f), new Vector2(-34f, 40f),
            new Vector2(240f, 64f), new Color(0.78f, 0.31f, 0.15f, 1f), Color.white, 26, Close);
    }

    private static Vector2 V(float x, float y)
    {
        return new Vector2(x, y);
    }

    private static Text L(RectTransform parent, string name, string text, int fontSize, float x, float yFromTop,
        float width, float height, Color color, bool bold, TextAnchor align = TextAnchor.UpperLeft)
    {
        return UiKit.Label(parent, name, text, fontSize, align, V(0f, 1f), V(0f, 1f),
            new Vector2(x, -yFromTop), new Vector2(width, height), color, bold);
    }
}