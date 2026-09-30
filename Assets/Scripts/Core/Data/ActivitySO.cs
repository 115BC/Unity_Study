using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §4.1 白天行动
    public class ActivitySO : ScriptableObject
    {
        public string key;
        public string displayName;
        public int cost;
        public bool costAllRemaining;         // v0.12:探索荒岛 吃掉当天全部剩余体力
        public bool oncePerDay;
        public bool requiresDailyRoll;        // v0.13:探索 需 ExploreState.availableToday
        public List<ItemSO> requiresAnyOf = new List<ItemSO>();
        public string requiresStructure;
        public string location;               // 地点卡片:沙滩 / 树林 / 礁石 / 营地 / 浅滩
        [TextArea] public string desc;
    }
}
