using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗 HUD（IMGUI，只画不点）。血/甲/体力、弹药、击杀与战利品、撤离进度、
/// 准星、命中标记、枪口闪光、受击方向指示。所有可点的东西在 TouchControls 里。
/// </summary>
public sealed class Hud : MonoBehaviour
{
    private GameApp _app;

    private GUIStyle _small;
    private GUIStyle _medium;
    private GUIStyle _big;
    private GUIStyle _center;
    private GUIStyle _rightStyle;

    private readonly List<string> _messages = new List<string>();
    private readonly List<float> _expiry = new List<float>();

    public void Initialize(GameApp app)
    {
        _app = app;
        Toast("搜刮物资，找到撤离点离开");
    }

    public void Toast(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        _messages.Add(text);
        _expiry.Add(Time.time + 4.5f);

        while (_messages.Count > 4)
        {
            _messages.RemoveAt(0);
            _expiry.RemoveAt(0);
        }
    }

    private void OnGUI()
    {
        if (_app == null) return;

        EnsureStyles();

        var width = Screen.width;
        var height = Screen.height;

        DrawVitals(width, height);
        DrawAmmo(width, height);
        DrawObjective(width, height);
        DrawStats(width, height);
        DrawProgressBars(width, height);
        DrawCrosshair(width, height);
        DrawWeaponFeedback(width, height);
        DrawDamageIndicator(width, height);
        DrawMessages(width, height);

        if (_app.Player != null && _app.Player.Health01 < 0.32f)
        {
            GUI.color = new Color(0.55f, 0.05f, 0.05f, 0.16f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    private void EnsureStyles()
    {
        var unit = Mathf.Max(16, Mathf.RoundToInt(Screen.height * 0.026f));

        if (_small == null)
        {
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(unit * 0.85f), alignment = TextAnchor.MiddleLeft };
            _small.normal.textColor = new Color(0.86f, 0.88f, 0.9f, 0.95f);
        }

        if (_medium == null)
        {
            _medium = new GUIStyle(GUI.skin.label) { fontSize = unit, alignment = TextAnchor.MiddleLeft };
            _medium.normal.textColor = Color.white;
        }

        if (_big == null)
        {
            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(unit * 1.7f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _big.normal.textColor = Color.white;
        }

        if (_center == null)
        {
            _center = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(unit * 0.95f),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _center.normal.textColor = new Color(1f, 0.9f, 0.6f, 0.95f);
        }

        if (_rightStyle == null)
        {
            _rightStyle = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
            _rightStyle.normal.textColor = new Color(0.9f, 0.9f, 0.94f, 0.95f);
        }
    }

    private void DrawVitals(float width, float height)
    {
        var player = _app.Player;
        if (player == null) return;

        var left = width * 0.022f;
        var top = height * 0.035f;
        var barWidth = Mathf.Min(width * 0.3f, 420f);
        var unit = height * 0.026f;

        GUI.Label(new Rect(left, top, barWidth, unit * 1.2f),
            "生命 " + Mathf.CeilToInt(player.Health) + " / " + Mathf.RoundToInt(player.MaxHealth), _medium);

        Bar(new Rect(left, top + unit * 1.15f, barWidth, unit * 0.62f),
            player.Health01, new Color(0.82f, 0.22f, 0.22f, 0.95f), new Color(0f, 0f, 0f, 0.45f));
        Bar(new Rect(left, top + unit * 1.85f, barWidth, unit * 0.34f),
            player.Armor01, new Color(0.3f, 0.55f, 0.85f, 0.95f), new Color(0f, 0f, 0f, 0.45f));
        Bar(new Rect(left, top + unit * 2.27f, barWidth, unit * 0.24f),
            player.Stamina01, new Color(0.55f, 0.8f, 0.35f, 0.9f), new Color(0f, 0f, 0f, 0.45f));

        var line = top + unit * 2.75f;

        if (player.Bleeding)
        {
            var pulse = 0.6f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.4f;
            GUI.color = new Color(1f, 0.35f, 0.3f, pulse);
            GUI.Label(new Rect(left, line, barWidth, unit), "● 出血中 —— 按「医」包扎", _medium);
            GUI.color = Color.white;
            line += unit * 0.9f;
        }

        GUI.Label(new Rect(left, line, barWidth, unit), "绷带 " + player.Bandages, _small);
    }

    private void DrawAmmo(float width, float height)
    {
        var player = _app.Player;
        if (player == null) return;

        var boxWidth = Mathf.Min(width * 0.32f, 460f);
        var x = width * 0.5f - boxWidth * 0.5f;
        var y = height - height * 0.135f;

        if (player.Reloading)
        {
            GUI.Label(new Rect(x, y, boxWidth, height * 0.05f), "换弹中…", _big);
        }
        else
        {
            var ammo = player.Magazine + " / " + player.Reserve;
            GUI.Label(new Rect(x, y, boxWidth, height * 0.05f), ammo, _big);
        }

        GUI.Label(new Rect(x, y - height * 0.032f, boxWidth, height * 0.03f),
            player.WeaponName + (player.Aiming ? "  ·  开镜" : string.Empty), _small);
    }

    private void DrawObjective(float width, float height)
    {
        var extractions = _app.Extractions;
        if (extractions == null || extractions.Count == 0) return;

        ExtractionZone nearest = null;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < extractions.Count; i++)
        {
            var zone = extractions[i];
            if (zone == null) continue;
            if (zone.DistanceToPlayer < bestDistance)
            {
                nearest = zone;
                bestDistance = zone.DistanceToPlayer;
            }
        }

        if (nearest == null) return;

        var boxWidth = width * 0.46f;
        var x = width * 0.5f - boxWidth * 0.5f;
        var unit = height * 0.026f;

        var text = nearest.ZoneName + "  " + Mathf.RoundToInt(nearest.DistanceToPlayer) + " m";
        GUI.Label(new Rect(x, unit * 1.4f, boxWidth, unit), text, _center);

        if (nearest.PlayerInside)
        {
            Bar(new Rect(x + boxWidth * 0.2f, unit * 2.5f, boxWidth * 0.6f, unit * 0.5f),
                nearest.Progress01, new Color(0.35f, 0.85f, 0.45f, 0.95f), new Color(0f, 0f, 0f, 0.5f));
            GUI.Label(new Rect(x, unit * 3.1f, boxWidth, unit), "撤离中… 留在圈里", _center);
        }
    }

    private void DrawStats(float width, float height)
    {
        var right = width * 0.978f;
        var top = height * 0.035f;
        var unit = height * 0.026f;
        var boxWidth = width * 0.26f;

        var style = _rightStyle;

        GUI.Label(new Rect(right - boxWidth, top, boxWidth, unit),
            "击杀 " + _app.Kills + "   敌人 " + _app.EnemiesAlive + "/" + _app.EnemiesTotal, style);

        GUI.Label(new Rect(right - boxWidth, top + unit * 1.05f, boxWidth, unit),
            "战利品 " + MetaProfile.FormatRoubles(_app.LootValue), style);

        var seconds = _app.RaidSeconds;
        GUI.Label(new Rect(right - boxWidth, top + unit * 2.1f, boxWidth, unit),
            Mathf.FloorToInt(seconds / 60f) + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00"), style);
    }

    private void DrawProgressBars(float width, float height)
    {
        var unit = height * 0.026f;
        var boxWidth = width * 0.34f;
        var x = width * 0.5f - boxWidth * 0.5f;
        var y = height - height * 0.2f;

        var player = _app.Player;
        if (player == null) return;

        if (player.Healing)
        {
            GUI.Label(new Rect(x, y - unit, boxWidth, unit), "包扎中…", _center);
            Bar(new Rect(x, y, boxWidth, unit * 0.4f), player.HealProgress01,
                new Color(0.9f, 0.45f, 0.5f, 0.95f), new Color(0f, 0f, 0f, 0.5f));
        }

        var search = LootContainer.CurrentSearch;
        if (search != null && search.Searching)
        {
            GUI.Label(new Rect(x, y - unit * 2f, boxWidth, unit), "搜刮 " + search.Label + "…", _center);
            Bar(new Rect(x, y - unit * 1.05f, boxWidth, unit * 0.4f), search.Progress01,
                new Color(0.3f, 0.7f, 0.95f, 0.95f), new Color(0f, 0f, 0f, 0.5f));
        }
    }

    private void DrawCrosshair(float width, float height)
    {
        var player = _app.Player;
        if (player == null || !player.IsAlive) return;

        var cx = width * 0.5f;
        var cy = height * 0.5f;
        var gap = height * 0.012f;
        var length = height * 0.012f;
        var thickness = Mathf.Max(2f, height * 0.0022f);

        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gap - length, thickness, length), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gap, thickness, length), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - gap - length, cy - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + gap, cy - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawWeaponFeedback(float width, float height)
    {
        var player = _app.Player;
        if (player == null) return;

        // 枪口闪光：准星下方的星形，尺寸随屏幕高度
        if (player.MuzzleFlashActive)
        {
            var t = player.MuzzleFlash01;
            var cx = width * 0.5f;
            var cy = height * 0.60f;
            var len = Mathf.Lerp(height * 0.012f, height * 0.055f, t);
            var core = Mathf.Lerp(height * 0.006f, height * 0.02f, t);
            var jitter = Mathf.Sin(Time.time * 110f) * 7f;

            var saved = GUI.matrix;

            for (var i = 0; i < 6; i++)
            {
                var armLength = i % 3 == 0 ? len * 1.35f : len * 0.8f;
                GUIUtility.RotateAroundPivot(i * 60f + 12f + jitter, new Vector2(cx, cy));
                GUI.color = new Color(1f, 0.82f, 0.45f, 0.5f * t);
                GUI.DrawTexture(new Rect(cx - 1.5f, cy - armLength, 3f, armLength + 5f), Texture2D.whiteTexture);
            }

            GUI.matrix = saved;

            GUI.color = new Color(1f, 0.97f, 0.86f, 0.7f * t);
            GUI.DrawTexture(new Rect(cx - core * 0.5f, cy - core * 0.5f, core, core), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 命中标记：实线 X
        if (player.HitMarkerActive)
        {
            var cx = width * 0.5f;
            var cy = height * 0.5f;
            var len = Mathf.Max(16f, height * 0.022f);
            var saved = GUI.matrix;

            GUI.color = new Color(1f, 0.35f, 0.28f, 0.95f);
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - len, 3f, len * 2f), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-90f, new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - len, 3f, len * 2f), Texture2D.whiteTexture);

            GUI.matrix = saved;
            GUI.color = Color.white;
        }
    }

    private void DrawDamageIndicator(float width, float height)
    {
        var player = _app.Player;
        if (player == null || !player.DamageFlashActive) return;

        var camera = Camera.main;
        if (camera == null) return;

        var toDamage = player.LastDamageFrom - player.CameraPosition;
        toDamage.y = 0f;

        var forward = camera.transform.forward;
        forward.y = 0f;

        if (toDamage.sqrMagnitude < 0.001f || forward.sqrMagnitude < 0.001f) return;

        var angle = Vector3.SignedAngle(forward.normalized, toDamage.normalized, Vector3.up);

        var cx = width * 0.5f;
        var cy = height * 0.5f;
        var radius = height * 0.12f;
        var alpha = player.DamageFlash01;

        var saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
        GUI.color = new Color(1f, 0.25f, 0.2f, 0.85f * alpha);
        GUI.DrawTexture(new Rect(cx - width * 0.02f, cy - radius - height * 0.02f,
            width * 0.04f, height * 0.008f), Texture2D.whiteTexture);
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    private void DrawMessages(float width, float height)
    {
        for (var i = _messages.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _expiry[i])
            {
                _messages.RemoveAt(i);
                _expiry.RemoveAt(i);
            }
        }

        var unit = height * 0.026f;
        var y = height * 0.62f;

        for (var i = _messages.Count - 1; i >= 0; i--)
        {
            var alpha = Mathf.Clamp01((_expiry[i] - Time.time) / 1.2f);
            var style = _center;
            style.normal.textColor = new Color(1f, 0.9f, 0.6f, 0.95f * alpha);
            GUI.Label(new Rect(width * 0.14f, y, width * 0.72f, unit * 1.1f), _messages[i], style);
            y += unit * 0.95f;
        }

        _center.normal.textColor = new Color(1f, 0.9f, 0.6f, 0.95f);

        GUI.Label(new Rect(24f, height - unit * 1.4f, 220f, unit),
            Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)) + " FPS", _small);
    }

    private static void Bar(Rect rect, float value, Color fill, Color background)
    {
        GUI.color = background;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        var clamped = Mathf.Clamp01(value);
        if (clamped > 0.001f)
        {
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * clamped, rect.height), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }
}