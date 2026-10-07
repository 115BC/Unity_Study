using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §7.2:BalanceConfig 单例存数值
    public class BalanceConfig : ScriptableObject
    {
        [Header("拾荒(§2.1)")]
        // v0.62:`scavengerSeconds`(60)与 `carrySlots`(4)两个字段 **删除** —— 节奏改成 15 查看 + 30 拾取、
        //   背包改成 3 格,都是定死的设计而非旋钮,搬到 `ScavengingPhase` 的代码常量里
        //   (LookSeconds / PickSeconds / CarrySlots)。留在 SO 里 = 留一份"资产还是旧数"的第二真源。
        //   已烘出来的 BalanceConfig.asset 里那两行会成为孤儿数据,Unity 下次存场景/资产时自己丢掉。
        public int cabinTotalSlotsMin = 31, cabinTotalSlotsMax = 42;
        public float teammateSpeedPenalty = 0.3f;
        public int hazardSecondsCost = 2;
        public float exitZoneRadius = 2.2f;
        public float exitZoneRadiusCarrying = 2.53f;   // 扛队友时放大 15%

        [Header("荒岛数值(§2.3)")]
        public int staminaMax = 3;    // v0.28:5 → 3(用户定;潜水3/充电3 因此等于整天,其余行动价一律没下调)
        public int hpMax = 5;
        public int survivalDaysToEndB = 50;
        public float foodPerCanFullness = 0.25f;       // 1 份罐头回 20~30%
        // v0.46:`mealFullnessGain` 字段 **删除**(§11-66 就此结案)。用户口径:"椰子是回饱,吃队友也回饱,
        //        但是吃队友会导致三天吃不了东西" ⇒ 食用 不再有一个"回饱比例",它一次回满(与 椰子 同类),
        //        代价改挂在天数上(`PlayerStats.foodLockDays`,常量 `IslandPhase.MealFoodLockDays = 3`)。
        public float playerFullnessDecayPerDay = 0.22f;
        // v0.28:playerHydrationDecayPerDay 已删 —— 喝水系统整条移除,饱食是唯一的资源型衰减数值
        public int chocolateStamina = 3;         // 白天唯一能加精力的东西(v0.15)

        [Header("探索刷新骰(v0.13)")]
        public float exploreRefreshBase = 0.30f;
        public float exploreRefreshRamp = 0.05f;
        public float exploreRefreshCap = 1.0f;
        public float exploreMaterialChance = 0.30f;
        public int exploreMaterialRolls = 6;           // 补录⑧:原 5
        public float exploreFoodChance = 0.60f;
        public int exploreFoodRolls = 6;
        // v0.28:探索不再产椰子(exploreCoconut* 两个字段已删)—— 椰子的唯一来源是夜晚事件「椰树」
        public float exploreHerbChance = 0.20f;        // v0.16:药草 一次判定
        public int exploreHerbRolls = 1;
        public float campAccidentChance = 0.25f;       // 无队友看家

        [Header("钓鱼 / 潜水(§2.3)")]
        public float fishMissBase = 0.20f;
        public float fishMissPerDay = 0.03f;
        public float fishMissCap = 0.80f;              // v0.13 转正
        public float baitCatchFloor = 0.80f;           // v0.18:挂鱼饵时上鱼概率的下限(实际更高就按实际)
        public float baitConsumeChance = 0.80f;        // v0.18:上鱼后 80% 消耗掉这份鱼饵(原 60%)
        public float weakToolChanceMultiplier = 0.5f;  // "减半" = 产出概率
        public float diveFoodChance = 0.60f;
        public int diveFoodRolls = 6;
        public float diveBaitChance = 0.40f;
        public int diveBaitRolls = 3;
        public float diveInjuryChance = 0.08f;
        public float keyChanceFish = 0.05f;            // v0.13:不查 hasMap
        public float keyChanceDive = 0.15f;

        [Header("救援 / 夜晚(§3.1)")]
        public float rescueRollPerNight = 0.20f;
        public float calmNightChance = 0.20f;
        // v0.24:骸骨(独立事件)—— 整局一次,每晚掷一次直到它出现;出现过之后 幽灵船 的那条独立 20% 抬到 30%
        // v0.26:彩蛋层统一定价 1% —— 骸骨从 20% 降到 1%(每晚各掷,整局仍只出一次)。
        //        这一条一改,铭牌(=结局F 的判定物)与 幽灵船 30% 档的可达率跟着一起掉,见 §10 风险32。
        public float bonesNightlyChance = 0.01f;
        public float ghostShipBonesChance = 0.30f;
        public float flashlightRescueChance = 0.50f;
        // v0.44:手电筒的 **电量存储格数**(每用一次夜晚功能扣一格;1 精力充一格;开局随机 0~这个数)。
        //        ⚠ 新增 BalanceConfig 字段必须同时写进 Assets/Data/BalanceConfig.asset ——
        //          SO 里缺 key 的数值字段会静默读成 0(§7.2 第一条),那样电量上限就是 0、手电筒永远用不了。
        public int flashlightChargeMax = 2;
        public float crudeFlareSuccessChance = 0.60f;
        public float coconutHitChance = 0.30f;         // v0.28:夜晚「椰树」事件 —— 走过去拿椰子里 30% 被砸 -1 生命
        public int shadowNightMin = 20, shadowNightMax = 25;
        public float bloodMoonDiveSuccess = 0.60f;
        public float bloodMoonMapChance = 0.30f;

        [Header("海鸥(§4.7)")]
        public float gullRefreshBase = 0.20f;
        public float gullRefreshRamp = 0.01f;
        public int gullEndingCount = 4;

        [Header("状态 / 队友(§2.4)")]
        public float medicineSuccessChance = 0.70f;
        // v0.28:dirtyWaterLockDays 已删(脏水与 3 天禁水随喝水系统一起移除)
        public float teammateSkillChance = 0.40f;
        public int talkMoodFirstTalk = 2;              // v0.18:当天第一次谈话回升 2 层
        public int talkMoodLater = 1;                  // v0.18:第二次起每次 1 层(每天不限次)
        public int teammateMoodForcedDropDays = 3;
        public float teammateMoodDailyDropChance = 0.35f;
        public float teammateSickSelfHeal = 0.40f;
        public int nextDayStaminaPenaltySick = 1;      // 生病:体力上限 -1
    }
}
