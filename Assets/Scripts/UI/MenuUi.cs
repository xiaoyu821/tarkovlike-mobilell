using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单与结算界面（uGUI）。IMGUI 的按钮在只启用新输入系统时不响应触摸，
/// 所以所有可点的东西都必须走 uGUI。
/// </summary>
public sealed class MenuUi : MonoBehaviour
{
    private GameApp _app;
    private Canvas _canvas;
    private Text _sensitivityText;
    private Text _roublesText;

    private static MenuUi _instance;

    public static void ShowMainMenu(GameApp app)
    {
        Close();
        var go = new GameObject("MenuUi");
        var menu = go.AddComponent<MenuUi>();
        menu._app = app;
        menu._canvas = UiKit.CreateCanvas("MenuCanvas", 20);
        menu._canvas.transform.SetParent(go.transform, false);
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
        menu.BuildResult(result);
        _instance = menu;
    }

    public static void Close()
    {
        if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    private void BuildMainMenu()
    {
        var root = _canvas.transform;

        UiKit.Panel(root, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.045f, 0.05f, 0.062f, 1f));

        UiKit.Panel(root, "Accent", new Vector2(0f, 0.78f), new Vector2(1f, 0.782f),
            Vector2.zero, Vector2.zero, new Color(0.85f, 0.55f, 0.2f, 0.55f));

        UiKit.Label(root, "Title", "撤离训练场", 96, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.80f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero, Color.white);

        UiKit.Label(root, "Subtitle", "类塔科夫 · 移动端原型  ·  一次突袭：搜刮 → 交战 → 撤离", 34,
            TextAnchor.MiddleCenter, new Vector2(0f, 0.73f), new Vector2(1f, 0.79f),
            Vector2.zero, Vector2.zero, new Color(0.72f, 0.74f, 0.78f, 1f));

        // ---- 战绩面板 ----
        var panel = UiKit.Panel(root, "Stats", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-420f, -110f), new Vector2(420f, 140f), new Color(0.09f, 0.1f, 0.12f, 0.95f));

        var profile = _app != null ? _app.Profile : new MetaProfile();

        _roublesText = UiKit.Label(panel, "Roubles", "账户 " + MetaProfile.FormatRoubles(profile.Roubles), 46,
            TextAnchor.MiddleCenter, new Vector2(0f, 0.62f), new Vector2(1f, 0.96f),
            Vector2.zero, Vector2.zero, new Color(0.95f, 0.82f, 0.4f, 1f));

        var stats = string.Format("突袭 {0} 次  ·  撤离 {1} 次  ·  阵亡 {2} 次\n击杀 {3} 人  ·  撤离率 {4:0}%  ·  单局最高战利品 {5}",
            profile.Raids, profile.Extracts, profile.Deaths,
            profile.Kills, profile.ExtractRate * 100f, MetaProfile.FormatRoubles(profile.BestLootValue));

        UiKit.Label(panel, "StatsText", stats, 30, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.05f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero,
            new Color(0.78f, 0.8f, 0.84f, 1f));

        // ---- 出击装备 ----
        for (var i = 0; i < Loadouts.All.Length; i++)
        {
            var kit = Loadouts.All[i];
            var selected = profile.SelectedKit == i;
            var affordable = profile.Roubles >= kit.Cost;

            var background = selected
                ? new Color(0.72f, 0.35f, 0.14f, 1f)
                : new Color(0.13f, 0.14f, 0.16f, 1f);

            var label = kit.Name + "\n" + (kit.Cost == 0 ? "免费" : (kit.Cost / 1000) + "k ₽") + "\n" + kit.Blurb;

            var index = i;
            UiKit.TextButton(root, "Kit" + i, label, new Vector2(0.5f, 0.5f),
                new Vector2(-330f + i * 330f, -125f), new Vector2(300f, 118f), background,
                affordable ? Color.white : new Color(0.62f, 0.48f, 0.48f, 1f), 24,
                () => SelectKit(index));
        }

        // ---- 主按钮 ----
        UiKit.TextButton(root, "StartButton", "进 入 突 袭", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -250f), new Vector2(520f, 118f), new Color(0.72f, 0.24f, 0.16f, 1f),
            Color.white, 46, () => { if (_app != null) _app.StartRaid(); });

        // ---- 灵敏度 ----
        UiKit.TextButton(root, "SensDown", "－", new Vector2(0.5f, 0.5f),
            new Vector2(-190f, -358f), new Vector2(110f, 84f), new Color(0.16f, 0.17f, 0.2f, 1f),
            Color.white, 40, () => ChangeSensitivity(-0.02f));

        _sensitivityText = UiKit.Label(root, "SensValue",
            "视角灵敏度 " + (profile.Sensitivity / 0.13f).ToString("0.00") + "x", 30,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-70f, -398f), new Vector2(70f, -318f), new Color(0.85f, 0.87f, 0.9f, 1f));

        UiKit.TextButton(root, "SensUp", "＋", new Vector2(0.5f, 0.5f),
            new Vector2(190f, -358f), new Vector2(110f, 84f), new Color(0.16f, 0.17f, 0.2f, 1f),
            Color.white, 40, () => ChangeSensitivity(0.02f));

        // ---- 底部 ----
        UiKit.TextButton(root, "ResetButton", "重置存档", new Vector2(0.5f, 0f),
            new Vector2(-180f, 78f), new Vector2(280f, 78f), new Color(0.14f, 0.15f, 0.17f, 1f),
            new Color(0.85f, 0.5f, 0.45f, 1f), 28, ResetProfile);

        UiKit.TextButton(root, "QuitButton", "退出", new Vector2(0.5f, 0f),
            new Vector2(180f, 78f), new Vector2(280f, 78f), new Color(0.14f, 0.15f, 0.17f, 1f),
            new Color(0.78f, 0.8f, 0.84f, 1f), 28, () => { Application.Quit(); });

        UiKit.Label(root, "Hint", "左半屏拖动移动 · 右半屏滑动看 · 开火/镜/搜/换 在右下 · 跑/医 在左下",
            24, TextAnchor.MiddleCenter, new Vector2(0f, 0.70f), new Vector2(1f, 0.72f),
            Vector2.zero, Vector2.zero, new Color(0.6f, 0.62f, 0.66f, 1f));
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

    private void ChangeSensitivity(float delta)
    {
        if (_app == null) return;

        _app.Profile.Sensitivity = Mathf.Clamp(_app.Profile.Sensitivity + delta, 0.04f, 0.5f);
        _app.Profile.Save();

        if (_sensitivityText != null)
        {
            _sensitivityText.text = "视角灵敏度 " + (_app.Profile.Sensitivity / 0.13f).ToString("0.00") + "x";
        }
    }

    private void ResetProfile()
    {
        if (_app == null) return;
        _app.Profile.ResetProgress();
        ShowMainMenu(_app);
    }

    private void BuildResult(RaidResult result)
    {
        var root = _canvas.transform;

        var accent = result.Extracted ? new Color(0.3f, 0.85f, 0.45f) : new Color(0.9f, 0.32f, 0.28f);

        UiKit.Panel(root, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.045f, 0.055f, 1f));

        UiKit.Label(root, "Title", result.Extracted ? "撤离成功" : "任务失败",
            100, TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.96f),
            Vector2.zero, Vector2.zero, accent);

        UiKit.Label(root, "Cause", result.Summary, 34, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.75f), new Vector2(1f, 0.81f), Vector2.zero, Vector2.zero,
            new Color(0.78f, 0.8f, 0.84f, 1f));

        // 战利品清单
        var panel = UiKit.Panel(root, "LootPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-470f, -210f), new Vector2(470f, 190f), new Color(0.08f, 0.09f, 0.11f, 0.96f));

        var builder = new StringBuilder();
        if (result.Loot == null || result.Loot.Count == 0)
        {
            builder.Append("背包是空的");
        }
        else
        {
            var shown = 0;
            for (var i = 0; i < result.Loot.Count && shown < 9; i++)
            {
                var entry = result.Loot[i];
                builder.Append(entry.DisplayName).Append(" x").Append(entry.Count)
                    .Append("    ").Append(MetaProfile.FormatRoubles(entry.TotalValue)).Append("\n");
                shown++;
            }

            if (result.Loot.Count > shown)
            {
                builder.Append("…… 还有 ").Append(result.Loot.Count - shown).Append(" 项");
            }
        }

        UiKit.Label(panel, "LootList", builder.ToString(), 30, TextAnchor.UpperLeft,
            Vector2.zero, new Vector2(0.62f, 1f), new Vector2(28f, 20f), new Vector2(0f, -20f),
            new Color(0.86f, 0.88f, 0.9f, 1f));

        UiKit.Label(panel, "LootValue",
            "战利品价值\n" + MetaProfile.FormatRoubles(result.LootValue) + "\n\n击杀 " + result.Kills + " 人\n用时 " +
            Mathf.FloorToInt(result.Seconds / 60f) + ":" + Mathf.FloorToInt(result.Seconds % 60f).ToString("00"),
            32, TextAnchor.UpperCenter, new Vector2(0.62f, 0f), Vector2.one,
            new Vector2(0f, 20f), new Vector2(-24f, -20f), new Color(0.95f, 0.82f, 0.4f, 1f));

        UiKit.Label(root, "Roubles",
            "账户  " + MetaProfile.FormatRoubles(result.RoublesBefore) + "  →  " +
            MetaProfile.FormatRoubles(result.RoublesAfter), 40, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.42f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero,
            result.Extracted ? new Color(0.95f, 0.85f, 0.45f, 1f) : new Color(0.8f, 0.55f, 0.5f, 1f));

        UiKit.TextButton(root, "AgainButton", result.Extracted ? "再 来 一 局" : "再 试 一 次",
            new Vector2(0.5f, 0f), new Vector2(-240f, 150f), new Vector2(420f, 120f),
            new Color(0.72f, 0.24f, 0.16f, 1f), Color.white, 42,
            () => { if (_app != null) _app.StartRaid(); });

        UiKit.TextButton(root, "MenuButton", "返回大厅", new Vector2(0.5f, 0f),
            new Vector2(240f, 150f), new Vector2(420f, 120f), new Color(0.16f, 0.17f, 0.2f, 1f),
            new Color(0.85f, 0.87f, 0.9f, 1f), 42, () => { if (_app != null) _app.ShowMenu(); });
    }
}