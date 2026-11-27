using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单与结算界面（uGUI）。IMGUI 的按钮在只启用新输入系统时不响应触摸，
/// 所以所有可点的东西都必须走 uGUI。
/// 版式全部从屏幕左上角往下排（anchor/pivot 都取左上），
/// 这样在 16:9 / 20:9 / 4:3 上都不会互相压。
/// </summary>
public sealed class MenuUi : MonoBehaviour
{
    private const string Accent = "#e0902f";
    private const string Gold = "#ffd479";
    private const string Muted = "#7d838c";

    private GameApp _app;
    private Canvas _canvas;
    private Text _sensitivityText;
    private Text _fpsText;

    private static MenuUi _instance;

    public static void ShowMainMenu(GameApp app)
    {
        Close();
        var go = new GameObject("MenuUi");
        var menu = go.AddComponent<MenuUi>();
        menu._app = app;
        menu._canvas = UiKit.CreateCanvas("MenuCanvas", 20);
        menu._canvas.transform.SetParent(go.transform, false);
        menu._canvas.gameObject.AddComponent<UiFade>();
        menu.BuildMainMenu();
        _instance = menu;
    }

    public static void ShowResult(GameApp app, RaidResult result)
    {
        Close();
        var go = new GameObject("ResultUi");
        var menu = go.AddComponent<MenuUi>();
        menu._app = app;
        menu._canvas = UiKit.CreateCanvas("ResultCanvas", 25);
        menu._canvas.transform.SetParent(go.transform, false);
        menu._canvas.gameObject.AddComponent<UiFade>();
        menu.BuildResult(result);
        _instance = menu;
    }

    public static void Close()
    {
        if (_instance == null) return;

        _instance.gameObject.SetActive(false);
        Destroy(_instance.gameObject);
        _instance = null;
    }

    // ---------- 主菜单 ----------

    private void BuildMainMenu()
    {
        var root = _canvas.transform;
        var profile = _app != null ? _app.Profile : new MetaProfile();

        UiKit.Background(root, UiKit.Gradient, Color.white);
        UiKit.Panel(root, "TopAccent", V(0f, 1f), V(0f, 1f), Vector2.zero, new Vector2(2600f, 4f),
            new Color(0.88f, 0.55f, 0.16f, 0.95f), false);
        UiKit.Panel(root, "BottomBar", V(0f, 0f), V(0f, 0f), Vector2.zero, new Vector2(2600f, 92f),
            new Color(0f, 0f, 0f, 0.32f), false);

        // ---- 标题 ----
        TopLabel("Title", "撤离训练场", 92, TextAnchor.UpperLeft, 118f, -56f, 1200f, 104f, Color.white, true);
        TopLabel("Subtitle", "EXTRACTION  SHOOTER   ·   MOBILE  PROTOTYPE", 22, TextAnchor.UpperLeft,
            122f, -164f, 1200f, 30f, new Color(0.62f, 0.66f, 0.72f), false);
        UiKit.Panel(root, "Divider", V(0f, 1f), V(0f, 1f), new Vector2(120f, -200f), new Vector2(880f, 2f),
            new Color(1f, 1f, 1f, 0.1f), false);

        // ---- 出击装备 ----
        TopLabel("KitTitle", "出击装备", 27, TextAnchor.UpperLeft, 120f, -216f, 300f, 34f,
            new Color(0.82f, 0.85f, 0.9f), true);
        TopLabel("KitHint", "进场先扣装备费 · 阵亡则装备与战利品全部丢失", 18, TextAnchor.UpperRight,
            520f, -220f, 480f, 30f, new Color(0.55f, 0.58f, 0.63f), false);

        for (var i = 0; i < Loadouts.All.Length; i++)
        {
            BuildKitCard(root, i, profile);
        }

        // ---- 简报 ----
        var brief = UiKit.Panel(root, "Brief", V(0f, 1f), V(0f, 1f), new Vector2(120f, -418f),
            new Vector2(880f, 104f), new Color(1f, 1f, 1f, 0.035f), true);

        CardLabel(brief, "BriefTitle", "突袭简报", 19, 18f, 12f, 300f, 26f, new Color(0.6f, 0.64f, 0.7f), true);
        CardLabel(brief, "BriefLine1", "92 × 92 米白盒地图   ·   10 名 Scav   ·   20 个可搜容器   ·   2 个撤离点",
            19, 18f, 40f, 840f, 26f, new Color(0.78f, 0.81f, 0.86f), false);
        CardLabel(brief, "BriefLine2", "地图与物资每次随机；站进绿圈 5 秒才能撤离", 18, 18f, 68f, 840f, 26f,
            new Color(0.55f, 0.58f, 0.63f), false);

        // ---- 开始 ----
        UiKit.TextButton(root, "StartButton", "进 入 突 袭", V(0f, 1f), V(0f, 1f),
            new Vector2(120f, -540f), new Vector2(430f, 96f), new Color(0.78f, 0.31f, 0.15f, 1f),
            Color.white, 40, () => { if (_app != null) _app.StartRaid(); });

        // ---- 灵敏度 ----
        TopLabel("SensLabel", "视角灵敏度", 20, TextAnchor.MiddleLeft, 120f, -514f, 150f, 52f,
            new Color(0.7f, 0.73f, 0.78f), false);

        UiKit.TextButton(root, "SensDown", "－", V(0f, 1f), V(0f, 1f), new Vector2(272f, -662f),
            new Vector2(52f, 52f), new Color(1f, 1f, 1f, 0.09f), Color.white, 32,
            () => ChangeSensitivity(-0.02f));

        _sensitivityText = UiKit.Label(root, "SensValue", FormatSensitivity(profile), 21, TextAnchor.MiddleCenter,
            V(0f, 1f), V(0f, 1f), new Vector2(332f, -662f), new Vector2(104f, 52f),
            new Color(0.9f, 0.92f, 0.95f), true);

        UiKit.TextButton(root, "SensUp", "＋", V(0f, 1f), V(0f, 1f), new Vector2(444f, -662f),
            new Vector2(52f, 52f), new Color(1f, 1f, 1f, 0.09f), Color.white, 32,
            () => ChangeSensitivity(0.02f));

        // ---- 帧率 ----
        TopLabel("FpsLabel", "帧率上限", 20, TextAnchor.MiddleLeft, 120f, -726f, 150f, 52f,
            new Color(0.7f, 0.73f, 0.78f), false);

        var fpsButton = UiKit.TextButton(root, "FpsButton", profile.TargetFps + " FPS", V(0f, 1f), V(0f, 1f),
            new Vector2(272f, -726f), new Vector2(150f, 52f), new Color(1f, 1f, 1f, 0.09f),
            new Color(0.9f, 0.92f, 0.95f), 20, CycleFps);
        _fpsText = fpsButton.GetComponentInChildren<Text>();

        TopLabel("FpsHint", "越高越耗电；165 需要高刷屏", 18, TextAnchor.MiddleLeft, 440f, -726f, 420f, 52f,
            new Color(0.5f, 0.53f, 0.58f), false);

        // ---- 按键布局 ----
        UiKit.TextButton(root, "LayoutButton", "按键布局", V(0f, 1f), V(0f, 1f), new Vector2(120f, -790f),
            new Vector2(200f, 52f), new Color(1f, 1f, 1f, 0.06f), new Color(0.85f, 0.88f, 0.92f), 20,
            () => LayoutEditor.Show(_app));

        // ---- 开镜方式 ----
        UiKit.TextButton(root, "AimModeButton", profile.AimMode == 0 ? "开镜：按住" : "开镜：点击", V(0f, 1f),
            V(0f, 1f), new Vector2(340f, -790f), new Vector2(220f, 52f), new Color(1f, 1f, 1f, 0.06f),
            new Color(0.85f, 0.88f, 0.92f), 20, ToggleAimMode);

        // ---- 底部左 ----
        UiKit.TextButton(root, "ResetButton", "重置存档", V(0f, 0f), V(0f, 0f), new Vector2(120f, 26f),
            new Vector2(160f, 50f), new Color(1f, 1f, 1f, 0.06f), new Color(0.85f, 0.55f, 0.5f), 20,
            ResetProfile);

        UiKit.TextButton(root, "QuitButton", "退出", V(0f, 0f), V(0f, 0f), new Vector2(292f, 26f),
            new Vector2(160f, 50f), new Color(1f, 1f, 1f, 0.06f), new Color(0.78f, 0.81f, 0.86f), 20,
            () => { Application.Quit(); });

        // ---- 右侧：账户 ----
        var account = UiKit.Panel(root, "Account", V(1f, 1f), V(1f, 1f), new Vector2(-120f, -216f),
            new Vector2(520f, 244f), new Color(1f, 1f, 1f, 0.045f), true);

        CardLabel(account, "AccTitle", "账户余额", 19, 22f, 14f, 400f, 26f, new Color(0.6f, 0.64f, 0.7f), false);
        CardLabel(account, "AccValue", MetaProfile.FormatRoubles(profile.Roubles), 40, 20f, 36f, 460f, 52f,
            new Color(1f, 0.83f, 0.47f), true);

        UiKit.Panel(account, "AccDivider", V(0f, 1f), V(0f, 1f), new Vector2(22f, -104f),
            new Vector2(472f, 1.5f), new Color(1f, 1f, 1f, 0.09f), false);

        CardLabel(account, "AccStats1",
            "突袭 " + profile.Raids + " 次   ·   撤离 " + profile.Extracts + " 次   ·   阵亡 " + profile.Deaths + " 次",
            19, 22f, 116f, 470f, 26f, new Color(0.78f, 0.81f, 0.86f), false);
        CardLabel(account, "AccStats2",
            "击杀 " + profile.Kills + " 人   ·   撤离率 " + Mathf.RoundToInt(profile.ExtractRate * 100f) + "%",
            19, 22f, 146f, 470f, 26f, new Color(0.78f, 0.81f, 0.86f), false);
        CardLabel(account, "AccStats3", "单局最高战利品 " + MetaProfile.FormatRoubles(profile.BestLootValue),
            19, 22f, 176f, 470f, 26f, new Color(0.8f, 0.72f, 0.55f), false);

        // ---- 右侧：操作 ----
        var controls = UiKit.Panel(root, "Controls", V(1f, 1f), V(1f, 1f), new Vector2(-120f, -484f),
            new Vector2(520f, 186f), new Color(1f, 1f, 1f, 0.03f), true);

        CardLabel(controls, "CtrlTitle", "操作", 19, 22f, 12f, 300f, 26f, new Color(0.6f, 0.64f, 0.7f), true);
        CardLabel(controls, "Ctrl1", "左半屏拖动移动   ·   右半屏滑动转视角", 18, 22f, 42f, 480f, 26f,
            new Color(0.74f, 0.77f, 0.82f), false);
        CardLabel(controls, "Ctrl2", "右下：开火 · 镜 · 搜 · 换      左下：跑 · 医", 18, 22f, 70f, 480f, 26f,
            new Color(0.74f, 0.77f, 0.82f), false);
        CardLabel(controls, "Ctrl3", "搜刮站定 1 秒；撤离要在绿圈里待满 5 秒", 18, 22f, 98f, 480f, 26f,
            new Color(0.55f, 0.58f, 0.63f), false);
        CardLabel(controls, "Ctrl4", "被打中会出血，按「医」包扎止血", 18, 22f, 126f, 480f, 26f,
            new Color(0.55f, 0.58f, 0.63f), false);

        // ---- 版本 ----
        UiKit.Label(root, "Version", "v0.4   ·   1 人 + AI   ·   无美术资源：模型与界面全部代码生成", 17,
            TextAnchor.LowerRight, V(1f, 0f), V(1f, 0f), new Vector2(-120f, 16f), new Vector2(740f, 26f),
            new Color(0.42f, 0.45f, 0.5f), false);
    }

    private void BuildKitCard(Transform root, int index, MetaProfile profile)
    {
        var kit = Loadouts.All[index];
        var selected = profile.SelectedKit == index;
        var affordable = profile.Roubles >= kit.Cost;

        var background = selected
            ? new Color(0.18f, 0.19f, 0.22f, 1f)
            : new Color(1f, 1f, 1f, affordable ? 0.05f : 0.025f);

        var card = UiKit.Panel(root, "Kit" + index, V(0f, 1f), V(0f, 1f),
            new Vector2(120f + index * 292f, -262f), new Vector2(276f, 140f), background, true);

        var image = card.GetComponent<Image>();
        image.raycastTarget = true;

        if (selected)
        {
            UiKit.Panel(card, "SelectedBar", V(0f, 0f), V(0f, 0f), new Vector2(14f, 9f),
                new Vector2(248f, 3f), new Color(0.88f, 0.55f, 0.16f, 1f), false);
        }

        var cost = kit.Cost == 0 ? "免费" : (kit.Cost / 1000) + "k ₽";
        var costColor = affordable ? Gold : "#c8665e";

        var text = "<b>" + kit.Name + "</b>\n<size=23><color=" + costColor + ">" + cost + "</color></size>\n" +
                   "<size=16><color=" + Muted + ">" + kit.Blurb + "</color></size>";

        if (selected) text += "\n<size=15><color=" + Accent + ">● 已装备</color></size>";

        UiKit.LabelIn(card, "Text", text, 26, TextAnchor.UpperLeft, 16f, 10f, 12f, 12f,
            affordable ? new Color(0.9f, 0.92f, 0.95f) : new Color(0.6f, 0.58f, 0.58f), false);

        if (!affordable) return;

        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        button.onClick.AddListener(() => SelectKit(index));
        card.gameObject.AddComponent<UiPressFx>();
    }

    // ---------- 结算 ----------

    private void BuildResult(RaidResult result)
    {
        var root = _canvas.transform;
        var good = result.Extracted;
        var accent = good ? new Color(0.32f, 0.82f, 0.46f) : new Color(0.88f, 0.33f, 0.28f);

        UiKit.Background(root, UiKit.Gradient, Color.white);
        UiKit.Panel(root, "TopAccent", V(0f, 1f), V(0f, 1f), Vector2.zero, new Vector2(2600f, 5f), accent, false);

        UiKit.Label(root, "Title", good ? "撤离成功" : "任务失败", 96, TextAnchor.UpperCenter,
            V(0.5f, 1f), V(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1600f, 118f), accent, true);

        UiKit.Label(root, "Cause", result.Summary, 26, TextAnchor.UpperCenter,
            V(0.5f, 1f), V(0.5f, 1f), new Vector2(0f, -176f), new Vector2(1600f, 40f),
            new Color(0.72f, 0.76f, 0.82f), false);

        // ---- 战利品 ----
        var lootCard = UiKit.Panel(root, "LootCard", V(0f, 0f), V(0f, 0f), new Vector2(120f, 268f),
            new Vector2(880f, 400f), new Color(1f, 1f, 1f, 0.045f), true);

        CardLabel(lootCard, "LootTitle", good ? "带出的物资" : "丢失的物资", 22, 24f, 16f, 500f, 30f,
            new Color(0.72f, 0.76f, 0.82f), true);

        UiKit.Panel(lootCard, "LootDivider", V(0f, 1f), V(0f, 1f), new Vector2(24f, -48f),
            new Vector2(832f, 1.5f), new Color(1f, 1f, 1f, 0.08f), false);

        if (result.Loot == null || result.Loot.Count == 0)
        {
            CardLabel(lootCard, "Empty", "背包是空的", 22, 24f, 64f, 700f, 30f,
                new Color(0.5f, 0.53f, 0.58f), false);
        }
        else
        {
            var shown = Mathf.Min(result.Loot.Count, 7);

            for (var i = 0; i < shown; i++)
            {
                var entry = result.Loot[i];
                var y = 62f + i * 36f;
                var color = good ? new Color(0.86f, 0.89f, 0.93f) : new Color(0.55f, 0.52f, 0.55f);

                CardLabel(lootCard, "Name" + i, entry.DisplayName, 20, 24f, y, 480f, 28f, color, false);
                CardLabel(lootCard, "Qty" + i, "×" + entry.Count, 20, 500f, y, 100f, 28f,
                    new Color(0.62f, 0.66f, 0.72f), false);
                CardLabel(lootCard, "Val" + i, MetaProfile.FormatRoubles(entry.TotalValue), 20, 596f, y, 260f,
                    28f, new Color(0.95f, good ? 0.82f : 0.55f, good ? 0.47f : 0.5f), false);
            }

            if (result.Loot.Count > shown)
            {
                CardLabel(lootCard, "More", "…… 还有 " + (result.Loot.Count - shown) + " 项", 18, 24f,
                    62f + shown * 36f, 700f, 26f, new Color(0.55f, 0.58f, 0.63f), false);
            }
        }

        UiKit.Panel(lootCard, "TotalDivider", V(0f, 0f), V(0f, 0f), new Vector2(24f, 52f),
            new Vector2(832f, 1.5f), new Color(1f, 1f, 1f, 0.08f), false);

        UiKit.Label(lootCard, "TotalLabel", "战利品总价值", 21, TextAnchor.MiddleLeft,
            V(0f, 0f), V(0f, 0f), new Vector2(24f, 12f), new Vector2(300f, 34f),
            new Color(0.7f, 0.73f, 0.78f), false);

        UiKit.Label(lootCard, "TotalValue", MetaProfile.FormatRoubles(result.LootValue), 28, TextAnchor.MiddleRight,
            V(1f, 0f), V(1f, 0f), new Vector2(-24f, 12f), new Vector2(420f, 34f),
            good ? new Color(1f, 0.84f, 0.48f) : new Color(0.7f, 0.6f, 0.55f), true);

        // ---- 统计 ----
        var statsCard = UiKit.Panel(root, "StatsCard", V(1f, 0f), V(1f, 0f), new Vector2(-120f, 268f),
            new Vector2(520f, 400f), new Color(1f, 1f, 1f, 0.045f), true);

        CardLabel(statsCard, "StatsTitle", "本局数据", 22, 24f, 16f, 400f, 30f, new Color(0.72f, 0.76f, 0.82f), true);
        UiKit.Panel(statsCard, "StatsDivider", V(0f, 1f), V(0f, 1f), new Vector2(24f, -48f),
            new Vector2(472f, 1.5f), new Color(1f, 1f, 1f, 0.08f), false);

        var seconds = result.Seconds;
        var timeText = Mathf.FloorToInt(seconds / 60f) + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00");

        var keys = new[] { "击杀", "存活时长", "战利品", "撤离点" };
        var values = new[]
        {
            result.Kills + " 人", timeText, MetaProfile.FormatRoubles(result.LootValue),
            good ? "已使用" : "未抵达"
        };

        for (var i = 0; i < keys.Length; i++)
        {
            var y = 66f + i * 40f;
            CardLabel(statsCard, "K" + i, keys[i], 20, 24f, y, 200f, 28f, new Color(0.6f, 0.64f, 0.7f), false);
            CardLabel(statsCard, "V" + i, values[i], 21, 240f, y, 250f, 28f,
                new Color(0.86f, 0.89f, 0.93f), true);
        }

        UiKit.Panel(statsCard, "AccDivider", V(0f, 0f), V(0f, 0f), new Vector2(24f, 92f),
            new Vector2(472f, 1.5f), new Color(1f, 1f, 1f, 0.08f), false);

        CardLabel(statsCard, "AccLabel", "账户余额", 19, 24f, 240f, 240f, 26f, new Color(0.6f, 0.64f, 0.7f), false);

        var delta = result.RoublesAfter - result.RoublesBefore;
        var deltaText = (delta >= 0 ? "+" : "−") + MetaProfile.FormatRoubles(Mathf.Abs(delta));

        CardLabel(statsCard, "AccDelta", deltaText, 26, 24f, 266f, 460f, 32f,
            delta >= 0 ? new Color(0.45f, 0.88f, 0.55f) : new Color(0.9f, 0.45f, 0.4f), true);

        CardLabel(statsCard, "AccNow", "现在 " + MetaProfile.FormatRoubles(result.RoublesAfter), 20, 24f, 306f,
            460f, 28f, new Color(0.66f, 0.7f, 0.76f), false);

        // ---- 按钮 ----
        UiKit.TextButton(root, "AgainButton", good ? "再 来 一 局" : "再 试 一 次", V(0.5f, 0f), V(0.5f, 0f),
            new Vector2(-212f, 108f), new Vector2(392f, 92f), new Color(0.78f, 0.31f, 0.15f, 1f),
            Color.white, 34, () => { if (_app != null) _app.StartRaid(); });

        UiKit.TextButton(root, "MenuButton", "返回大厅", V(0.5f, 0f), V(0.5f, 0f),
            new Vector2(212f, 108f), new Vector2(392f, 92f), new Color(1f, 1f, 1f, 0.09f),
            new Color(0.86f, 0.89f, 0.93f), 34, () => { if (_app != null) _app.ShowMenu(); });
    }

    // ---------- 小工具 ----------

    private static Vector2 V(float x, float y)
    {
        return new Vector2(x, y);
    }

    private Text TopLabel(string name, string text, int fontSize, TextAnchor align, float x, float yFromTop,
        float width, float height, Color color, bool bold)
    {
        return UiKit.Label(_canvas.transform, name, text, fontSize, align, V(0f, 1f), V(0f, 1f),
            new Vector2(x, yFromTop), new Vector2(width, height), color, bold);
    }

    private static Text CardLabel(RectTransform card, string name, string text, int fontSize, float x,
        float yFromTop, float width, float height, Color color, bool bold)
    {
        return UiKit.Label(card, name, text, fontSize, TextAnchor.UpperLeft,
            V(0f, 1f), V(0f, 1f), new Vector2(x, -yFromTop), new Vector2(width, height), color, bold);
    }

    private static string FormatSensitivity(MetaProfile profile)
    {
        return (profile.Sensitivity / 0.13f).ToString("0.00") + "×";
    }

    private void ToggleAimMode()
    {
        if (_app == null) return;

        _app.Profile.AimMode = _app.Profile.AimMode == 0 ? 1 : 0;
        _app.Profile.Save();
        ShowMainMenu(_app);
    }

    private void CycleFps()
    {
        if (_app == null) return;

        _app.Profile.TargetFps = MetaProfile.NextFps(_app.Profile.TargetFps);
        _app.Profile.Save();
        Application.targetFrameRate = Mathf.Clamp(_app.Profile.TargetFps, 30, 165);

        if (_fpsText != null) _fpsText.text = _app.Profile.TargetFps + " FPS";
    }

    private void ChangeSensitivity(float delta)
    {
        if (_app == null) return;

        _app.Profile.Sensitivity = Mathf.Clamp(_app.Profile.Sensitivity + delta, 0.04f, 0.5f);
        _app.Profile.Save();

        if (_sensitivityText != null) _sensitivityText.text = FormatSensitivity(_app.Profile);
    }

    private void SelectKit(int index)
    {
        if (_app == null) return;

        var kit = Loadouts.Get(index);
        if (_app.Profile.Roubles < kit.Cost) return;

        _app.Profile.SelectedKit = index;
        _app.Profile.Save();
        ShowMainMenu(_app);
    }

    private void ResetProfile()
    {
        if (_app == null) return;
        _app.Profile.ResetProgress();
        ShowMainMenu(_app);
    }
}