using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 场景入口（文件名沿用 M0Bootstrap，避免破坏 Main.unity 里已保存的脚本引用）。
/// 只负责全局设置：帧率、屏幕常亮、物理同步、环境光/雾、太阳光，然后把 GameApp 挂上。
/// </summary>
public sealed class M0Bootstrap : MonoBehaviour
{
    private void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // 只允许横屏：竖屏下左右分区的触屏布局会挤成一团
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // 运行时用 transform 摆好物体后，物理世界必须立刻同步，
        // 否则 CharacterController 会以为墙/地面还在原点（这是之前穿地的直接原因）。
        Physics.autoSyncTransforms = true;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.37f, 0.4f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0095f;
        RenderSettings.fogColor = new Color(0.13f, 0.14f, 0.16f);

        var sunGo = new GameObject("Sun");
        sunGo.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.12f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;

        if (GetComponent<GameApp>() == null)
        {
            gameObject.AddComponent<GameApp>();
        }
    }
}