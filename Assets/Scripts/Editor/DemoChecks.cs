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
            Bag(); Monkey(); Breakage(); Prices(); Balance(); ExploreDice(); MaterialBudget(); FishMiss(); Names(); Endings(); Inv(); MateMeal(); V045(); V046(); V047(); V049(); V049T(); V051(); V052(); V053(); V055(); V057(); V058(); V061(); V062(); V063(); V064(); V065(); V066(); V067(); V068(); V069();

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

        // 补录②③④⑥:赶狐狸三条,breakChanceNight 全 0
        static void Monkey()
        {
            var spear = C("monkey", "spear"); var flare = C("monkey", "flare"); var fire = C("monkey", "fire");
            Check(spear != null && flare != null && fire != null && C("monkey", "grab") != null && C("monkey", "refuse") != null,
                  "狐狸应有 鱼叉/信号弹/篝火/空手/不给它");
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
            Check(!inv.HasBroken(DB.FishingRod) && inv.Has(DB.FishingRod), "钓竿永不损坏(狐狸抢回也完好)");
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
                "土制打火石 = 消耗品、不进狐狸货单");
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
                "药草:不进机舱点位池、不进狐狸货单");
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
            Check(!DB.Map.tradeable && !DB.Key.tradeable && !DB.Chest.tradeable, "暗线道具硬排除出狐狸货单");
            Eq(DB.Chest.slots, 2, "藏宝箱 2 格");
            // v0.62(用户:"**背包改为3格**"):潜水装置 4 → 3(3 格背包装不下 4 格件,潜水线会死),携带条 4 → 3
            Eq(DB.DiveGear.slots, 3, "潜水装置 3 格(= 装满新背包一整趟)");
            Eq(DB.DiveGear.slots, ScavengingPhase.CarrySlots, "潜水装置 必须恰好装满携带条(多了带不走、少了浪费一格)");

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
            Check(grab != null && !grab.alwaysAvailable, "v0.27:空手抢夺 不再挂 alwaysAvailable(它只能靠点那只狐狸触发)");
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
            Check(cf.consumesFlint, "v0.48:垒灶那一手就把它点着 ⇒ 吃一块火种(consumesFlint=true)");
            Eq(cf.materials, 3, "垒灶 材料3"); Eq(cf.stamina, 2, "垒灶 2 体力");
            Eq(sf.materials, 6, "信号火堆 材料6"); Eq(sf.stamina, 2, "信号火堆 2 体力(原 3)");
            // ⚠ v0.65(用户:"**升级免火种**"):这条原来钉的是 `sf.consumesFlint == true`("信号火堆 同样是建好即点着")。
            //   "建好即点着" **没变**(DoCraft 那一手显式补一次 Relight),变的是 **不吃火种** ⇒ 断言翻向,理由记在 V065() ①。
            Check(!sf.consumesFlint, "v0.65:信号火堆 这一手是垒高,不该再吃火种(资产里仍是 true 的话 ⇒ 跑菜单 ① 走那条迁移)");
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
            // ⚠ 这条断言的期望值 **不写死**:它原来钉的是"背包 4 格"那本旧账,v0.62 把格子改成 3 之后
            //   资产与代码都是 3,只有这条还写着 4 ⇒ 自检红在一条陈旧的钉子上(这类"陈旧断言"已经栽过第二次)。
            Eq(s.UsedCarrySlots(), ScavengingPhase.CarrySlots,
               "潜水装置 占满一整趟(" + ScavengingPhase.CarrySlots + " 格)");

            // v0.33:「损坏的物品不能再使用」这条不变量在"被狐狸挑走"那一侧也要成立 ——
            //        挑偷对象与友善狐狸的"换出"候选都只读 ok 本,破损件根本进不了候选表。
            var s2 = new RunState();
            s2.storage.Add(DB.Spear, 1);
            s2.storage.MarkBroken(DB.Spear);
            Check(NightResolver.PeekStealable(s2) == null, "破损的鱼叉不该被当成可用物品(狐狸偷不走它)");
            Check(!s2.storage.Has(DB.Spear) && s2.storage.HasBroken(DB.Spear), "破损件:持有但不计可用");
            s2.storage.Unbreak(DB.Spear);
            Eq(NightResolver.PeekStealable(s2), DB.Spear, "修好之后它重新变成可用物品");
        }

        // v0.49(用户:"改吧"):这场兽在屏幕上改叫 狐狸,而 key / 字段名 / 夜晚 target 一律保持 monkey。
        //   这段断言真正要挡的是 **"顺手修正"**:以后谁看见代码里叫 monkey、界面写狐狸,那是 §11-79 的定案,不是漏改。
        static void V049()
        {
            Eq(DB.MonkeyNaughty.key, "monkeynaughty", "v0.49:key 不动,改的只有 displayName");
            Eq(DB.MonkeyNaughty.displayName, "调皮的狐狸", "v0.49:事件名");
            Eq(DB.MonkeyFriendly.displayName, "友善的狐狸", "v0.49:事件名");
            Eq(DB.MonkeyGift.displayName, "狐狸的回礼", "v0.49:暗线件 D1 跟着改");

            int stray = 0;
            foreach (var it in DB.scavengedItems) stray += MonkeyStray(it.displayName) + MonkeyStray(it.desc);
            foreach (var r in DB.recipes) stray += MonkeyStray(r.displayName) + MonkeyStray(r.desc);
            foreach (var a in DB.activities) stray += MonkeyStray(a.displayName) + MonkeyStray(a.desc);
            foreach (var e in DB.bagEvents)
            {
                stray += MonkeyStray(e.displayName) + MonkeyStray(e.description);
                if (e.choices != null)
                    foreach (var c in e.choices) stray += MonkeyStray(c.label) + MonkeyStray(c.resultHint);
            }
            Check(stray == 0, "v0.49:还有 " + stray + " 处「给用户看的文字」写着 猴(key 与字段名不算,它们只活在代码里)");

            string tail = DiaryLines.Night("monkeynaughty", null, 3);
            Check(tail != null && tail.Contains("狐狸") && !tail.Contains("猴"), "v0.49:日记那句要写狐狸,实际 " + tail);

            // v0.49(用户:"3.可以不用了"):信号弹 不再摆实体 ⇒ **它不能是任何选项的点击目标**,否则那条选项永远点不到。
            //   ⚠ 这里刻意不调 `NightResolver.ChoicesFor` —— 那个函数要读静态 `cur / DB`,自检里没有场景(红线 65)。
            //     所以直接扫全部 GameChoiceSO 资产:绑在 信号弹 上的选项必须一条都没有(v0.19 起目标是 信号枪)。
            int stranded = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameChoiceSO"))
            {
                var c = AssetDatabase.LoadAssetAtPath<GameChoiceSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.requiresItem == DB.Flare) stranded++;
            }
            Check(stranded == 0, "v0.49:有 " + stranded + " 条选项绑在 信号弹 上,而它已经不再摆实体 ⇒ 那条选项会点不到");
        }

        // v0.49 技术清单那一轮(§11-83 ①~⑩)里能被静态钉住的两条。
        static void V049T()
        {
            // ①夜晚那张 `事件→曲` 表:key 写错 **不会报错**,只会"那一晚永远放默认那首"——
            //   这种静默降级最没人会注意到(而 monkeynaughty 那次改名正好证明 key 会动),所以钉住。
            var known = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:GameEventSO"))
            {
                var e = AssetDatabase.LoadAssetAtPath<GameEventSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (e != null) known.Add(e.key);
            }
            int ghost = 0;
            foreach (var k in Music.Spooky) if (!known.Contains(k)) ghost++;
            Check(ghost == 0, "v0.49 ⑨:音乐表里有 " + ghost + " 个事件 key 在资产里不存在(那条分支永远不会命中)");

            // ②山包 **不能吞掉玩法落点**:道具落点与各锚点仍然是 y=0,所以高度场在它们那儿必须还是平的。
            //   (自检不能改场景,所以它验的是"这条不变量现在还成立";哪天挪了落点或挪了山包,这里会响。)
            //   ⚠ 这一条要 **读场景**,所以它单独成一个 if —— 以前写成 `return`,会把下面几条纯数据的检查一起跳过。
            var st = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (st == null) Debug.Log("[自检] v0.49 ⑤:当前场景没开 Demo ⇒ 跳过\"山包不吞落点\"这一条(其余仍照查)。");
            else
            {
                var pts = new List<Transform>(st.propSlots);
                pts.Add(st.mateAnchor); pts.Add(st.gullAnchor); pts.Add(st.bonesAnchor);
                pts.Add(st.seaAnchor); pts.Add(st.fireAnchor);
                int swallowed = 0; float worst = 0f; string who = "";
                foreach (var t in pts)
                {
                    if (t == null) continue;
                    float h = IslandTerrain.Height(t.position.x, t.position.z);
                    if (h <= 0.15f) continue;
                    swallowed++;
                    if (h > worst) { worst = h; who = t.name; }
                }
                Check(swallowed == 0, "v0.49 ⑤:有 " + swallowed + " 个落点被小山包埋住(最高 " + worst.ToString("F2") +
                      " 米,在 " + who + ")—— 落点仍是 y=0,要么把它按高度场抬,要么把山包挪开");
            }

            // ③v0.50 #8:海鸥那条"A 或 B"。这条要钉的不是概率,是 **三边一致**:
            //   标签写着"罐头或鱼饵"、结算 `case "gull"` 两边都收、而以前 `requiresItem` 只绑罐头 ⇒
            //   有鱼饵没罐头的那一晚选项根本不生成(标签一直在说谎)。
            var feed = GullFeed();
            Check(feed != null && feed.requiresItem == DB.Can && feed.requiresItemAlt == DB.Bait &&
                  feed.label != null && feed.label.Contains("鱼饵"),
                  "v0.50 #8:海鸥 的「喂它」必须 绑罐头 + 备胎鱼饵 + 标签写清两件,实际 " +
                  (feed == null ? "找不到那条选项" : feed.requiresItem + " / " + feed.requiresItemAlt + " / " + feed.label));

            // ④v0.50 #9:彩蛋件平时不摆沙滩,而 **献宝那条的入口就是 藏宝箱 那块实体** ——
            //   所以沙滩那句 `if (k.lore && NightResolver.ChoicesFor(k).Count == 0) continue;` 是它唯一的活路。
            //   这条断言的意义:哪天有人把 藏宝箱 改成非 lore(或把献宝改成绑别的东西),它会提醒两边要一起改。
            int offers = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameChoiceSO"))
            {
                var c = AssetDatabase.LoadAssetAtPath<GameChoiceSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.key == "offer") offers++;
            }
            Check(offers > 0 && DB.Chest != null && DB.Chest.lore,
                  "v0.50 #9:献宝 选项有 " + offers + " 条、绑的实体是 藏宝箱(lore=" +
                  (DB.Chest == null ? "?" : DB.Chest.lore.ToString()) + ")⇒ 沙滩那条「当晚有选项绑它才摆」的例外不能删");

            // ⑤顺手把 v0.50 #9 的**前提**钉成代码(以前只是我离线扫过一次):没有任何选项用 requiresItem
            //   绑着一件 lore 件当**唯一**入口 —— 除了上面那条走 BoundItem 的 献宝。
            int bound = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameChoiceSO"))
            {
                var c = AssetDatabase.LoadAssetAtPath<GameChoiceSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.requiresItem != null && c.requiresItem.lore && c.key != "feed") bound++;
            }
            Check(bound == 0, "v0.50 #9:有 " + bound + " 条选项把 requiresItem 绑在 彩蛋/暗线件 上 ⇒ 平时不摆实体就会让它没有入口");
        }

        static GameChoiceSO GullFeed()
        {
            if (DB.Gull == null || DB.Gull.choices == null) return null;
            foreach (var c in DB.Gull.choices) if (c != null && c.key == "feed") return c;
            return null;
        }

        static int MonkeyStray(string s) { return s != null && s.IndexOf('猴') >= 0 ? 1 : 0; }

        // v0.51:这一轮钉的三条 **全是"不报错那一类"** —— key 拼错、名单少一件、子碰撞体没删,
        //        编译与 Play 都不会响,只有画面/点击悄悄不对。
        static void V051()
        {
            // ① 沙滩小件表 `SmallOnBeach` 的 key 拼错 = **静默不生效**(那件东西照旧那么大)。
            //    `flint` 与 `crudeflint` 只差一个前缀,正是最容易拼错的那种 ⇒ 逐个对回真实物品。
            var keys = new HashSet<string>();
            foreach (var filter in new[] { "t:ItemSO", "t:ToolSO" })
                foreach (var guid in AssetDatabase.FindAssets(filter))
                {
                    // 注意是 **按 Object 取再转 ItemSO**:`LoadAssetAtPath<ItemSO>` 对 ToolSO 这类子类不保证回东西。
                    var it = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        AssetDatabase.GUIDToAssetPath(guid)) as ItemSO;
                    if (it != null) keys.Add(it.key);
                }
            int ghost = 0;
            foreach (var k in IslandPhase.SmallOnBeach.Keys) if (!keys.Contains(k)) ghost++;
            Check(ghost == 0, "v0.51 ①:沙滩小件表里有 " + ghost + " 个 key 在物品资产里不存在 ⇒ 那一档缩放永远不生效" +
                  "(表里 " + IslandPhase.SmallOnBeach.Count + " 档、库里认出 " + keys.Count + " 件)");

            // ② 「收集」页右栏与按钮上那行进度共读 `TitlePhase.EggItems(DB)`。这条钉的是 **名单 == 全部 lore 件**:
            //    哪天新加一件彩蛋而名单忘了补,页面上就永远看不见它(而沙滩那条"不摆实体"是按 lore 走的,
            //    于是这件彩蛋变成 **既看不见也收不到** —— 两处各说各话)。
            var eggs = TitlePhase.EggItems(DB);
            var listed = new HashSet<string>();
            int nulls = 0, notLore = 0;
            for (int i = 0; i < eggs.Length; i++)
            {
                if (eggs[i] == null) { nulls++; continue; }
                if (!eggs[i].lore) notLore++;
                listed.Add(eggs[i].key);
            }
            var all = new HashSet<string>();
            foreach (var filter in new[] { "t:ItemSO", "t:ToolSO" })
                foreach (var guid in AssetDatabase.FindAssets(filter))
                {
                    var it = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        AssetDatabase.GUIDToAssetPath(guid)) as ItemSO;
                    if (it != null && it.lore) all.Add(it.key);
                }
            int missed = 0;
            foreach (var k in all) if (!listed.Contains(k)) missed++;
            Check(nulls == 0 && missed == 0 && notLore == 0 && listed.Count == all.Count,
                  "v0.51 ②:库里 lore 件有 " + all.Count + " 个,收集页名单里 " + listed.Count + " 个(漏 " + missed +
                  " 件)、" + nulls + " 格引用是空的、" + notLore + " 件根本不是 lore ⇒ §5.3 那张表与页面对不上");

            // ③ 拼出来的那张网 **只能有组上一块 collider**:`CreatePrimitive` 每根绳子/浮子都自带一块,
            //    而 `IslandProp`(牌子 + 点击)挂在 **组** 上 ⇒ 留着子碰撞体,点绳子就不触发点击;
            //    而 渔网 正是夜晚"下渔网"那条选项的入口(§11-60 那一类"选项失去可点落点")。
            //    ⚠ 这条 **只能现场试**(拼的物体是运行时生成的,盘上没有资产可扫)⇒ 临时物体整批打
            //      `hideFlags=DontSave`(**子物体不继承**),用完 `DestroyImmediate` 立刻销毁。
            //    ⚠ 而 `DontSave` 挡不住"场景被标脏" ⇒ 事先记下脏状态,是探针弄脏的就打一行说明,
            //      免得他以为自检改了他的场景(自检不改场景是这条项目的老规矩)。
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasDirty = scene.IsValid() && scene.isDirty;
            var temp = new GameObject("V051NetProbe");
            temp.hideFlags = HideFlags.DontSave;
            var pm = new Material(Shader.Find("Standard"));
            pm.hideFlags = HideFlags.DontSave;
            var netGo = World.Net(temp.transform, "net_probe", Vector3.zero, 1f, pm);
            // ⚠ **先把"建出来没有"记成 bool 再销毁**:`netGo` 是 `temp` 的子物体,`DestroyImmediate(temp)` 之后
            //   它就是一把 **假 null**(Unity 重载了 `==`,已销毁的对象 `!= null` 返回 false)。
            //   我第一版就在销毁 **之后** 才写 `netGo != null` ⇒ 数明明是对的(1 块、0 块在子上)却报失败。
            //   口径:**凡是要在销毁后引用的,判断必须在销毁前落成值。**
            bool built = netGo != null;
            int total = 0, onKids = 0;
            if (built)
            {
                var tfs = netGo.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < tfs.Length; i++) tfs[i].gameObject.hideFlags = HideFlags.DontSave;
                var cols = netGo.GetComponentsInChildren<Collider>(true);
                total = cols.Length;
                for (int i = 0; i < cols.Length; i++) if (cols[i].gameObject != netGo) onKids++;
            }
            UnityEngine.Object.DestroyImmediate(temp, true);
            UnityEngine.Object.DestroyImmediate(pm);
            Check(built, "v0.51 ③:探针没能拼出 渔网(`World.Net` 回了空)⇒ 这一条本轮没验成,先确认它还能被调用");
            Check(built && total == 1 && onKids == 0,
                  "v0.51 ③:拼出来的 渔网 有 " + total + " 块 collider,其中 " + onKids +
                  " 块挂在子物体上 ⇒ 点绳子/浮子不会触发挂在组上的 IslandProp(网就点不到了)。" +
                  "组上那一块是唯一的可点面;子物体自带的要 **在 `World.Net` 里删掉**,而且 **必须走 `World.Strip`** —— " +
                  "`Destroy` 在编辑模式不生效(运行时要它,帧末才删,是安全的)");
            if (!wasDirty && scene.IsValid() && scene.isDirty)
                Debug.Log("[自检] v0.51 ③ 那条探针在编辑模式建了临时物体又删掉,把场景标成了\"未保存\"——" +
                          "那是探针留下的脏标记,不是真改动(它没碰场景里任何既有物体)。不用保存。");
        }

        // v0.52:两条。① 饥荒判据现在是纯函数,所以能真跑三天(以前它埋在 TickTeammate 里,自检碰不了);
        //        ② 钓竿 的新落点是"唯一不在道具格里的一件",没有别的东西替它盯着,所以这里盯。
        static void V052()
        {
            var m = new TeammateState();
            m.hunger = HungerLevel.Famine;
            bool day5Dies = IslandPhase.FamineClaimsLife(m, 5);
            Check(!day5Dies && m.famineDay == 5,
                  "v0.52 #1:降进 饥荒 的那个清晨(第 5 天)**不该判死**,只该挂期限;实际 claims=" + day5Dies +
                  "、famineDay=" + m.famineDay + "(=判死那一半提前了,正是用户这轮否掉的旧行为)");
            Check(IslandPhase.FamineClaimsLife(m, 6), "v0.52 #1:第二天(第 6 天)仍然饥荒 ⇒ 这才该是 次日消失");

            var fed = new TeammateState();
            fed.hunger = HungerLevel.Famine;
            IslandPhase.FamineClaimsLife(fed, 5);            // 第 5 天:挂期限
            fed.hunger = HungerLevel.Full;                   // 白天喂了 1 份罐头(投喂把 hunger 拉回 饱食)
            fed.hungerDaysSinceMeal = 0;
            Check(!IslandPhase.FamineClaimsLife(fed, 6) && fed.famineDay < 0,
                  "v0.52 #1:中间喂过 ⇒ 期限必须解除(而且 famineDay 要清掉),实际 famineDay=" + fed.famineDay);
            fed.hunger = HungerLevel.Famine;
            Check(!IslandPhase.FamineClaimsLife(fed, 9) && fed.famineDay == 9,
                  "v0.52 #1:解除之后再次饿到 饥荒 ⇒ 那是一 **新** 的期限(第 9 天晨不该死),实际 famineDay=" + fed.famineDay);

            var st = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (st == null) { Debug.Log("[自检] v0.52 #2:当前场景没开 Demo ⇒ 跳过\"钓竿 海边落点不撞不埋\"这一条。"); return; }
            var rod = IslandPhase.RodSeaPos();
            var pts = new List<Transform>(st.propSlots);
            pts.Add(st.mateAnchor); pts.Add(st.gullAnchor); pts.Add(st.bonesAnchor);
            pts.Add(st.seaAnchor); pts.Add(st.fireAnchor);
            int tooClose = 0; float nearest = 999f; string who = "";
            foreach (var t in pts)
            {
                if (t == null) continue;
                float d = new Vector2(t.position.x - rod.x, t.position.z - rod.z).magnitude;
                if (d < nearest) { nearest = d; who = t.name; }
                if (d < 1.0f) tooClose++;
            }
            float h = IslandTerrain.Height(rod.x, rod.z);
            Check(tooClose == 0 && h < 0.15f, "v0.52 #2:钓竿 的海边落点(" + rod.x.ToString("F1") + ", " + rod.z.ToString("F1") +
                  ")与 " + tooClose + " 个既有落点靠得过近(最近 " + nearest.ToString("F2") + " 米,在 " + who +
                  ")或地面高 " + h.ToString("F2") + " 米 ⇒ 它会压住别人的可点面,或自己陷进山包");
        }

        // v0.53(§11-84 第一人称共存):**这一块最容易出的事不是崩,是"按了没反应"与"走到某件东西跟前却按不到"** ——
        // 两条都不会报错,所以把"出生点不该一进来就锁定某件东西"与"每件该走得到的东西确实走得到"钉成断言。
        static void V053()
        {
            var st = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (st == null) { Debug.Log("[自检] v0.53:当前场景没开 Demo ⇒ 跳过自由视角那两条区域检查。"); return; }

            // 该走得到的落点:24 个道具格 + 篝火 + 尸骨 + 队友 + 钓竿(它现在在海边)+ 出生点自身要合法。
            // ⚠ **显式排除 大海 与 海鸥**:v0.57 之后 大海 那块板就在水线 **之外**(`WaterR` 外 1.5 米)、
            //   海鸥 在 2.6 米空中 —— 按用户裁定的"不能下水",这两块 **走不到跟前**,所以 E 不覆盖它们(鼠标点仍然是入口)。
            //   把它们写进"应该走得到"的名单会让检查天天红;把它们悄悄漏掉又会让检查说谎 ⇒ 列在这儿并注明理由。
            var reach = ScavengingPhase.Reach;
            var spawn = IslandWalk.ClampToBeach(IslandWalk.Spawn);
            Check(IslandWalk.Walkable(IslandWalk.Spawn),
                  "v0.53 ①:自由视角的出生点 " + IslandWalk.Spawn.ToString("F1") + " 落在可行走区之外" +
                  "(被夹到 " + spawn.ToString("F1") + ")⇒ 一按 V 镜头会瞬移一下,不该这样");

            int tooClose = 0, unreachable = 0;
            var who = new StringBuilder();
            var pts = new List<Transform>(st.propSlots);
            pts.Add(st.fireAnchor); pts.Add(st.bonesAnchor); pts.Add(st.mateAnchor);
            foreach (var t in pts)
            {
                if (t == null) continue;
                float d = new Vector2(t.position.x - spawn.x, t.position.z - spawn.z).magnitude;
                if (d < reach) { tooClose++; who.Append(" " + t.name + "(" + d.ToString("F2") + "米)"); }
                if (!IslandWalk.Walkable(t.position)) { unreachable++; who.Append(" 走不到:" + t.name); }
            }
            var rod = IslandPhase.RodSeaPos();
            if (!IslandWalk.Walkable(rod)) { unreachable++; who.Append(" 走不到:钓竿(海边那个点)"); }
            float rd = new Vector2(rod.x - spawn.x, rod.z - spawn.z).magnitude;
            if (rd < reach) { tooClose++; who.Append(" 钓竿(" + rd.ToString("F2") + "米)"); }

            Check(tooClose == 0, "v0.53 ①:出生点离这些落点不到 " + reach + " 米,一按 V 就会 **自动锁定一个 E 目标**" +
                  "(一按 E 就会做掉一件东西):" + who);
            Check(unreachable == 0, "v0.53 ②:有 " + unreachable + " 个该走得到的落点在可行走区之外:" + who +
                  " ⇒ 自由视角里那些东西按 E 永远进不去(§11-84③ 要求夜晚点物体同样成立)");
            // ④(信息,**不是断言**)已接线的模型里 **哪些整包不带材质** —— 它们靠 `World.ModelHasMaterial` 那条改吃项目的物品色。
            //   以后谁补了贴图或换了带贴图的模型,这条数字自己会变小,不用改代码。
            var bareWho = new StringBuilder();
            int bare = 0;
            // 直接扫目录而不是 `t:Model` 搜索:**这条要的是"确定性"** —— 搜索语法哪天不认 FBX 了,
            // 它会安静地报 "0 件",而 0 件看起来正好像好消息(这类"检查静默不跑"本项目记过账)。
            var dir = UnityEngine.Application.dataPath + "/Resources/Models";
            if (System.IO.Directory.Exists(dir))
                foreach (var f in System.IO.Directory.GetFiles(dir, "*.fbx"))
                {
                    var key = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (key.EndsWith("_broken")) continue;                 // 破损态单独模型不参与这条统计
                    if (!World.ModelHasMaterial(key, false)) { bare++; bareWho.Append(" " + key); }
                }
            else Debug.Log("[自检] v0.53 ④:找不到 Assets/Resources/Models ⇒ 这条没跑(不是通过)。");
            Debug.Log("[自检] v0.53 ④:已接线模型里有 " + bare + " 件 **整包不带材质**" +
                      (bare > 0 ? "(它们现在改吃 `World.ItemMat` 的物品色:" + bareWho.ToString() + ")" : "") +
                      " —— 渔网 就是这条第一次逼出来的(它 `Material` 记录为 0,完好态原本是 Unity 白模)。");
            // 水线这条也要看得见:v0.57 之后可行走区是 **一个圆**,所以看得见的数是"能走到第几米 / 浪线在第几米"
            Debug.Log("[自检] v0.53:可行走 = 以 " + IslandTerrain.Center.ToString("F0") + " 为心、半径 " +
                      IslandTerrain.WalkR + " 米的圆(浪线 r=" + IslandTerrain.WaterR.ToString("F2") +
                      ",所以最后 " + (IslandTerrain.WaterR - IslandTerrain.WalkR).ToString("F2") +
                      " 米是墙)| 出生点 " + spawn.ToString("F1") + " | 眼高 " + ScavengingPhase.EyeHeight +
                      " | 够得着 " + reach + " 米 —— 手感那五个数全部与机舱那层同源,改一处两层一起改。");
        }

        // v0.55:一条"源码级"的断言,钉的是他截图抓到的那个 bug —— 自由视角期间相机归 `IslandWalk` 管,
        //   而上一版 `Off()` 里 `Destroy(eye.gameObject)` 时相机还是 `eye` 的子物体 ⇒ 相机被一起销毁,
        //   回固定镜头后整屏 "No cameras rendering"(日志还照样打"挂到 CameraPose",所以光看日志会以为没事)。
        //   现在组件 **不再生成任何承载相机的物体**,这条断言保证以后谁再往回改就会红。
        static void V055()
        {
            var path = UnityEngine.Application.dataPath + "/Scripts/Phases/IslandWalk.cs";
            if (!System.IO.File.Exists(path))
            {
                Check(false, "v0.55:找不到 Assets/Scripts/Phases/IslandWalk.cs ⇒ 这条断言没跑(不是通过)");
                return;
            }
            var src = System.IO.File.ReadAllText(path);
            // 只看 **代码**,不看注释(这条文件里就有一段注释在讲"上一版为什么写错了",里面有 `Destroy(` 的字样)
            var code = new StringBuilder();
            foreach (var line in src.Split('\n'))
            {
                int c = line.IndexOf("//");
                code.Append(c >= 0 ? line.Substring(0, c) : line).Append('\n');
            }
            string s = code.ToString();
            int hits = 0;
            foreach (var token in new[] { "Destroy(", "DestroyImmediate(" })
                for (int i = s.IndexOf(token); i >= 0; i = s.IndexOf(token, i + 1))
                {
                    // `OnDestroy()` 里含 "Destroy(" 这个子串 ⇒ 不算(那是生命周期回调,不是销毁别人)
                    if (i > 1 && s[i - 2] == 'O' && s[i - 1] == 'n') continue;
                    hits++;
                }
            Check(hits == 0, "v0.55:`IslandWalk.cs` 里出现了 " + hits + " 处 Destroy/DestroyImmediate —— " +
                  "自由视角期间主相机由这个组件管,**销毁任何可能承载相机的物体就会把相机一起带走**" +
                  "(上一版正是这么黑的屏)。要清理东西请改成「不创建需要清理的物体」。");
        }

        // v0.57(用户:"**荒岛建模为一个岛,周围全是海(固定资产)**" + 定案「**大圆岛:现有东西全不动**」)
        //   这一轮最容易出的事不是崩,而是 **"岛形改了,但场景里还有东西在岛外"** —— 那种东西不会报错,
        //   它就静静立在水底下或者悬在浪线上。所以三条:
        //   ① 岛形自己要对得上(浪线半径处,岛面高度必须正好等于海面 —— 这条不读场景,什么时候都能验);
        //   ② **现有内容一个都不能在干岛面之外**(读场景 ⇒ 要跑过菜单 ④ 才是新那一版);
        //   ③ 大海 那块板必须真的在水上、钓竿 必须真的还在浪线之内(它两个都是从岛形推的,岛一改就会漂)。
        static void V057()
        {
            // ── ① 纯代码 ──
            Check(IslandTerrain.WalkR < IslandTerrain.WaterR && IslandTerrain.WaterR < IslandTerrain.BeachR,
                  "v0.57 ①:岛形三个半径的顺序错了(应 可行走 " + IslandTerrain.WalkR + " < 浪线 " +
                  IslandTerrain.WaterR.ToString("F2") + " < 沙滩外沿 " + IslandTerrain.BeachR +
                  ")⇒ 要么人能走进海里,要么浪线落在岛面之外看不见");
            float atWater = IslandTerrain.SurfaceY(IslandTerrain.Center.x, IslandTerrain.Center.y - IslandTerrain.WaterR);
            Check(Mathf.Abs(atWater - IslandTerrain.SeaY) < 0.01f,
                  "v0.57 ①:`WaterR` 推出来的浪线上,岛面高 " + atWater.ToString("F3") + " 米 ≠ 海面 " +
                  IslandTerrain.SeaY + " 米 ⇒ 画出来的浪线与算出来的不是同一条(海与沙滩接缝会浮空或插进沙里)");
            Check(IslandTerrain.SurfaceY(IslandTerrain.Center.x, IslandTerrain.Center.y) > IslandTerrain.SeaY,
                  "v0.57 ①:岛心比海面还低 ⇒ 整座岛在水下");

            // ── ②③ 要读场景 ──
            var st = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (st == null) { Debug.Log("[自检] v0.57 ②③:当前场景没开 Demo ⇒ 跳过\"岛装得下东西吗\"这两条(其余仍照查)。"); return; }
            var disc = AssetDatabase.LoadAssetAtPath<Mesh>(DemoAssetBaker.IslandDiscAsset);
            if (disc == null)
            {
                Debug.Log("[自检] v0.57 ②③:**跳过,不是通过** —— 工程里还没有 " + DemoAssetBaker.IslandDiscAsset +
                          ",说明这一轮的烘焙器没跑过。请跑菜单 Tools/60slike/④ 只重建荒岛层,再看这一条。");
                return;
            }
            var pts = new List<Transform>(st.propSlots);
            pts.Add(st.mateAnchor); pts.Add(st.gullAnchor); pts.Add(st.bonesAnchor);
            pts.Add(st.fireAnchor);
            int off = 0; float worst = 0f; string who = "";
            foreach (var t in pts)
            {
                if (t == null) continue;
                float r = IslandTerrain.Radius(t.position.x, t.position.z);
                if (r <= IslandTerrain.FlatR) continue;
                off++;
                if (r > worst) { worst = r; who = t.name; }
            }
            Check(off == 0, "v0.57 ②:有 " + off + " 个既有落点在干岛面(r=" + IslandTerrain.FlatR +
                  ")之外,最远的 " + worst.ToString("F1") + " 米在 " + who +
                  " ⇒ 它会站在水里/浪线上。他的裁定是「**现有东西全不动**」,所以要改的是岛的大小(`IslandTerrain.FlatR`)而不是搬东西");

            if (st.seaAnchor != null)
            {
                float sr = IslandTerrain.Radius(st.seaAnchor.position.x, st.seaAnchor.position.z);
                Check(sr > IslandTerrain.WaterR, "v0.57 ③:大海 那块可点板落在了岸内(r=" + sr.ToString("F1") +
                      " < 浪线 " + IslandTerrain.WaterR.ToString("F1") + ")⇒ 半透明蓝板会立在干沙上(烘焙器里它是从 WaterR 推的," +
                      "这条红了说明有人把那个偏移改回了写死的 z)");
                Check(sr <= IslandTerrain.BeachR + 4f, "v0.57 ③:大海 的板落在海面网格之外(r=" + sr.ToString("F1") + ")⇒ 点不到它");
            }
            var rod = IslandPhase.RodSeaPos();
            float rr = IslandTerrain.Radius(rod.x, rod.z);
            Check(rr > IslandTerrain.FlatR - 6f && rr < IslandTerrain.WaterR,
                  "v0.57 ③:钓竿 的落点离浪线不对(r=" + rr.ToString("F1") + ",浪线 " + IslandTerrain.WaterR.ToString("F1") +
                  ")—— 它必须 **在浪线之内、又贴着水**(v0.52「挪到海边」)。太远就成了一根插在沙滩中间的竿子");
            Debug.Log("[自检] v0.57:岛 = 中心 " + IslandTerrain.Center.ToString("F0") + " 干岛面 r=" + IslandTerrain.FlatR +
                      " / 沙滩到 r=" + IslandTerrain.BeachR + "(入水到 " + IslandTerrain.BeachDeepY + " 米)/ 浪线 r=" +
                      IslandTerrain.WaterR.ToString("F2") + " 且 SeaY=" + IslandTerrain.SeaY +
                      " | 大海 r=" + (st.seaAnchor != null ? IslandTerrain.Radius(st.seaAnchor.position.x, st.seaAnchor.position.z).ToString("F1") : "?") +
                      " | 钓竿 r=" + rr.ToString("F1") + " | 可走到 r=" + IslandTerrain.WalkR + " —— 五个数全部同源。");
        }

        // v0.58(用户:"**之前的岛屿改为外围一圈是沙滩,中间为草地(有椰树,灌木等植物),物品放在沙滩上,
        //   沙滩上最好有一些贝类/海带等物品;如果能有海浪更好**" + 四条定案:地面仍用解析网格 /
        //   贝类海带「**纯装饰,不可点**」/ 海浪「**演出:浪线一条会动的泡沫带**」)。
        //   这一轮最容易出的事仍然 **不是崩,而是"分区改了之后东西不在该在的地面上"**,所以四条都盯着落点:
        static void V058()
        {
            // ① 泡沫带与浪线同源(纯代码,不读场景)
            Check(Shoreline.InnerR < IslandTerrain.WaterR && Shoreline.OuterR > IslandTerrain.WaterR,
                  "v0.58 ①:泡沫带 r " + Shoreline.InnerR.ToString("F2") + "~" + Shoreline.OuterR.ToString("F2") +
                  " 没有跨过浪线 " + IslandTerrain.WaterR.ToString("F2") + " ⇒ 那条白沫会画在干沙上或海中央");

            // ② "中间为草地"不能是空话:缓包的包心必须在草地之内(绿的是内陆那一块,包是绿地里的起伏)
            Check(IslandTerrain.OnGrass(9f, -2f),
                  "v0.58 ②:缓包包心 (9,-2) 不在草地之内(草地外沿 r=" + IslandTerrain.GrassOuterR +
                  "、营地挖空 r=" + IslandTerrain.CampClearR + ")⇒ '中间为草地加植被'成了谎话,包会顶在沙地上");

            var st = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (st == null) { Debug.Log("[自检] v0.58 ③④:当前场景没开 Demo ⇒ 跳过这两条(其余仍照查)。"); return; }
            if (AssetDatabase.LoadAssetAtPath<Mesh>(DemoAssetBaker.IslandDiscAsset) == null ||
                AssetDatabase.LoadAssetAtPath<Mesh>(DemoAssetBaker.GrassPatchAsset) == null)
            {
                Debug.Log("[自检] v0.58 ③④:**跳过,不是通过** —— 还没跑菜单 Tools/60slike/④ 重烘荒岛层," +
                          "场景里仍是上一版几何(草地贴片与沙滩装饰都是烘焙器生成的,v0.60 起草地是一张单独的网)。");
                return;
            }

            // ③ **每个落点都必须在沙上** —— 这条就是用户那句"物品放在沙滩上"的钉子。
            //    判据与烘焙器同一个 `IslandTerrain.OnGrass`(不在检查里另写一份圆)。
            //    ⚠ **海鸥 显式除外**:它的挂点在 2.6 米空中,根本不落地(v0.53 把它从"走得到"里除名是同一个理由),
            //      拿它去判"在不在草地上"没有意义;大海 那块板在浪线外的沙带上,天然在沙上,不用除。
            var pts = new List<Transform>(st.propSlots);
            pts.Add(st.mateAnchor); pts.Add(st.bonesAnchor);
            pts.Add(st.fireAnchor);
            var rod = IslandPhase.RodSeaPos();
            int onGrass = 0; var who = new StringBuilder();
            foreach (var t in pts)
            {
                if (t == null) continue;
                if (!IslandTerrain.OnGrass(t.position.x, t.position.z)) continue;
                onGrass++; who.Append(" " + t.name);
            }
            if (IslandTerrain.OnGrass(rod.x, rod.z)) { onGrass++; who.Append(" 钓竿"); }
            Check(onGrass == 0, "v0.58 ③:有 " + onGrass + " 个落点落在 **草地之内**(" + who +
                  ")—— 用户要的是「物品放在沙滩上」。要么把营地挖空扩大(`IslandTerrain.CampClear / CampClearR`)," +
                  "要么把这些落点搬出草地,两者都行,但不能像现在这样东西长在草里");

            // ④ 沙滩装饰:按裁定 **纯装饰** ⇒ 一块 collider 都不许有(有它就会抢走身后那一格道具的点击/悬浮),
            //    而且不许压在任何一个落点上。这条同时钉住"到底烘出来没有"(0 件 = 这条没跑)。
            var dec = st.transform.Find("ShoreDecor");
            if (dec == null) { Check(false, "v0.58 ④:场景里没有 Island/ShoreDecor ⇒ 装饰这一层没烘出来(不是通过)"); return; }
            int n = dec.childCount, withCollider = 0, inGrass = 0, tooClose = 0; var bad = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                var c = dec.GetChild(i);
                if (c.GetComponent<Collider>() != null || c.GetComponentInChildren<Collider>() != null) { withCollider++; bad.Append(" " + c.name); }
                if (IslandTerrain.OnGrass(c.position.x, c.position.z)) inGrass++;
                foreach (var t in pts)
                {
                    if (t == null) continue;
                    if (new Vector2(t.position.x - c.position.x, t.position.z - c.position.z).magnitude < 1.0f) { tooClose++; break; }
                }
            }
            Check(n > 0 && withCollider == 0 && inGrass == 0 && tooClose == 0,
                  "v0.58 ④:沙滩装饰 " + n + " 件里 " + withCollider + " 件带 collider(" + bad +
                  ")、" + inGrass + " 件落在草地上、" + tooClose + " 件压着落点(<1 米)—— 裁定是「**纯装饰,不可点**」:" +
                  "带 collider 就会抢走道具的点击,落进草地就不是'沙滩上的贝类'");
            Debug.Log("[自检] v0.58:草地 = 岛心到 r=" + IslandTerrain.GrassOuterR + "(营地挖空 r=" +
                      IslandTerrain.CampClearR + " @ " + IslandTerrain.CampClear.ToString("F0") +
                      ") | 装饰 " + n + " 件(贝壳/海带,无 collider,只在外围沙带) | 泡沫带 r " + Shoreline.InnerR.ToString("F2") + "~" +
                      Shoreline.OuterR.ToString("F2") + "(浪线 " + IslandTerrain.WaterR.ToString("F2") +
                      ")—— 三个数全部由 `IslandTerrain` 推,没有第二份。");
        }

        // v0.61(用户:"**增加一下灌木植被的分布情况 / 第一人称模式加入shift奔跑 / 姓名·重置结局·日记日期显示
        //   均放在设置的子页面里(设置里还有音量、键位…) / 再检查一遍文案,删去调试测试文本**")
        //   这一轮能被静态钉住的是三件"改坏了没人发现"的事:
        static void V061()
        {
            // ① §12.6 那条名字安全规则 **拆成两个函数之后仍然成立**:过滤/截断/上限不变;
            //    空串在设置页保持空(要能清空),在开局兜默认名(老行为一个字没动)。
            Check(GameRoot.StripName("") == "" && GameRoot.StripName("ab<c>d\nef").Length == 6 &&
                  GameRoot.StripName("1234567890").Length == 8 && GameRoot.SanitizeName("") == "Daylily",
                  "v0.61 ①:名字安全规则在拆分后变了(§12.6:过滤 < > 与控制字符、上限 8、渲染不用 richText;" +
                  "设置页允许清空,开局空串才兜默认名)");

            // ② 音量四档:存的是 0~1,`Music.SetVolume` 必须夹住并落盘(滑出范围 = 静音或爆音,都不报错)
            float keep = SaveData.musicVolume;
            SaveData.SetMusicVolume(2f);
            bool hi = Mathf.Approximately(SaveData.musicVolume, 1f);
            SaveData.SetMusicVolume(-1f);
            bool lo = Mathf.Approximately(SaveData.musicVolume, 0f);
            SaveData.SetMusicVolume(keep);
            Check(hi && lo, "v0.61 ②:音乐音量没被夹在 0~1(存进去的是 " + SaveData.musicVolume + ")");

            // ③ **调试文本不许回到玩家看得见的地方**:标题屏那一角 "demo:…| 开发文档 v0.x" 已删(v0.61 文案轮),
            //    这条源码级断言防它哪天被"顺手加回来"(与 `V055()` 扫 Destroy 是同一种钉子)。
            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Core/GameRoot.cs");
            Check(!src.Contains("Ui.Label(rt, \"ver\""),
                  "v0.61 ③:标题屏又出现了玩家可见的版本角标(`Ui.Label(rt, \"ver\"…)`)—— 那是调试文本,要版本信息请走 Console");
            Debug.Log("[自检] v0.61:设置页 = 姓名/日期格式/音量四档/重置真结局/键位表(只读);奔跑 = 自由视角 Shift ×" +
                      IslandWalk.SprintMul + "(不耗任何数值);灌木改撒点循环(目标 16 丛,黄金角 + 间距过滤)。");
        }

        // v0.62(用户:"**背包改为3格,拾取的物品的小模型/贴图显示在物品栏里…标明占用的格子**" +
        //   "**开局拾荒时间改为15+30s,前15s自由活动查看,后30s才能拾取物品(15s后才出现漏电/起火)**")
        static void V062()
        {
            // ① 节奏:15 + 30 = 45,且查看期真的存在(0 或负数 = 这条裁定被改没了)
            Check(ScavengingPhase.LookSeconds > 0f && ScavengingPhase.PickSeconds > 0f &&
                  Mathf.Approximately(ScavengingPhase.TotalSeconds, 45f),
                  "v0.62 ①:拾荒节奏不是 15+30=45 秒(现在 " + ScavengingPhase.LookSeconds + "+" +
                  ScavengingPhase.PickSeconds + ")—— 用户这轮裁的就是这两个数");

            // ② **任何一件拾荒池里的东西都必须装得进背包** —— 潜水装置 4 格那次的死线:
            //    占格 > 携带条 ⇒ 那件永远带不走,而它挂在身上的行动(潜水/修/赶…)就变成只能看不能拿。
            int tooBig = 0; string who = "";
            foreach (var it in DB.scavengedItems)
            {
                if (it == null || it.slots <= ScavengingPhase.CarrySlots) continue;
                tooBig++; who += " " + it.displayName + "(" + it.slots + "格)";
            }
            Check(tooBig == 0, "v0.62 ②:有 " + tooBig + " 件拾荒池物品的占格 **超过携带条 " +
                  ScavengingPhase.CarrySlots + " 格**(" + who + ")⇒ 它们永远带不走,挂在它们身上的行动等于死了");

            // ③ 缩略图:有就是显示,没有就是"格子只显名字"(不报错)—— 但 **0 张要明说**,免得以为做了
            var dir = UnityEngine.Application.dataPath + "/Resources/Icons";
            int png = System.IO.Directory.Exists(dir) ? System.IO.Directory.GetFiles(dir, "*.png").Length : 0;
            if (png == 0)
                Debug.Log("[自检] v0.62 ③:**还没有任何缩略图**(携带条每格只显名字)—— 跑菜单 Tools/60slike/⑥ 生成物品栏缩略图。");
            else
                Debug.Log("[自检] v0.62 ③:缩略图 " + png + " 张(拾荒池 " + DB.scavengedItems.Count +
                          " 件 + 队友 3 件);没烘到的件在携带条里只显名字,不报错。");
        }

        // v0.63(用户:"**漏电/火焰/机舱/舱门/储物箱等文字都不要出现了**" + "**查看期不会出现危险,危险均是15s后才出现**")
        static void V063()
        {
            // ① 机舱那层"印在表面上"的标牌照这轮裁定 **不印字**(它覆盖 v0.49 那条"标牌照旧保留",§11-80 划的边界改了)
            Check(!CabinAnchors.CabinSignsVisible,
                  "v0.63 ①:机舱那层的标牌又印字了(`CabinAnchors.CabinSignsVisible`)—— 用户这轮要的是「漏电/火焰/机舱/舱门/储物箱等文字都不要出现」");

            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Phases/ScavengingPhase.cs");
            // ② 危险方块上那句文字同样收掉;并且 hazard **开局整块不在场**(建好即关、查看期结束才点亮)
            //   ⇒ "扣 2 秒"这件事在查看期根本不可能发生,不是"扣不到",是那块方块连同触发器都不存在。
            Check(!src.Contains("+ \"(-2秒)\""),
                  "v0.63 ②:危险方块又开始印字了(`name + \"(-2秒)\"`)—— 这一层的文字这轮全收");
            Check(src.Contains("go.SetActive(false);") && src.Contains("!hazardsOn && !Looking"),
                  "v0.63 ②:漏电/起火 不再是「15 秒后才出现」—— 「建好即关」与「查看期结束才点亮」这两句少了一句(用户:危险均是 15s 后才出现)");

            // ③ 那条 NRE 的根因钉住:`Ui.Panel` 建出来的物体 **本来就带 Image**,
            //    再 `AddComponent<Image>()` 会被 Unity 拒绝并 **返回 null**(实测:携带条第一格就炸,整条只剩 1 格)。
            Check(!src.Contains(".gameObject.AddComponent<Image>()"),
                  "v0.63 ③:又有地方在 `Ui.Panel` 的结果上 `AddComponent<Image>()` —— 第二份会被拒并返回 null;要带图的块走 `Ui.Icon`");
        }

        // v0.64(用户:"**物品损坏后白天也不能使用,检查一下逻辑**")
        static void V064()
        {
            // ① 破损件的"不可用"在数据层是 `Has`(只数完好件)⇒ 这一条先确认地基没被谁挪走:
            //    三件会坏的 白天工具 各自 MarkBroken 之后都必须 `Has == false`。
            var inv = new Inventory();
            int wrong = 0, probed = 0; string who = "";
            foreach (var t in new[] { DB.DiveGear, DB.Spear, DB.SimpleMask })
            {
                if (t == null) continue;
                probed++;
                inv.Add(t, 1); inv.MarkBroken(t);
                if (inv.Has(t)) { wrong++; who += " " + t.displayName; }
                if (!inv.HasBroken(t)) { wrong++; who += " " + t.displayName + "(没进破损态)"; }
            }
            Check(probed == 3, "v0.64 ①:潜水装置 / 鱼叉 / 简易浮镜 这三件白天工具少了一件(只探到 " + probed +
                  " 件)⇒ 这条断言本身要跟着这一轮的设计走,别让它悄悄变成'查了两件也算过'");
            Check(wrong == 0, "v0.64 ①:白天那三件会坏的工具(" + who + ")坏后仍被算成可用 ⇒ '坏了不能用'的地基没了");

            // ② 那条漏路的钉子:**点一件东西 = 用这一件**,所以 `UseItem` 里必须先落在 **这一件** 自己身上
            //   (`S.storage.Has(k, 1)`),只有行动的闸(`CanDo`)是不够的 —— 潜水那条闸是"三件任一完好"。
            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Phases/IslandPhase.cs");
            Check(src.Contains("if (!S.storage.Has(k, 1))"),
                  "v0.64 ②:`UseItem` 里那道'这一件自己完好才能用'的闸不见了(只剩行动的闸 ⇒ 拿完好的潜水装置去点坏鱼叉也能下水)");
            // ③ v0.64(用户:"**信号火堆直接替代火堆**" + 定案「**升级替换:篝火建到信号火堆就变成它**」):
            //    营地里 **同一时刻只可能有一档火**,升级 = 重新点燃(补满两晚,不继承剩余晚数)。
            var s3 = new RunState();
            s3.Build(Database.Campfire); s3.Relight(Database.Campfire);
            s3.Build(Database.SignalFire); s3.Relight(Database.SignalFire);   // DoCraft 那一支:建 = 当场点着
            Check(!s3.structures.Contains(Database.Campfire) && !s3.brokenStructures.Contains(Database.Campfire)
                  && s3.structures.Contains(Database.SignalFire),
                  "v0.64 ③:升级之后营地里 **两档火并存**(篝火 还在 structures/破损账里)—— 用户裁的是「替代」,画面上是同一格那一堆");
            Check(s3.HasStructure(Database.SignalFire) &&
                  s3.NightsLeft(Database.SignalFire) == RunState.FireNightsPerLighting,
                  "v0.64 ③:「升级 = 重新点燃」没落地(现在着没=" + s3.HasStructure(Database.SignalFire) +
                  " 还剩 " + s3.NightsLeft(Database.SignalFire) + " 晚,该是 " + RunState.FireNightsPerLighting + " 晚)");
            Check(s3.fireLitTonight && s3.SignalFireBurning,
                  "v0.64 ③:灶与免检这两条判据在升级后必须一起成立(fireLitTonight / SignalFireBurning)");
            // ④ "垒 篝火" 那一行不能因为 campfire 退场而 **重新出现** —— 那等于花 3 材料把信号火堆 降级回去。
            Check(s3.StructureEverBuilt(Database.Campfire),
                  "v0.64 ④:升级后 `StructureEverBuilt(campfire)` 成了 false ⇒ 制造面板会把「垒 篝火」再摆一次,可以反向降级");
            // ⑤ 灭着(= 坏)的那一堆照样能垒高:破损那本账要跟着一起走,不能留一行"重新点燃 篝火"
            var s4 = new RunState();
            s4.Build(Database.Campfire); s4.Extinguish(Database.Campfire);
            s4.Build(Database.SignalFire); s4.Relight(Database.SignalFire);
            Check(s4.HasStructure(Database.SignalFire) && !s4.brokenStructures.Contains(Database.Campfire)
                  && s4.NightsLeft(Database.Campfire) == 0,
                  "v0.64 ⑤:篝火 灭着时升级 ⇒ 破损账/计数器没被带走(维修面板会出现一行点不着的「篝火」)");

            Debug.Log("[自检] v0.64:白天'坏了不能用'= 数据层 Has 只数完好件 + 点击那一道闸落在 **这一件** 上;" +
                      "夜晚那侧本来就按完好件算目标(`NightToolFor`/选项生成都走 Has),所以两条路一条判据。" +
                      "火 = **一摊两档**:垒 篝火(材料3)→ 垒高成 信号火堆(材料6),同一格、建好即点着、烧 " +
                      RunState.FireNightsPerLighting + " 晚、会被涨潮打灭、免检只在它真烧着的那几晚。");
        }

        // v0.65(用户:"**1.升级免火种 / 2.不要显示剩余晚数,只显示生存了xx天,或者直接Dxx / 3.复用放大**")
        static void V065()
        {
            // ① 升级免火种 —— 这条读的是 **资产**,所以它同时证明"菜单① 里那条迁移跑过了"
            //   (① 的约定是"已有的不覆盖":没跑迁移 ⇒ 运行时仍读到 true ⇒ 升级照吃火种、面板那行一直挂着" + 打火石")。
            var sf = R("signalfire");
            Check(sf != null && !sf.consumesFlint,
                  "v0.65 ①:信号火堆 在建造那一手仍吃火种(现在 " + (sf == null ? "配方读不到" : sf.consumesFlint.ToString()) +
                  ")—— 跑菜单 Tools/60slike/①,里面有一条只改这个字段的迁移");

            // ② 天数的写法只有一条出处:`SaveData.Date()`(设置 › 日记日期显示 那颗开关管的就是它)
            int keep = SaveData.dateMode;
            SaveData.ToggleDateMode(); string first = SaveData.Date(12);
            SaveData.ToggleDateMode(); string second = SaveData.Date(12);
            Check(SaveData.dateMode == keep &&
                  ((first == "D12" && second == "第 12 天") || (first == "第 12 天" && second == "D12")),
                  "v0.65 ②:日期两档写法变了(现在 " + first + " / " + second + ")—— HUD 与 日记 都读这一条,改它等于同时改两处");
            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Phases/IslandPhase.cs");
            Check(src.Contains("Ui.Label(rt, \"day\", SaveData.Date("),
                  "v0.65 ②:HUD 那行天数又改成自己拼字符串了(应当走 `SaveData.Date`,与 日记 同一条真源)");

            // ③ **剩余晚数不再出现在玩家眼前**(数值一条没动:计数器 / 清晨减一 / 到点熄灭 / 免检窗口全留着)
            Check(!src.Contains("S.NightsLeft("),
                  "v0.65 ③:画面层又开始读剩余晚数了(用户:不要显示剩余晚数)—— 计数只服务夜晚结算,不服务画面");

            // ④ 信号档 **复用篝火的模型并放大一档**,但账上两档仍是两个 key(免检那条要单独认信号档)
            Check(src.Contains("FireModelKey(key == Database.Wall ? null : key)") &&
                  src.Contains("World.HasModel(FireModelKey(Database.SignalFire))"),
                  "v0.65 ④:信号档的模型口子不在了(`FireModelKey`)—— 它该复用 篝火 的两态模型、吃自己那格的身量(2.4 vs 1.7)," +
                  "而不是去改 structures 的 key");
            Debug.Log("[自检] v0.65:升级 = 材料6 + 2 体力、**不吃火种**,照样当场点着两晚;" +
                      "火的牌子只剩名字(灭了多两个字「熄了」);HUD 天数走 `SaveData.Date`(D12 / 第 12 天,与日记同一开关);" +
                      "信号档复用篝火的模型放大一档。");
        }

        // v0.66(用户:"**再检查一下文案,有关提示/概率的文字都不要出现**"(裁定范围:**只摘概率数字**,
        //   代价数值与操作提示留着) + "**小影怪的直接睡觉和正常的尝试睡去合并**" +
        //   "**彩蛋界面可以选择并查看详情,不要直接把描述和物品放一起(结局也这么处理)**")
        static void V066()
        {
            // ① **玩家能看见的那几个文件里,代码里的字符串不许出现 "数字%" 或 `+ "%"`**
            //   (整行注释与行尾注释不算 —— 那些是给开发看的;要留一条真·数值,就在行尾写 `// 允许:` 并说明它不是概率)
            int bad = 0; var where = new StringBuilder();
            foreach (var f in new[] { "Phases/IslandPhase.cs", "Phases/NightPhase.cs", "Phases/ScavengingPhase.cs",
                                      "Core/GameRoot.cs", "Core/DiaryLines.cs" })
            {
                var path = UnityEngine.Application.dataPath + "/Scripts/" + f;
                if (!System.IO.File.Exists(path)) { bad++; where.Append(" " + f + "(读不到)"); continue; }
                var lines = System.IO.File.ReadAllLines(path);
                for (int li = 0; li < lines.Length; li++)
                {
                    var line = lines[li].Trim();
                    if (line.StartsWith("//")) continue;
                    int cut = line.IndexOf("//");
                    string code = cut >= 0 ? line.Substring(0, cut) : line;
                    string note = cut >= 0 ? line.Substring(cut) : "";
                    if (!System.Text.RegularExpressions.Regex.IsMatch(code, "[0-9]+\\s*%") && !code.Contains("+ \"%\"")) continue;
                    if (note.Contains("允许:")) continue;
                    bad++; where.Append(" " + f + ":" + (li + 1));
                }
            }
            Check(bad == 0, "v0.66 ①:玩家可见文案里又出现了概率数字(" + bad + " 处:" + where +
                  ")—— 用户:有关提示/概率的文字都不要出现;要留代价数值请挂 `// 允许:` 并写明它不是概率");

            // ①b **资产那一侧**(夜晚选项括号、事件描述、彩蛋说明)由 `V069()` 判红 —— v0.69 用户裁定
            //     "所有概率文案都隐藏",那三处不再只是"待裁定",所以钉子也从"只报数"升成断言。

            // ② 小影怪 那两行合并成一条:「直接睡觉」整件退场,「啥也不做 / 躲起来」(睡去的落点)必须在
            var ss = DB.SmallShadow;
            Check(ss != null && ss.choices != null, "v0.66 ②:读不到 小影怪 事件(名单本身没了 ⇒ 这轮判定无从校起)");
            int alone = 0, nothing = 0;
            if (ss != null && ss.choices != null)
                foreach (var c in ss.choices)
                {
                    if (c == null) continue;
                    if (c.key == "sleepalone") alone++;
                    if (c.key == "nothing") nothing++;
                }
            Check(alone == 0, "v0.66 ②:小影怪 的选项里还有 " + alone + " 条「直接睡觉」—— 它与通用「尝试睡去」在" +
                  "「没有队友」那一晚走的是 **同一个结算分支**(!mate.present ⇒ 次日体力 -1),所以面板上是两行同一件事。" +
                  "跑菜单 Tools/60slike/① 走那条迁移(烘焙器里这行已经不写了)");
            Check(nothing == 1, "v0.66 ②:小影怪 少了那条 alwaysAvailable 的「啥也不做」—— 「尝试睡去」就是靠它落地的(合并后它是唯一出口)");

            // ③ 收集页 = 左选右读;行号必须在建按钮时 **当场抓住**(写成 `collectRowTexts.Count` 会让每一行都选中最后一行)
            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Core/GameRoot.cs");
            Check(src.Contains("int row = detailRows.Count;") && src.Contains("() => SelectDetail(row)"),
                  "v0.66 ③:列表页的闭包又直接读了可变的列表长度 ⇒ 点哪行都选中最后一行(行号要当场抓住)");
            Check(!src.Contains("it.displayName + \" — \" + it.desc"),
                  "v0.66 ③:收集页又回到\"名字 — 说明全文 堆在一行\"(用户:不要直接把描述和物品放一起)");
            Debug.Log("[自检] v0.66:**代码里**拼给玩家的文案 0 处概率数字(代价数值/操作提示按裁定留着,靠行尾 `// 允许:` 白名单);" +
                      "资产那一侧(夜晚选项括号 / 事件描述 / 彩蛋说明)由 v0.69 那条判红;" +
                      "小影怪 = 一条「尝试睡去」;收集页 = 左栏只列名字、右边出详情(不写达成条件)。");
        }

        // v0.67(用户给出渔网来源页 + 问"**署名页怎么做**")⇒ 致谢页进游戏 + 渔网许可登记成 CC BY 4.0
        static void V067()
        {
            // ① 玩家可见的署名文本 **只有一份**:`Assets/Resources/Text/Credits.txt`(打包后 Resources.Load 读得到)
            var txtPath = UnityEngine.Application.dataPath + "/Resources/Text/Credits.txt";
            Check(System.IO.File.Exists(txtPath),
                  "v0.67 ①:读不到 Assets/Resources/Text/Credits.txt —— 致谢页会是空的;而且 **不在 Resources 下的文本进不了包**");
            string txt = System.IO.File.Exists(txtPath) ? System.IO.File.ReadAllText(txtPath) : "";
            // 三条硬义务各查一条:CC BY 的两处署名(音乐 / 渔网)与 OFL 的那条(字体)
            Check(txt.Contains("Kevin MacLeod") && txt.Contains("creativecommons.org/licenses/by/4.0"),
                  "v0.67 ①:音乐那段 CC BY 4.0 的署名串没在致谢文本里(这是本项目第一批「必须署名」的素材)");
            Check(txt.Contains("cozee4sure"), "v0.67 ①:渔网的署名没写(kaykit/quaternius 是 CC0 可以不说,**渔网是 CC BY,必须说**)");
            Check(txt.Contains("Open Font License") || txt.Contains("OFL"),
                  "v0.67 ①:字体那条 OFL 1.1 说明没写(它不要求界面署名,但要求 **随包带许可证文本** —— 这一行是告诉玩家它在哪)");

            // ② 主菜单必须有那颗入口(与收集页同一级;挂在屏幕外的那种"看着做了其实看不见"已栽过两次)
            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Core/GameRoot.cs");
            Check(src.Contains("Ui.Button(rt, \"credits\"") && src.Contains("OpenCredits"),
                  "v0.67 ②:致谢页的入口不在主屏上(代码里有页、界面上没按钮 = 玩家找不到)");

            // ③ 台账 **不抄玩家文案**:ATTRIBUTION.md 记许可依据/URL/取证,署名串只在 txt 里有一份
            var md = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Art/ATTRIBUTION.md");
            Check(!md.Contains("Music: \"Feral Chase\""),
                  "v0.67 ③:ATTRIBUTION.md 里又抄了一份游戏内署名串 ⇒ 两处会不同步(改成指向 Resources/Text/Credits.txt)");
            Check(md.Contains("cozee4sure") && md.Contains("sketchfab.com") && !md.Contains("许可未确认"),
                  "v0.67 ③:渔网还挂在「许可未确认」那一节 —— 用户这轮给了来源页(API 核到 CC BY 4.0),台账要跟着改口径");

            Debug.Log("[自检] v0.67:致谢页 = 主屏第四颗,文本只有一份(Resources/Text/Credits.txt);" +
                      "义务三档各记各的 —— CC BY(音乐 6 首 / 渔网)要署名,OFL(字体)要随包带许可文本,CC0(KayKit/Quaternius)什么都不用做。");
        }

        // v0.68(用户:"**致谢的文字排版有问题**" —— 截图:详情正文第一行压在标题上;顺带"简化留用"这条裁定落地,
        //        ⚠ 那半句已被 **v0.69「那就不简化了」撤销**,这里只留历史记录,钉子是排版那四块常量)
        // v0.69(用户:"**所有概率文案都隐藏**")⇒ 玩家看得见的三个 **资产** 出处一起清零,并且文字只有一个出处
        static void V069()
        {
            // ① 三条上屏入口:**夜晚选项括号(`resultHint`)/ 事件描述(`description`)/ 彩蛋说明(`desc`)**
            //    都不许再出现"机会有多大"这一类写法(数字百分比、概率/几率/掷、以及 "3:2:1" 这种权重)。
            //    ⚠ 范围只算 **有读者的那三处**:`ActivitySO.desc` 与 `RecipeSO.desc` 全工程没有读者(Inspector 文档),
            //      把它们算进来就会把"设计说明里写概率"也判红 —— 那不是玩家看见的东西(v0.66 划的边界)。
            var re = new System.Text.RegularExpressions.Regex("[0-9]+\\s*%|概率|几率|掷|[0-9]+\\s*:\\s*[0-9]+");
            int dirty = 0; var where = new StringBuilder();
            foreach (var typeName in new[] { "GameChoiceSO", "GameEventSO" })
                foreach (var gu in AssetDatabase.FindAssets("t:" + typeName))
                {
                    var p = AssetDatabase.GUIDToAssetPath(gu);
                    var o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
                    var c = o as GameChoiceSO;
                    string v = c != null ? c.resultHint : (o as GameEventSO != null ? (o as GameEventSO).description : null);
                    if (string.IsNullOrEmpty(v) || !re.IsMatch(v)) continue;
                    dirty++; where.Append(" " + System.IO.Path.GetFileNameWithoutExtension(p));
                }
            foreach (var e in TitlePhase.EggItems(DB))
            {
                if (e == null || string.IsNullOrEmpty(e.desc) || !re.IsMatch(e.desc)) continue;
                dirty++; where.Append(" 彩蛋:" + e.key);
            }
            Check(dirty == 0, "v0.69 ①:玩家看得见的文案里又出现概率写法(" + dirty + " 处:" + where +
                  ")—— 用户:" + "\u300c所有概率文案都隐藏\u300d" + ";代价数值(体力/生命/材料/占格)不算,但它要在文案里说清后果");

            // ② **文字只有一个出处**:`DemoAssetBaker.PlayerText` 这张表既被调用点 `P("id")` 取,也被 菜单①
            //    的迁移按资产文件名刷 ⇒ 表自己必须干净(表脏 = 重烘一批干净资产也没用,反过来也一样)。
            int badText = 0;
            foreach (var kv in DemoAssetBaker.PlayerText)
                if (re.IsMatch(kv.Value ?? "")) { badText++; where.Append(" 表:" + kv.Key); }
            Check(badText == 0, "v0.69 ②:唯一出处那张表里有 " + badText + " 条还写着概率(" + where + ")");

            // ③ 表必须仍然盖住这三处:18 条夜晚提示 + 1 条事件描述 + 10 条彩蛋说明。
            //    ⚠ `P("id")` 取不到 key 时给的是 **空串**(按钮括号整段消失、收集页右栏空白),
            //      所以"有人删了表项"这种事故必须在这里响,而不是等玩家截图。
            Check(DemoAssetBaker.PlayerText.Count >= 29,
                  "v0.69 ③:玩家可见文案表只剩 " + DemoAssetBaker.PlayerText.Count + " 条(应有 18+1+10 = 29)" +
                  " ⇒ 少一条就少一块上屏文字(取不到 key 会静默给空串)");

            Debug.Log("[自检] v0.69:夜晚选项括号 / 事件描述 / 彩蛋说明 三处 0 处概率写法(文字唯一出处 = DemoAssetBaker.PlayerText);" +
                      "渔网 按「不简化」保持现状;死字段 signalFireUsed 已清。");
        }

        static void V068()
        {
            // ① **正文的上边必须在标题的下边之下**。上一版写的是 `(-66, -14)`:矩形方向是对的
            //   (所以 `Ui` 那条"负尺寸"警告不会响),但 **上边放到了 -14,正好盖住标题(-12 ~ -42)** ——
            //   "位置错、形状对"这一类只有把上下边写成具名常量才判得出来。
            Check(TitlePhase.DetailBodyTop <= TitlePhase.DetailHeadBottom,
                  "v0.68 ①:详情页正文的上边(" + TitlePhase.DetailBodyTop + ")又跑到标题下边(" + TitlePhase.DetailHeadBottom +
                  ")之上了 ⇒ 标题与正文第一行会叠成一片(用户截图那个症状)");
            // ⚠ **比较号写反过一次(这条自检第一次跑就红了)**:这套 offset 是 **y 向上**,-48 比 -560 高,
            //   所以"上边高于下边"要写 `>`,不是 `<`。这类"断言自己带 bug"的教训:v0.69 那条陈旧断言同一族 ——
            //   **新写一条比大小的钉子时,要拿它已知的真值代进去念一遍**,不能只看消息文本顺不顺。
            Check(TitlePhase.DetailBodyTop > TitlePhase.DetailBodyBottom,
                  "v0.68 ①:正文矩形自身反了(上边 " + TitlePhase.DetailBodyTop + " 应高于下边 " + TitlePhase.DetailBodyBottom + ")");

            // ② **左栏最后一行不许越过框底**(v0.66 那条"最后一行画在框外"的同类):按实际名单数算一遍
            int rows = 0;
            if (DB.endingTable != null) rows += DB.endingTable.Count;
            rows += TitlePhase.EggItems(DB).Length + 2;          // 两个组标题
            float lastTop = TitlePhase.DetailRowTop - rows * TitlePhase.DetailRowPitch;
            Check(lastTop >= TitlePhase.DetailPaneBottom - 40f,
                  "v0.68 ②:名单排到 " + lastTop + " 已经越过框底 " + TitlePhase.DetailPaneBottom +
                  "(行数 " + rows + ")⇒ 最后一两行会画在面板外(不报错、不进 Hierarchy 异常)");

            // ③ 左栏是名单不是按钮阵列:文字必须左对齐;选中态必须有底色(只改字色在深色底上几乎看不出来)
            var src = System.IO.File.ReadAllText(UnityEngine.Application.dataPath + "/Scripts/Core/GameRoot.cs");
            Check(src.Contains("t.alignment = TextAnchor.MiddleLeft"),
                  "v0.68 ③:左栏又回到居中排版(名单要左对齐才读得成列表)");
            Check(src.Contains("detailRowBgs[i].color"),
                  "v0.68 ③:选中态又只剩字色了(底色那条高亮要在)");
            Debug.Log("[自检] v0.68:详情页四块位置都是具名常量(标题 " + TitlePhase.DetailHeadTop + "~" + TitlePhase.DetailHeadBottom +
                      " / 正文 " + TitlePhase.DetailBodyTop + "~" + TitlePhase.DetailBodyBottom + " / 框底 " + TitlePhase.DetailPaneBottom +
                      "),名单 " + rows + " 行仍在框内;" +
                      "渔网 按 v0.69 那句「不简化」保持现状(86k 三角,§11-112 那三步只当备用手册)。");
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
