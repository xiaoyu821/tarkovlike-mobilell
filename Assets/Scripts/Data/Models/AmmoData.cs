using System;

namespace Game.Data
{
    /// <summary>弹药参数。对应 schemas/ammo.schema.json。</summary>
    [Serializable]
    public sealed class AmmoData
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public string Caliber { get; set; }
        public int Damage { get; set; }
        public int Penetration { get; set; }
        public int MuzzleVelocity { get; set; }

        /// <summary>弹丸数：普通弹为 1，霰弹大于 1。</summary>
        public int Pellets { get; set; } = 1;
    }
}
