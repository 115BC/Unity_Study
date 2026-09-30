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
        public bool requiresFire;
        public string requiresStructure;
        public bool requiresTeammate;
        public bool requiresNoTeammate;
        public int costStamina;
        public int nextDayStaminaPenalty;
        public float breakChanceNight;        // v0.10:1.0 影怪/毯子/武器a;0.8 暴雨;0.5 小雨;0 采集与驱赶
        public bool consumesItem;             // 一次性品 / 信号弹赶猴子
        public bool drainsFlashlight;         // v0.12
        public bool recoversDamaged;          // v0.13 补录③:被抢那件以破损态归还
        public int rewardFood;                // 猴子:武器驱赶 +1 罐头
        public bool applySick;
        public bool alwaysAvailable;          // "什么都不做"
    }
}
