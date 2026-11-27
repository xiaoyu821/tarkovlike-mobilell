using UnityEngine;

/// <summary>开镜时隐藏的部件（枪托会捅到镜头里，挡视线）。</summary>
public sealed class ViewmodelHideOnAim : MonoBehaviour
{
}

/// <summary>记录这把枪的机械瞄具高度（准星线相对枪根的高度），开镜时按它对齐屏幕中心。</summary>
public sealed class ViewmodelSight : MonoBehaviour
{
    public float Height = 0.085f;
}

/// <summary>
/// 美术层：CC0 贴图（ambientCG）+ 代码内拼的低多边形模型。
/// 没有任何 .fbx/.blend：模型全部由方块和圆柱在运行时拼出来，
/// 尺寸/朝向可控，IL2CPP 下也不会出现导入比例错误。
/// </summary>
public static class Art
{
    private static Mesh _cube;
    private static Mesh _cylinder;
    private static Material _ground;
    private static Material _wall;
    private static Material _wood;
    private static Material _metal;
    private static Material _metalDark;
    private static Material _gunMetal;
    private static Material _gunPolymer;
    private static Material _gunWood;
    private static Material _skin;
    private static Material _uniform;
    private static Material _vest;
    private static Material _helmet;
    private static Material _beacon;
    private static Material _pad;
    private static Material _black;

    public static Mesh CubeMesh => _cube != null ? _cube : (_cube = GrabMesh(PrimitiveType.Cube));
    public static Mesh CylinderMesh => _cylinder != null ? _cylinder : (_cylinder = GrabMesh(PrimitiveType.Cylinder));

    public static Material Ground =>
        _ground != null ? _ground : (_ground = Textured("ground", new Color(0.66f, 0.66f, 0.66f), 26f, 0.12f));

    public static Material Wall =>
        _wall != null ? _wall : (_wall = Textured("wall", new Color(0.74f, 0.74f, 0.76f), 4f, 0.14f));

    public static Material Wood =>
        _wood != null ? _wood : (_wood = Textured("wood", new Color(0.9f, 0.78f, 0.62f), 1f, 0.2f));

    public static Material Metal =>
        _metal != null ? _metal : (_metal = Textured("metal", new Color(0.6f, 0.63f, 0.6f), 1f, 0.45f));

    public static Material MetalDark =>
        _metalDark != null ? _metalDark : (_metalDark = Textured("metal_dark", new Color(0.5f, 0.53f, 0.5f), 1f, 0.4f));

    public static Material GunMetal => _gunMetal != null ? _gunMetal : (_gunMetal = Flat(new Color(0.13f, 0.135f, 0.145f), 0.55f));
    public static Material GunPolymer => _gunPolymer != null ? _gunPolymer : (_gunPolymer = Flat(new Color(0.16f, 0.16f, 0.17f), 0.25f));
    public static Material GunWood => _gunWood != null ? _gunWood : (_gunWood = Flat(new Color(0.42f, 0.28f, 0.16f), 0.3f));
    public static Material Skin => _skin != null ? _skin : (_skin = Flat(new Color(0.62f, 0.48f, 0.4f), 0.15f));
    public static Material Uniform => _uniform != null ? _uniform : (_uniform = Flat(new Color(0.34f, 0.36f, 0.26f), 0.12f));
    public static Material Vest => _vest != null ? _vest : (_vest = Flat(new Color(0.22f, 0.24f, 0.19f), 0.15f));
    public static Material Helmet => _helmet != null ? _helmet : (_helmet = Flat(new Color(0.24f, 0.26f, 0.22f), 0.35f));
    public static Material Black => _black != null ? _black : (_black = Flat(new Color(0.07f, 0.07f, 0.08f), 0.3f));

    public static Material ExtractPad
    {
        get
        {
            if (_pad != null) return _pad;
            _pad = Flat(new Color(0.14f, 0.34f, 0.2f), 0.15f);
            _pad.EnableKeyword("_EMISSION");
            _pad.SetColor("_EmissionColor", new Color(0.1f, 0.34f, 0.16f) * 0.7f);
            return _pad;
        }
    }

    public static Material Beacon
    {
        get
        {
            if (_beacon != null) return _beacon;
            _beacon = Flat(new Color(0.3f, 0.95f, 0.5f), 0.6f);
            _beacon.EnableKeyword("_EMISSION");
            _beacon.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 0.4f) * 0.9f);
            return _beacon;
        }
    }

    /// <summary>从上方往下打射线找地面高度。生成任何东西之前都用它对齐，避免悬空。</summary>
    public static float GroundY(Vector3 position)
    {
        var origin = new Vector3(position.x, position.y + 8f, position.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return 0f;
    }

    // ---------- 基础构件 ----------

    private static Mesh GrabMesh(PrimitiveType type)
    {
        var temp = GameObject.CreatePrimitive(type);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);
        return mesh;
    }

    private static Material Flat(Color color, float gloss)
    {
        var material = BaseMaterial(color);
        material.SetFloat("_Glossiness", gloss);
        return material;
    }

    private static Material Textured(string textureName, Color tint, float tiling, float gloss)
    {
        var material = BaseMaterial(tint);
        var texture = Resources.Load<Texture2D>("Textures/" + textureName);

        if (texture != null)
        {
            material.mainTexture = texture;
            material.mainTextureScale = new Vector2(tiling, tiling);
        }

        material.SetFloat("_Glossiness", gloss);
        return material;
    }

    private static Material BaseMaterial(Color color)
    {
        var shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        return new Material(shader) { color = color };
    }

    /// <summary>无碰撞体的方块构件（装饰物绝对不能挡住射线）。</summary>
    public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
        Material material, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.transform.localRotation = Quaternion.Euler(euler);

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = CubeMesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        return go;
    }

    public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        return Box(parent, name, position, size, material, Vector3.zero);
    }

    /// <summary>圆柱构件。Unity 的圆柱原始体高 2、直径 1，这里按"直径 + 长度"换算。</summary>
    public static GameObject Cylinder(Transform parent, string name, Vector3 position, float diameter,
        float length, Material material, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
        go.transform.localRotation = Quaternion.Euler(euler);

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = CylinderMesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        return go;
    }

    // ---------- 第一人称武器 ----------

    /// <summary>
    /// 按武器 ID 拼一把第一人称枪。枪口朝 +Z（相机前方），原点大致在握把上方。
    /// </summary>
    public static GameObject BuildViewmodel(Transform parent, string weaponId)
    {
        var root = new GameObject("Viewmodel");
        root.transform.SetParent(parent, false);

        var sight = root.AddComponent<ViewmodelSight>();

        switch (weaponId)
        {
            case "wpn_p9":
                BuildPistol(root.transform);
                sight.Height = 0.072f;
                break;
            case "wpn_wasp":
                BuildSmg(root.transform);
                sight.Height = 0.082f;
                break;
            case "wpn_mule12":
                BuildShotgun(root.transform);
                sight.Height = 0.066f;
                break;
            case "wpn_kestrel74":
                BuildRifle(root.transform, 1.1f);
                sight.Height = 0.086f;
                break;
            default:
                BuildRifle(root.transform, 1f);
                sight.Height = 0.086f;
                break;
        }

        return root;
    }

    private static void BuildRifle(Transform root, float lengthScale)
    {
        // 机匣
        Box(root, "Receiver", new Vector3(0f, 0f, 0.02f), new Vector3(0.062f, 0.085f, 0.30f), GunMetal);
        // 护木
        Box(root, "Handguard", new Vector3(0f, -0.005f, 0.20f * lengthScale),
            new Vector3(0.052f, 0.058f, 0.26f * lengthScale), GunPolymer);
        // 枪管
        Cylinder(root, "Barrel", new Vector3(0f, 0.012f, 0.30f * lengthScale),
            0.020f, 0.36f * lengthScale, GunMetal, new Vector3(90f, 0f, 0f));
        // 消焰器
        Cylinder(root, "Muzzle", new Vector3(0f, 0.012f, 0.48f * lengthScale),
            0.030f, 0.05f, Black, new Vector3(90f, 0f, 0f));
        // 弹匣（略前倾）
        Box(root, "Magazine", new Vector3(0f, -0.115f, 0.055f), new Vector3(0.042f, 0.16f, 0.062f),
            GunPolymer, new Vector3(-8f, 0f, 0f));
        // 握把
        Box(root, "Grip", new Vector3(0f, -0.095f, -0.045f), new Vector3(0.038f, 0.11f, 0.05f),
            GunPolymer, new Vector3(16f, 0f, 0f));
        // 枪托（开镜时隐藏，否则会糊在镜头前）
        var stock = Box(root, "Stock", new Vector3(0f, -0.012f, -0.20f), new Vector3(0.045f, 0.07f, 0.20f), GunMetal);
        stock.AddComponent<ViewmodelHideOnAim>();
        // 导轨 + 机械瞄具
        Box(root, "Rail", new Vector3(0f, 0.052f, 0.04f), new Vector3(0.028f, 0.014f, 0.34f), GunMetal);
        // 机械瞄具：后照门留缺口，准星柱在正中，开镜时正好套在屏幕中心
        Box(root, "RearBase", new Vector3(0f, 0.058f, -0.06f), new Vector3(0.055f, 0.009f, 0.02f), GunMetal);
        Box(root, "RearLeft", new Vector3(-0.015f, 0.078f, -0.06f), new Vector3(0.009f, 0.032f, 0.012f), Black);
        Box(root, "RearRight", new Vector3(0.015f, 0.078f, -0.06f), new Vector3(0.009f, 0.032f, 0.012f), Black);
        Box(root, "FrontBase", new Vector3(0f, 0.058f, 0.24f * lengthScale), new Vector3(0.03f, 0.009f, 0.02f), GunMetal);
        Box(root, "FrontPost", new Vector3(0f, 0.082f, 0.24f * lengthScale), new Vector3(0.007f, 0.028f, 0.01f), Black);
    }

    private static void BuildSmg(Transform root)
    {
        Box(root, "Receiver", new Vector3(0f, 0f, 0.02f), new Vector3(0.055f, 0.08f, 0.22f), GunMetal);
        Box(root, "Handguard", new Vector3(0f, -0.005f, 0.15f), new Vector3(0.045f, 0.05f, 0.12f), GunPolymer);
        Cylinder(root, "Barrel", new Vector3(0f, 0.012f, 0.24f), 0.018f, 0.14f, GunMetal, new Vector3(90f, 0f, 0f));
        Cylinder(root, "Muzzle", new Vector3(0f, 0.012f, 0.31f), 0.026f, 0.04f, Black, new Vector3(90f, 0f, 0f));
        Box(root, "Magazine", new Vector3(0f, -0.14f, 0.04f), new Vector3(0.04f, 0.22f, 0.055f),
            GunPolymer, new Vector3(-4f, 0f, 0f));
        Box(root, "Grip", new Vector3(0f, -0.09f, -0.05f), new Vector3(0.036f, 0.10f, 0.048f),
            GunPolymer, new Vector3(16f, 0f, 0f));
        var smgStock = Box(root, "Stock", new Vector3(0f, -0.005f, -0.16f), new Vector3(0.03f, 0.05f, 0.14f), GunMetal);
        smgStock.AddComponent<ViewmodelHideOnAim>();
        Box(root, "Rail", new Vector3(0f, 0.05f, 0.02f), new Vector3(0.024f, 0.012f, 0.24f), GunMetal);
        Box(root, "RearLeft", new Vector3(-0.013f, 0.074f, -0.04f), new Vector3(0.008f, 0.03f, 0.01f), Black);
        Box(root, "RearRight", new Vector3(0.013f, 0.074f, -0.04f), new Vector3(0.008f, 0.03f, 0.01f), Black);
        Box(root, "FrontPost", new Vector3(0f, 0.078f, 0.16f), new Vector3(0.006f, 0.026f, 0.008f), Black);
    }

    private static void BuildShotgun(Transform root)
    {
        Box(root, "Receiver", new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.09f, 0.26f), GunMetal);
        Cylinder(root, "Barrel", new Vector3(0f, 0.022f, 0.36f), 0.030f, 0.62f, GunMetal, new Vector3(90f, 0f, 0f));
        Cylinder(root, "Tube", new Vector3(0f, -0.018f, 0.30f), 0.026f, 0.50f, Black, new Vector3(90f, 0f, 0f));
        Box(root, "Pump", new Vector3(0f, -0.018f, 0.26f), new Vector3(0.055f, 0.055f, 0.16f), GunWood);
        var sgStock = Box(root, "Stock", new Vector3(0f, -0.02f, -0.24f), new Vector3(0.05f, 0.09f, 0.24f), GunWood);
        sgStock.AddComponent<ViewmodelHideOnAim>();
        Box(root, "Grip", new Vector3(0f, -0.075f, -0.055f), new Vector3(0.036f, 0.09f, 0.05f),
            GunWood, new Vector3(14f, 0f, 0f));
        Box(root, "FrontPost", new Vector3(0f, 0.064f, 0.60f), new Vector3(0.008f, 0.01f, 0.008f), Black);
    }

    private static void BuildPistol(Transform root)
    {
        Box(root, "Slide", new Vector3(0f, 0.028f, 0.06f), new Vector3(0.036f, 0.055f, 0.20f), GunMetal);
        Box(root, "Frame", new Vector3(0f, -0.012f, 0.03f), new Vector3(0.032f, 0.035f, 0.15f), GunPolymer);
        Box(root, "Grip", new Vector3(0f, -0.10f, -0.03f), new Vector3(0.032f, 0.14f, 0.06f),
            GunPolymer, new Vector3(14f, 0f, 0f));
        Cylinder(root, "Barrel", new Vector3(0f, 0.028f, 0.17f), 0.014f, 0.04f, Black, new Vector3(90f, 0f, 0f));
        Box(root, "RearLeft", new Vector3(-0.011f, 0.064f, -0.02f), new Vector3(0.006f, 0.016f, 0.01f), Black);
        Box(root, "RearRight", new Vector3(0.011f, 0.064f, -0.02f), new Vector3(0.006f, 0.016f, 0.01f), Black);
        Box(root, "FrontPost", new Vector3(0f, 0.066f, 0.14f), new Vector3(0.005f, 0.014f, 0.008f), Black);
    }

    // ---------- Scav 士兵 ----------

    /// <summary>方块拼的士兵，脚底在 y=0，总高约 1.8（和 CharacterController 对齐）。</summary>
    public static GameObject BuildSoldier(Transform parent)
    {
        var root = new GameObject("Body");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // 腿
        Box(root.transform, "LegL", new Vector3(-0.11f, 0.38f, 0f), new Vector3(0.15f, 0.76f, 0.17f), Uniform);
        Box(root.transform, "LegR", new Vector3(0.11f, 0.38f, 0f), new Vector3(0.15f, 0.76f, 0.17f), Uniform);
        // 靴子
        Box(root.transform, "BootL", new Vector3(-0.11f, 0.05f, 0.03f), new Vector3(0.17f, 0.1f, 0.24f), Black);
        Box(root.transform, "BootR", new Vector3(0.11f, 0.05f, 0.03f), new Vector3(0.17f, 0.1f, 0.24f), Black);
        // 躯干 + 战术背心
        Box(root.transform, "Torso", new Vector3(0f, 1.02f, 0f), new Vector3(0.36f, 0.55f, 0.22f), Uniform);
        Box(root.transform, "Vest", new Vector3(0f, 1.04f, 0f), new Vector3(0.40f, 0.42f, 0.26f), Vest);
        // 背包
        Box(root.transform, "Pack", new Vector3(0f, 1.02f, -0.19f), new Vector3(0.30f, 0.36f, 0.14f), Vest);
        // 手臂
        Box(root.transform, "ArmL", new Vector3(-0.235f, 1.0f, 0.04f), new Vector3(0.12f, 0.46f, 0.13f),
            Uniform, new Vector3(-24f, 0f, 0f));
        Box(root.transform, "ArmR", new Vector3(0.235f, 1.0f, 0.04f), new Vector3(0.12f, 0.46f, 0.13f),
            Uniform, new Vector3(-24f, 0f, 0f));
        // 头 + 头盔
        Box(root.transform, "Head", new Vector3(0f, 1.42f, 0f), new Vector3(0.19f, 0.21f, 0.19f), Skin);
        Box(root.transform, "Helmet", new Vector3(0f, 1.53f, -0.005f), new Vector3(0.24f, 0.13f, 0.24f), Helmet);
        Box(root.transform, "Visor", new Vector3(0f, 1.44f, 0.10f), new Vector3(0.16f, 0.07f, 0.03f), Black);
        // 手里的枪
        Box(root.transform, "Gun", new Vector3(0.16f, 1.02f, 0.30f), new Vector3(0.05f, 0.07f, 0.55f), GunMetal);
        Box(root.transform, "GunMag", new Vector3(0.16f, 0.94f, 0.26f), new Vector3(0.04f, 0.14f, 0.06f), GunPolymer);
        Box(root.transform, "GunStock", new Vector3(0.16f, 1.0f, 0.02f), new Vector3(0.045f, 0.06f, 0.16f), GunPolymer);

        return root;
    }

    // ---------- 箱子 ----------

    /// <summary>木质/军械箱。边长 1，由调用方用 localScale 缩放。</summary>
    public static GameObject BuildCrate(Transform parent, bool military)
    {
        var root = new GameObject("CrateVisual");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        var body = military ? MetalDark : Wood;

        Box(root.transform, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.96f, 0.96f, 0.96f), body);

        if (military)
        {
            Box(root.transform, "Lid", new Vector3(0f, 0.44f, 0f), new Vector3(1f, 0.1f, 1f), Metal);
            Box(root.transform, "LatchL", new Vector3(-0.28f, 0.42f, 0.5f), new Vector3(0.1f, 0.16f, 0.03f), Metal);
            Box(root.transform, "LatchR", new Vector3(0.28f, 0.42f, 0.5f), new Vector3(0.1f, 0.16f, 0.03f), Metal);
            Box(root.transform, "BandH", new Vector3(0f, 0.02f, 0f), new Vector3(1.02f, 0.075f, 1.02f), Metal);
        }
        else
        {
            // 板条：两横两竖 + 四角护木
            Box(root.transform, "SlatTop", new Vector3(0f, 0.42f, 0f), new Vector3(1.02f, 0.1f, 1.02f), Wood);
            Box(root.transform, "SlatBot", new Vector3(0f, -0.42f, 0f), new Vector3(1.02f, 0.1f, 1.02f), Wood);
            Box(root.transform, "PostA", new Vector3(-0.47f, 0f, -0.47f), new Vector3(0.12f, 1f, 0.12f), GunWood);
            Box(root.transform, "PostB", new Vector3(0.47f, 0f, -0.47f), new Vector3(0.12f, 1f, 0.12f), GunWood);
            Box(root.transform, "PostC", new Vector3(-0.47f, 0f, 0.47f), new Vector3(0.12f, 1f, 0.12f), GunWood);
            Box(root.transform, "PostD", new Vector3(0.47f, 0f, 0.47f), new Vector3(0.12f, 1f, 0.12f), GunWood);
        }

        return root;
    }

    /// <summary>撤离信标：一根 7 米高的杆 + 顶部发光块，远处就能看见。</summary>
    public static void BuildBeacon(Transform parent, Vector3 position)
    {
        Cylinder(parent, "BeaconPole", position + new Vector3(0f, 3.5f, 0f), 0.12f, 7f, Metal, Vector3.zero);
        Box(parent, "BeaconLight", position + new Vector3(0f, 7.1f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Beacon);
        Box(parent, "BeaconFlag", position + new Vector3(0.3f, 6.4f, 0f), new Vector3(0.6f, 0.4f, 0.02f), Beacon);
    }
}