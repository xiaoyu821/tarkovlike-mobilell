using System;
using UnityEngine;

/// <summary>跨局存档：卢布、战绩、灵敏度。用 PlayerPrefs + JsonUtility，不需要额外文件权限。</summary>
[Serializable]
public sealed class MetaProfile
{
    private const string StorageKey = "tarkovlike.profile.v1";

    public int Roubles = 150000;
    public int Raids;
    public int Extracts;
    public int Deaths;
    public int Kills;
    public int BestLootValue;
    public int TotalLootValue;
    public float Sensitivity = 0.13f;
    public int SelectedKit = 1;
    public int TargetFps = 60;
    public bool FirstRunDone;

    /// <summary>可选帧率档位，最高 165（需要高刷屏才有意义）。</summary>
    public static readonly int[] FpsPresets = { 30, 45, 60, 90, 120, 144, 165 };

    public static int NextFps(int current)
    {
        for (var i = 0; i < FpsPresets.Length; i++)
        {
            if (FpsPresets[i] == current) return FpsPresets[(i + 1) % FpsPresets.Length];
        }

        return 60;
    }

    public float ExtractRate => Raids <= 0 ? 0f : Extracts / (float)Raids;

    public static MetaProfile Load()
    {
        try
        {
            var raw = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                var profile = JsonUtility.FromJson<MetaProfile>(raw);
                if (profile != null)
                {
                    profile.Sensitivity = Mathf.Clamp(profile.Sensitivity, 0.04f, 0.5f);
                    return profile;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Meta] 读档失败，使用默认值: " + exception.Message);
        }

        return new MetaProfile();
    }

    public void Save()
    {
        try
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Meta] 存档失败: " + exception.Message);
        }
    }

    public void ResetProgress()
    {
        Roubles = 150000;
        Raids = 0;
        Extracts = 0;
        Deaths = 0;
        Kills = 0;
        BestLootValue = 0;
        TotalLootValue = 0;
        FirstRunDone = false;
        Save();
    }

    public static string FormatRoubles(int value)
    {
        // 12 345 ₽
        return value.ToString("N0").Replace(",", " ") + " ₽";
    }
}