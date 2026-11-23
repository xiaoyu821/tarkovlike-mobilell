using System.Collections;
using System.IO;
using Game.Data;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 启动时把 StreamingAssets/GameData 复制到 persistentDataPath，
/// 再交给 JsonCatalogLoader。Android 上 StreamingAssets 在 APK 内，System.IO 读不到。
/// 加载结果直接画在屏幕上，方便真机验证（无需 logcat）。
/// </summary>
public sealed class GameDataBootstrap : MonoBehaviour
{
    private static readonly string[] DataFiles =
    {
        "items.json",
        "weapons.json",
        "ammo.json",
        "armor.json",
        "loot_tables.json"
    };

    private string _status = "正在加载数据…";

    public DataCatalog Catalog { get; private set; }

    private IEnumerator Start()
    {
        var sourceDir = Path.Combine(Application.streamingAssetsPath, "GameData");
        var targetDir = Path.Combine(Application.persistentDataPath, "GameData");
        Directory.CreateDirectory(targetDir);

        foreach (var fileName in DataFiles)
        {
            var sourcePath = Path.Combine(sourceDir, fileName);
            var targetPath = Path.Combine(targetDir, fileName);

            if (sourcePath.Contains("://"))
            {
                using var request = UnityWebRequest.Get(sourcePath);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _status = "读取 " + fileName + " 失败: " + request.error;
                    Debug.LogError("[Data] " + _status);
                    yield break;
                }

                File.WriteAllBytes(targetPath, request.downloadHandler.data);
            }
            else
            {
                File.Copy(sourcePath, targetPath, true);
            }
        }

        try
        {
            Catalog = JsonCatalogLoader.LoadFromDirectory(targetDir);
            var issues = Catalog.Validate();

            if (issues.Count > 0)
            {
                _status = "数据校验失败，共 " + issues.Count + " 项:\n" + string.Join("\n", issues);
                Debug.LogError("[Data] " + _status);
            }
            else
            {
                _status = "数据加载成功\n" +
                          "items=" + Catalog.Items.Count +
                          "  weapons=" + Catalog.Weapons.Count +
                          "  ammo=" + Catalog.Ammo.Count +
                          "  armor=" + Catalog.Armor.Count +
                          "  loot=" + Catalog.LootTables.Count;
                Debug.Log("[Data] " + _status.Replace("\n", " "));
            }
        }
        catch (System.Exception exception)
        {
            _status = "数据加载异常: " + exception.Message;
            Debug.LogException(exception);
        }
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(24, Screen.width / 40),
            wordWrap = true
        };

        GUI.Label(new Rect(24f, 24f, Screen.width - 48f, Screen.height - 48f), _status, style);
    }
}
