using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SixtySLike
{
    // 批处理自检:-executeMethod SixtySLike.DemoChecks.Run
    // 验的是开发文档里"写死"的数与规则,不是画面。
    public static class DemoChecks
    {
        static readonly List<string> fails = new List<string>();
        static Database DB;
        static BalanceConfig B;

        static void Check(bool ok, string what) { if (!ok) fails.Add(what); }
        static void Eq(object a, object b, string what) { if (!object.Equals(a, b)) fails.Add(what + " — 实际 " + a + ",期望 " + b); }
        static RecipeSO R(string k) { foreach (var r in DB.recipes) if (r.key == k) return r; return null; }
        static ActivitySO A(string k) { foreach (var a in DB.activities) if (a != null && a.key == k) return a; return null; }
        static GameChoiceSO C(string ev, string k)
        {
            var e = ev == "monkey" ? DB.MonkeyNaughty : null;
            foreach (var c in e.choices) if (c.key == k) return c;
            return null;
        }
        static GameChoiceSO Ch(GameEventSO e, string k)
        {
            if (e == null) return null;
            foreach (var c in e.choices) if (c.key == k) return c;
            return null;
        }
        // 缺选项时记一条失败而不是抛异常,免得一条崩掉整个自检
        static float BC(GameEventSO e, string k)
        {
            var c = Ch(e, k);
            if (c == null) { fails.Add(e.displayName + " 缺少选项 " + k); return -1f; }
            return c.breakChanceNight;
        }

        [MenuItem("Tools/60slike/运行自检")]
        public static void Run()
        {
            fails.Clear();
            // 数据现在是资产(唯一真源),自检读的就是 Inspector 里那份,不再是代码现建的临时对象
            DB = AssetDatabase.LoadAssetAtPath<Database>(DemoAssetBaker.DatabaseAsset);
            if (DB == null)
            {
                Debug.LogError("[60slike] 找不到 " + DemoAssetBaker.DatabaseAsset +
                               " —— 先跑菜单 Tools/60slike/① 生成数据资产");
                return;
            }
            B = DB.bal;
            Bag(); Monkey(); Breakage(); Prices(); Balance(); ExploreDice(); MaterialBudget(); FishMiss(); Names(); Endings(); Inv(); MateMeal(); V045(); V046(); V047();

            var sb = new StringBuilder("=== 60slike demo 自检 ===\n");
            if (fails.Count == 0) sb.Append("SELFCHECK PASS:全部通过\n");
            else
            {
                sb.Append("SELFCHECK FAILED:").Append(fails.Count).Append(" 条\n");
                foreach (var f in fails) sb.Append(" - ").Append(f).Append("\n");
            }
            Debug.Log(sb.ToString());
            // ⚠ v0.32:红色那条自己带上清单。以前只写 "SELFCHECK FAILED 5",
            //   名字在上一条 Debug.Log 里 —— 报障时只贴红色那条就会丢掉全部线索。
            if (fails.Count > 0)
            {
                var d = new StringBuilder("SELFCHECK FAILED " + fails.Count + " 条 ——\n");
                foreach (var f in fails) d.Append(" - ").Append(f).Append('\n');
                Debug.LogError(d.ToString());
            }
        }

        static void Bag()
        {
            Eq(DB.bagEvents.Count, 15, "轮空池常驻项应为 15(v0.28:椰树 进池,原 14)");
            var s = new RunState(); s.nightCount = 6;
            var seen = new HashSet<string>();
            for (int i = 0; i < 15; i++) seen.Add(EventBagSystem.Draw(s, DB.bagEvents).key);
            Eq(seen.Count, 15, "一轮 15 次应弹满 15 个不同事件(弹过的移出本轮)");
            Eq(s.bag.roundIndex, 1, "第一轮后 roundIndex=1");

            var s0 = new RunState(); s0.nightCount = 0;
            var seen0 = new HashSet<string>();
            for (int i = 0; i < 14; i++) seen0.Add(EventBagSystem.Draw(s0, DB.bagEvents).key);
            Check(!seen0.Contains("bloodmoon"), "minNight 未到 → 血月顺延不入本轮");

            var cnt = new Dictionary<string, int>();
            var rng = new System.Random(12345);
            for (int i = 0; i < 6000; i++)
            {
                var v = EventBagSystem.PickVariant(DB.Rain, rng);
                int c; cnt.TryGetValue(v.key, out c); cnt[v.key] = c + 1;
            }
            Check(Mathf.Abs(cnt["rain_light"] / 6000f - 0.5f) < 0.03f, "小雨 ≈50%");
            Check(Mathf.Abs(cnt["rain_heavy"] / 6000f - 0.333f) < 0.03f, "暴雨 ≈33%");
            Check(Mathf.Abs(cnt["rain_storm"] / 6000f - 0.167f) < 0.03f, "雷雨 ≈17%");

            Eq(DB.Ship.nightlyIndependentRoll, 0.2f, "轮船独立掷 20%");
            Eq(DB.Ghost.nightlyIndependentRoll, 0.2f, "幽灵船独立掷 20%");
            Check(!DB.Ship.inEventBag && !DB.Ghost.inEventBag, "轮船/幽灵船不入轮");
            Check(DB.Ghost.oncePerRun, "幽灵船 oncePerRun");
            Check(DB.Shadow.oncePerRun && !DB.Shadow.inEventBag, "影怪 oncePerRun 且不入轮");
            Eq(DB.Shadow.minNight, 0, "影怪靠强制窗口触发,不用 minNight");
        }

        // 补录②③④⑥:赶猴子三条,breakChanceNight 全 0
        static void Monkey()
        {
            var spear = C("monkey", "spear"); var flare = C("monkey", "flare"); var fire = C("monkey", "fire");
            Check(spear != null && flare != null && fire != null && C("monkey", "grab") != null && C("monkey", "refuse") != null,
                  "猴子应有 鱼叉/信号弹/篝火/空手/不给它");
            Eq(spear.breakChanceNight, 0f, "鱼叉驱赶 breakChanceNight=0");
            Eq(flare.breakChanceNight, 0f, "信号弹驱赶 breakChanceNight=0");
            Eq(fire.breakChanceNight, 0f, "篝火驱赶 breakChanceNight=0(打火石不坏)");
            Eq(spear.rewardFood, 1, "鱼叉驱赶保留 +1 份罐头");
            Check(flare.consumesItem, "信号弹驱赶消耗 1 发");
            Check(fire.recoversDamaged, "篝火驱赶 = 破损回收");
            Check(fire.requiresFire, "篝火驱赶需当晚火在烧");
            Eq(spear.requiresItem, DB.Spear, "鱼叉驱赶 requires 鱼叉");
            Eq(flare.requiresItem, DB.FlareGun, "信号枪驱赶 requires 信号枪(v0.19:消耗的是弹,不是枪)");
            var banned = new ItemSO[] { DB.FishingRod, DB.Net, DB.DiveGear, DB.Blanket, DB.Umbrella, DB.Flashlight };
            foreach (var c in DB.MonkeyNaughty.choices)
                foreach (var b in banned) Check(c.requiresItem != b, "驱赶选项不该出现 " + b.displayName);
            Check(!DB.Flint.indestructible, "打火石仍会因生火而坏,只是不因驱赶而坏");
        }

        // §2.3 损坏三档 / v0.12 手电筒电量 / §12.2 血月代价口径。这一组是回归网,别删。
        static void Breakage()
        {
            // ① 打火石 身上不许挂 breakChanceNight(§5.1 补录③:"别把它改回去")
            foreach (var e in new[] { DB.ColdNight, DB.MonkeyNaughty, DB.Shadow, DB.BloodMoon, DB.SmallShadow })
                foreach (var c in e.choices)
                    Check(!(c.requiresItem == DB.Flint && c.breakChanceNight > 0f),
                          e.displayName + "/" + c.key + ":打火石 不许挂 breakChanceNight(它走累积损坏)");

            // ② 挡灾型 = 必坏(§2.3:默认 1.0,雨伞按雨量分档)
            Eq(BC(DB.Shadow, "spear"), 1.0f, "影怪 鱼叉硬挡 = 必坏");
            Eq(BC(DB.BloodMoon, "blanket"), 1.0f, "血月 裹毯子 = 必损");
            Eq(BC(DB.ColdNight, "blanket"), 1.0f, "低温夜 裹毯子 = 必损");
            Eq(BC(DB.WeaponA, "umbrella"), 1.0f, "武器a 雨伞 = 必损(全游戏唯一解)");
            float[] rain = { 0.5f, 0.8f, 1.0f };
            Check(DB.Rain.variants.Count == 3, "雨应有 小/暴/雷 三档");
            for (int i = 0; i < DB.Rain.variants.Count && i < rain.Length; i++)
                Eq(BC(DB.Rain.variants[i], "umbrella"), rain[i], "雨伞挡" + DB.Rain.variants[i].displayName);
            Eq(Ch(DB.Rain.variants[2], "umbrella").nextDayStaminaPenalty, 1, "雷雨即使挡了也要 次日体力 -1");

            // ③ v0.12:手电筒任何夜晚用法都"用完没电",不是免费、也不是损坏
            foreach (var e in new[] { DB.SearchPlane, DB.Ship, DB.Ghost, DB.Shadow, DB.Snake, DB.FakeMate })
                foreach (var c in e.choices)
                    if (c.key == "flashlight")
                    {
                        Check(c.drainsFlashlight, e.displayName + " 的手电筒选项必须 drainsFlashlight");
                        Check(c.breakChanceNight <= 0f, e.displayName + " 的手电筒不该走损坏(它 indestructible)");
                    }
            Check(DB.Flashlight.indestructible && DB.FishingRod.indestructible, "手电筒与钓竿 indestructible");

            // ④ 血月的代价是"次日体力上限 -1",走 nextDayCapPenalty;
            //    再挂 nextDayStaminaPenalty 就会与 BloodMoonDive 里的结算双扣成 -2
            Eq(Ch(DB.BloodMoon, "dive").nextDayStaminaPenalty, 0, "血月下水不许挂 flat nextDayStaminaPenalty(双扣)");
            Eq(Ch(DB.SmallShadow, "watch").nextDayStaminaPenalty, 2, "守夜 = 次日体力 -2");

            // ⑤ MarkBroken 的闸:只有"会坏的工具"进得了破损态
            var inv = new Inventory();
            inv.Add(DB.FishingRod, 1); inv.MarkBroken(DB.FishingRod);
            Check(!inv.HasBroken(DB.FishingRod) && inv.Has(DB.FishingRod), "钓竿永不损坏(猴子抢回也完好)");
            inv.Add(DB.Can, 1); inv.MarkBroken(DB.Can);
            Check(!inv.HasBroken(DB.Can) && inv.Has(DB.Can), "消耗品没有'破损'这回事,直接完好归位");
            inv.Add(DB.Spear, 1); inv.MarkBroken(DB.Spear);
            Check(inv.HasBroken(DB.Spear) && !inv.Has(DB.Spear), "鱼叉应能进破损态");
        }

        static void Prices()
        {
            int m, st;
            DB.RepairCost(DB.Spear, out m, out st); Check(m == 2 && st == 2, "鱼叉修理 材料2+2体力,实际 " + m + "+" + st);
            DB.RepairCost(DB.DiveGear, out m, out st); Check(m == 4 && st == 3, "潜水装置修理 材料4+3体力,实际 " + m + "+" + st);
            DB.RepairCost(DB.Blanket, out m, out st); Check(m == 1 && st == 1, "毯子修理 材料1+1体力,实际 " + m + "+" + st);
            DB.RepairCost(DB.Flint, out m, out st);
            Check(m == 1 && st == 1, "打火石修理 材料1+1体力(v0.48 收回 v0.5 的 0 材料) —— 实际 " + m + "+" + st);
            DB.RepairCost(DB.Umbrella, out m, out st); Check(m == 1, "雨伞修理 材料1(制造量一半)");
            DB.RepairCost(DB.Net, out m, out st); Check(m == 2, "渔网修理 材料2(制造量一半)");

            // v0.28:净水器 / 集水器 两件建筑随喝水系统删除 ⇒ 配方表里不再有"前置"这回事
            Check(R("purifier") == null && R("collector") == null, "v0.28:净水器与集水器配方已整行删除");
            Check(A("drink") == null, "v0.28:喝水行动已删除");
            Check(typeof(Database).GetField("FreshWater") == null && DB.Coconut != null,
                  "v0.28:Database.FreshWater 字段已删,椰子留着(来源改成夜晚事件)");
            Check(DB.bagEvents.Contains(DB.PalmTree) && DB.bagEvents.Count == 15,
                "v0.28:椰树 进常规池 ⇒ 常驻项 14 → 15,实际 " + DB.bagEvents.Count);
            Eq(Ch(DB.PalmTree, "take") != null && !Ch(DB.PalmTree, "take").alwaysAvailable, true,
                "椰树:『走过去看看』不是 alwaysAvailable(它是那条要点的选项)");
            Eq(Ch(DB.PalmTree, "ignore").alwaysAvailable, true, "椰树:『不去看』= 尝试睡去那条");
            Eq(B.coconutHitChance, 0.30f, "被椰子砸 30%");
            Eq(B.staminaMax, 3, "v0.28:精力上限 3(原 5)");
            Eq(R("flint").materials, 1, "土制打火石 材料1(v0.17)");
            Eq(R("flint").resultItem, DB.CrudeFlint, "土制打火石配方产出 土制打火石(一次性的那件)");
            Check(!R("flint").oncePerRun, "土制打火石可以反复做(v0.18:限的是'用一次',不是'做一次')");
            Eq(R("flint").supersededBy, DB.Flint, "打火石 是 土制打火石 的上位替代");
            Check(DB.CrudeFlint != null && DB.CrudeFlint.consumable && !DB.CrudeFlint.tradeable,
                "土制打火石 = 消耗品、不进猴子货单");
            Eq(R("crudeflare").materials, 3, "土制信号弹 材料3(补录⑤)");
            Check(R("crudeflare").oncePerRun, "土制信号弹每局限 1 发");
            Eq(R("net").materials, 4, "渔网 材料4");
            // v0.47:篝火 升格成建筑(灶)⇒ 原来那三样(材料2 / 体力1 / 当晚烧一次)一起作废
            Eq(R("campfire").materials, 3, "篝火(垒灶) 材料3");
            Eq(R("campfire").stamina, 2, "篝火(垒灶) 2 体力");
            Check(R("campfire").isStructure && R("campfire").resultStructure == Database.Campfire,
                  "篝火 现在是建筑(灶)—— 它不再是「当晚烧一次」的那条配方");
            // ⚠ 这里原来有一条 `Check(!consumesFlint, "建造不吃火种")` —— 那是 v0.48 早期的读法,
            //   被你追答的"垒灶当天默认点火"推翻了(建造那一手就点着 ⇒ 吃一块火种)。资产侧已改对,断言漏改,
            //   自检报的就是这一条。**现在这条只由 V047() 统一管**(火模型的断言集中在那里,不两头维护)。
            Eq(R("wall").materials, 5, "围墙 材料5");
            Eq(R("signalfire").materials, 6, "信号火堆 材料6");

            // v0.16:制造无前置 / 高级平替不上面板 / 药草
            Eq(R("medicine").requiresExtraItem, DB.Herb, "自制药品那一味 = 药草");
            Eq(R("medicine").requiresExtraItemCount, 1, "自制药品需 药草1");
            Eq(R("rod").materials, 4, "钓鱼竿 材料4(v0.21,用户定的价)");
            Eq(R("rod").stamina, 2, "钓鱼竿 2 体力");
            Eq(R("rod").resultItem, DB.FishingRod, "钓鱼竿配方产出 钓鱼竿(简易钓钩 已整件删除)");
            Check(DB.FishingRod.indestructible, "钓鱼竿 永不损坏(v0.21 重申)");
            Eq(A("talk").cost, 2, "谈话 2 精力(v0.21,原 1)");
            Eq(A("fish").cost, 1, "钓鱼仍 1 体力");
            Eq(R("mask").supersededBy, DB.DiveGear, "简易浮镜的高级平替 = 潜水装置");
            Check(R("crudeflare").supersededBy == null && R("medicine").supersededBy == null,
                "例外:土制信号弹 与 自制药品 不挂上位替代(v0.16);土制弹另有 v0.19 的『没枪就不出现』那道闸");
            Eq(R("crudeflare").requiresItemToShow, DB.FlareGun,
                "v0.19(§11-51 结案):没有 信号枪 就没有 土制信号弹 的配方");
            Check(DB.Herb != null && !DB.Herb.tradeable && DB.Herb.cabinMin == 0,
                "药草:不进机舱点位池、不进猴子货单");
            Eq(B.exploreHerbChance, 0.20f, "探索药草 20%");
            Eq(B.exploreHerbRolls, 1, "药草 = 一次判定");
            Eq(B.exploreMaterialRolls, 6, "探索材料判定 30%×6(补录⑧)");
            Eq(B.exploreMaterialChance, 0.30f, "探索材料概率 30%");
        }

        static void Balance()
        {
            Eq(B.exploreRefreshBase, 0.30f, "刷新起始 30%");
            Eq(B.exploreRefreshRamp, 0.05f, "未刷新 +5%");
            Eq(B.exploreRefreshCap, 1.0f, "刷新上限 100%");
            Eq(B.fishMissCap, 0.80f, "空军率封顶 80%");
            Eq(B.rescueRollPerNight, 0.20f, "救援独立掷 20%");
            Eq(B.calmNightChance, 0.20f, "今夜无事 20%");
            Eq(B.gullEndingCount, 4, "结局I = 在场 4 只");
            // v0.28:dirtyWaterLockDays 已删(脏水与 3 天禁水随喝水系统一起移除)
            Eq(B.medicineSuccessChance, 0.70f, "自制药品 70%");
            Eq(B.crudeFlareSuccessChance, 0.60f, "土制弹 60% 生效");
            Eq(B.shadowNightMin, 20, "影怪窗口起");
            Eq(B.shadowNightMax, 25, "影怪窗口止");
            Eq(B.survivalDaysToEndB, 50, "50 天 → 结局B");
            Eq(B.teammateSkillChance, 0.40f, "三家技能 40%");
            Eq(B.talkMoodFirstTalk, 2, "当天第一次谈话回升 2 层(v0.18)");            Eq(B.talkMoodLater, 1, "第二次起每次 1 层(v0.18)");
            Eq(B.baitCatchFloor, 0.80f, "挂鱼饵 → 上鱼概率至少 80%(v0.18)");
            Eq(B.baitConsumeChance, 0.80f, "上鱼后 80% 消耗掉鱼饵(v0.18)");

            // v0.24:骸骨独立事件 + 抢救露天物资的次日惩罚
            Check(DB.Bones != null, "骸骨事件已接到 Database.DB.Bones");
            Check(DB.Bones.oncePerRun && !DB.Bones.inEventBag, "骸骨:整局一次、不入轮空池");
            Check(Ch(DB.Bones, "look") != null, "骸骨:有『走过去看』这条");
            Check(Ch(DB.Bones, "ignore") != null && Ch(DB.Bones, "ignore").alwaysAvailable,
                "骸骨:『不理会』是 alwaysAvailable(= 尝试睡去那条)");
            Check(Ch(DB.Tide, "bones") == null, "v0.24:涨潮不再有尸骨选项(独立成骸骨事件)");
            Eq(Ch(DB.Tide, "save").nextDayStaminaPenalty, 2, "抢救露天物资 = 次日体力上限 -2(不丢道具)");
            Eq(B.bonesNightlyChance, 0.01f, "骸骨 每晚 1%(v0.26:与其余彩蛋同价)");
            Eq(B.ghostShipBonesChance, 0.30f, "骸骨出现过之后 幽灵船 30%");
            LoreEggs();
            AssetWiring();
            Check(DB.FishingRod.indestructible && DB.Flashlight.indestructible, "钓竿/手电筒永不损坏");
            // v0.39(用户):白天 **有精力消耗的只有 潜水(3)/钓鱼(1)/丢漂流瓶(1)** ——
            //   其余白天可使用的物品一律 0 精力,悬浮时也就不该标 ⚡。
            // v0.44:手摇充电 回到 1 精力(充 1 格电),它不在这张"0 精力"名单里了。
            Eq(A("dive").cost, 3, "v0.39:潜水 3 精力");
            Eq(A("throwbottle").cost, 1, "v0.39:丢漂流瓶 1 精力");
            Eq(A("charge").cost, 1, "v0.44:手摇充电 1 精力 = 1 格电");
            foreach (var free in new[] { "coconut", "heal", "eat", "feed", "craft", "repair", "endday" })
                Eq(A(free).cost, 0, "v0.39:白天行动 " + free + " 应当 0 精力");
            // v0.44:电量从两态改成 2 格存储。上限读的是 BalanceConfig 字段 ——
            //        资产里缺这一行会静默变成 0(§7.2 第一条),那样手电筒永远用不了,所以必须点名验。
            Eq(B.flashlightChargeMax, 2, "v0.44:手电筒电量上限(资产缺 flashlightChargeMax 会读成 0)");
            var fs = new RunState();
            Check(fs.flashlightCharge >= 0 && fs.flashlightCharge <= B.flashlightChargeMax,
                  "v0.44:开局电量应当随机落在 0~2,实际 " + fs.flashlightCharge);
            Check(DB.Map.lore && DB.Key.lore && DB.Chest.lore, "图/钥匙/箱 = 暗线道具");
            Check(!DB.Map.tradeable && !DB.Key.tradeable && !DB.Chest.tradeable, "暗线道具硬排除出猴子货单");
            Eq(DB.Chest.slots, 2, "藏宝箱 2 格");
            Eq(DB.DiveGear.slots, 4, "潜水装置 4 格");
            Eq(DB.bal.carrySlots, 4, "携带条 4 格");

            // v0.19:开局投放收紧 + 信号枪
            Eq(DB.FlareGun.cabinMin, 1, "信号枪 全舱 1 把");
            Eq(DB.FlareGun.cabinMax, 1, "信号枪 全舱 1 把");
            Eq(DB.Flare.cabinMax, 0, "信号弹 不再散刷(枪自带 1 发)");
            Eq(DB.MedKit.cabinMax, 1, "医疗箱 只刷 1");
            Eq(DB.Chocolate.cabinMax, 1, "巧克力棒 只刷 1");
            Eq(DB.Bottle.cabinMax, 1, "漂流瓶 只刷 1");
            Eq(DB.Coconut.cabinMax, 0, "椰子 开局不刷(只在 探索荒岛)");
            Eq(DB.Material.cabinMax, 0, "材料 开局不刷(只在 探索荒岛)");
            Eq(DB.Net.slots, 2, "渔网 占 2 格(v0.19,原 3)");
            Eq(DB.Net.cabinMax, 1, "渔网 只刷 1 件");
        }

        // v0.26 §5.3 彩蛋层:8 件新的接好了,全表统一定价 1%,source 拼写与代码里的 LoreRoll 参数一致
        static void LoreEggs()
        {
            Check(DB.Luggage != null && DB.Claim != null && DB.Badge != null && DB.Boarding != null
                  && DB.Tally != null && DB.Raft != null && DB.TwoNames != null && DB.MonkeyGift != null,
                  "v0.26:8 件新彩蛋里有没接到 Database 的");

            int rows = 0;
            foreach (var src in new[] { "fish", "explore", "shadow", "tide", "matelost", "monkeytrade", "loop" })
            {
                int n = 0;
                foreach (var l in DB.loreDrops) if (l != null && l.source == src) n++;
                Check(n > 0, "彩蛋来源 '" + src + "' 在表里一条都没有(source 拼写必须和 LoreRoll(\"" + src + "\") 一致)");
            }

            foreach (var l in DB.loreDrops)
            {
                if (l == null || l.item == null) { fails.Add("彩蛋表有一项是空引用(资产没接上)"); continue; }
                rows++;
                Eq(l.chance, 0.01f, "彩蛋统一 1%:" + l.item.displayName);
                Check(l.item.lore && !l.item.tradeable && l.item.slots == 1 && !l.item.consumable,
                      "彩蛋件必须 1 格 / lore / 不可交易 / 非消耗:" + l.item.displayName);
                Check(!string.IsNullOrEmpty(l.loreText), "彩蛋缺文案:" + l.item.displayName);
            }
            Eq(rows, 12, "彩蛋表 12 条(旧 4 条 + v0.26 新 8 条)");
        }

        // ⚠ v0.26 血的教训(§7.2 第二条铁律):离线手改 .asset 的 YAML 只要格式偏离 Unity 自己的写法
        //   (留了空白行、丢了文件末尾换行),Unity 就 不报错、静默丢掉那一段之后的全部字段 ——
        //   这次是 Database.asset 里 Pilot/拾荒池/配方表全空,开局 SpawnContents 直接 NRE。
        //   所以"排在资产文件后半段的字段"必须逐条点名验一遍,而不是只验我这次动过的那些。
        static void AssetWiring()
        {
            Check(DB.Pilot != null && DB.Navigator != null && DB.Mechanic != null,
                  "队友三件没接上(Pilot/Navigator/Mechanic)—— Database.asset 的 YAML 可能被截断");
            Eq(DB.scavengedItems.Count, 17, "拾荒池 17 件(v0.28:淡水删除后从 18 降下来)");
            Eq(DB.recipes.Count, 11, "配方 11 行(v0.28:集水器与净水器删掉,从 13 降下来)");
            Eq(DB.activities.Count, 13, "白天行动 13 条(v0.28:喝水删掉,从 14 降下来)");
            Eq(DB.endingTable.Count, 7, "结局表 7 个");
            Check(DB.Tide != null && DB.Shadow != null && DB.Bones != null && DB.MonkeyFriendly != null,
                  "具名事件引用没接全(Tide/Shadow/Bones/MonkeyFriendly)");
            foreach (var r in DB.recipes)
                Check(r != null && !string.IsNullOrEmpty(r.key), "配方表里有一项是空的或没 key");
            foreach (var a in DB.activities)
                Check(a != null && !string.IsNullOrEmpty(a.key), "行动表里有一项是空的或没 key");

            // v0.27 两条改动的落点
            var grab = Ch(DB.MonkeyNaughty, "grab");
            var refuse = Ch(DB.MonkeyNaughty, "refuse");
            Check(grab != null && !grab.alwaysAvailable, "v0.27:空手抢夺 不再挂 alwaysAvailable(它只能靠点那只猴子触发)");
            Check(refuse != null && refuse.alwaysAvailable, "v0.27:不给它 才是这条线上的『什么都不做』");
            Check(DB.bagEvents.Contains(DB.SearchPlane), "v0.27:搜寻飞机 必须在常规轮空池里(第二发靠它自己轮上来)");

            YamlLint();
            DiaryCopy();
            DiaryMerge();
        }

        // v0.40:日记"同类项合并"的契约 —— 第一次不替换、重复必须有句子、句子里必须带次数。
        //        (这条本身就是防"合并后玩家读到一句没有数字的怪话"。)
        static void DiaryMerge()
        {
            Eq(DiaryLines.MergeDay("fish", 1, 3), null, "n=1 不该被合并句替换(第一次用自己的原句)");
            foreach (var k in new[] { "fish", "fishmiss", "eat", "coconut", "heal", "healfail",
                                      "talk", "craft", "repair", "charge", "throwbottle" })
            {
                var t = DiaryLines.MergeDay(k, 2, 2);
                if (string.IsNullOrEmpty(t)) fails.Add("白天行动 " + k + " 一天里会重复,但没有合并文案");
                else if (!t.Contains("2")) fails.Add("白天行动 " + k + " 的合并句里没出现次数:" + t);
            }
        }

        // v0.30:日记文案覆盖率 —— 每个事件的每条选项都得有一句日记,否则那一晚的日记会空一行
        static void DiaryCopy()
        {
            var evs = new List<GameEventSO>();
            foreach (var e in DB.bagEvents) if (e != null && !evs.Contains(e)) evs.Add(e);
            foreach (var e in DB.bagEvents)
                for (int i = 0; i < e.variants.Count; i++) if (!evs.Contains(e.variants[i])) evs.Add(e.variants[i]);
            foreach (var e in new[] { DB.Shadow, DB.TwoLights, DB.Ship, DB.Ghost, DB.Bones })
                if (e != null && !evs.Contains(e)) evs.Add(e);

            int miss = 0;
            foreach (var e in evs)
            {
                if (e == null) continue;
                if (DiaryLines.Night(e.key, "__no_such_choice__", 3) == null)
                { fails.Add("事件「" + e.displayName + "」连事件级兜底日记都没有"); miss++; }
                foreach (var c in e.choices)
                    if (DiaryLines.Night(e.key, c.key, 3) == null)
                    { fails.Add("日记文案缺失:" + e.key + ":" + c.key + "(" + c.label + ")"); miss++; }
            }
            if (miss == 0) Debug.Log("[自检] 日记文案覆盖 " + evs.Count + " 个事件的全部选项。");
        }

        // §7.2 第三条铁律的自动版:Unity 会把长字符串 折行(续行以 4+ 空格开头),
        // 而 "按行首正则替换一个字段" 只改第一行 ⇒ 旧值的续行被留下,变成一句孤立的 \uXXXX,
        // Unity 报 "Parser Failure: Expect ':' between key and value"。v0.27 改 searchplane_flare 时踩过。
        //
        // ⚠ v0.32 修的是 **这个检查自己的 bug**:旧版判据是"第 4 个字符是空格 ⇒ 孤立续行",
        //   可每一份 Unity 资产的第 3 行都是文档头 `--- !u!114 &11400000` —— 第 4 个字符正好是空格。
        //   于是 170 份资产全部误报,自检红成一片,真出事时反而被淹掉。
        //   新判据换成 **结构**:一行只要"长得像"文档头 / 键 / 列表项 / 流式集合的一角就不算孤立;
        //   引号标量的折行与 |- 块标量的正文整段跳过。再加 YamlLintSelfTest() 正反两个方向钉住它 ——
        //   一个只会乱叫(或只会沉默)的检查,比没有检查更糟。
        static void YamlLint()
        {
            string root = UnityEngine.Application.dataPath + "/Data";
            if (!System.IO.Directory.Exists(root)) return;
            int files = 0, bad = 0;
            foreach (var f in System.IO.Directory.GetFiles(root, "*.asset", System.IO.SearchOption.AllDirectories))
            {
                files++;
                foreach (var lineNo in OrphanLines(System.IO.File.ReadAllLines(f)))
                {
                    fails.Add("YAML 有一行既不是键也不是列表项(孤立续行):" + System.IO.Path.GetFileName(f)
                              + " 第 " + lineNo + " 行");
                    bad++;
                }
            }
            Debug.Log("[自检] YAML 续行扫描:" + files + " 份资产," + bad + " 处可疑。");
            YamlLintSelfTest();
        }

        // 返回"不像任何合法形态"的行号(1 起)。一份文件里有几处就报几处,不再只报第一条。
        static List<int> OrphanLines(string[] lines)
        {
            var hits = new List<int>();
            bool openScalar = false;        // 引号标量折行中
            int blockIndent = -1;           // >=0 表示正在 |- 块标量的正文里,值 = 那个键的缩进
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string t = raw.Trim();
                int indent = raw.Length - raw.TrimStart().Length;
                if (blockIndent >= 0)
                {
                    if (t.Length == 0) continue;
                    if (indent > blockIndent) continue;   // 块标量正文,合法
                    blockIndent = -1;                     // 缩进掉回去了 ⇒ 块结束,这一行照常判
                }
                if (openScalar)
                {
                    if (t.EndsWith("\"")) openScalar = false;
                    continue;                             // 折行的引号值,合法
                }
                if (t.Length == 0) continue;
                if (t.StartsWith("%") || t.StartsWith("---")) continue;      // 文件头 / 文档头
                if (t.StartsWith("- ") || t == "-") continue;                // 列表项
                if (t.StartsWith("{") || t.StartsWith("}") || t.StartsWith("[")
                    || t.StartsWith("]") || t.StartsWith("\"")) continue;     // 流式集合与引号续行
                int c = t.IndexOf(':');
                if (c > 0 && IsKeyStart(t, c))
                {
                    string v = t.Substring(c + 1).Trim();
                    if (v == "|" || v == "|-" || v == ">" || v == ">-") blockIndent = indent;
                    else if (v.Length > 1 && v[0] == '"' && CountUnescapedQuotes(v) % 2 == 1) openScalar = true;
                    continue;
                }
                hits.Add(i + 1);
            }
            return hits;
        }

        // 键名只允许 字母/数字/下划线,且不以数字开头(以数字开头的行是折行内容,不是键)
        static bool IsKeyStart(string t, int colon)
        {
            for (int i = 0; i < colon; i++)
            {
                char ch = t[i];
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')
                          || (ch >= '0' && ch <= '9') || ch == '_';
                if (!ok) return false;
            }
            return !(t[0] >= '0' && t[0] <= '9');
        }

        // 正反两个方向钉住检测器:干净的必须过,坏掉的必须被抓出来。
        static void YamlLintSelfTest()
        {
            var clean = new[]
            {
                "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000",
                "MonoBehaviour:", "  m_Name: ", "  desc: \"\\u4e00\\u53e5", "  \\u5f88\\u957f\\u7684\\u8bdd\"",
                "  choices:", "  - {fileID: 11400000, guid: abc, type: 2}",
                "  note: |-", "    body line", "  slot: 1"
            };
            var broken = new[]
            {
                "--- !u!114 &11400000", "MonoBehaviour:", "  desc: \"\\u65b0\\u7684\\u4e00\\u53e5\"",
                "  \\u65e7\\u7684\\u7eed\\u884c\\u6ca1\\u6709\\u4eba\\u5220"
            };
            var falsePos = OrphanLines(clean);
            Check(falsePos.Count == 0, "YamlLint 自检:干净的资产被判成有问题(误报,行号 " + string.Join(",", falsePos) + ")");
            var got = OrphanLines(broken);
            Check(got.Count == 1 && got[0] == 4, "YamlLint 自检:孤立续行没被抓到,实际行号 " + string.Join(",", got));
        }
        static int CountUnescapedQuotes(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '"') continue;
                int back = 0;
                for (int j = i - 1; j >= 0 && s[j] == '\\'; j--) back++;
                if (back % 2 == 0) n++;
            }
            return n;
        }

        static void ExploreDice()
        {
            var rng = new System.Random(7);
            int days = 0, avail = 0, maxGap = 0;
            for (int run = 0; run < 2000; run++)
            {
                float p = B.exploreRefreshBase; int gap = 0;
                for (int d = 0; d < 200; d++)
                {
                    days++;
                    if (rng.NextDouble() < p)
                    {
                        avail++; if (gap > maxGap) maxGap = gap; gap = 0;
                        p = B.exploreRefreshBase;                 // 只有真的探索才回落
                    }
                    else { gap++; p = Mathf.Min(B.exploreRefreshCap, p + B.exploreRefreshRamp); }
                }
            }
            float rate = (float)avail / days;
            Check(Mathf.Abs(rate - 0.37f) < 0.02f, "稳态可用率 ≈37%,实际 " + (rate * 100).ToString("F1") + "%");
            Check(maxGap <= 14, "最迟第 15 个白天必定可用(实测最大间隔 " + maxGap + ")");
            Debug.Log("[自检] 探索可用率 " + (rate * 100).ToString("F2") + "% / 最大连续未刷新 " + maxGap + " 天");
        }

        static void MaterialBudget()
        {
            var rng = new System.Random(99);
            float total = 0; int runs = 3000;
            for (int run = 0; run < runs; run++)
            {
                float p = B.exploreRefreshBase; int got = 0;
                for (int d = 1; d <= 50; d++)
                {
                    if (rng.NextDouble() < p)
                    {
                        for (int i = 0; i < B.exploreMaterialRolls; i++) if (rng.NextDouble() < B.exploreMaterialChance) got++;
                        p = B.exploreRefreshBase;
                    }
                    else p = Mathf.Min(B.exploreRefreshCap, p + B.exploreRefreshRamp);
                }
                total += got;
            }
            float avg = total / runs;
            Check(avg > 28 && avg < 38, "50 天材料期望 ≈33 份(风险31),实际 " + avg.ToString("F1"));
            Debug.Log("[自检] 50 天材料期望 " + avg.ToString("F1") + " 份 ⇒ 对 ≈50 份需求缺口 " + (50f / avg).ToString("F2") + "×");
        }

        static void FishMiss()
        {
            Eq(B.keyChanceFish, 0.05f, "钓鱼钥匙 5%");
            Eq(B.keyChanceDive, 0.15f, "潜水钥匙 15%");
            Check(Mathf.Abs(Miss(1) - 0.20f) < 1e-4f, "第1天空军 20%");
            Check(Mathf.Abs(Miss(10) - 0.47f) < 1e-3f, "第10天空军 47%,实际 " + Miss(10));
            Check(Mathf.Abs(Miss(21) - 0.80f) < 1e-4f, "第21天封顶 80%");
            Check(Mathf.Abs(Miss(50) - 0.80f) < 1e-4f, "第50天仍 80%");
        }

        static float Miss(int day) { return Mathf.Min(B.fishMissCap, B.fishMissBase + B.fishMissPerDay * (day - 1)); }

        static void Names()
        {
            Eq(GameRoot.SanitizeName("Daylily"), "Daylily", "默认名原样通过");
            Eq(GameRoot.SanitizeName(""), "Daylily", "空名回落默认");
            Eq(GameRoot.SanitizeName(null), "Daylily", "null 回落默认");
            string t = GameRoot.SanitizeName("<b>hi</b>");
            Check(!t.Contains("<") && !t.Contains(">"), "过滤尖括号,实际 " + t);
            Eq(GameRoot.SanitizeName("123456789").Length, 8, "长度上限 8");
            Eq(GameRoot.SanitizeName("a\u0001b"), "ab", "过滤控制字符");
        }

        static void Endings()
        {
            Eq(DB.endings.Count, 7, "结局应为 7 个(T/A/B/C/F/H/I)");
            foreach (var id in new[] { EndingId.T, EndingId.A, EndingId.B, EndingId.C, EndingId.F, EndingId.H, EndingId.I })
                Check(DB.endings.ContainsKey(id), "缺结局 " + id);
            Check(!DB.endings[EndingId.C].usesClaimedEpilogue, "C 不读 epilogueIfTrueClaimed");
            Check(!DB.endings[EndingId.F].usesClaimedEpilogue, "F 不读 epilogueIfTrueClaimed");
            foreach (var id in new[] { EndingId.A, EndingId.B, EndingId.H, EndingId.I })
                Check(DB.endings[id].epilogueIfTrueClaimed.Count > 0, id + " 应有「打过T」的换句尾声");

            var s1 = new RunState(); s1.hidden.nameTagTaken = true;
            var s2 = new RunState();
            Eq(EndingResolver.DeathEnding(s1), EndingId.C, "拿过铭牌 → 死于 C");
            Eq(EndingResolver.DeathEnding(s2), EndingId.F, "从未拿铭牌 → 回环 F");
        }

        // v0.41 立、v0.46 改口径:食用队友(第五种失去队友的路径)。钉三件事:
        //   回饱不再是一个可调比例(那个字段已经删干净)、三天咽不下东西这条惩罚真的挂在代码里、
        //   以及"没罐头 + 饿到 0"这个闸用的存储判定没走歪(空仓库必须真的判成"没有罐头")。
        static void MateMeal()
        {
            Check(typeof(BalanceConfig).GetField("mealFullnessGain") == null,
                  "BalanceConfig.mealFullnessGain 应该已经删除 —— v0.46 起 食用 一次回满(与 椰子 同类),代价改挂在天数上");
            Eq(IslandPhase.MealFoodLockDays, 3, "吃掉队友之后 咽不下东西 的天数(用户:\"三天吃不了东西\")");
            Check(DiaryLines.Day("matemeal", 3) != null, "食用队友 缺日记文案(Day_ 里没有 matemeal)");
            var s = new RunState();
            Check(!s.storage.Has(DB.Can), "新局的空仓库应当判为\"没有罐头\"(食用那条的闸靠它)");
            s.storage.Add(DB.Can, 1);
            Check(s.storage.Has(DB.Can), "有一份罐头时不该再给\"食用\"选项");
        }

        // v0.46 这一轮的账:围墙进了损坏表 / 救援事件对火堆免检 / 鱼群只剩两件 / 建筑"建过"与"还好"分家。
        static void V046()
        {
            // ① 建过 ≠ 还在用。这两本账一旦合并,围墙裂了就会在制造面板上重新出现"再花 5 材料建一座"。
            var s = new RunState();
            s.Build(Database.Wall);
            Check(s.HasStructure(Database.Wall), "建好的围墙:功能上算'在'");
            s.MarkStructureBroken(Database.Wall);
            Check(!s.HasStructure(Database.Wall), "裂了的围墙:不再挡事(涨潮/毒蛇 会重新进来,QTE 加成也失效)");
            Check(s.structures.Contains(Database.Wall), "但它仍然算'建过'——配方行不该因为坏了而重新出现");
            Check(s.RepairStructure(Database.Wall) && s.HasStructure(Database.Wall), "修好 = 直接划掉破损那一本账");
            Check(!s.RepairStructure(Database.Wall), "没坏的时候不该修(修理段会把它筛掉)");

            // ② 修理价走"制造量一半向上取整 + 1 体力"这条既有口径(§11-38 的有配方那一套)
            int m, st;
            DB.RepairCost(Database.Wall, out m, out st);
            Check(m == 3 && st == 1, "围墙修理 材料3 + 1 体力(制造 5/3 的一半向上取整),实际 " + m + "+" + st);

            // ③ 信号火堆 免检:它只在三条救援事件上生效,别的晚上一个字都不改
            Check(DB.SearchPlane != null && DB.Ship != null && DB.Ghost != null, "三条救援事件都在");
            Eq(R("signalfire").materials, 6, "信号火堆 仍然 材料6(这轮没动价 —— 它是这条免检路径唯一的门槛)");

            // ④ 鱼群:钓竿 已经不在这一场里(选项闸 + 结算两侧都要成立)
            var hand = Ch(DB.FishSchool, "hand");
            Check(hand != null, "鱼群 的 鱼叉 那条选项还在");
            Check(hand != null && hand.requiresItem == null, "鱼叉 那条不写 requiresItem(它靠 AvailableChoices 的闸生成,与 v0.45 一致)");
            Check(Ch(DB.FishSchool, "net") != null && Ch(DB.FishSchool, "net").requiresItem == DB.Net, "下渔网 仍绑 渔网");
            Eq(DB.Net.breakChanceStart, 0.3f, "渔网 从 30% 起(§2.3 例外),鱼叉 从 20% 起");
            Eq(DB.Spear.breakChanceStart, 0.2f, "鱼叉 起始概率");

            // ⑤ 伞:机舱里最多一把(v0.46,与 v0.45"整堆坏"配套 —— 兜底改成"回来再做一把",材料2)
            Eq(DB.Umbrella.cabinMax, 1, "雨伞 拾荒阶段最多刷 1 把");
        }

        // v0.47 开、v0.48 按"坏 / 不坏"一套模型重写。这里钉的是 **数据 + 状态机**;
        // ⚠ 两件真事只能靠 Play 看:垒完灶是否当场着火、两晚之后是否自己进「坏」。
        static void V047()
        {
            Eq(RunState.FireNightsPerLighting, 2, "一次点燃管两晚(用户:篝火和信号火堆都持续两晚上,按点燃那天算)");

            var s = new RunState();
            Check(!s.fireLitTonight, "什么都没建 ⇒ 没有火");
            s.Build(Database.Campfire);
            Check(!s.fireLitTonight && s.brokenStructures.Contains(Database.Campfire),
                  "刚建好的灶 = 还没点着 = 「坏」—— 这样漏调 Relight 只会得到看得见的没火,不会是假火");
            s.Relight(Database.Campfire);
            Check(s.fireLitTonight && s.NightsLeft(Database.Campfire) == 2, "点燃(= 修好这一件坏)之后:有火 + 还剩 2 晚");
            s.TickFiresAtDawn();
            Check(s.fireLitTonight && s.NightsLeft(Database.Campfire) == 1, "第一个清晨还剩 1 晚(点燃当天算第 1 晚)");
            s.TickFiresAtDawn();
            Check(!s.fireLitTonight && s.brokenStructures.Contains(Database.Campfire),
                  "第二个清晨归零 ⇒ 自动进「坏」这一本账 —— 统一模型的核心断言");
            Check(s.structures.Contains(Database.Campfire), "「建过」那本账不动 ⇒ 配方行不会重新出现「再垒一座」");
            s.Extinguish(Database.Campfire);
            Check(!s.fireLitTonight, "涨潮/小影怪 用同一个 Extinguish 入口,幂等(已经是坏,再浇一次不报错)");
            s.Relight(Database.Campfire);
            Check(s.fireLitTonight && s.NightsLeft(Database.Campfire) == 2, "维修面板那行「重新点燃」走的就是 Relight");

            var t = new RunState();
            t.Build(Database.SignalFire); t.Relight(Database.SignalFire);
            Check(t.fireLitTonight && t.SignalFireBurning, "只点 信号火堆:篝火的作用一并成立(上位平替就落在这个「或」上)");
            t.Extinguish(Database.SignalFire);
            Check(!t.fireLitTonight && !t.SignalFireBurning, "它一灭,连 篝火 的作用一起没有 —— **平替不是叠加**");

            var cf = R("campfire"); var sf = R("signalfire");
            Check(cf.isStructure && cf.resultStructure == Database.Campfire, "篝火 是建筑(灶)");
            Check(cf.consumesFlint, "v0.48:建造那一手就把它点着 ⇒ 吃一块火种(consumesFlint 回到 true,含义已换)");
            Eq(cf.materials, 3, "垒灶 材料3"); Eq(cf.stamina, 2, "垒灶 2 体力");
            Eq(sf.materials, 6, "信号火堆 材料6"); Eq(sf.stamina, 2, "信号火堆 2 体力(原 3)");
            Check(sf.consumesFlint, "信号火堆 同样是建好即点着");
            Eq(sf.requiresStructure, Database.Campfire, "信号火堆 的唯一前置 = 篝火(§6 目前仅有的一道前置)");
            Check(!string.IsNullOrEmpty(sf.requiresStructure), "⚠ 这条 requiresStructure 必须真的写进资产:没填在 Unity 里是 \"\" 不是 null");

            // v0.48 你收的两条:打火石 只有一块、修理开始收材料
            Eq(DB.Flint.cabinMax, 1, "打火石 全舱只刷一块(用户:\"打火石只有一个\")");
            Eq(R("flint").resultItem, DB.CrudeFlint, "配方 flint 产出的仍是 土制打火石(不是 打火石 本身)");
            Eq(R("flint").supersededBy, DB.Flint, "\"存在打火石则不会出现土制打火石的制作方式\" = 既有的 supersededBy 规则");
            int fm, fst; DB.RepairCost(DB.Flint, out fm, out fst);
            Check(fm == 1 && fst == 1, "打火石 修理 材料1 + 1 体力(v0.48 收回 v0.5 的「0 材料」)—— 实际 " + fm + "+" + fst);

            // §11-74 的修法:打过 T 之后 A/B/H 的专属描写不再丢;I 按 v0.47 #4 保持不同
            foreach (var id in new[] { EndingId.A, EndingId.B, EndingId.H })
            {
                var e = DB.endings[id];
                Check(e.epilogueIfTrueClaimed.Count > 0 && e.epilogueIfTrueClaimed[0] == e.epilogueLines[0],
                      id + " 的「打过T」版本应以它自己的专属描写开头(§4.8 那句「前面照旧」)");
            }
            var ie = DB.endings[EndingId.I];
            Check(ie.epilogueIfTrueClaimed[0] != ie.epilogueLines[0],
                  "I 是刻意的例外:你定过「打过真结局就不加那句回到空难」⇒ 它的 claimed 版第一行不是新句");
        }

        static void Inv()
        {
            var inv = new Inventory();
            inv.Add(DB.Spear, 1);
            Check(inv.Has(DB.Spear) && inv.Count(DB.Spear) == 1, "入库后可用");
            inv.MarkBroken(DB.Spear);
            Check(!inv.Has(DB.Spear) && inv.BrokenCount(DB.Spear) == 1, "破损件不可用(持有但不生效)");
            inv.Unbreak(DB.Spear);
            Check(inv.Has(DB.Spear) && inv.BrokenCount(DB.Spear) == 0, "修理后恢复可用");
            Check(!inv.Remove(DB.Spear, 2), "不能超量移除");
            var s = new RunState();
            s.carried.Add(DB.DiveGear);
            Eq(s.UsedCarrySlots(), 4, "潜水装置占满 4 格");

            // v0.33:「损坏的物品不能再使用」这条不变量在"被猴子挑走"那一侧也要成立 ——
            //        挑偷对象与友善猴子的"换出"候选都只读 ok 本,破损件根本进不了候选表。
            var s2 = new RunState();
            s2.storage.Add(DB.Spear, 1);
            s2.storage.MarkBroken(DB.Spear);
            Check(NightResolver.PeekStealable(s2) == null, "破损的鱼叉不该被当成可用物品(猴子偷不走它)");
            Check(!s2.storage.Has(DB.Spear) && s2.storage.HasBroken(DB.Spear), "破损件:持有但不计可用");
            s2.storage.Unbreak(DB.Spear);
            Eq(NightResolver.PeekStealable(s2), DB.Spear, "修好之后它重新变成可用物品");
        }

        // v0.45:这一轮改动里能被静态钉住的部分。① 是那条"只有两种状态"的硬不变量,
        //        ② 钉的是"闸的值 == 潜水价 == 一天精力上限"(脱钩就会变成玩家根本下不了水)。
        static void V045()
        {
            var inv = new Inventory();
            inv.Add(DB.Spear, 2);
            inv.MarkBroken(DB.Spear);
            Check(inv.Count(DB.Spear) == 0 && inv.BrokenCount(DB.Spear) == 2,
                  "整堆一起坏:2 把鱼叉 MarkBroken 后应当 可用0/破损2,实际 " +
                  inv.Count(DB.Spear) + "/" + inv.BrokenCount(DB.Spear));
            Eq(inv.Total(DB.Spear), 2, "坏了不等于没了:总数仍是 2");
            inv.Unbreak(DB.Spear);
            Check(inv.Count(DB.Spear) == 2 && inv.BrokenCount(DB.Spear) == 0, "修好 = 整堆回到可用");

            var dive = A("dive"); var explore = A("explore");
            Check(dive != null && explore != null, "dive / explore 两条行动都还在");
            Eq(IslandPhase.FullDayStamina, 3, "大行动闸 = 满 3 点精力");
            Check(dive != null && dive.cost == IslandPhase.FullDayStamina, "潜水价必须与闸同值");
            Check(explore != null && explore.costAllRemaining, "探索仍然吃掉当天全部剩余精力");
            Eq(new RunState().stats.staminaMax, IslandPhase.FullDayStamina, "精力上限 == 闸:普通一天正好够下一次水");

            var s = new RunState();
            Check(NightResolver.NoTeammateBlocks(DB.FakeMate, s), "没有队友 → 假队友 这一晚必须被跳过");
            s.mate.present = true;
            Check(!NightResolver.NoTeammateBlocks(DB.FakeMate, s), "有队友 → 假队友 照常出现");
            s.mate.present = false;
            Check(!NightResolver.NoTeammateBlocks(DB.FishSchool, s), "这道闸只管 假队友,不牵连别的事件");
        }
    }
}
