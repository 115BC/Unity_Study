using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // 工具:累积损坏概率 + 修理价(§2.3 / §11-38)
    public class ToolSO : ItemSO
    {
        public float breakChanceStart = 0.2f; // 渔网 0.3
        public int repairMaterials;           // §11-38:拾荒独占件是写死常量
        public int repairStamina = 1;
        public bool repairFreeOfMaterials;    // 打火石:0 材料
    }
}
