using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §6 制作表
    public class RecipeSO : ScriptableObject
    {
        public string key;
        public string displayName;
        public int materials = 1;
        public int stamina = 1;
        public ItemSO resultItem;             // 产出到仓库;为空则表示产出建筑
        public string resultStructure;        // v0.28 起只剩 wall / signalfire(集水器与净水器随喝水系统删除)
        // v0.16:制造没有"前置"了 —— 不再有 需某技能当天生效 这类解锁条件。
        // 剩下的是"要被吃掉的一味素材"(篝火吃打火石、自制药品吃药草),它是价,不是门槛。
        public ItemSO requiresExtraItem;      // 额外消耗的一味素材
        public int requiresExtraItemCount = 1;
        // ⚠ v0.28:requiresStructure 字段已删除 —— 它唯一的使用者就是 净水器 的那道前置,
        //    净水器 没了之后"制造无前置"重新变成一条没有例外的规则。(§7.2 那条 IsNullOrEmpty 铁律照旧有效,
        //    它现在管的是 GameChoiceSO.requiresStructure —— 信号火堆 那条夜晚选项还在用。)
        // ⚠ v0.28:它曾随 净水器 一起被删,那时"制造无前置"重新变成没有例外的规则。
        //    **v0.46 用户改回来了:信号火堆 必须先有 篝火 才建得了** ⇒ 这条规则从此有一个例外。
        //    它是 **真前置**(解锁条件),不是"要被吃掉的一味素材"—— 两者别混(素材走 requiresExtraItem)。
        //    ⚠ string 字段在 Unity 载入后 **没填是 "" 不是 null**(§7.2 第一条铁律)⇒ 判它一定要 IsNullOrEmpty。
        public string requiresStructure;
        public ItemSO supersededBy;           // 上位替代:仓库里有它时这条不进制造面板(v0.16)
        public ItemSO requiresItemToShow;     // 没有这件东西时这条根本不进制造面板(v0.19:土制信号弹 需要 信号枪)
        public bool oncePerRun;               // 每局限做 1 次:土制信号弹(v0.12)、打火石(v0.17)
        public bool isStructure;
        public bool consumesFlint;            // 篝火:材料2 + 打火石(当晚在烧)
        [TextArea] public string desc;
    }
}
