using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §2.4 队友:三家技能概率 v0.12 拉平 40%
    public class TeammateSO : ScriptableObject
    {
        public string key;
        public string displayName;
        public string profession;             // 飞行员 / 领航员 / 机械师
        public float skillChance = 0.4f;      // v0.12:三家拉平 40%
        [TextArea] public string skillDesc;
        public Color color = Color.white;
    }
}
