/// <summary>
/// 出击装备套。撤离赚到的卢布在这里花掉：进场先扣钱，死了钱和战利品都没了，
/// 这就是整套循环的风险/收益来源。
/// </summary>
public sealed class LoadoutKit
{
    public string Name = "轻装";
    public string Blurb = string.Empty;
    public int Cost;
    public string WeaponId = "wpn_p9";
    public float Armor;
    public int Reserve = 68;
    public int Bandages = 2;
}

public static class Loadouts
{
    public static readonly LoadoutKit[] All =
    {
        new LoadoutKit
        {
            Name = "轻装", Blurb = "P9 手枪 · 无甲 · 2 绷带",
            Cost = 0, WeaponId = "wpn_p9", Armor = 0f, Reserve = 68, Bandages = 2
        },
        new LoadoutKit
        {
            Name = "标准", Blurb = "Viper-15 · PACA 软甲 · 3 绷带",
            Cost = 15000, WeaponId = "wpn_viper", Armor = 40f, Reserve = 120, Bandages = 3
        },
        new LoadoutKit
        {
            Name = "重装", Blurb = "Kestrel-74 · MK4 插板 · 4 绷带",
            Cost = 45000, WeaponId = "wpn_kestrel74", Armor = 60f, Reserve = 150, Bandages = 4
        }
    };

    public static LoadoutKit Get(int index)
    {
        if (index < 0 || index >= All.Length) return All[0];
        return All[index];
    }

    /// <summary>买得起就用选中的，买不起自动退回免费的轻装。</summary>
    public static LoadoutKit Resolve(MetaProfile profile)
    {
        var kit = Get(profile != null ? profile.SelectedKit : 0);
        if (profile != null && profile.Roubles < kit.Cost) return All[0];
        return kit;
    }
}