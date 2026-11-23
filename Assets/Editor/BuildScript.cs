using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// CI 构建入口：Unity -batchmode -executeMethod BuildScript.BuildAndroid
/// 若场景不存在，会自动生成一个带 GameDataBootstrap 的启动场景。
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
        if (File.Exists(ScenePath))
        {
            return;
        }

        Directory.CreateDirectory("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var bootstrap = new GameObject("GameDataBootstrap");
        bootstrap.AddComponent<GameDataBootstrap>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[Build] 已生成启动场景: " + ScenePath);
    }
}
