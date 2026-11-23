using System;
using System.Collections.Generic;

namespace Game.Data
{
    public enum AttachmentSlot
    {
        Optic,
        Muzzle,
        Grip,
        Magazine,
        Stock,
        Laser
    }

    [Serializable]
    public sealed class RecoilCurve
    {
        /// <summary>第 n 发使用第 n 个值，越界取最后一个。</summary>
        public List<float> Vertical { get; set; } = new List<float>();

        public List<float> Horizontal { get; set; } = new List<float>();
    }

    /// <summary>武器战斗参数。对应 schemas/weapon.schema.json。</summary>
    [Serializable]
    public sealed class WeaponData
    {
        public string Id { get; set; }
        public string ItemId { get; set; }

        /// <summary>口径字符串（如 5.45x39），必须与所配弹药一致。</summary>
        public string Caliber { get; set; }

        /// <summary>默认弹药下的单发伤害，只用于 UI 预估与数值审查。</summary>
        public int BaseDamage { get; set; }

        public float DamageMultiplier { get; set; }
        public int Rpm { get; set; }
        public int MagazineCapacity { get; set; }
        public RecoilCurve RecoilCurve { get; set; } = new RecoilCurve();
        public List<AttachmentSlot> AttachmentSlots { get; set; } = new List<AttachmentSlot>();
        public List<string> AllowedAmmo { get; set; } = new List<string>();
    }
}
