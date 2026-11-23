using System;
using System.Collections.Generic;

namespace Game.Data
{
    /// <summary>4 部位模型：头、胸、四肢（四肢合并）。</summary>
    public enum BodyZone
    {
        Head,
        Chest,
        Limbs
    }

    /// <summary>护甲/头盔参数。对应 schemas/armor.schema.json。</summary>
    [Serializable]
    public sealed class ArmorData
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public int ProtectionLevel { get; set; }
        public int Durability { get; set; }
        public List<BodyZone> Coverage { get; set; } = new List<BodyZone>();
    }
}
