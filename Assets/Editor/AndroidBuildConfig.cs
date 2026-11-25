using UnityEditor;
using UnityEngine;

/// <summary>
/// 在编辑器加载阶段先应用 Android 构建配置（尤其是 IL2CPP 脚本后端）。
/// 切换脚本后端会触发重编译和域重载；若放在 BuildAndroid 内部再立即 BuildPlayer，
/// 会报 "Must set an output directory through SetCompileScriptsOutputDirectory before compiling"。
/// </summary>
[InitializeOnLoad]
public static class AndroidBuildConfig
{
    static AndroidBuildConfig()
    {
        if (Application.isBatchMode)
        {
            BuildScript.ConfigurePlayerSettings();
        }
    }
}
