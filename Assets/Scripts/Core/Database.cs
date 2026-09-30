using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §7.1:数据全部落成 .asset(Assets/Data/**),Inspector 里直接改数。
    // 建表代码不在运行时跑 —— 资产是唯一真源;要重新生成看 Assets/Scripts/Editor/DemoAssetBaker.cs。
    public partial class Database : ScriptableObject
    {
        public BalanceConfig bal;

        // 拾荒池
        // v0.28:FreshWater 与 DirtyWater 已删 —— 喝水系统整条移除,食物只剩 罐头(椰子/巧克力棒按你的裁定留着)
        public ItemSO Can, Coconut, Chocolate, Bait, MedKit, Flare, Material;
        // v0.19:信号枪 是枪(不会被消耗、不会坏),信号弹 退成它的弹药(开局不再散刷,枪自带 1 发)
        public ItemSO FlareGun;
        public ToolSO FishingRod, DiveGear, SimpleMask, Spear, Flint, Blanket, Umbrella, Net;   // v0.21:简易钓钩 已删
        public FlashlightSO Flashlight;
        // 制作物
        public ItemSO Medicine, CrudeFlare, Bottle;
        // v0.18:自制的火种只能用一次;拾荒带出的 打火石 是"上位"那件,可反复用
        public ItemSO CrudeFlint;
        // 岛上采集(v0.16:探索荒岛 20%×1;只作为 自制药品 的那一味素材)
        public ItemSO Herb;
        // 暗线道具(§5.3:全部 1 格、不可制作、不给数值收益)
        public ItemSO NameTag, Map, Key, Chest, Scrap, Newspaper, Photo;
        // v0.26:§5.3 彩蛋层拍板的 8 件。全部走同一张 LoreDropSO 表(1% / 整局一次),没有任何数值效果。
        public ItemSO Luggage, Claim, Badge, Boarding, Tally, Raft, TwoNames, MonkeyGift;
        // 建筑 key(v0.28:Collector / Purifier 已删 —— 那两件建筑只服务淡水线)
        public const string Wall = "wall";
        public const string SignalFire = "signalfire";
        public const string Campfire = "campfire";      // v0.47:篝火 从"每晚现烧"升格成建筑(灶),它是 信号火堆 的前置

        // v0.48:这两件的"坏"就是"熄了"—— 全项目只有这一个判据,别在 UI/结算里各写一份 key 比较
        public static bool IsFireStructure(string key) { return key == Campfire || key == SignalFire; }

        public List<ItemSO> scavengedItems = new List<ItemSO>();
        public List<RecipeSO> recipes = new List<RecipeSO>();
        public List<ActivitySO> activities = new List<ActivitySO>();
        public List<TeammateSO> teammates = new List<TeammateSO>();
        public List<EndingSO> endingTable = new List<EndingSO>();
        public List<LoreDropSO> loreDrops = new List<LoreDropSO>();
        public TeammateSO Pilot, Navigator, Mechanic;

        // 结局按 id 查表。Dictionary 不能被序列化,所以盘上存 List,加载时建索引。
        public Dictionary<EndingId, EndingSO> endings { get; private set; }

        void OnEnable()
        {
            endings = new Dictionary<EndingId, EndingSO>();
            for (int i = 0; i < endingTable.Count; i++)
                if (endingTable[i] != null) endings[endingTable[i].id] = endingTable[i];
        }

        // 修理价:拾荒独占件是写死常量(补录⑦),有配方的 = 制造量一半向上取整
        public void RepairCost(ItemSO item, out int materials, out int stamina)        {
            var t = item as ToolSO;
            if (t != null)
            {
                materials = t.repairMaterials;
                stamina = t.repairStamina;
                if (t.cabinMin > 0 || item == FishingRod) return;   // 拾荒独占件:直接用常量
            }
            materials = 1; stamina = 1;
            foreach (var r in recipes)
            {
                if (r.resultItem == item)
                {
                    materials = Mathf.CeilToInt(r.materials * 0.5f);
                    stamina = 1;
                    return;
                }
            }
        }

        // v0.46:建筑也进这张表了(围墙会坏)。同一条口径:**制造量一半向上取整 + 1 点体力**。
        //        它查的是 resultStructure,所以只有"有配方建出来的建筑"修得动 —— 现在就是 围墙。
        public void RepairCost(string structureKey, out int materials, out int stamina)
        {
            materials = 1; stamina = 1;
            foreach (var r in recipes)
            {
                if (r.isStructure && r.resultStructure == structureKey)
                {
                    materials = Mathf.CeilToInt(r.materials * 0.5f);
                    stamina = 1;
                    return;
                }
            }
        }
    }
}
