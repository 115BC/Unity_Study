using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §5.4 彩蛋层的掉落判定(暗线只买结局,不买强度)
    // v0.26:这一张表现在同时服务两类落点 ——
    //        重复来源(fish / explore:每次行动各掷一次)
    //        一次性来源(shadow / tide / matelost / monkeytrade / loop:触发那一刻(或次日清晨)只掷一次)
    //        两条硬规则:chance 一律 0.01;同一个 item 整局最多给一次(判定见 IslandPhase.LoreRoll 的 loreFound 闸)。
    public class LoreDropSO : ScriptableObject
    {
        public ItemSO item;                   // 残图 / 剪报 / 合影 / v0.26 的 8 件
        public string source;                 // fish / explore / shadow / tide / matelost / monkeytrade / loop
        public float chance = 0.01f;
        [TextArea] public string loreText;
    }
}
