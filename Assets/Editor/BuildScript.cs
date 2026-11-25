using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CI 构建入口：Unity -batchmode -executeMethod BuildScript.BuildAndroid
/// 构建配置全部保存在提交的 ProjectSettings 中，运行时不再修改，避免构建中途触发资产重新导入。
/// </summary>
public static class BuildScript
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string DefaultOutput = "build/Android/tarkovlike.apk";

    public static void BuildAndroid()
    {
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

    private static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            throw new Exception("启动场景缺失: " + ScenePath);
        }
    }
}
