using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §3.1 夜晚选项:breakChanceNight / consumesItem / drainsFlashlight / recoversDamaged
    // 四个字段各管一件事,不许合并(§11-36)
    public class GameChoiceSO : ScriptableObject
    {
        public string key;
        public string label;
        [TextArea] public string resultHint;  // 按钮上把代价写清楚(全局口径:不做确认框)
        public ItemSO requiresItem;
        // v0.50(用户:"8.海鸥也可以使用鱼饵触发"):有些选项是 **"A 或 B"** 的应对 —— 这个字段就是那第二个。
        //   ⚠ 它是 **引用型** 字段:盘上没这个 key 的资产读回来是 **null**(不是 `""` 那个坑,§7.2 第一条管的是 string),
        //     所以只给需要它的那一份资产写值就安全,其余 60 条选项一行都不用动。
        public ItemSO requiresItemAlt;
        public bool requiresFire;
        public string requiresStructure;
        public bool requiresTeammate;
        public bool requiresNoTeammate;
        public int costStamina;
        public int nextDayStaminaPenalty;
        public float breakChanceNight;        // v0.10:1.0 影怪/毯子/武器a;0.8 暴雨;0.5 小雨;0 采集与驱赶
        public bool consumesItem;             // 一次性品 / 信号弹赶狐狸
        public bool drainsFlashlight;         // v0.12
        public bool recoversDamaged;          // v0.13 补录③:被抢那件以破损态归还
        public int rewardFood;                // 狐狸:武器驱赶 +1 罐头
        public bool applySick;
        public bool alwaysAvailable;          // "什么都不做"
    }
}
