using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // 开发文档 §7.1:物品 / 工具 / 配方 / 活动 / 事件 / 选项 / 队友 / 结局 / 图鉴 全是 ScriptableObject。
    // 一个类型一个文件,而且它必须是文件里的第一个类:Unity 的 MonoScript 只认主类,
    // 挤在同一个 .cs 里的其它类型会被烘焙成 m_Script: {fileID: 0},读回来字段全是 0。
    public class ItemSO : ScriptableObject
    {
        public string key;
        public string displayName;
        [TextArea] public string desc;
        public int slots = 1;

        [Header("机舱投放(§2.1 点位池)")]
        public int cabinMin;
        public int cabinMax = 1;
        public int spawnPoints = 4;      // 每件 3~6 个候选点位
        public bool highValue;           // 每区高价值预算 1~2 件

        [Header("性质")]
        public bool consumable;
        public bool tradeable = true;    // 友善的狐狸货单(§3.4)
        public bool lore;                // 暗线道具:永不进货单、不给数值收益(§5.3)
        public bool indestructible;      // v0.12:钓竿 / 手电筒

        [Header("吃 / 喝(§2.3)")]
        public int fullnessRestore;      // 罐头 20~30%
        public int staminaRestore;       // 巧克力棒 3
        public int hpRestore;            // 椰子 1 / 医疗箱
        public bool curesSickness;       // 医疗箱 / 自制药品
        // v0.28:hydrationRestore 与 makesSick 已删 —— 它们只属于 淡水/脏水,那两件物品随喝水系统一起移除了。
        //        (盘上旧资产里残留的这两个 key 会被 Unity 直接忽略,不用清。)
        public float medicineSuccessChance = 1f; // 自制药品 0.7
    }
}
