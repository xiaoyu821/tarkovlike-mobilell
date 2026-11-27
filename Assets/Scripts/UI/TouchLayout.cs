using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 触屏按键布局存档。每个按钮记录一个 anchoredPosition，外加一个整体缩放。
/// 保存在 PlayerPrefs 里，一局结束、重启游戏都还在。
/// </summary>
[Serializable]
public sealed class TouchLayoutData
{
    public List<string> Names = new List<string>();
    public List<float> Xs = new List<float>();
    public List<float> Ys = new List<float>();
    public float Scale = 1f;
}

public static class TouchLayout
{
    private const string StorageKey = "tarkovlike.touch.v1";

    private static TouchLayoutData _data;

    public static TouchLayoutData Data
    {
        get
        {
            if (_data == null) _data = Load();
            return _data;
        }
    }

    public static float Scale => Mathf.Clamp(Data.Scale, 0.7f, 1.4f);

    private static TouchLayoutData Load()
    {
        try
        {
            var raw = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                var data = JsonUtility.FromJson<TouchLayoutData>(raw);
                if (data != null)
                {
                    data.Scale = Mathf.Clamp(data.Scale, 0.7f, 1.4f);
                    return data;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TouchLayout] 读取失败: " + exception.Message);
        }

        return new TouchLayoutData();
    }

    public static bool TryGet(string key, out Vector2 position)
    {
        var data = Data;
        var index = data.Names.IndexOf(key);
        if (index >= 0 && index < data.Xs.Count && index < data.Ys.Count)
        {
            position = new Vector2(data.Xs[index], data.Ys[index]);
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    public static void Set(string key, Vector2 position)
    {
        var data = Data;
        var index = data.Names.IndexOf(key);

        if (index < 0)
        {
            data.Names.Add(key);
            data.Xs.Add(position.x);
            data.Ys.Add(position.y);
        }
        else
        {
            data.Xs[index] = position.x;
            data.Ys[index] = position.y;
        }
    }

    public static void SetScale(float scale)
    {
        Data.Scale = Mathf.Clamp(scale, 0.7f, 1.4f);
        Save();
    }

    public static void ResetAll()
    {
        _data = new TouchLayoutData();
        PlayerPrefs.DeleteKey(StorageKey);
        PlayerPrefs.Save();
    }

    public static void Save()
    {
        try
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TouchLayout] 保存失败: " + exception.Message);
        }
    }
}

/// <summary>编辑模式下让按钮能拖着走。</summary>
public sealed class DragMove : MonoBehaviour,
    UnityEngine.EventSystems.IBeginDragHandler,
    UnityEngine.EventSystems.IDragHandler,
    UnityEngine.EventSystems.IEndDragHandler
{
    public string Key;

    private RectTransform _rect;
    private Canvas _canvas;
    private Vector2 _pointerStart;
    private Vector2 _positionStart;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        _pointerStart = eventData.position;
        _positionStart = _rect.anchoredPosition;
    }

    public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        var scale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
        _rect.anchoredPosition = _positionStart + (eventData.position - _pointerStart) / scale;
    }

    public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        TouchLayout.Set(Key, _rect.anchoredPosition);
        TouchLayout.Save();
    }
}