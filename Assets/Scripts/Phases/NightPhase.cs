using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // §4.5 优先级 T > I > H > A > B > F > C;F 只在荒岛阶段判(拾荒归零 = C 的坠机分支)
    public static class EndingResolver
    {
        public static EndingId DeathEnding(RunState S)
        {
            return S.hidden.nameTagTaken ? EndingId.C : EndingId.F;
        }
    }

    // §3.1 夜晚结算三步次序(写死):①两条独立 20%(轮船/幽灵船)②20% 今夜无事 ③轮空池
    // v0.14:这一层 只负责"掷什么 + 结算成什么样",呈现全部交给 IslandPhase 的那张场景
    //       (夜晚与白天是同一套界面,区别只是夜晚只能使用道具)。不再画弹层。
    public static class NightResolver
    {
        static GameRoot root; static RunState S; static Database DB; static BalanceConfig B;
        static GameEventSO cur;
        static ItemSO stolen;
        // v0.33:友善的猴子 那一对(它要收走的 / 它带来的)在事件摊开那一晚就定好,玩家才看得见
        static ItemSO tradeGive, tradeGot;
        static bool shipAlsoHit;

        // v0.46(用户):"应对事件都是点对应的道具" ⇒ 夜晚也要知道 **他这一下点的是哪件**。
        //   原来 血月 的"下水"是 `BoundItem` 替你挑("有潜水装置就用它,否则鱼叉"),两件事都归它管:
        //   产出与损坏落在哪件上、以及那块牌子亮不亮。这里记下点击的那一件,结算只认它。
        //   ⚠ 每摊开一个事件就清一次(Show),点地面物体(篝火/大海/队友)时它是 null ⇒ 走原来的选路。
        public static ItemSO NightTool { get; set; }

        public static GameEventSO Current { get { return cur; } }
        public static bool Calm { get; private set; }
        public static EndingId Pending { get; private set; }
        public static bool HasPending { get; private set; }

        // 天亮由场景那层的"尝试睡去"推进,这里不再持有回调
        public static void Begin(GameRoot r)
        {
            root = r; S = r.state; DB = r.db; B = r.db.bal;
            Calm = false; HasPending = false; Pending = EndingId.None;
            S.Log("—— 第 " + S.day + " 夜 ——");
            shipAlsoHit = false;

            // 影怪:第 20~25 天必出 1 次,oncePerRun,之后永久移出池
            if (!S.hidden.shadowNightResolved && S.day >= B.shadowNightMin && S.day <= B.shadowNightMax)
            {
                Show(DB.Shadow); return;
            }

            // ① 两条彼此独立的 20%(v0.12);同晚双命中 → 幽灵船优先且不消费轮船那次
            // v0.24:骸骨出现过之后,幽灵船那一条从 20% 抬到 30%(仍然必须先丢过 2 个漂流瓶)
            float ghostP = S.hidden.bonesSeen ? B.ghostShipBonesChance : B.rescueRollPerNight;
            bool ghostHit = S.bottle.thrownBottles >= 2 && !S.bottle.ghostShipResolved && Roll(ghostP);
            bool shipHit = S.bottle.thrownBottles >= 1 && !S.bottle.shipResolved && Roll(B.rescueRollPerNight);
            if (ghostHit) { shipAlsoHit = shipHit; Show(DB.Ghost); return; }
            if (shipHit) { Show(DB.Ship); return; }

            // 骸骨(v0.24:从 涨潮 里独立出来)—— 整局一次,每晚掷一次直到它出现
            if (!S.hidden.bonesSeen && Roll(B.bonesNightlyChance)) { Show(DB.Bones); return; }

            // v0.27:原来这里有一条 "planeStage==1 → 2~4 夜后插播 搜寻飞机(第二次)" 的线,整个删除。
            //        第一发打出去之后不再有任何插播:搜寻飞机 本来就在常规轮空池里,
            //        下一次它自己轮上来的时候,那一发就是"第二发"(见 Special 的 searchplane 分支)。

            // 两点光:影怪过后的补救窗口(需要 箱+钥匙,且没打过 T)
            if (S.hidden.shadowNightResolved && !S.hidden.twoLightsSeen && S.hidden.hasChest && S.hidden.hasKey
                && !S.hidden.offeredThisRun && !SaveData.trueEndingClaimed)
            {
                Show(DB.TwoLights); return;
            }

            // ② 20% 今夜无事发生
            if (Roll(B.calmNightChance))
            {
                Calm = true;
                S.Diary(DiaryLines.Calm(S.day), DiaryLineKind.Night);   // v0.30:无事的一夜也要在日记里留一行
                return;
            }

            // ③ 轮空池
            Show(NextFromBag());
        }

        // 天亮收尾:把这一晚的静态状态清干净,下一晚重新掷
        public static void End()
        {
            cur = null; stolen = null; Calm = false;
            tradeGive = null; tradeGot = null;
            HasPending = false; Pending = EndingId.None;
        }

        static bool Roll(float p) { return S.rng.NextDouble() < p; }

        // ---------------- v0.30 日记:夜里那一条 ----------------
        // 分支只负责把"结果不同"的那几种改一下 key 或参数,落笔统一在 Choose 末尾。
        // v0.33:参数改成 params —— 友善的猴子那句要同时点名"换出哪件、换入哪件"。
        static string diaryKey;
        static object[] diaryArgs;

        static void Diary(string key, params object[] args) { diaryKey = key; diaryArgs = args; }

        static void WriteNightDiary(GameChoiceSO c)
        {
            string key = diaryKey ?? (c != null ? c.key : null);
            string t = DiaryLines.Night(cur != null ? cur.key : null, key, S.day, diaryArgs);
            if (t != null) S.Diary(t, DiaryLineKind.Night);
            diaryKey = null; diaryArgs = null;
        }

        static GameEventSO NextFromBag()
        {
            var ev = EventBagSystem.Draw(S, DB.bagEvents);
            if (ev.variants.Count > 0) return EventBagSystem.PickVariant(ev, S.rng);
            return ev;
        }

        // ---------------- 呈现交给场景,这里只留"这一晚是什么" ----------------
        static int redraws;

        static void Show(GameEventSO e)
        {
            // v0.24:围墙挡住 涨潮 与 毒蛇 —— 这两场当晚直接不出现,改抽轮空池里的下一件。
            //        铭牌不再受这件事影响:骸骨是独立事件(见 DB.Bones),围墙不挡它。
            // v0.45:假队友 跟着队友走 —— 队友永久消失之后,营地边不会再站出一个"他"。
            string blocked = BlockedReason(e);
            if (blocked != null)
            {
                S.Log(blocked);
                // v0.46(用户):"围墙也会损坏,按标准的损坏概率" ⇒ 它每挡下一场 涨潮 / 毒蛇 就算"用了一次",
                //        由 IslandPhase 那一本按 key 累积的建筑概率账掷一次(20% 起、+10%/次、上限 90%)。
                //        挡还是挡成的 —— 掷的是"这一晚撞完之后它裂没裂",所以裂了也不回溯放过这场。
                if (BlockedByWall(e) && root != null && root.island != null) root.island.WearStructure(Database.Wall);
                if (++redraws > 8) { redraws = 0; cur = null; Calm = true; return; }  // 兜底:池里只剩这几件
                Show(NextFromBag()); return;
            }
            redraws = 0;
            cur = e;
            NightTool = null;
            // v0.46(用户):"如果有信号火堆,则再刷新 轮船/幽灵船/飞机 的时候不需要选择道具,直接判定通过"
            //   ⇒ 这一晚在摊开的那一瞬间就定了。写在 Show 而不是 AvailableChoices,是因为三条救援来源
            //     (轮船 / 幽灵船 走两条独立 20%、搜寻飞机 走轮空池)全都从这一个入口进,一处就盖住。
            if (e != null && S.SignalFireBurning &&
                (e == DB.SearchPlane || e == DB.Ship || e == DB.Ghost))
            {
                Pending = e == DB.Ghost ? EndingId.H : EndingId.A;
                HasPending = true;
                S.Diary(DiaryLines.Night(e.key, "signalfire", S.day), DiaryLineKind.Night);
                S.Log("信号火堆 在沙丘上烧着:它老远就看见了 —— " + e.displayName + " 这一晚不需要你做任何应对(→ 结局" + Pending + ")。");
                return;
            }
            stolen = null;
            tradeGive = null; tradeGot = null;
            // v0.24:"出现过骸骨"这件事在它被摊开的那一晚就成立(幽灵船 30% 的开关),
            //        与玩家当晚有没有走过去看无关 —— 看不看只决定铭牌拿不拿(= 结局F 的判定物)。
            if (e == DB.Bones) S.hidden.bonesSeen = true;
            if (e == DB.MonkeyNaughty)
            {
                if (PeekStealable(S) == null) { Show(NextFromBag()); return; }   // 兜底:仓库无可偷之物 → 这个事件不出现
                stolen = RandomStealable(S);
            }
            // v0.33:友善的猴子 在摊开这一晚就把"换出哪件 / 换入哪件"定下来,好让它能事先显示在屏幕上;
            //        点"交易"只是执行这一对,不再另掷一次(否则屏幕上说的与发生的可以不是同一件事)。
            if (e == DB.MonkeyFriendly) PickTrade();
        }

        static bool BlockedByWall(GameEventSO e)
        {
            if (!S.HasStructure(Database.Wall)) return false;
            return e == DB.Snake || e == DB.Tide;     // v0.24:涨潮不再需要"首次那晚放它进来"的例外了
        }

        // 返回 null = 这一晚它可以出现;否则返回那句"为什么进不来"(打日志用)。
        static string BlockedReason(GameEventSO e)
        {
            if (BlockedByWall(e)) return "围墙挡住了 " + e.displayName + ":它这一晚进不来。";
            if (NoTeammateBlocks(e, S)) return "营地里已经没有队友了:假队友 不再出现。";
            return null;
        }

        // v0.45:队友一旦永久消失,营地边就不会再站出一个"他"。写成纯函数是为了让 DemoChecks 直接钉住它。
        public static bool NoTeammateBlocks(GameEventSO e, RunState s)
        {
            return e == DB.FakeMate && !s.mate.present;
        }

        public static string StolenName
        {
            get { return stolen != null ? stolen.displayName : null; }
        }

        public static string TradeGiveName
        {
            get { return tradeGive != null ? tradeGive.displayName : null; }
        }

        // 换入那件可能还没有(已集齐 → 它改给 2 份罐头),界面按 null 显示"它没带新东西来"
        public static string TradeGetName
        {
            get { return tradeGot != null ? tradeGot.displayName : null; }
        }

        // v0.33:这两件的抽法与 MonkeyTrade 原来内联的写法逐条一致(排除表、渔网那道闸都不动),
        //        只是从"点交易那一刻"提前到"摊开这一晚",这样才敢显示给玩家。
        static void PickTrade()
        {
            tradeGive = null; tradeGot = null;
            var give = new List<ItemSO>();
            foreach (var kv in S.storage.ok)
                if (kv.Key.tradeable && !kv.Key.lore && kv.Key != DB.Can && kv.Key != DB.Material) give.Add(kv.Key);
            if (give.Count > 0) tradeGive = give[S.rng.Next(give.Count)];

            var want = new List<ItemSO>();
            foreach (var it in DB.scavengedItems)
            {
                if (!it.tradeable || it.lore) continue;
                if (it == DB.Can || it == DB.Material || it == DB.Bottle) continue;
                if (S.hidden.everOwned.Contains(it)) continue;
                if (it == DB.Net && S.storage.Total(DB.Net) > 0) continue;
                want.Add(it);
            }
            if (want.Count > 0) tradeGot = want[S.rng.Next(want.Count)];
        }

        // 这一晚现在能选的选项(选项"不生成",不是"点了被拒":§3.2 猴子行 / §11-31)
        public static List<GameChoiceSO> Available()
        {
            return cur == null ? new List<GameChoiceSO>() : AvailableChoices(cur);
        }

        // 场景里那件东西今晚能做的那件事:先按 requiresItem 认,再按 key 认数据里没填物品的几件
        public static List<GameChoiceSO> ChoicesFor(ItemSO it)
        {
            var l = new List<GameChoiceSO>();
            if (cur == null || it == null) return l;
            foreach (var c in Available())
            {
                // v0.22:凡是这条选项 requires 某件物品,那件物品当晚就必须能点它 ——
                //        哪怕它同时还绑在别的物体上(低温夜的"生篝火"既绑 篝火 也 requires 打火石)。
                //        原来只有 BoundItem 一条路,结果 打火石 在低温夜点下去是"帮不上忙"。
                if (c.requiresItem != null && c.requiresItem == it) { l.Add(c); continue; }
                // v0.46(用户):"应对事件都是点对应的道具" —— 血月 的"下水"这一条绑的是 **两件**(潜水装置 / 鱼叉),
                //        而 BoundItem 一个函数只能回一件 ⇒ 这里按 key 把它放宽,让两件都算"这条选项在我身上"。
                //        ⚠ 简易浮镜 仍然不参与血月(那是 v0.12 定的选项生成条件,不在这轮六条里,见 §11-70)。
                if (c.key == "dive" && cur == DB.BloodMoon && (it == DB.DiveGear || it == DB.Spear)) { l.Add(c); continue; }
                if (BoundItem(c) == it) l.Add(c);
            }
            return l;
        }

        // v0.22:既不绑物品、也不绑场景物体的选项(影怪的"躲进被子"QTE、毒蛇的"点击驱赶"、飞行员的"升临时信号帆"、
        //        没有队友时小影怪的"直接睡觉"、友善猴子的"交易")—— 它们没有可点物体,只能排在睡去按钮上方,
        //        否则整条选项在场景界面里根本没有入口。
        public static List<GameChoiceSO> ChoicesWithoutObject()
        {
            var l = new List<GameChoiceSO>();
            if (cur == null) return l;
            foreach (var c in Available())
            {
                // 与 Sleep() 用同一条判据:"什么都不做"就是睡去按钮本身,不必再列一遍;
                // 但 友善猴子的"交易"虽然挂着 alwaysAvailable,它不是一条"什么都不做",必须给入口。
                if (c.alwaysAvailable && c.key != "trade") continue;
                if (BoundItem(c) != null) continue;           // 绑在某件物品上 → 点那件物品
                if (BoundTarget(c) != null) continue;         // 绑到 篝火/队友/尸骨/大海/营地 → 点那块地
                l.Add(c);
            }
            return l;
        }

        public static List<GameChoiceSO> ChoicesByTarget(string target)
        {
            var l = new List<GameChoiceSO>();
            if (cur == null) return l;
            foreach (var c in Available()) if (BoundTarget(c) == target) l.Add(c);
            return l;
        }

        // 一件选项落在哪个可点物体上:fire / mate / bones / sea / strip(营地这一类的),或它需要的物品
        public static ItemSO BoundItem(GameChoiceSO c)
        {
            string t = BoundTarget(c);
            if (t != null) return null;
            if (c.requiresItem != null)
                // 手上只有自制那块时,这一条要绑到 土制打火石 上,否则低温夜那晚点谁都点不到
                return c.requiresItem == DB.Flint && !S.storage.Has(DB.Flint, 1) && S.storage.Has(DB.CrudeFlint, 1)
                    ? DB.CrudeFlint : c.requiresItem;
            switch (c.key)
            {
                case "flare": return S.storage.Has(DB.FlareGun, 1) ? DB.FlareGun : DB.Flare;   // v0.19:点枪,不是点弹
                case "flashlight": return DB.Flashlight;
                case "umbrella": return DB.Umbrella;
                case "blanket": return DB.Blanket;
                case "spear": return DB.Spear;
                case "net": return DB.Net;
                case "dive": return S.storage.Has(DB.DiveGear) ? DB.DiveGear : DB.Spear;
                case "grab": return S.storage.Has(DB.Net) ? DB.Net : DB.DiveGear;
                case "offer": return DB.Chest;
            }
            return null;
        }

        static string BoundTarget(GameChoiceSO c)
        {
            if (c.requiresFire || c.key == "fire" || c.key == "firepile") return "fire";
            if (c.requiresTeammate || c.key == "watch" || c.key == "lure") return "mate";
            if (c.key == "look") return "bones";      // v0.24:骸骨事件 —— "走过去看"绑在沙滩那具骸骨上
            // v0.27:调皮的猴子 —— "空手抢夺"绑在那只猴子身上。夜晚它会叼着东西站在营地边,
            //        点它 = 空手扑上去;而 鱼叉/信号枪/篝火 三条各自绑在自己的物件上,所以
            //        "手里拿着东西点物件"永远不会走到这一条。⚠ key=="grab" 是 漂流瓶 也在用的键,必须按事件区分。
            if (c.key == "grab" && cur == DB.MonkeyNaughty) return "monkey";
            if (c.key == "hand") return "sea";
            if (c.requiresItem == null && (c.key == "high" || c.key == "save")) return "strip";
            return null;
        }

        // 选项"不生成",不是"点了被拒"(§3.2 猴子行 / §11-31)
        static List<GameChoiceSO> AvailableChoices(GameEventSO e)
        {
            var l = new List<GameChoiceSO>();
            foreach (var c in e.choices)
            {
                if (c.requiresItem != null && !HasForChoice(c.requiresItem)) continue;
                // 没枪或没弹就不生成"打信号弹"这个选项(选项不生成,不是点了被拒)
                if (c.key == "flare" && !(S.storage.Has(DB.FlareGun, 1) && HasAmmo())) continue;
                if (c.requiresFire && !S.fireLitTonight) continue;
                // ⚠ 同上:string 字段在 Unity 载入后是 "" 不是 null。写 != null 会把每一条选项都筛掉,
                //    结果夜晚任何物品都"帮不上忙"(v0.22 那个 bug 的根因)。
                if (!string.IsNullOrEmpty(c.requiresStructure) && !S.HasStructure(c.requiresStructure)) continue;
                if (c.requiresTeammate && !S.mate.present) continue;
                if (c.requiresNoTeammate && S.mate.present) continue;
                if (c.drainsFlashlight && !Usable(DB.Flashlight, true)) continue;
                if (c.key == "offer" && (e == DB.Shadow || e == DB.TwoLights)
                    && !(S.hidden.hasChest && S.hidden.hasKey && !SaveData.trueEndingClaimed)) continue;
                if (e == DB.Ship && c.key == "sail" && !PilotActive()) continue;
                if (e == DB.Gull && c.key == "feed" && S.gull.gullFedOnce) continue;
                // v0.46(用户):"鱼群不能使用鱼竿,只有鱼叉与渔网" ⇒ 这条选项的生成闸从"有叉 或 有竿"收窄成"有叉"。
                //        两件都没有的那一晚,鱼群 就只剩"啥也不做"(它本来就是无惩罚的纯机会事件)。
                if (e == DB.FishSchool && c.key == "hand" && !S.storage.Has(DB.Spear)) continue;
                if (e == DB.Driftbottle && c.key == "grab" && !S.storage.Has(DB.Net) && !S.storage.Has(DB.DiveGear)) continue;
                if ((e == DB.BloodMoon && c.key == "dive") && !S.storage.Has(DB.DiveGear) && !S.storage.Has(DB.Spear)) continue;
                // v0.47:低温夜"生火"的门槛从"有 材料2"改成 **有灶可点**(灶 = 篝火堆 或 信号火堆 建过任一件;
                //        熄了也算,因为这一手的实质就是"重新点燃")。⚠ **没垒过灶的局这一条 不生成**(不是"点了被拒")。
                //   火种那一半 **不在这里重复判** —— 这条选项挂着 `requiresItem = Flint`,
                //   上面那道通用闸 `HasForChoice(Flint)` 已经"认 打火石 也认 土制打火石"了(v0.18 那条)。
                if (e == DB.ColdNight && c.key == "fire" &&
                    !(S.structures.Contains(Database.Campfire) || S.structures.Contains(Database.SignalFire))) continue;
                // v0.27:欠着第二发的那一轮(planeStage==1)答卷与原来的 搜寻飞机(第二次) 一致 ——
                //        只认 信号弹 与 手电筒;信号火堆 是"第一发"那一档的手段,这一轮不出现(选项不生成)。
                // ⚠ v0.48 补一句(§11-59 按你的话改):**火还烧着的话,那一晚在 Show 里就被免检抢先解决了,
                //    根本走不到选项生成**;所以这条只在 **火灭着(= 坏)且欠第二发** 时才真的起作用。
                if (e == DB.SearchPlane && c.key == "firepile" && S.rescue.planeStage == 1) continue;
                l.Add(c);
            }
            return l;
        }

        static bool Usable(FlashlightSO f, bool needCharge) { return S.storage.Has(f, 1) && (!needCharge || S.flashlightCharge > 0); }
        // v0.18:生火的火种有两种 —— 只有"要 打火石"这一条额外认 土制打火石(自制、只能用一次)
        // v0.19:信号枪 与 信号弹 是"枪 + 弹" —— 两边都在手才打得出去(土制信号弹 也得用这把枪)
        static bool HasForChoice(ItemSO it)
        {
            if (it == DB.Flint) return S.storage.Has(DB.Flint, 1) || S.storage.Has(DB.CrudeFlint, 1);
            if (it == DB.FlareGun) return S.storage.Has(DB.FlareGun, 1) && HasAmmo();
            return S.storage.Has(it, 1);
        }

        static bool HasAmmo() { return S.storage.Has(DB.Flare, 1) || S.storage.Has(DB.CrudeFlare, 1); }
        static bool PilotActive() { return S.mate.present && S.mate.skillActiveToday && S.mate.who == DB.Pilot; }
        static bool NavigatorActive() { return S.mate.present && S.mate.skillActiveToday && S.mate.who == DB.Navigator; }

        // ---------------- 结算 ----------------
        public static string Choose(GameChoiceSO c)
        {
            var log = new StringBuilder();
            // 通用字段先走(§7.2:一张 choice 表,不为每件事写 if)
            if (c.costStamina > 0) S.stats.stamina = Mathf.Max(0, S.stats.stamina - c.costStamina);
            if (c.nextDayStaminaPenalty > 0) S.stats.nextDayStaminaPenalty += c.nextDayStaminaPenalty;
            if (c.applySick) { S.stats.sick = true; log.Append("你生病了(体力上限 -1、睡眠不再回满)。\n"); }
            if (c.rewardFood > 0) { S.storage.Add(DB.Can, c.rewardFood); log.Append("+" + c.rewardFood + " 份罐头(武器驱赶的旧奖励)。\n"); }

            string flareKind = null;
            if (c.consumesItem)
            {
                // v0.19:打出去的是"弹",枪留着 —— 所以这条不能按 requiresItem(现在是 信号枪)去扣
                if (c.requiresItem == DB.Flare || c.requiresItem == DB.FlareGun
                    || cur == DB.Ship || cur == DB.Ghost || cur == DB.SearchPlane)
                {
                    if (S.storage.Has(DB.Flare)) { S.storage.Remove(DB.Flare); flareKind = "信号弹"; }
                    else if (S.storage.Has(DB.CrudeFlare)) { S.storage.Remove(DB.CrudeFlare); flareKind = "土制信号弹"; }
                }
                else if (c.requiresItem != null) S.storage.Remove(c.requiresItem, 1);
            }
            // v0.44:夜用不再是"一次用光",而是 **用掉一格电**(存储 2 格,白天 1 精力充 1 格)。
            if (c.drainsFlashlight && S.flashlightCharge > 0)
            {
                S.flashlightCharge--;
                log.Append("手电筒用掉一格电(剩 " + S.flashlightCharge + "/" + B.flashlightChargeMax
                           + ";白天 1 精力充一格)。" + Environment.NewLine);
            }
            if (c.breakChanceNight > 0f && c.requiresItem != null)
            {
                if (S.rng.NextDouble() < c.breakChanceNight)
                {
                    S.storage.MarkBroken(c.requiresItem);
                    log.Append(c.requiresItem.displayName + " 坏了(" + Mathf.RoundToInt(c.breakChanceNight * 100) + "%)。" + Environment.NewLine);
                }
                else log.Append(c.requiresItem.displayName + " 撑住了这一次。" + Environment.NewLine);
            }

            // 语义分支(效果不是数据能表达的,按 key 走)
            string head = Special(c, flareKind, log);
            WriteNightDiary(c);        // v0.30:夜里那一条日记由这里统一落笔(分支只改 key / 参数)
            if (head.StartsWith("END:"))
            {
                S.Log(cur.displayName + " → 结局触发");
                Pending = (EndingId)Enum.Parse(typeof(EndingId), head.Substring(4).Trim());
                HasPending = true;
                return cur.displayName + "\n" + log.ToString();
            }
            S.Log(head);
            return cur.displayName + " — " + head + "\n" + log.ToString();
        }

        // "尝试睡去" = 这一晚的"什么都不做"。每个事件都有一条 alwaysAvailable 的选项;
        // 涨潮没有,它的"什么都不做"就是那条无条件的"转移到高处"(= 被卷走 1~2 件露天物资)。
        public static string Sleep()
        {
            if (Calm) return "今夜无事发生";
            if (cur == null) return "今夜无事发生";
            var list = AvailableChoices(cur);
            foreach (var c in list) if (c.alwaysAvailable && c.key != "trade") return Choose(c);
            foreach (var c in list) if (c.key == "high" || c.key == "nothing" || c.key == "ignore") return Choose(c);
            foreach (var c in list)
                if (c.requiresItem == null && !c.requiresFire && string.IsNullOrEmpty(c.requiresStructure)
                    && !c.requiresTeammate && !c.requiresNoTeammate) return Choose(c);
            return "你在滩声里睁着眼到天亮(这一晚没有可应对的选项)。";
        }

        // v0.24:尸骨那三选(拿走铭牌 / 就地掩埋 / 不理会)整条删除 —— 骸骨现在是独立事件,
        //        "走过去看"就必定取走铭牌,没有掩埋这一步(骸骨第二天自动消失)。

        static string Special(GameChoiceSO c, string flareKind, StringBuilder log)
        {
            var e = cur;
            switch (e.key)
            {
                case "tide":
                    // v0.24:涨潮只剩两条选项(尸骨搬去独立的"骸骨"事件);有围墙时这场根本不会发生。
                    // v0.26 B3:不管选哪一条,涨潮都算发生过 → 登记"次日清晨掷 无人认领的救生筏残片"。
                    S.hidden.nextMorningEgg.Add("tide");
                    // v0.47(用户):"篝火和信号火堆 …… 会被涨潮损坏" ⇒ 海水漫进营地的这一晚,两把火都被打灭。
                    //   ⚠ 两条选项都算"潮水进来了",所以 **抢救露天物资 保不住火**;而做了围墙 ⇒ 这一场根本不进
                    //     ⇒ 火自然安全。这是 围墙 的新价值(它以前只保护露天物资与队友精神)。
                    // v0.48:统一成"坏 / 不坏"一套 ⇒ 涨潮 打灭火 = 把这两座灶标成 **坏**,而不是把计数器抹平。
                    //   之后 低温夜 / 小影怪 / 赶猴子 / 影怪减伤 这些读者只要问 `HasStructure` 就自动不对它们生效,
                    //   恢复途径与 围墙 一样在维修面板那一行(火的那一行写"重新点燃",只花一块火种)。
                    if (S.HasStructure(Database.Campfire) || S.HasStructure(Database.SignalFire))
                    {
                        S.Extinguish(Database.Campfire); S.Extinguish(Database.SignalFire);
                        log.Append("海水漫过火塘:火灭了(在面板里重新点燃,只花一块火种)。" + Environment.NewLine);
                    }
                    if (c.key == "save")
                        return "你把露天物资拖进了庇护所,一样没丢 —— 代价是这一夜没睡成:明天体力上限 -2。";
                    DropOutdoor(log);
                    return "你转移到了高处:人没事,露天的东西被海水卷走了。";

                case "bones":
                    // v0.24:骸骨是独立事件。走过去看 = 必定取走铭牌,没有第二层分支;不掩埋,天亮它自己消失。
                    if (c.key == "look")
                    {
                        S.hidden.nameTagTaken = true;
                        S.storage.Add(DB.NameTag, 1);
                        return "你翻开那具被潮水冲上来的骸骨 —— 胸口的铭牌刻着:" + S.playerName
                               + "(铭牌进仓库。它只影响图鉴与结局F判定,不是真结局前置)";
                    }
                    return "夜色里你没有靠近那具骸骨。天亮之前,重新涌上来的海水把它带走了。";

                case "palmtree":
                    // v0.28:椰树 —— 椰子的唯一来源(探索荒岛不再产它)。零惩罚的机会事件,但"去看"自带 30% 被砸。
                    if (c.key == "take")
                    {
                        S.storage.Add(DB.Coconut, 1);
                        if (Roll(B.coconutHitChance))
                        {
                            Diary("hit");                       // 同一条选项两种结果,日记要分开写
                            Hurt(1, "被椰子砸", log);
                            return "你摸回来 1 个椰子 —— 可是树上那颗先砸了下来:-1 生命。";
                        }
                        return "你摸回来 1 个椰子,没被砸到。";
                    }
                    return "你没有过去。风把椰叶摇了两下,这件事就过去了。";

                case "bloodmoon":
                    if (c.key == "dive") return BloodMoonDive(log);
                    if (c.key == "blanket") return "毯子裹住了他/她:队友精神不降层(毯子这次必损)。";
                    DropMateMood();
                    return "你什么都没做 —— 队友精神降 1 层(" + IslandPhase.MoodName(S.mate.present ? S.mate.mood : MoodLevel.Good) + ")。";

                case "searchplane":
                {
                    // v0.27:搜寻飞机(第二次) 这个专属插播事件整个删除 —— 第一发之后 搜寻飞机 就按常规轮空池
                    //        自己再轮上来,那一轮就是"第二次"。planeStage:0 = 没开始,1 = 欠第二发,2 = 这一局关闭。
                    bool second = S.rescue.planeStage == 1;
                    if (c.key == "ignore")
                    {
                        // ⚠ 旧 planesecond 的"不理会 = 这条线作废"照搬过来,不软化
                        if (second) { S.rescue.planeStage = 2; return "你没有理会它。欠着的第二发到此作废 —— 这一局它不再来了。"; }
                        return "你没有理会。这条线每轮还会再来(不回应没有累积惩罚)。";
                    }
                    if (c.key == "flashlight")
                    {
                        // v0.43(用户改口):"不管是第几轮,飞机线用手电筒都是 50%" ⇒ v0.42 那个
                        //   "第一轮不掷、只推进 planeStage"的实现撤销:任何一轮照过去都掷 50%,命中即结局A。
                        //   两轮这条规矩从此 **只属于信号弹/信号火堆**。落空不关闭这条线(手电筒没电,飞机还会再来)。
                        // ⚠ 顺带修一处旧文案 bug:命中时原来也会落到 "searchplane:flashlight" 那句
                        //   ("灯空了,答案还没拿到")—— 获救那一晚写"答案还没拿到"是错的,现在命中走 flashhit。
                        bool lit = Roll(0.5f);
                        Diary(lit ? "flashhit" : "flashlight");
                        if (lit) return "END:A";
                        return "50% 落空:这一轮机会用掉了(手电筒没电,飞机还会再来)。";
                    }
                    if (c.key == "firepile")
                    {
                        // v0.47:走到这一条时 信号火堆 一定是 **灭着的**(烧着的话 搜寻飞机 当晚根本不给选,
                        //        见 Show 里那条免检)。所以这一手的实质是"当场把信号火堆点起来" ——
                        //        只花火种、不收材料,并且它一起就是两晚(免检窗口从下一场救援事件开始生效)。
                        S.Relight(Database.SignalFire);
                        S.Log("当晚点的是 信号火堆。" + root.island.LightFireStarter());
                        S.rescue.planeStage = 1;
                        return "火堆烧起来了:机影绕了一圈 —— 它记下这一发,下一次 搜寻飞机 再轮上来时还要第二发(而这堆火会先烧满两晚)。";
                    }
                    if (FlareFails(flareKind, log))
                    {
                        if (second) { S.rescue.planeStage = 2; return "第二发哑火:飞机走了 —— 这一局它不再来了。"; }
                        return "信号没有打上去:机影消失了(这一轮的两阶段线作废)。";
                    }
                    if (second) { Diary("flare2"); return "END:A"; }
                    S.rescue.planeStage = 1;
                    return "第一发打出去了 —— 机影绕了一圈走了。下一次 搜寻飞机 再轮上来时,还得再打一发。";
                }

                case "ship":
                    if (c.key == "sail") return "END:A";
                    if (c.key == "ignore") { S.bottle.shipResolved = true; return "你没有回应 —— 错过即永久:海面上再也没有这条船了。"; }
                    if (c.key == "flashlight") return FlashlightRescue(0.5f, EndingId.A, true, log);
                    if (FlareFails(flareKind, log)) { S.bottle.shipResolved = true; return "土制弹哑火 = 直接算错过:这条船永久消失。"; }
                    return "END:A";

                case "ghostship":
                    if (c.key == "ignore") { S.bottle.ghostShipResolved = true; return "它天亮自己走了 —— 错过即永久:结局H 在这一局关闭。"; }
                    if (c.key == "flashlight") return FlashlightRescue(0.5f, EndingId.H, true, log);
                    if (FlareFails(flareKind, log)) { S.bottle.ghostShipResolved = true; return "哑火。它没有回头,永久不再来。"; }
                    return "END:H";

                case "coldnight":
                    if (c.key == "fire")
                    {
                        // v0.47(用户):生火改成 **点燃已经垒好的灶** —— 不收材料、不收精力,只花一块火种(掷累积损坏),
                        //        而且一烧就是两晚(与白天点同一个灶同一套数)。
                        //   ⚠ 真实后果:**从没垒过灶的局,低温夜 这一晚只剩 裹毯子 或 硬挨** ——
                        //        原来那条"材料2 现烧"的免费路没有了。代价记在 风险36。
                        // v0.48:这一手 = **把灶重新点燃**(0 材料 0 精力 + 一块火种),烧两晚 —— 与白天建好即点着同一套数。
                        //   信号火堆 在(哪怕熄了)就点它,因为它是 篝火 的上位平替;否则点 篝火。
                        //   ⚠ 真实后果:**从没垒过灶的局,低温夜 这一晚只剩 裹毯子 或 硬挨**(风险36)。
                        bool signal = S.structures.Contains(Database.SignalFire);
                        S.Relight(signal ? Database.SignalFire : Database.Campfire);
                        S.Log("这一晚点的是 " + (signal ? "信号火堆" : "篝火") + "。" + root.island.LightFireStarter());
                        return "你点燃了自己的灶(只花火种):这一夜不冷了,而且它会一直烧到 " + RunState.FireNightsPerLighting + " 个晚上之后。";
                    }
                    if (c.key == "blanket") return "毯子挡住了寒气(它本次必损)。";
                    Hurt(1, "低温夜", log);
                    S.stats.sick = true;
                    return "你硬挨了一夜:-1 生命,并且生病了。";

                case "snake":
                    if (c.key == "flashlight") return "光照过去,东西散了(手电筒没电了)。";
                    if (c.key == "swat")
                    {
                        if (Roll(0.6f)) return "你赶走了它。";
                        Diary("bite");
                        return SnakeBite(log);
                    }
                    Diary("bite");
                    return SnakeBite(log);

                case "fishschool":
                    if (c.key == "net")
                    {
                        S.stats.stamina = Mathf.Max(0, S.stats.stamina - 2);
                        S.storage.Add(DB.Can, 3);
                        Wear(DB.Net);
                        return "下网:3 份罐头(渔网也会坏 —— 走 30% 起的累积概率)。";
                    }
                    if (c.key == "hand")
                    {
                        // v0.46(用户):这一晚 **只有 鱼叉 一家** —— 钓竿 被从这条事件里拿掉了
                        //        ("鱼群不能使用鱼竿,只有鱼叉与渔网"),所以不再有"零损耗的备选",
                        //        也不需要在两件里替你挑一件(§11-69 就此结案)。
                        S.storage.Add(DB.Can, 3);
                        Wear(DB.Spear);
                        return "用鱼叉叉住:3 份罐头(鱼叉照常掷累积损坏 —— 这一晚没有不坏的那条路)。";
                    }
                    return "你看着鱼群散去。纯机会事件,没有惩罚。";

                case "driftbottle":
                    if (c.key == "grab")
                    {
                        S.stats.stamina = Mathf.Max(0, S.stats.stamina - 2);
                        S.storage.Add(DB.Bottle, 1);
                        S.hidden.everOwned.Add(DB.Bottle);
                        if (S.storage.Has(DB.Net)) Wear(DB.Net); else Wear(DB.DiveGear);
                        return "捞到了 1 个漂流瓶。瓶身的纸写着另一艘船的求救,日期比你的起飞日早。";
                    }
                    return "空手够不到。它随退潮漂走了。";

                case "gull":
                    if (c.key == "feed")
                    {
                        if (S.storage.Has(DB.Can)) S.storage.Remove(DB.Can, 1);
                        else S.storage.Remove(DB.Bait, 1);
                        S.gull.gullFedOnce = true;
                        S.gull.presentCount = 1;
                        S.gull.refreshP = B.gullRefreshBase;
                        return "它吃下了那一份,留在岛上(在场 1 只)。全游戏只需喂这一次,后来的直接来落。";
                    }
                    return "它飞走了(不算损失,下轮还会来)。";

                case "smallshadow":
                    if (!S.mate.present)
                    {
                        S.stats.nextDayStaminaPenalty += 1;
                        return "没有队友可守:你直接睡了,次日体力 -1(不丢罐头、不坏工具、不扣生命)。";
                    }
                    if (c.key == "watch") return "你守了一整夜:队友 100% 存活,代价是次日体力 -2。";
                    if (c.key == "fire")
                    {
                        // v0.9 定的"火本次必熄"照字面实现:**整堆扑灭**(不是只烧掉一晚)。
                        //   ⚠ 这条代价在 v0.47 之后 **变重了** —— 以前"熄"只是回到本来的一晚,
                        //      现在它把"点燃一次管两晚"的预算一起吞掉,想再用就得再花一块火种。
                        //      这是两条已定规矩撞出来的,不是我加的税;要收就在 §11 里说。
                        // v0.48:统一模型下"火本次必熄"= **把两座灶都标成坏**(整堆灭)。
                        //   你这次答的是"是的,必然熄灭"⇒ §11-77 结案,不做只扣一晚的软化。
                        S.Extinguish(Database.Campfire); S.Extinguish(Database.SignalFire);
                        if (Roll(0.7f)) return "火光撑着,队友活了下来(火本次必熄)。";
                        KillMate("小影怪", log);
                        return "火熄了 —— 队友被拖走了(永久消失)。";
                    }
                    KillMate("小影怪", log);
                    return "你躲了起来:队友被杀,永久消失。";

                case "monkeynaughty":
                    return Monkey(c, log);

                case "monkeyfriendly":
                    if (c.key == "decline") return "你不换。它自己走掉了,没有任何负面。";
                    return MonkeyTrade(log);

                case "rain_light": case "rain_heavy": case "rain_storm":
                    if (c.key == "umbrella") return "伞挡住了雨(" + e.displayName + "),你免于生病。";
                    S.stats.sick = true;
                    return "你淋了一夜:生病 + 次日体力 -2。";

                case "weapona":
                    if (c.key == "umbrella") return "伞遮住了那道光(伞本次必损)。天亮没事。";
                    return "没有伞,只能硬挨:次日体力 -2。";

                case "fakemate":
                    if (c.key == "flashlight")
                    {
                        // v0.45:这一场只在营地里有人时才会摊开(见 BlockedReason),不用再判"只有你一个人"。
                        if (Roll(0.6f)) { S.mate.sick = true; return "是真的:他/她确实不舒服 → 队友进入生病。"; }
                        return "是假的。它散开成一片湿气(真伪早已决定,手电筒只是告诉你答案)。";
                    }
                    return "你没有照。它站了一会儿,自己走了 —— 你不会知道那是不是真的。";

                case "shadow":
                    return Shadow(c, log);

                case "twolights":
                    if (c.key == "offer") return "END:T";
                    S.hidden.twoLightsSeen = true;
                    return "你没有动。两点光熄灭了,这一局不会再有第三次。";
            }
            return "(无事发生)";
        }

        static string Shadow(GameChoiceSO c, StringBuilder log)
        {
            S.hidden.shadowNightResolved = true;
            S.hidden.metShadow = true;
            if (c.key == "offer") return "END:T";
            // v0.26 A3:压弯的公司徽章 —— 这一晚只要不是献宝结束,就登记"次日清晨掷 1%"
            S.hidden.nextMorningEgg.Add("shadow");
            if (c.key == "spear" || c.key == "flashlight") return "你硬挡住了它:玩家免伤(" + (c.key == "spear" ? "鱼叉本次必坏" : "手电筒没电") + ")。";
            int baseDmg = S.fireLitTonight ? 2 : 3;
            if (c.key == "lure") { KillMate("引开影怪", log); return "你把影怪引开了:玩家免伤,但那名队友永久消失。"; }
            if (c.key == "hide")
            {
                if (Roll(S.HasStructure(Database.Wall) ? 0.7f : 0.5f))   // 围墙:QTE 判定窗口 +20%
                {
                    int d = Mathf.CeilToInt(baseDmg * 0.5f);
                    Hurt(d, "影怪(躲被子成功)", log);
                    return "你屏住呼吸:伤害减半并向上取整(-" + d + " 生命)。";
                }
                Hurt(baseDmg, "影怪(躲被子失败)", log);
                Diary("hidefail");
                return "QTE 失败,按未处理结算:-" + baseDmg + " 生命。";
            }
            Hurt(baseDmg, S.fireLitTonight ? "影怪(火在烧 -2)" : "影怪(无火 -3)", log);
            return "你什么都没做:" + (S.fireLitTonight ? "火在烧,-2 生命。" : "没有火,-3 生命。");
        }

        static string Monkey(GameChoiceSO c, StringBuilder log)
        {
            if (stolen == null) return "仓库里一件可偷的东西都没有 —— 它空手走了(兜底:该选项本该不出现)。";
            string head; bool damaged = false, lost = false;
            switch (c.key)
            {
                case "spear":
                    Wear(DB.Spear);            // 补录⑥:驱赶那一下按正常概率掷损坏
                    head = "你用鱼叉把它赶走了:东西完好回来 +1 份罐头(鱼叉自己照常掷损坏)。";
                    break;
                case "flare":
                    head = "一发打出去,猴子散了:东西完好回来,但那一发弹没了(它不是无损,只是不坏)。";
                    break;
                case "fire":
                    damaged = true;
                    head = "火光把它吓跑了 —— 但抢回来的 " + stolen.displayName + " 是破损的(坏的是被抢那件,不是打火石)。";
                    break;
                case "grab":
                    Hurt(1, "空手抢夺", log);
                    damaged = true;
                    head = "你扑上去抢:-1 生命,而且抢回来的是破损状态。";
                    break;
                default:
                    lost = true;
                    Diary("refuse", stolen.displayName);      // v0.30:日记里要写清丢了哪一件
                    S.Log("猴子带走了 " + stolen.displayName + "(永久丢失)。");
                    head = "你看着它跑掉:" + stolen.displayName + " 永久丢失。";
                    break;
            }
            // ⚠ v0.33 修的真 bug(用户问"用鱼叉回收的物品是不损坏的,还是逻辑错了会丢东西?"—— 两个都是真的):
            //   物品在事件摊开那一晚就被 RandomStealable 从仓库 Remove 掉了,而 spear 与 flare 两条
            //   **只写了一句"完好地回到了仓库",从来没调用 ReturnStolen** ⇒ 鱼叉/信号弹驱赶 = 东西永久丢失,
            //   日志却说它回来了。现在"回收"只有这一个出口,分支只决定 带不带破损 / 干脆不回收。
            if (!lost) ReturnStolen(damaged);
            return head;
        }

        static void ReturnStolen(bool damaged)
        {
            if (stolen == null) return;
            if (damaged && !stolen.indestructible && !stolen.consumable)
            {
                S.storage.Add(stolen, 1);
                S.storage.MarkBroken(stolen);
            }
            else S.storage.Add(stolen, 1);
        }

        static string MonkeyTrade(StringBuilder log)
        {
            // v0.26 D1:猴子的回礼 —— 只要这一晚真的换成了东西,就登记"次日清晨掷 1%"。
            //        (换不回东西的那两条分支也算它来过、给了罐头,所以照登。)
            S.hidden.nextMorningEgg.Add("monkeytrade");

            // v0.33:这一对是 PickTrade() 在摊开这一晚就定好、并且已经显示给玩家的(用户:"友善的猴子会表示换出/换入的物品")。
            //        这里只执行,不再另掷 —— 否则屏幕上说的那件与兜里少掉的那件可能不是同一件。
            if (tradeGive == null)
            {
                S.storage.Add(DB.Can, 2);
                Diary("nogive");
                return "你没有它能收的东西 → 它给了你 2 份罐头。";
            }
            S.storage.Remove(tradeGive, 1);
            if (tradeGot == null)
            {
                S.storage.Add(DB.Can, 2);
                Diary("full", tradeGive.displayName);
                return "它收走了 " + tradeGive.displayName + ",但你已经集齐了 → 它给了 2 份罐头。";
            }
            S.storage.Add(tradeGot, 1);
            S.hidden.everOwned.Add(tradeGot);
            Diary("trade", tradeGive.displayName, tradeGot.displayName);
            return "它收走了 " + tradeGive.displayName + ",推给你一件你从没见过的:" + tradeGot.displayName + "。";
        }

        static string BloodMoonDive(StringBuilder log)
        {
            // v0.46(用户):"应对事件都是点对应的道具" ⇒ 这一晚下水用的是 **他点的那件**;
            //        从别的入口进来(NightTool 为 null)才退回旧的"有潜水装置就用它,否则鱼叉"。
            ItemSO tool = NightTool != null && S.storage.Has(NightTool) ? NightTool
                        : (S.storage.Has(DB.DiveGear) ? DB.DiveGear : DB.Spear);
            if (!Roll(B.bloodMoonDiveSuccess))
            {
                if (!NavigatorActive()) S.stats.nextDayCapPenalty += 1;
                return "下水失败:空手而归(不降队友精神),代价是次日体力上限 -1" + (NavigatorActive() ? "(领航员免掉)" : "") + "。";
            }
            int cans = 1 + S.rng.Next(3);
            S.storage.Add(DB.Can, cans);
            string extra = "";
            if (!S.hidden.hasMap && Roll(B.bloodMoonMapChance))
            {
                S.hidden.hasMap = true;
                S.storage.Add(DB.Map, 1);
                extra = " —— 而且在礁缝里摸到了一张 藏宝图(单夜期望约 18%)。";
            }
            if (!NavigatorActive()) S.stats.nextDayCapPenalty += 1;
            Wear(tool);            // v0.46:坏的只可能是他点进来那一件
            Diary("dive", cans);      // v0.30:日记里那句要带上今天真正拿到的份数
            return "下水成功(" + tool.displayName + "):" + cans + " 份罐头" + extra + "(此用法不强制损坏装备;次日体力上限 -1" + (NavigatorActive() ? ",领航员免掉" : "") + ")。";
        }

        static string FlashlightRescue(float chance, EndingId id, bool missIsPermanent, StringBuilder log)
        {
            if (Roll(chance)) return "END:" + id;
            if (missIsPermanent)
            {
                if (id == EndingId.A) S.bottle.shipResolved = true;
                else S.bottle.ghostShipResolved = true;
                return "50% 落空:光柱扫过去,什么都没有回应 —— 错过即永久,它不再来了。";
            }
            return "50% 落空:这一轮机会用掉了(手电筒没电,飞机还会再来)。";
        }

        static bool FlareFails(string kind, StringBuilder log)
        {
            if (kind == null) { log.Append("你手上没有可用的信号弹。\n"); return true; }
            if (kind == "土制信号弹" && !Roll(B.crudeFlareSuccessChance))
            {
                log.Append("土制信号弹哑火了(40%)。" + Environment.NewLine);
                return true;
            }
            log.Append(kind + " 打出去了。" + Environment.NewLine);
            return false;
        }

        static void DropOutdoor(StringBuilder log)
        {
            int n = 1 + S.rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                var it = RandomStealable(S);
                if (it != null) { S.storage.Remove(it, 1); log.Append("海水卷走了 " + it.displayName + "。\n"); }
            }
            DropMateMood();
        }

        static void DropMateMood()
        {
            if (!S.mate.present) return;
            if (S.mate.mood < MoodLevel.Broken) S.mate.mood++;
            S.Log("队友精神降 1 层 → " + IslandPhase.MoodName(S.mate.mood));
        }

        static void KillMate(string why, StringBuilder log)
        {
            if (!S.mate.present) return;
            S.Log(S.mate.who.displayName + " 永久消失(" + why + ")。");
            S.mate.Reset();
            log.Append(root.island.LoreRoll("matelost"));   // v0.26 C3(夜里这条线也走同一个 1% 闸)
        }

        static string SnakeBite(StringBuilder log)
        {
            if (S.storage.Has(DB.Can) && Roll(0.5f)) { S.storage.Remove(DB.Can, 1); return "它叼走了 1 份罐头。"; }
            Hurt(1, "毒蛇", log);
            return "你被咬了一口:-1 生命。";
        }

        static void Hurt(int n, string why, StringBuilder log)
        {
            S.stats.hp = Mathf.Max(0, S.stats.hp - n);
            log.Append("-" + n + " 生命(" + why + "),剩 " + S.stats.hp + "/" + B.hpMax + "。" + Environment.NewLine);
        }

        static void Wear(ItemSO tool)
        {
            if (root.island != null) root.island.Wear(tool);
        }

        public static ItemSO PeekStealable(RunState s)
        {
            foreach (var kv in s.storage.ok) if (!kv.Key.lore) return kv.Key;
            return null;
        }

        public static ItemSO RandomStealable(RunState s)
        {
            var list = new List<ItemSO>();
            foreach (var kv in s.storage.ok)
            {
                if (kv.Key.lore) continue;
                for (int i = 0; i < kv.Value; i++) list.Add(kv.Key);
            }
            if (list.Count == 0) return null;
            var pick = list[s.rng.Next(list.Count)];
            s.storage.Remove(pick, 1);
            return pick;
        }

        // 今夜无事发生:唯一例外是"趁这一晚和队友说话"免体力(§2.4 交流只在白天,这是唯一破例)
        public static bool CalmTalkOffered
        {
            get { return Calm && S.mate.present && S.mate.mood > MoodLevel.Good; }
        }

        public static string CalmTalk()
        {
            // v0.18:免体力那一晚也是"当天第一次谈话"的口径 —— 回升 2 层(白天再聊就是每次 1 层)
            S.mate.mood = (MoodLevel)Mathf.Max((int)MoodLevel.Good, (int)S.mate.mood - B.talkMoodFirstTalk);
            S.mate.moodDaysSinceTalk = 0;
            S.Log("无事发生的夜晚:你们说了很久的话(免体力),精神回升 " + B.talkMoodFirstTalk + " 层 → "
                  + IslandPhase.MoodName(S.mate.mood) + "。");
            // v0.30:这一晚有两条日记 —— Begin 里那条"无事发生" + 这条"说了很久的话"
            S.Diary(DiaryLines.CalmTalk(S.day), DiaryLineKind.Night);
            return "今夜无事发生 — 你们说了很久的话";
        }
    }

    // ---------- 结局屏 ----------
    public class EndingPhase : MonoBehaviour
    {
        public GameRoot root;

        void Start()
        {
            var rt = root.hud;
            root.ClearHud();
            Ui.Panel(rt, "bg", new Color(0.04f, 0.05f, 0.07f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var e = root.state.endingSo;
            string title = e != null ? e.title : ("结局 " + root.state.ending);
            Ui.Label(rt, "title", title, 46, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -90));

            var lines = e != null ? Epilogue(e) : new List<string>();
            var sb = new StringBuilder();
            foreach (var l in lines) sb.Append(l).Append("\n\n");
            Ui.Label(rt, "epi", sb.ToString(), 20, TextAnchor.MiddleCenter, new Color(0.88f, 0.9f, 0.94f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -430), new Vector2(0, -210));

            Ui.Label(rt, "stat", StatLine(), 15, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.72f),
                     new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 120), new Vector2(0, 200));

            Ui.Button(rt, "again", "再走一次那 60 秒", 20, () => root.EnterTitle(), new Color(0.8f, 0.35f, 0.25f, 1f),
                      new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-160, 40), new Vector2(160, 90));
        }

        List<string> Epilogue(EndingSO e)
        {
            if (SaveData.trueEndingClaimed && e.usesClaimedEpilogue && e.epilogueIfTrueClaimed.Count > 0)
                return e.epilogueIfTrueClaimed;
            return e.epilogueLines;
        }

        string StatLine()
        {
            var S = root.state;
            return "活了 " + S.day + " 天 | 队友:" + (S.mate.present ? S.mate.who.displayName : "无") +
                   " | 海鸥在场 " + S.gull.presentCount + " | 丢瓶 " + S.bottle.thrownBottles +
                   " | 暗线:" + (S.hidden.hasMap ? "图 " : "") + (S.hidden.hasKey ? "钥匙 " : "") + (S.hidden.hasChest ? "箱 " : "") +
                   (S.hidden.nameTagTaken ? "铭牌" : "") +
                   // v0.26:彩蛋层是"背景补充",结算屏给一个收集数;回环次数用来核 B2 那道跨局闸
                   "\n彩蛋 " + S.hidden.loreFound.Count + " 件 | 这个档已经回环 " + SaveData.loopCount + " 次" +
                   "\n真结局标记:" + (SaveData.trueEndingClaimed ? "已宣称" : "未宣称");
        }
    }
}
