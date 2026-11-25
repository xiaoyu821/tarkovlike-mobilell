using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CI 构建入口：Unity -batchmode -executeMethod BuildScript.BuildAndroid
/// 若场景不存在，会自动生成一个空场景并挂上数据加载与 M0 白盒引导组件。
/// </summary>
public static class BuildScript
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string DefaultOutput = "build/Android/tarkovlike.apk";

    public static void BuildAndroid()
    {
        ConfigurePlayerSettings();
        EnsureScene();

        var output = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output))
        {
            output = DefaultOutput;
        }

        var directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogWarning("[Build] 切换到 Android 平台失败，继续尝试构建");
        }

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        var summary = report.summary;
        Debug.Log("[Build] result=" + summary.result +
                  " size=" + summary.totalSize +
                  " errors=" + summary.totalErrors +
                  " output=" + output);

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception("Android 构建失败: " + summary.result);
        }
    }

    internal static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "xiaoyu";
        PlayerSettings.productName = "TarkovLike M0";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.xiaoyu.tarkovlike");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.bundleVersionCode = 1;
    }

    private static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            throw new Exception("启动场景缺失: " + ScenePath);
        }
    }
}
