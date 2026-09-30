using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §3.1 夜晚事件;inEventBag / nightlyIndependentRoll / oncePerRun 决定它走哪条调度线
    public class GameEventSO : ScriptableObject
    {
        public string key;
        public string displayName;
        [TextArea] public string description;
        public bool inEventBag = true;
        public float nightlyIndependentRoll;  // v0.11:轮船 / 幽灵船 0.2
        public int priority;                  // 同轮顺位(越大越靠前)
        public int minNight;                  // 未到则顺延,不跳过
        public bool oncePerRun;               // 影怪
        public bool neverInBag;               // 插播:两点光 / 首次涨潮尸骨 / 结局
        public List<GameEventSO> variants = new List<GameEventSO>(); // 雨:内部 3:2:1
        public List<int> variantWeights = new List<int>();
        public List<GameChoiceSO> choices = new List<GameChoiceSO>();
    }
}
