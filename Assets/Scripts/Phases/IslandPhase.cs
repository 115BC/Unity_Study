using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // M2:荒岛白天循环(§2.3 / §2.4 / §6 / §7)。地点卡片式,全部按钮 + 文字标注。
    public class IslandPhase : MonoBehaviour
    {
        public GameRoot root;
        RunState S { get { return root.state; } }
        Database DB { get { return root.db; } }
        BalanceConfig B { get { return root.db.bal; } }

        bool firstEntry = true;
        int talksToday;
        bool fedToday, healedToday;

        public void Start()
        {
            diagDone = false;
            diagAt = Time.unscaledTime + 0.4f;
            root.ApplyIslandView("IslandPhase.Start");
            // 跳伞带上岛的储物箱内容一律转入无限容量仓库(§2.3 荒岛仓储)
            if (firstEntry)
            {
                firstEntry = false;
                S.day = 0;
                if (S.mate.present) S.Log(S.mate.who.displayName + " 跟你一起落在了沙滩上。");
                if (root.islandStage == null)
                    S.Log("⚠ 场景里没有 Island 层(天空/大海/沙滩/篝火/道具落点)。跑一次菜单 Tools/60slike/② —— 它只补缺的那一层,不覆盖你改过的东西。");
                MorningTick(true);
                // v0.30:第一页日记 —— 从飞机上活着下来那一刻起,这本日记就开始写。
                S.Diary(DiaryLines.Opening(S.storage.TotalUnits(), S.mate.present));
            }
            Refresh();
        }

        // ---------------- 清晨结算(§7.2 DailyTick) ----------------
        public void MorningTick(bool firstDay)
        {
            S.day++;
            S.nightCount++;
            // v0.48(用户):火不再需要每天点一次 —— **建的时候就是点着的**,两晚烧完它自己"坏"掉。
            //   这一行只做倒计数与"归零 → 进 brokenStructures",逻辑收在 RunState.TickFiresAtDawn() 里。
            S.TickFiresAtDawn();
            talksToday = 0; fedToday = false; healedToday = false;

            // v0.13:探索刷新骰(一天只掷一次;未命中 +5%;只有真的探索过才回落)
            if (S.explore.exploredToday) { S.explore.refreshP = B.exploreRefreshBase; S.explore.exploredToday = false; }
            float roll = (float)S.rng.NextDouble();
            S.explore.availableToday = roll < S.explore.refreshP;
            if (!S.explore.availableToday)
                S.explore.refreshP = Mathf.Min(B.exploreRefreshCap, S.explore.refreshP + B.exploreRefreshRamp);

            // 体力 / 状态
            if (!firstDay)
            {
                S.stats.capPenaltyToday = S.stats.nextDayCapPenalty;   // 血月下水:压的是"上限",只压这一天
                S.stats.nextDayCapPenalty = 0;
                S.stats.stamina = S.SleepStaminaRestore() - S.stats.nextDayStaminaPenalty;
                S.stats.nextDayStaminaPenalty = 0;
                S.stats.stamina = Mathf.Clamp(S.stats.stamina, 0, EffectiveStaminaMax());
                S.stats.fullness = Mathf.Clamp01(S.stats.fullness - B.playerFullnessDecayPerDay);

                // v0.28:水分这条线整个删除(喝水系统已移除)⇒ 清晨只结算 饱食,不再有 脱水 判定。
                if (S.stats.fullness <= 0f) { S.stats.noFoodDays++; Damage(1, "饥饿"); }
                else S.stats.noFoodDays = 0;
                if (S.stats.foodLockDays > 0) S.stats.foodLockDays--;   // v0.46:吃不下东西的三天,每天清晨减一天
            }
            else
            {
                S.stats.stamina = B.staminaMax;
            }

            // v0.26 彩蛋层:①前一晚登记的三件(A3/B3/D1)在今晨各掷一次 1%;
            //              ②B2 只在 "这个存档至少掉过一次 结局F" 的局里、开局第一个清晨掷那唯一一次。
            if (S.hidden.nextMorningEgg.Count > 0)
            {
                foreach (var src in S.hidden.nextMorningEgg) LoreRoll(src);
                S.hidden.nextMorningEgg.Clear();
            }
            if (firstDay && SaveData.loopCount > 0) LoreRoll("loop");

            // v0.28:集水器/净水器 与 整条淡水线已删除 —— 清晨不再产出任何"水"。

            // 海鸥:在场才掷;命中 p=max(20%,p/2),未命中 p+=1%(§4.7)
            if (S.gull.presentCount > 0)
            {
                if (S.rng.NextDouble() < S.gull.refreshP)
                {
                    S.gull.presentCount++;
                    S.gull.refreshP = Mathf.Max(B.gullRefreshBase, S.gull.refreshP * 0.5f);
                    S.Log("又有一只海鸥落了下来(在场 " + S.gull.presentCount + " 只)。");
                }
                else S.gull.refreshP += B.gullRefreshRamp;
            }

            // 队友:精神 / 饥饿 每 2~3 天降一层(§2.4)
            if (S.mate.present) TickTeammate();

            // 技能:饱食 + 精神良好 + !sick 才刷 40%(§2.4)
            S.mate.skillOfferedToday = false;
            S.mate.skillActiveToday = false;
            if (S.mate.present && !S.mate.sick && S.mate.mood == MoodLevel.Good && S.mate.hunger == HungerLevel.Full
                && S.rng.NextDouble() < B.teammateSkillChance)
            {
                S.mate.skillOfferedToday = true;
                S.Log(S.mate.who.displayName + " 今天状态不错(技能图标亮了:悬浮到他身上,菜单里的\"技能\"那条花 1 精力发动)。");
            }

            if (S.gull.presentCount >= B.gullEndingCount) { root.EndRun(EndingId.I); return; }
            if (S.day > B.survivalDaysToEndB) { root.EndRun(EndingId.B); return; }
            if (S.stats.hp <= 0) { root.EndRun(EndingResolver.DeathEnding(S)); return; }
        }

        void TickTeammate()
        {
            var m = S.mate;
            m.moodDaysSinceTalk++;
            if (m.moodDaysSinceTalk >= B.teammateMoodForcedDropDays) DropMood();
            else if (S.rng.NextDouble() < B.teammateMoodDailyDropChance) DropMood();

            m.hungerDaysSinceMeal++;
            if (m.hungerDaysSinceMeal >= B.teammateMoodForcedDropDays) DropHunger();
            else if (S.rng.NextDouble() < B.teammateMoodDailyDropChance) DropHunger();

            if (m.sick && !IsHungry(m) && m.mood == MoodLevel.Good && S.rng.NextDouble() < B.teammateSickSelfHeal)
            {
                m.sick = false;
                S.Log(m.who.displayName + " 的病自己好了。");
            }

            if (m.hunger == HungerLevel.Famine)
            {
                S.Log(m.who.displayName + " 在饥荒里没能撑过来 —— 空吊床在风里晃。(饥荒当天没吃东西 → 次日消失)");
                S.Diary(DiaryLines.Day("matefamine", S.day), DiaryLineKind.Bad);
                LoseTeammate("饥荒");
                return;
            }
            if (m.mood == MoodLevel.Broken)
            {
                // v0.29:自杀 不再有任何数值惩罚(原"当天饱食与水分各 -1"整条删除,水分那半在 v0.28 已经没了)。
                //        它只留一句日志;真正的后果是"失去看家保护"这一条机制,由下面的 LoseTeammate 统一记。
                S.Log(m.who.displayName + " 不声不响地离开了。");
                S.Diary(DiaryLines.Day("matesuicide", S.day), DiaryLineKind.Bad);
                LoseTeammate("自杀");
            }
        }

        bool IsHungry(TeammateState m) { return m.hunger >= HungerLevel.Hungry; }

        void DropMood()
        {
            if (S.mate.mood < MoodLevel.Broken) S.mate.mood++;
            S.mate.moodDaysSinceTalk = 0;
        }

        void DropHunger()
        {
            if (S.mate.hunger < HungerLevel.Famine) S.mate.hunger++;
            S.mate.hungerDaysSinceMeal = 0;
        }

        void LoseTeammate(string why)
        {
            S.mate.Reset();
            S.Log("队友永久消失(" + why + "),不补刷。看家事故骰从此每个探索日都掷。");
            // v0.30:日记不在这里落笔 —— 白天那两条(饥荒/自杀)各自在 TickTeammate 里写,
            //        夜里那两条(小影怪/引开影怪)由 NightResolver 的那条夜用日记写。
            LoreRoll("matelost");      // v0.26 C3:两个被划掉的名字 —— 每个"永久失去"的清晨各掷一次 1%
        }

        // v0.30:白天的日记通道。文案在 DiaryLines 里,按 天数 取模轮换(绝不用 S.rng —— 那会改判定结果)。
        // v0.40:key 同时是 **合并键** —— 同一件白天行动做几次,日记里只留一行"今天做了 N 次"。
        void D(string key, params object[] args) { S.Diary(DiaryLines.Day(key, S.day, args), DiaryLineKind.Day, key); }

        // "材料 3 份、罐头 5 份" 这种小摘要(0 的那项不写),给日记用
        static string Sum(int a, string an, int b, string bn, int c, string cn)
        {
            var sb = new StringBuilder();
            if (a > 0) sb.Append(a).Append(' ').Append(an);
            if (b > 0) { if (sb.Length > 0) sb.Append('、'); sb.Append(b).Append(' ').Append(bn); }
            if (c > 0) { if (sb.Length > 0) sb.Append('、'); sb.Append(c).Append(' ').Append(cn); }
            return sb.Length > 0 ? sb.ToString() : "两手空空";
        }

        public void Damage(int n, string why)
        {
            S.stats.hp = Mathf.Max(0, S.stats.hp - n);
            S.Log("-" + n + " 生命(" + why + "),剩 " + S.stats.hp + "/" + B.hpMax);
            if (S.stats.hp <= 0) root.EndRun(EndingResolver.DeathEnding(S));
        }

        // ---------------- 表现层:沙滩上看得见的一层 + 四个按钮 ----------------
        // 荒岛不是文字冒险:仓库里的每一件都是摆在 Island/Slot_* 上的实体,点它 = 与它有关的行动。
        // 标签只写名字(数量与代价在点开的菜单里),状态只留一行,长文本收进"日志"。
        readonly List<GameObject> props = new List<GameObject>();

        public void Refresh()
        {
            if (S.phase != RunPhase.Island) return;
            // 光照/镜头按"现在是不是夜晚"重算,不依赖天亮那一刻的转移有没有跑成
            // (以前只在 OnDawn 里把太阳调回来,一旦夜晚被异常打断,白天就一直是黑的)
            SetNightLook(night);
            root.ApplyIslandView("Refresh");
            BuildProps();
            var rt = root.hud;
            root.ClearHud();
            BuildStatusStrip(rt);
            if (night) BuildNightHud(rt); else BuildButtons(rt);
        }

        void BuildStatusStrip(Transform rt)
        {
            Ui.Panel(rt, "strip", new Color(0, 0, 0, 0.42f), new Vector2(0, 1), new Vector2(0, 1),
                     new Vector2(10, -118), new Vector2(600, -10));
            Ui.Label(rt, "day", "第 " + S.day + " 天 / " + B.survivalDaysToEndB + "   " + S.playerName, 20,
                     TextAnchor.UpperLeft, Color.white, new Vector2(0, 1), new Vector2(0, 1),
                     new Vector2(20, -36), new Vector2(590, -10));
            BuildHeart(rt);
            BuildStamina(rt);
            // v0.28:水分条与"几天喝不下东西"随喝水系统一起删除;生病 标记留着(它另有 低温夜/雨 两个来源)
            Ui.Label(rt, "needs", "饱食 " + Pct(S.stats.fullness) +
                                  (S.stats.sick ? "   生病" : ""),
                     15, TextAnchor.UpperLeft, new Color(0.88f, 0.9f, 0.94f),
                     new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -88), new Vector2(590, -62));
            string mate = S.mate.present
                ? "队友 " + S.mate.who.displayName + "  精神 " + MoodName(S.mate.mood) + "  饱食 " + HungerName(S.mate.hunger)
                  + (S.mate.sick ? "  生病" : "") + (S.mate.skillOfferedToday && !S.mate.skillActiveToday ? "  ✦技能待发动(悬浮队友)" : "")
                : "队友 无(每个探索日掷 25% 看家事故)";
            Ui.Label(rt, "mate", mate, 15, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.9f),
                     new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -114), new Vector2(700, -88));
        }

        // 行楷是书法体,♥ ⚡ 这类符号不一定有字形;缺字形时 Ui.Glyph 会退到 GB2312 一定有的 ◆
        static string HeartGlyph { get { return Ui.Glyph("♥", "◆"); } }
        static string BoltGlyph { get { return Ui.Glyph("⚡", "◆"); } }

        // 一颗红心:碎裂程度 = 已经失去的生命(设计文档 §2.3 就是"一颗心裂纹贴图,无数字")。
        // 灰盒里没有贴图:心用 ♥ 字形,裂纹用几道转过的细黑条,失去几点生命就压上几道。
        void BuildHeart(Transform rt)
        {
            int lost = B.hpMax - S.stats.hp;
            float t = B.hpMax <= 0 ? 0f : (float)S.stats.hp / B.hpMax;
            Ui.Label(rt, "heart", HeartGlyph, 52, TextAnchor.MiddleCenter,
                     Color.Lerp(new Color(0.42f, 0.1f, 0.12f), new Color(0.93f, 0.17f, 0.2f), t),
                     new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -92), new Vector2(84, -36));
            for (int i = 0; i < lost; i++)
            {
                var c = Ui.Panel(rt, "crack" + i, new Color(0.04f, 0.04f, 0.05f, 0.9f),
                                 new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -70), new Vector2(78, -66));
                c.localEulerAngles = new Vector3(0f, 0f, -34f + i * 24f);
            }
        }

        // 精力:满格画满 staminaMax 个 ⚡。亮黄 = 还能用;灰 = 已花掉,或今天因为生病/下水被扣掉不能用。
        void BuildStamina(Transform rt)
        {
            int full = EffectiveStaminaMax();
            string bolt = BoltGlyph;
            for (int i = 0; i < B.staminaMax; i++)
            {
                bool usable = i < full;
                var col = !usable ? new Color(0.28f, 0.28f, 0.3f, 0.85f)
                                  : (i < S.stats.stamina ? new Color(1f, 0.82f, 0.2f, 1f) : new Color(0.42f, 0.42f, 0.45f, 1f));
                Ui.Label(rt, "sp" + i, bolt, 22, TextAnchor.MiddleCenter, col,
                         new Vector2(0, 1), new Vector2(0, 1),
                         new Vector2(96 + i * 26, -62), new Vector2(122 + i * 26, -34));
            }
        }

        void BuildButtons(Transform rt)
        {
            Ui.Button(rt, "diary", "日记", 20, OpenDiary, new Color(1, 1, 1, 0.14f),
                      new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -70), new Vector2(-16, -18));

            // v0.15:探索按钮"刷新才出现"。没刷出的那天这一屏上根本没有它 ——
            // 不再是 v0.13 那版"置灰 + 一行小字"。仍然不弹窗、不加红点、不加解释文案。
            var explore = Act("explore");
            string why = "";
            if (explore != null && CanDo(explore, out why))
                Ui.Button(rt, "explore", explore.displayName + "\n吃掉全部剩余精力" + BoltGlyph,
                          18, DoExploreNow, new Color(0.85f, 0.5f, 0.18f, 1f),
                          new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-232, -34), new Vector2(-16, 46));

            Ui.Button(rt, "bench", "制造(维修)面板", 20, OpenBench, new Color(0.3f, 0.42f, 0.55f, 1f),
                      new Vector2(1, 0), new Vector2(1, 0), new Vector2(-210, 120), new Vector2(-16, 172));

            Ui.Button(rt, "endday", "结束一天", 22, () => BeginNight(), new Color(0.55f, 0.25f, 0.55f, 1f),
                      new Vector2(1, 0), new Vector2(1, 0), new Vector2(-190, 16), new Vector2(-16, 72));

            // v0.18:鱼饵开关。手上没有鱼饵时这个开关根本不出现(入口不生成,不是置灰)。
            if (S.storage.Has(DB.Bait))
                Ui.Button(rt, "bait", (S.useBait ? "鱼饵:挂着" : "鱼饵:不挂") + "\n点一下切换",
                          15, () => { S.useBait = !S.useBait; Refresh(); },
                          S.useBait ? new Color(0.30f, 0.46f, 0.30f, 1f) : new Color(1f, 1f, 1f, 0.14f),
                          new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(210, 74));
        }

        ActivitySO Act(string key)
        {
            foreach (var a in DB.activities) if (a != null && a.key == key) return a;
            return null;
        }

        // ---------------- v0.30 日记页(按天数翻页)----------------
        // 玩家能读的就这一份:一天一页,里面是那天白天做过的事 + 那天夜里发生的事。
        // 状态读数(营地/海鸥/瓶子/刷新概率/暗线/真结局标记) **不再出现在这里** —— 那些是调试通道,留在控制台。
        int diaryPage;

        void OpenDiary() { OpenDiary(S.day); }

        void OpenDiary(int day)
        {
            diaryPage = EntryIndex(day);
            DrawDiary();
        }

        int EntryIndex(int day)
        {
            for (int i = 0; i < S.diary.Count; i++) if (S.diary[i].day == day) return i;
            return S.diary.Count - 1;
        }

        static Color DiaryColor(DiaryLineKind k)
        {
            switch (k)
            {
                case DiaryLineKind.Night: return new Color(0.64f, 0.74f, 0.95f);   // 夜里的事:冷蓝
                case DiaryLineKind.Egg: return new Color(0.72f, 0.58f, 0.92f);     // 彩蛋:与岛上暗线件同一支紫
                case DiaryLineKind.Bad: return new Color(0.95f, 0.52f, 0.42f);     // 失去/受伤:红
                default: return new Color(0.88f, 0.9f, 0.94f);                     // 白天:纸白
            }
        }

        void DrawDiary()
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "box", new Color(0.12f, 0.14f, 0.18f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-430, -320), new Vector2(430, 320));
            if (S.diary.Count == 0)
            {
                Ui.Label(box, "empty", "还没有可读的一页。", 18, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.82f),
                         new Vector2(0, 0), Vector2.one, new Vector2(-200, -20), new Vector2(200, 40));
            }
            else
            {
                diaryPage = Mathf.Clamp(diaryPage, 0, S.diary.Count - 1);
                var e = S.diary[diaryPage];
                Ui.Label(box, "day", SaveData.Date(e.day), 22, TextAnchor.UpperLeft, Color.white,
                         new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -52), new Vector2(-18, -18));
                // 日期下面那道横线(答案 8:换 —— 关键行换色 + 日期分隔)
                Ui.Panel(box, "rule", new Color(1, 1, 1, 0.14f), new Vector2(0, 1), new Vector2(1, 1),
                         new Vector2(18, -60), new Vector2(-18, -58));
                float y = -70;
                for (int i = 0; i < e.lines.Count; i++)
                {
                    var ln = e.lines[i];
                    // 中文按 ~46 字/行 估高度,免得长句叠在一起
                    int wrapped = Mathf.Max(1, Mathf.CeilToInt(ln.text.Length / 46f));
                    float h = 22f * wrapped;
                    Ui.Label(box, "l" + i, ln.text, 16, TextAnchor.UpperLeft, DiaryColor(ln.kind),
                             new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, y - h), new Vector2(-18, y));
                    y -= h + 8f;
                }
            }

            // 翻页:按天数走,不做滚动条
            int p = diaryPage;
            Ui.Button(box, "prev", "‹ 前一天", 18, () => { if (p > 0) { diaryPage = p - 1; DrawDiary(); } },
                      new Color(1, 1, 1, 0.10f), new Vector2(0, 0), new Vector2(0, 0),
                      new Vector2(18, 14), new Vector2(150, 56));
            Ui.Button(box, "next", "后一天 ›", 18, () => { if (p < S.diary.Count - 1) { diaryPage = p + 1; DrawDiary(); } },
                      new Color(1, 1, 1, 0.10f), new Vector2(1, 0), new Vector2(1, 0),
                      new Vector2(-150, 14), new Vector2(-18, 56));
            Ui.Label(box, "page", (diaryPage + 1) + " / " + S.diary.Count, 14, TextAnchor.MiddleCenter,
                     new Color(0.6f, 0.65f, 0.72f), new Vector2(0, 0), new Vector2(1, 0),
                     new Vector2(-60, 24), new Vector2(60, 48));
            Ui.Button(box, "close", "合上", 16, () => root.ClearModal(), new Color(1, 1, 1, 0.12f),
                      new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-70, 70), new Vector2(70, 104));
        }

        void OnDestroy()
        {
            // 道具是 Island 那层的子物体,不是本阶段的:阶段被销毁时不自己收,它们会留在结局画面上
            for (int i = 0; i < props.Count; i++) if (props[i] != null) Destroy(props[i]);
            props.Clear();
        }

        void LateUpdate()
        {
            if (S.phase != RunPhase.Island) return;
            root.KeepIslandView();
            DiagOnce();
        }

        // ---------------- 沙滩上的东西 ----------------
        // v0.46:夜晚"今晚用得上"的那件东西,悬浮时牌子换成这个亮黄(完好/破损的材质色不动,只动字)
        static readonly Color NightUsableGlow = new Color(1f, 0.86f, 0.30f, 1f);

        void BuildProps()
        {
            for (int i = 0; i < props.Count; i++) if (props[i] != null) Destroy(props[i]);
            props.Clear();
            var st = root.islandStage;
            if (st == null) return;

            var keys = new List<ItemSO>(S.storage.AllKinds());
            keys.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.Ordinal));
            foreach (var k in keys)
            {
                var it = k;
                // v0.37:这件东西 **现在能不能用** 由材质说 —— 完好色 / 破损暗红两种。
                //        v0.45 起不需要再判"还剩没剩完好件":一件东西只有两种状态。
                bool brokenLook = S.storage.HasBroken(k);
                // 点一下就直接执行(夜晚则直接做今晚那条选项);代价不再写在悬浮条上 —— 那条渠道已删
                var go = Prop(st.Slot(StableSlot(k)), k.displayName, PropShape(k), World.ItemMat(k, brokenLook),
                              () => { if (night) NightClick(it); else UseItem(it); }, HoverExtra(k));
                // v0.46(用户):"夜晚可以应对事件的道具,鼠标悬浮上去后把名字更改颜色"
                //   ⇒ 判据就是 夜晚那条选项的同一张表(NightResolver.ChoicesFor),不是另写一份名单 ——
                //     否则"标了亮色但点下去说帮不上忙"迟早会出现。绑在 篝火/大海/队友 上的那几条不在物品上,不发光。
                if (night)
                {
                    var ip = go.GetComponent<IslandProp>();
                    if (ip != null && NightResolver.ChoicesFor(k).Count > 0) ip.hoverColor = NightUsableGlow;
                }
                props.Add(go);
            }

            if (night)
            {
                // 夜里"篝火"这处不是物品,给它一块能点的地面(名字已经印在沙上了,不再挂标签)
                props.Add(PropAt(st.fireAnchor.position, "", PrimitiveType.Cylinder, new Color(0.30f, 0.20f, 0.10f),
                                 () => NightClickTarget("fire"), new Vector3(1.9f, 0.05f, 1.9f), 0.05f));
            }
            // 大海:v0.28 起 喝海水 已随喝水系统删除 ⇒ 白天点它什么都不发生,它只在夜晚当"徒手抓鱼/下水"的落点
            if (st.seaAnchor != null)
                props.Add(PropAt(st.seaAnchor.position, "", PrimitiveType.Cube, new Color(0.08f, 0.26f, 0.44f, 0.6f),
                                 () => { if (night) NightClickTarget("sea"); },
                                 new Vector3(8f, 0.06f, 3f), 0.04f));

            if (S.mate.present)
            {
                // v0.41:队友不再是"悬浮才出菜单",而是 **点一下开子面板**(谈话 / 喂食 /〔技能〕/〔食用〕)。
                // 夜晚仍然不开面板 —— 那晚他身上挂的是事件选项,点一下直接做。
                var mateGo = PropAt(st.mateAnchor.position, S.mate.who.displayName, PrimitiveType.Capsule,
                                    new Color(0.85f, 0.55f, 0.2f),
                                    () => { if (night) NightClickTarget("mate", NightResolver.CalmTalkOffered ? "说说话(免体力)" : null, CalmTalkNow); else OpenMatePanel(); },
                                    new Vector3(0.9f, 0.9f, 0.9f), 0.9f);
                props.Add(mateGo);
            }
            // v0.24:骸骨只在"它出现的那一晚"摆在地上 —— 第二天它就被海水带回去了(不掩埋、不留到白天)。
            //        白天没有可点的尸骨,所以也不再有"到白天再掩埋"这条路。
            if (night && NightResolver.ChoicesByTarget("bones").Count > 0)
                props.Add(PropAt(st.bonesAnchor.position, "骸骨", PrimitiveType.Capsule,
                                 new Color(0.86f, 0.84f, 0.76f),
                                 () => NightClickTarget("bones"),
                                 new Vector3(0.7f, 0.5f, 0.7f), 0.25f));

            // v0.27:调皮的猴子 也要有一个能点的身体。它叼着东西站在营地边,点它 = 空手扑上去(唯一入口);
            //        鱼叉 / 信号枪 / 篝火 那三条各自绑在自己的物件上,所以"拿着东西点"永远走不到这一条。
            //        没有专用挂点(那属于 §11-53 的布局轮),按营地方位推一格,挪 篝火 挂点它会跟着走。
            if (night && NightResolver.ChoicesByTarget("monkey").Count > 0)
                props.Add(PropAt(st.fireAnchor.position + new Vector3(-1.6f, 0f, 1.2f),
                                 "猴子", PrimitiveType.Capsule, new Color(0.42f, 0.30f, 0.20f),
                                 () => NightClickTarget("monkey"),
                                 new Vector3(0.6f, 0.8f, 0.6f), 0.4f));

            for (int i = 0; i < S.gull.presentCount; i++)
            {
                var p = st.gullAnchor.position + new Vector3(i * 1.15f - 1.7f, 0f, i % 2 * 0.9f);
                props.Add(PropAt(p, i == 0 ? "海鸥" : "", PrimitiveType.Sphere, new Color(0.94f, 0.95f, 0.97f),
                                 ScatterGulls, new Vector3(0.5f, 0.35f, 0.5f), 0f));
            }
            // v0.47:两把火各有各的火苗 —— 篝火 在 fireAnchor 那格,信号火堆 在自己那一格。
            //        原来那一条 "if (S.fireLitTonight)" 的火苗现在只算 篝火 的(否则 信号火堆 烧着时
            //        会在篝火的位置冒出一根不存在的火)。
            // v0.48:两把火各有各的火苗,**判据是"没坏"(= 还点着)**,不再是直接读计数器。
            if (S.HasStructure(Database.Campfire))
                props.Add(PropAt(st.fireAnchor.position + Vector3.up * 0.4f, "", PrimitiveType.Capsule,
                                 new Color(1f, 0.55f, 0.12f), null, new Vector3(0.7f, 0.4f, 0.7f), 0f));
            if (S.HasStructure(Database.SignalFire))
                props.Add(PropAt(StructureBase(st) + new Vector3(-4.2f, 1.7f, 0.4f), "", PrimitiveType.Capsule,
                                 new Color(1f, 0.68f, 0.20f), null, new Vector3(1.1f, 1.0f, 1.1f), 0f));

            // 建筑摆在篝火两侧;x 的符号按"屏幕左 = 世界 +x"取(相机 yaw=180)
            // v0.28:集水器 / 净水器 两件建筑随喝水系统删除,所以这里也不再摆它们(围墙与信号火堆照旧)
            StructureProp(st, Database.Wall, "围墙", PrimitiveType.Cube, new Vector3(7f, 0.9f, 0.18f),
                          new Color(0.52f, 0.4f, 0.26f), new Vector3(0f, 0.45f, -1.4f));
            // v0.48:两件火 **不用点** —— 建的时候就是烧着的。白天点它只会告诉你状态,夜晚点它交给 target "fire"。
            StructureProp(st, Database.SignalFire, "信号火堆", PrimitiveType.Cylinder, new Vector3(1.6f, 0.8f, 1.6f),
                          new Color(0.75f, 0.66f, 0.55f), new Vector3(-4.2f, 0.8f, 0.4f), FireInfoSignalFire, true);
            StructureProp(st, Database.Campfire, "篝火", PrimitiveType.Cube, new Vector3(1.1f, 0.24f, 1.1f),
                          new Color(0.35f, 0.26f, 0.18f), Vector3.zero, FireInfoCampfire, false, st.fireAnchor);
        }

        // burning = 这件建筑是"火"(要点的是它、牌子上要写剩余晚数);at 是相对营地方位的偏移,
        //           host 允许它改挂到别的挂点(篝火 用 fireAnchor,与那根火苗同一格,免得两样东西叠在一起)。
        // 建筑的统一基准点:fireAnchor 的父亲就是 "Island" 那一格(= IslandStage 所在的物体)。
        // 灶块与它上方那根火苗 **必须走同一个表达式**,否则一旦有人把 Island 挪出原点,两者就会分家。
        Vector3 StructureBase(IslandStage st)
        {
            return st.fireAnchor != null && st.fireAnchor.parent != null
                ? st.fireAnchor.parent.position : st.transform.position;
        }

        void StructureProp(IslandStage st, string key, string name, PrimitiveType shape, Vector3 scale, Color c, Vector3 at,
                           Action onClick = null, bool burning = false, Transform host = null)
        {
            if (!S.structures.Contains(key)) return;   // 建过就一直摆着:裂了也还在原地(下面换成裂开的颜色)
            // v0.46:围墙 会坏 ⇒ 建过 ≠ 还在用。灰盒里没有"裂开的模型",所以先用 **同一块几何换成破损暗红** 表达
            //        "它立在那儿,但这一晚它挡不住东西了"。以后换真模型时,这里就是那个口子。
            var baseAt = host != null ? host.position : StructureBase(st);
            var pos = baseAt + at;
            string label = name;
            Color col = S.HasStructure(key) ? c : BrokenStructureColor;
            if (burning)
            {
                // v0.48:**火的"坏"就是"熄了"** —— 与 围墙 共用同一套"坏 / 不坏"模型(用户:"统一成一套吧")。
                //   所以这里读的不再是计数器,而是 `HasStructure`(没坏 = 还点着);计数器只回答"还剩几晚"。
                bool lit = S.HasStructure(key);
                label = name + (lit ? "  烧 " + S.NightsLeft(key) + " 晚" : "  熄了 · 面板里重新点燃");
                if (lit) col = FireBurningColor;
                // ⚠ 这块灶与"夜里 篝火 那块能点的地面"(上面 night 分支)**在同一格重叠**,而 Unity 的射线取最近碰撞体
                //   ⇒ 夜里点上去命中的是这块灶。**所以夜晚不能在这里回一句"点火是白天的事"就完事** ——
                //   那会把绑在 target "fire" 上的四条夜晚选项(低温夜点灶 / 小影怪硬撑 / 赶猴子篝火驱赶 / 搜寻飞机 firepile)
                //   全部挡住。正确做法与物品那条一模一样:夜里点它 = 走夜晚那条路(上一轮我写成了拒绝,是 bug)。
                if (onClick != null) { var act = onClick; onClick = () => { if (night) NightClickTarget("fire"); else act(); }; }
            }
            props.Add(PropAt(pos, label, shape, col, onClick, scale, scale.y * 0.5f));
        }
        static readonly Color BrokenStructureColor = new Color(0.30f, 0.12f, 0.11f, 1f);   // 与 World.ItemMat 的破损色同一支
        static readonly Color FireBurningColor = new Color(1f, 0.62f, 0.16f, 1f);

        PrimitiveType PropShape(ItemSO k)
        {
            if (k is ToolSO) return PrimitiveType.Cube;
            if (k.consumable) return PrimitiveType.Sphere;
            return PrimitiveType.Cube;
        }

        GameObject Prop(Transform slot, string name, PrimitiveType shape, Material mat, Action onClick, string hover = null)
        {
            return PropAt(slot.position, name, shape, Color.white, onClick, Vector3.one * 0.75f, 0.375f, mat, hover);
        }

        // v0.38:悬浮时那块牌子换成"名字 + 剩余 / 精力价"(用户:不要单独的卡片)。
        //   点不动的东西(没绑任何行动的:材料/信号弹/毯子…)就只有名字,不额外说话。
        string HoverExtra(ItemSO k)
        {
            var parts = new List<string>();
            if (k.consumable) parts.Add("×" + S.storage.Count(k));
            // v0.44:手电筒的电量写在牌子上(2 格存储)—— 它是唯一"看不见的资源",不给个数字没法决策
            if (k == DB.Flashlight) parts.Add("电量 " + S.flashlightCharge + "/" + B.flashlightChargeMax);
            // 白天行动价只在白天显示 —— 夜晚那件东西挂的是事件选项,写"3⚡"会误导
            if (!night)
            {
                string act; Action run;
                if (TryBind(k, out act, out run))
                {
                    var a = Act(act);
                    if (a != null && a.cost > 0)
                    parts.Add(a.costAllRemaining ? "吃满今天" + BoltGlyph : a.cost + BoltGlyph);
                }
            }
            return parts.Count == 0 ? null : k.displayName + "  " + string.Join("  ", parts);
        }

        // ⚠ v0.38(用户):消耗 **≥2 精力** 的行动,点下去之前先确认一次 —— 这是全局"不做确认框"那条规矩的
        //   **唯一例外**;1 点与 0 点的行动照旧"点即执行"。
        //   阈值写成代码常量而不是 BalanceConfig 字段:SO 里缺 key 的数值字段会读成 0(§7.2 第一条铁律),
        //   那样等于"所有行动都要确认",这个坑不值得冒。
        //   本轮只管 **两条一步到位的入口**:点物品、点"探索荒岛"。制造面板与队友菜单里的 ≥2⚡ 行动
        //   **没加**(它们本身已经在面板里点了一次),要不要一起加等用户说。
        const int ConfirmAtStamina = 2;

        bool ConfirmStamina(string what, int cost, Action go)
        {
            if (cost < ConfirmAtStamina) { go(); return true; }
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "box", new Color(0.13f, 0.15f, 0.19f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-280, -120), new Vector2(280, 120));
            Ui.Label(box, "t", what, 20, TextAnchor.UpperCenter, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -46), new Vector2(0, -14));
            Ui.Label(box, "c", "要花 " + cost + BoltGlyph + "   今天还剩 " + S.stats.stamina + BoltGlyph, 16,
                     TextAnchor.UpperCenter, new Color(0.95f, 0.8f, 0.45f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -78), new Vector2(0, -50));
            Ui.Button(box, "ok", "确认 · 花 " + cost + BoltGlyph, 16,
                      () =>
                      {
                          root.ClearModal();
                          go();
                          // 确认之后要把这一手的后果画出来(与"点即执行"那条路做同样两步收尾)
                          if (S.phase == RunPhase.Island) { CheckDeath(); Refresh(); }
                      }, new Color(0.85f, 0.5f, 0.18f, 1f),
                      new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(16, 16), new Vector2(-20, 52));
            Ui.Button(box, "no", "先不做", 16, () => root.ClearModal(), new Color(0.25f, 0.28f, 0.34f, 1f),
                      new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(20, 16), new Vector2(-16, 52));
            return false;
        }

        // 每件物品的位置固定:落点由"它在数据表里的序号"决定,不随仓库增减而挪动。
        // (以前是"按名字排序 + 顺序填格子",捡到一件新的就把后面全部推一格,屏幕上东西天天换地方。)
        int StableSlot(ItemSO k)
        {
            var st = root.islandStage;
            int n = st != null && st.propSlots.Count > 0 ? st.propSlots.Count : 1;
            int idx = ItemTableIndex(k);
            if (idx < 0) idx = StableHash(k.key);
            idx %= n;
            return idx < 0 ? idx + n : idx;      // 哈希可能是负数,别把负数喂给 Slot()
        }

        // 先按 §5.1 拾荒池的表序,再按 §6 制作表的表序 —— 两份都是资产里写死的顺序。
        // 去重:雨伞/渔网/鱼饵 既在拾荒池又有配方,只占拾荒池那一个序号;
        // 建筑配方(resultItem 为空)不占序号,因为它们走 StructureProp 而不是仓库道具。
        int ItemTableIndex(ItemSO k)
        {
            int i = 0;
            foreach (var it in DB.scavengedItems) { if (it == k) return i; i++; }
            foreach (var r in DB.recipes)
            {
                if (r == null || r.resultItem == null) continue;
                if (DB.scavengedItems.Contains(r.resultItem)) continue;
                if (r.resultItem == k) return i;
                i++;
            }
            if (k == DB.Herb) return i;      // v0.16:岛上采的那一味,接在表尾(哈希会撞上别的格子)
            return -1;
        }

        static int StableHash(string key)
        {
            int h = unchecked((int)2166136261);
            if (key != null)
                for (int i = 0; i < key.Length; i++) { h ^= key[i]; h = unchecked(h * 16777619); }
            return h;
        }

        // 名字仍然贴在物体朝镜头那一面的表面上(不飘空)。朝向不再自己算:
        // 用机舱里每件物品都在用的 billboard 那条路(World.LabelAt + FaceCamera)——
        // 那条路在本项目里是被验证过"读得通"的,而 LabelFlat 的左右手序我连着猜错两次。
        // 相机是固定的,所以 billboard 与"印在面上"在这一层看起来完全一样。
        GameObject PropAt(Vector3 groundPos, string name, PrimitiveType shape, Color c, Action onClick,
                          Vector3 scale, float lift, Material mat = null, string hover = null)
        {
            var go = mat != null
                ? World.Primitive(shape, name, groundPos + Vector3.up * lift, scale, mat,
                                  root.islandStage.transform, null, onClick != null)
                : World.Primitive(shape, name, groundPos + Vector3.up * lift, scale, c,
                                  root.islandStage.transform, null, onClick != null);
            TextMesh tm = null;
            if (!string.IsNullOrEmpty(name))
            {
                var at = go.transform.position + Vector3.up * (scale.y * 0.28f)
                         + FaceToCam(go.transform.position) * (Mathf.Max(scale.x, scale.z) * 0.5f + World.LabelGap);
                tm = World.LabelAt(go.transform, name, at, 0.14f, Color.white, TextAnchor.MiddleCenter);
            }
            if (onClick != null || !string.IsNullOrEmpty(hover))
            {
                var p = go.AddComponent<IslandProp>();
                p.onClick = onClick;
                p.label = tm; p.baseText = name; p.hoverText = hover;   // 悬浮只改这块牌子的字
            }
            return go;
        }

        Vector3 FaceToCam(Vector3 at)
        {
            var pose = root.islandStage != null ? root.islandStage.cameraPose : null;
            if (pose == null) return Vector3.forward;
            var d = pose.position - at;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? Vector3.forward : d.normalized;
        }

        // ---------------- 点一件东西 = 直接用它 ----------------
        // v0.15:物品不弹子菜单 —— 点一下就把那件事做完,代价写在悬浮条上(全局口径:不做确认框)。
        // 什么都做不了的物品(材料 / 信号弹 / 毯子 / 雨伞 / 渔网 / 暗线件)点一下什么都不发生。
        void UseItem(ItemSO k)
        {
            string act; Action run;
            if (!TryBind(k, out act, out run)) return;
            var a = Act(act);
            string why;
            if (a != null && !CanDo(a, out why)) { S.Log(k.displayName + ":" + why); return; }
            // v0.38:≥2 精力的行动先过确认窗(那条规矩的唯一例外);0/1 点的照旧一步做完
            int cost = a == null ? 0 : (a.costAllRemaining ? S.stats.stamina : a.cost);
            if (!ConfirmStamina(k.displayName + " — " + (a == null ? "" : a.displayName), cost, run)) return;
            if (S.phase != RunPhase.Island) return;
            CheckDeath();
            Refresh();
        }

        bool TryBind(ItemSO k, out string actKey, out Action run)
        {
            if (k == DB.Can)        { actKey = "eat";   run = EatCan;       return true; }
            if (k == DB.Chocolate)  { actKey = "eat";   run = EatChocolate; return true; }
            if (k == DB.Coconut)    { actKey = "coconut"; run = OpenCoconut; return true; }
            if (k == DB.MedKit)     { actKey = "heal";  run = UseMedKit;    return true; }
            if (k == DB.Medicine)   { actKey = "heal";  run = UseMedicine;  return true; }
            if (k == DB.FishingRod) { actKey = "fish"; run = DoFish; return true; }   // v0.21:简易钓钩 已删
            // v0.45(用户):潜水装置 / 鱼叉 / 简易浮镜 各点各的 —— 点哪件就用哪件下水,
            //        不再由代码替你挑"手里最好的那把";损坏也只落在你真正用的那件上。
            if (k == DB.DiveGear)   { actKey = "dive"; run = () => DoDive(DB.DiveGear); return true; }
            if (k == DB.Spear)      { actKey = "dive"; run = () => DoDive(DB.Spear); return true; }
            if (k == DB.SimpleMask) { actKey = "dive"; run = () => DoDive(DB.SimpleMask); return true; }
            if (k == DB.Flashlight) { actKey = "charge"; run = DoCharge;      return true; }
            if (k == DB.Bottle)     { actKey = "throwbottle"; run = DoThrowBottle; return true; }
            actKey = null; run = null; return false;
        }

        void EatCan()
        {
            S.storage.Remove(DB.Can, 1);
            S.stats.fullness = Mathf.Clamp01(S.stats.fullness + B.foodPerCanFullness);
            S.Log("吃了 1 份罐头(+" + Mathf.RoundToInt(B.foodPerCanFullness * 100) + "% 饱食)。");
            D("eat");
        }

        void EatChocolate()
        {
            S.storage.Remove(DB.Chocolate, 1);
            S.stats.stamina = Mathf.Min(EffectiveStaminaMax(), S.stats.stamina + B.chocolateStamina);
            S.Log("吃了巧克力棒:+" + B.chocolateStamina + " 精力 —— 白天唯一能加精力的东西。");
            D("chocolate");
        }

        void OpenCoconut()
        {
            // v0.39:开椰子不再花精力(用户:白天可使用的物品里只有 潜水装置/鱼叉/钓鱼竿/漂流瓶 有精力消耗)
            S.storage.Remove(DB.Coconut);
            S.stats.fullness = 1f;
            Heal(1, "椰子");
            S.Log("开了椰子:饱食回满,+1 生命(0 精力)。");
            D("coconut");
        }

        // v0.28:DrinkFresh / DrinkDirty / DrinkSeawater / ApplyDirtyWater / SeaTipText 全部删除
        //        —— 喝水系统整条移除(淡水/脏水/海水/集水器/净水器/3天禁水 一起走),
        //        生存线只剩 饱食(罐头)一条资源数值。生病 留着,但它现在只有 低温夜 与 雨 两个来源。

        // 医疗箱必定成功;只有自制药品掷 70%(§2.3,两条结算不许合并)
        // v0.39:用药不再花精力(同上那条口径 —— 治生病不该再收一次体力税)
        void UseMedKit()
        {
            S.storage.Remove(DB.MedKit, 1); ApplyMedicine(1f, "医疗箱");
        }

        void UseMedicine()
        {
            S.storage.Remove(DB.Medicine, 1); ApplyMedicine(B.medicineSuccessChance, "自制药品");
        }

        // v0.39:手摇充电 **不再花精力**。原来它是"全游戏最贵的单个白天行动"(3 精力),
        //        现在它的唯一代价是"要占掉白天的一次点击,而且只能在没电的时候摇"。
        //        ⚠ 后果记在 §10:手电筒那条 50% 直通结局A 的线,门槛从"3 点精力"降到"一次免费点击"。
        // v0.44:一格电 = 1 精力(存储上限 2 格,开局随机 0~2)。v0.39 那句"充电 0 精力"到此为止 ——
        //        它现在不是"全游戏最贵的白天行动",也不再是免费的:1 点精力换 1 格,一次只能充一格。
        void DoCharge()
        {
            Spend(1);
            S.flashlightCharge = Mathf.Min(B.flashlightChargeMax, S.flashlightCharge + 1);
            S.Log("手摇充电:+1 格电(1 精力)—— 现在 " + S.flashlightCharge + "/" + B.flashlightChargeMax + "。");
            D("charge");
        }

        // v0.24:白天"掩埋尸骨"这个动作随骸骨改成独立事件一起删除了(不掩埋、第二天自动消失)。

        // ---------------- 队友面板(v0.41:点击才开,悬浮那条路整个废掉) ----------------
        // 三段:左上 = 头像(灰盒色块)+ 名字;中间 = 状态(饱食 4 段 / 精神 4 层 / 生病);下方 = 操作。
        // ⚠ 这一改顺带结束了 v0.33~v0.36 那几轮"悬浮面板位置怎么都不对"的拉锯 ——
        //   面板现在是画布正中的 modal,不再跟着世界点投影,所以 PlaceBesideObject / UiScale 一起删了。
        //   点队友 = 开面板;夜晚点队友 仍走事件选项(NightClickTarget("mate")),不受这里影响。

        void OpenMatePanel()
        {
            if (!S.mate.present) return;
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.66f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "mateBox", new Color(0.10f, 0.12f, 0.16f, 1f),
                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-260, -300), new Vector2(260, 300));

            // —— 左上:头像 + 名字 ——
            Ui.Panel(box, "avatar", S.mate.who.color, new Vector2(0, 1), new Vector2(0, 1),
                     new Vector2(18, -100), new Vector2(114, -4));
            Ui.Label(box, "name", S.mate.who.displayName, 22, TextAnchor.UpperLeft, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(128, -48), new Vector2(-18, -14));
            Ui.Label(box, "prof", S.mate.who.profession + (S.mate.sick ? "  ·  生病" : ""), 15,
                     TextAnchor.UpperLeft, new Color(0.72f, 0.76f, 0.84f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(128, -82), new Vector2(-18, -52));

            // —— 中间:状态 ——
            Ui.Label(box, "sh", "— 状态 —", 15, TextAnchor.UpperLeft, new Color(0.6f, 0.65f, 0.72f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -138), new Vector2(-18, -112));
            StatRow(box, "hunger", "饱食", HungerName(S.mate.hunger), 4 - (int)S.mate.hunger,
                    new Color(0.86f, 0.66f, 0.28f), -176);
            StatRow(box, "mood", "精神", MoodName(S.mate.mood), 4 - (int)S.mate.mood,
                    new Color(0.46f, 0.68f, 0.92f), -214);
            Ui.Label(box, "skill", S.mate.skillOfferedToday && !S.mate.skillActiveToday
                        ? "今天他/她想起了什么 —— 要 1 精力去问" : " ",
                     14, TextAnchor.UpperLeft, new Color(0.9f, 0.82f, 0.5f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -252), new Vector2(-18, -228));

            // —— 下方:操作 ——
            var rows = new List<string>(); var acts = new List<Action>(); var en = new List<bool>();
            var cols = new List<Color>();
            string why = "";

            var talk = Act("talk");
            bool talkOk = talk != null && CanDo(talk, out why);
            rows.Add("聊天 — " + (talk == null ? "1 精力" : StaminaCostText(talk)) + "  精神回升 " + TalkMoodNow() + " 层" +
                     (talkOk ? "" : "(" + why + ")"));
            acts.Add(DoTalk); en.Add(talkOk); cols.Add(new Color(0.20f, 0.30f, 0.44f, 1f));

            var feed = Act("feed");
            bool feedOk = feed != null && CanDo(feed, out why);
            rows.Add("喂食 — 0 精力  给 1 份罐头,饱食回到饱食" + (feedOk ? "" : "(" + why + ")"));
            acts.Add(DoFeed); en.Add(feedOk); cols.Add(new Color(0.20f, 0.36f, 0.30f, 1f));

            // v0.46(用户):这条按钮 **只在当天真的刷出了技能时出现**,名字按他给的叫"帮助"(机制名仍是"技能")。
            if (S.mate.skillOfferedToday && !S.mate.skillActiveToday)
            {
                rows.Add("帮助 — 1 精力  " + S.mate.who.skillDesc);
                acts.Add(DoSkill); en.Add(S.stats.stamina >= 1); cols.Add(new Color(0.34f, 0.30f, 0.46f, 1f));
            }
            // 食用:只有"没罐头 且 玩家已经饿到 0"才出现,而且要点两次确认(见 ConfirmMeal)
            if (MealAvailable)
            {
                rows.Add("食用 — 他/她成为食物:饱食回满,但接下来 " + MealFoodLockDays + " 天你什么都咽不下去");
                acts.Add(ConfirmMeal); en.Add(true); cols.Add(new Color(0.46f, 0.16f, 0.14f, 1f));
            }

            float y = -290;
            for (int i = 0; i < rows.Count; i++)
            {
                int idx = i;
                var b = Ui.Button(box, "a" + i, rows[i], 15,
                    () =>
                    {
                        if (!en[idx]) return;
                        root.ClearModal();
                        acts[idx]();
                        if (S.phase == RunPhase.Island) { CheckDeath(); Refresh(); }
                    },
                    cols[idx], new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, y - 40), new Vector2(-18, y));
                Ui.SetEnabled(b, en[idx]);
                y -= 44;
            }
            Ui.Button(box, "close", "关上", 15, () => root.ClearModal(), new Color(1, 1, 1, 0.10f),
                      new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 52));
        }

        // 一行状态:标签 + 4 个方块(点亮几段 = 还剩几段)+ 层级名。
        // 方块用几何体画,不靠字体里的符号字形(华文行楷不一定收 ●○,见 §7.1 那条 Glyph 规矩)。
        void StatRow(Transform box, string key, string label, string name, int lit, Color c, float y)
        {
            Ui.Label(box, key + "L", label, 15, TextAnchor.UpperLeft, new Color(0.8f, 0.84f, 0.9f),
                     new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, y - 24), new Vector2(86, y));
            for (int i = 0; i < 4; i++)
            {
                float x = 96 + i * 26;
                Ui.Panel(box, key + i, i < lit ? c : new Color(1, 1, 1, 0.10f),
                         new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y - 20), new Vector2(x + 20, y - 2));
            }
            Ui.Label(box, key + "N", name, 15, TextAnchor.UpperLeft, c,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(212, y - 24), new Vector2(-18, y));
        }

        // 第五种失去队友的路径(前四种:自杀 / 饥荒 / 小影怪那晚 / 影怪那夜引开)。
        // 出现条件写死:**仓库没有罐头 且 玩家饱食度已经归零** —— 它是"要么吃人要么饿着"的那一格。
        // v0.46(用户):吃掉队友的回饱是"一次回满"(与 椰子 同类),代价改挂在天数上 —— **三天吃不下东西**。
        //   常量写在代码里、没进 BalanceConfig:§7.2 那条"资产缺这个键 → 数值静默读成 0",
        //   一个读成 0 的 foodLockDays 等于这条惩罚根本不存在。
        public const int MealFoodLockDays = 3;

        bool MealAvailable { get { return S.mate.present && !S.storage.Has(DB.Can) && S.stats.fullness <= 0f; } }

        // ⚠ "不做确认框"这条全局规矩现在有两个例外:v0.38 的"消耗 ≥2 精力"、v0.41 的"食用队友"。
        //   食用要 **两次**:第一次说清代价,第二次再问一遍 —— 之后队友永久不回来。
        void ConfirmMeal()
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "meal1", new Color(0.14f, 0.10f, 0.10f, 1f),
                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -130), new Vector2(250, 130));
            Ui.Label(box, "t", "第一次确认", 20, TextAnchor.UpperCenter, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -48), new Vector2(0, -16));
            Ui.Label(box, "b", "没有罐头了,你也已经饿到什么都没有。\n吃掉他/她 = 队友永久消失;饱食回满,但接下来 " + MealFoodLockDays + " 天你什么都咽不下去。",
                     15, TextAnchor.UpperCenter, new Color(0.9f, 0.86f, 0.86f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108), new Vector2(0, -54));
            Ui.Button(box, "no", "不,还有别的办法", 16, OpenMatePanel, new Color(0.24f, 0.30f, 0.38f, 1f),
                      new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 54));
            Ui.Button(box, "yes", "继续", 16, ConfirmMealAgain, new Color(0.52f, 0.26f, 0.20f, 1f),
                      new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 76), new Vector2(-18, 114));
        }

        void ConfirmMealAgain()
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "meal2", new Color(0.16f, 0.09f, 0.09f, 1f),
                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, -130), new Vector2(250, 130));
            Ui.Label(box, "t", "最后一次", 20, TextAnchor.UpperCenter, new Color(0.95f, 0.62f, 0.5f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -48), new Vector2(0, -16));
            Ui.Label(box, "b", "从今天起,这座岛上只剩我一个人说话。\n现在收手还来得及。",
                     15, TextAnchor.UpperCenter, new Color(0.9f, 0.86f, 0.86f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108), new Vector2(0, -54));
            Ui.Button(box, "no", "算了", 16, OpenMatePanel, new Color(0.24f, 0.30f, 0.38f, 1f),
                      new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 54));
            Ui.Button(box, "yes", "动手", 16, DoMeal, new Color(0.62f, 0.20f, 0.16f, 1f),
                      new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 76), new Vector2(-18, 114));
        }

        void DoMeal()
        {
            string who = S.mate.who.displayName;
            // v0.46(用户):"椰子是回饱,吃队友也回饱,但是吃队友会导致三天吃不了东西"
            //   ⇒ 回饱量不再是"我拍的一个比例"(原来那个 BalanceConfig.mealFullnessGain = 0.5 已连字段一起删):
            //     它与 椰子 同类,**一次回满**;代价改挂在"之后三天咽不下任何东西"上(闸在 CanDo 的 eat / coconut)。
            //     用药不挡 —— 否则那三天连生病都治不了,惩罚就从"代价"变成"死刑"。
            S.stats.fullness = 1f;
            S.stats.foodLockDays = MealFoodLockDays;
            S.Log("你吃掉了" + who + ":饱食回满 —— 但接下来 " + MealFoodLockDays + " 天你什么都咽不下去(罐头 / 椰子 / 巧克力 全吃不了)。");
            S.Diary(DiaryLines.Day("matemeal", S.day), DiaryLineKind.Bad, "matemeal");   // Bad 那支颜色:红
            LoseTeammate("食用");
            root.ClearModal();
            CheckDeath();
            Refresh();
        }
        void ScatterGulls()        {
            int n = S.gull.presentCount;
            S.gull.presentCount = 0;
            S.Log("你点了一下海鸥:" + n + " 只全部飞走了(gullFedOnce 仍在,下一次海鸥事件它们会免费回来)。");
            Refresh();
        }

        // ---------------- 悬浮:只剩队友那一个菜单 ----------------
        // ⚠ v0.37:物品的悬浮卡片整条删除(用户:"除了队友菜单,其余都不需要卡牌了")。
        //   物品"坏没坏"改由 **两种材质** 表达(见 `World.ItemMat`),以后换成两种建模/贴图也只改那一个函数。
        //   代价要记清楚:卡片原来还带着 消耗品"剩余 N"、白天行动的精力价、夜晚"这件能做什么"
        //   与"今晚它帮不上忙"之外的白天拒绝理由 —— 这些现在 **在界面上没有出口了**(只进控制台)。

        // HUD 画布挂着 CanvasScaler(参考 1280×720,match 0.5)⇒ **1 画布单位 ≠ 1 屏幕像素**:
        // v0.33~v0.36 那几轮"悬浮面板越漂越远"的根因就是直接把 Input.mousePosition 当画布单位用。
        // v0.41 起队友面板改成画布正中的 modal,物品卡片也删了 ⇒ 这套投影换算(UiScale/PlaceBesideObject)
        // 没有读者了,已删除。**规矩留在 §7.2 那条铁律里:任何"跟着鼠标/世界点走"的 uGUI 都要先除 scaleFactor。**

        static string StaminaCostText(ActivitySO a)
        {
            return a.costAllRemaining ? "吃掉全部剩余精力" : (a.cost > 0 ? "消耗 " + a.cost + " 点精力" : "0 精力");
        }

        static string Pct(float v) { return Mathf.RoundToInt(v * 100) + "%"; }
        public static string MoodName(MoodLevel m)
        {
            switch (m) { case MoodLevel.Good: return "良好"; case MoodLevel.Lonely: return "孤独"; case MoodLevel.Depressed: return "沮丧"; default: return "崩溃"; }
        }
        public static string HungerName(HungerLevel h)
        {
            switch (h) { case HungerLevel.Full: return "饱食"; case HungerLevel.Empty: return "空腹"; case HungerLevel.Hungry: return "饥饿"; default: return "饥荒"; }
        }

        // ---------------- 行动 ----------------
        // v0.45(用户):精力不满 3 点就不能 潜水 / 探索荒岛 —— 这两件是"把一整天押出去"的行动。
        //        写成代码常量而不是 BalanceConfig 字段:§7.2 那条"资产里没这个键 → 读到 0"的坑。
        //        也没有用 staminaMax:飞行员会把上限抬到 4,那条规矩就会反过来变成减益。
        public const int FullDayStamina = 3;

        bool CanDo(ActivitySO a, out string reason)
        {
            reason = null;
            // 走在通用价目判定之前:潜水 的 3 点价本来就会被下面那句挡掉,但那样只能说"体力不足"。
            if ((a.key == "explore" || a.key == "dive") && S.stats.stamina < FullDayStamina)
            {
                reason = "要满 " + FullDayStamina + " 点体力才" + (a.key == "dive" ? "下得了水" : "出得去");
                return false;
            }
            // v0.46(用户):"吃队友会导致三天吃不了东西" ⇒ 吃罐头 / 吃巧克力 / 开椰子 三件一起挡。
            //   用药(医疗箱 / 自制药品)不算"吃东西",不挡 —— 否则那三天连生病都治不了,变成死刑而不是代价。
            if ((a.key == "eat" || a.key == "coconut") && S.stats.foodLockDays > 0)
            { reason = "咽不下去 —— 还要 " + S.stats.foodLockDays + " 天才吃得下东西"; return false; }
            int cost = a.costAllRemaining ? 1 : a.cost;
            if (a.key != "endday" && S.stats.stamina < cost)
            {
                if (a.costAllRemaining && S.stats.stamina <= 0) { reason = "没有体力了"; return false; }
                if (!a.costAllRemaining) { reason = "体力不足"; return false; }
            }
            switch (a.key)
            {
                case "explore":
                    if (!S.explore.availableToday) { reason = "今天没有可探索的发现"; return false; }
                    if (S.explore.exploredToday) { reason = "今天已经探索过了"; return false; }
                    break;
                case "fish":
                    if (!CanFish()) { reason = "需要 钓鱼竿 / 领航员技能"; return false; }
                    break;
                case "dive":
                    if (!CanDive()) { reason = "需要 潜水装置 / 鱼叉 / 简易浮镜"; return false; }
                    break;
                case "coconut":
                    if (!S.storage.Has(DB.Coconut)) { reason = "没有椰子"; return false; }
                    break;
                case "repair":
                    if (BrokenList().Count == 0 && BrokenStructures().Count == 0) { reason = "没有破损的东西"; return false; }
                    break;
                case "charge":
                    if (!S.storage.Has(DB.Flashlight)) { reason = "没有手电筒"; return false; }
                    if (S.flashlightCharge >= B.flashlightChargeMax) { reason = "电量已经满了"; return false; }
                    break;
                case "talk":
                    if (!S.mate.present) { reason = "没有队友"; return false; }
                    break;      // v0.18:每天不限次(第一次 +2 层,之后每次 +1 层),上限只由精力决定
                case "feed":
                    if (!S.mate.present) { reason = "没有队友"; return false; }
                    if (fedToday) { reason = "今天已经喂过了"; return false; }
                    if (!S.storage.Has(DB.Can)) { reason = "没有罐头"; return false; }
                    break;
                case "eat":
                    if (!S.storage.Has(DB.Can) && !S.storage.Has(DB.Chocolate)) { reason = "没有可吃的"; return false; }
                    break;
                case "heal":
                    if (!S.storage.Has(DB.MedKit) && !S.storage.Has(DB.Medicine)) { reason = "没有医疗箱或自制药品"; return false; }
                    if (!S.stats.sick && S.stats.hp >= B.hpMax) { reason = "不需要"; return false; }
                    break;
                case "throwbottle":
                    if (!S.storage.Has(DB.Bottle)) { reason = "没有漂流瓶"; return false; }
                    break;
            }
            return true;
        }

        int EffectiveStaminaMax() { return S.stats.staminaMax - (S.stats.sick ? 1 : 0) - S.stats.capPenaltyToday; }   // 飞行员的 +1 已在 DoTalk 里写进 staminaMax,别重复加
        bool CanFish() { return Usable(DB.FishingRod) || NavigatorActive(); }   // v0.21:简易钓钩 已删
        bool CanDive() { return Usable(DB.DiveGear) || Usable(DB.Spear) || Usable(DB.SimpleMask); }
        bool Usable(ItemSO i) { return S.storage.Has(i, 1); }
        bool NavigatorActive() { return S.mate.present && S.mate.skillActiveToday && S.mate.who == DB.Navigator; }
        bool MechanicActive() { return S.mate.present && S.mate.skillActiveToday && S.mate.who == DB.Mechanic; }
        bool PilotActive() { return S.mate.present && S.mate.skillActiveToday && S.mate.who == DB.Pilot; }

        List<ItemSO> BrokenList()
        {
            var l = new List<ItemSO>();
            foreach (var k in S.storage.broken.Keys) l.Add(k);
            return l;
        }

        // v0.46:坏掉的建筑(现在只有 围墙 一家)也列进修理那一段
        List<string> BrokenStructures()
        {
            var l = new List<string>();
            foreach (var k in S.brokenStructures) l.Add(k);
            return l;
        }

        public void Spend(int n)
        {
            S.stats.stamina = Mathf.Max(0, S.stats.stamina - n);
        }

        // 白天行动全部由"点那件东西"直接分派(见 UseItem),这里不再有统一的 DoActivity 入口。
        void CheckDeath() { if (S.stats.hp <= 0) root.EndRun(EndingResolver.DeathEnding(S)); }

        public void Heal(int n, string why)
        {
            int before = S.stats.hp;
            S.stats.hp = Mathf.Min(B.hpMax, S.stats.hp + n);
            if (S.stats.hp > before) S.Log("+" + (S.stats.hp - before) + " 生命(" + why + ")");
        }

        // 探索按钮的入口:DoExplore 自己不收尾(以前是 DoActivity 统一 CheckDeath + Refresh),
        // 现在没有 DoActivity 了,这一步必须自己补上,否则探索完画面不更新。
        void DoExploreNow()
        {
            // 探索吃掉当天全部剩余精力 ⇒ 只要今天还剩 ≥2 点,它就要过确认窗
            if (!ConfirmStamina("探索荒岛", S.stats.stamina, DoExplore)) return;
            if (S.phase != RunPhase.Island) return;
            CheckDeath();
            Refresh();
        }

        void DoExplore()
        {
            int spent = S.stats.stamina;
            S.stats.stamina = 0;
            S.explore.exploredToday = true;
            S.explore.availableToday = false;
            S.explore.refreshP = B.exploreRefreshBase;      // 只有真的探索才回落

            int mat = Rolls(B.exploreMaterialChance, B.exploreMaterialRolls);
            int food = Rolls(B.exploreFoodChance, B.exploreFoodRolls);
            int herb = Rolls(B.exploreHerbChance, B.exploreHerbRolls);   // v0.16:20% 一次判定
            // v0.28:探索不再产椰子 —— 椰子的唯一来源改成夜晚事件「椰树」(§3.2)
            if (mat > 0) S.storage.Add(DB.Material, mat);
            if (food > 0) S.storage.Add(DB.Can, food);
            if (herb > 0) S.storage.Add(DB.Herb, herb);
            S.Log("探索荒岛(吃掉 " + spent + " 点体力):材料 +" + mat + "、罐头 +" + food +
                  (herb > 0 ? "、药草 +" + herb : ""));
            D("explore", Sum(mat, "份材料", food, "份罐头", herb, "份药草"));

            // 暗线:持图那天 100% 捡回藏宝箱(§12.2)
            if (S.hidden.hasMap && !S.hidden.hasChest)
            {
                S.hidden.hasChest = true;
                S.storage.Add(DB.Chest, 1);
                S.hidden.everOwned.Add(DB.Chest);
                S.Log("按图挖出了 藏宝箱(2 格,直接进仓库,不做二次搬运)。");
            }
            LoreRoll("explore");

            // 无队友 → 25% 看家事故:只丢 1 件仓库物品(v0.12)
            if (!S.mate.present && S.rng.NextDouble() < B.campAccidentChance)
            {
                var lost = RandomStorageItem();
                if (lost != null)
                {
                    S.storage.Remove(lost, 1);
                    S.Log("营地事故:没人看家," + lost.displayName + " 不见了(-1 件仓库物品)。");
                }
            }
        }

        void DoFish()
        {
            Spend(1);
            float miss = Mathf.Min(B.fishMissCap, B.fishMissBase + B.fishMissPerDay * (S.day - 1));
            if (NavigatorActive()) miss = 0f;
            // v0.18:鱼饵是"开关",不是有就自动挂 —— 挂上则上鱼概率至少 80%(本来就更高就按实际)
            // v0.21:鱼饵只加上钩率,不加产出("产出 +1"文档写了三年、代码里从来没有,现在按你的裁定把文档删了)
            bool baited = S.useBait && S.storage.Has(DB.Bait);
            float catchP = 1f - miss;
            if (baited) catchP = Mathf.Max(catchP, B.baitCatchFloor);
            int caught = 0;
            bool baitSpent = false;
            if (S.rng.NextDouble() < catchP)
            {
                caught = 1;
                if (baited && S.rng.NextDouble() < B.baitConsumeChance) { S.storage.Remove(DB.Bait, 1); baitSpent = true; }
                if (NavigatorActive()) caught *= 2;
            }
            if (caught > 0)
            {
                S.storage.Add(DB.Can, caught);
                S.Log("钓鱼:上鱼了,罐头 +" + caught + (baited ? "(挂了鱼饵" + (baitSpent ? ",这份被吃掉了" : ",这份还在") + ")" : ""));
                D("fish", caught + " 份罐头");
            }
            else
            {
                S.Log("钓鱼:空军(第 " + S.day + " 天上鱼率 " + Mathf.RoundToInt(catchP * 100) + "%"
                      + (baited ? ",挂了鱼饵" : "") + ")。");
                D("fishmiss");
            }

            if (S.rng.NextDouble() < B.keyChanceFish && !S.hidden.hasKey)
            {
                S.hidden.hasKey = true; S.storage.Add(DB.Key, 1);
                S.Log("鱼肚子里有一颗 宝藏钥匙(5%,不需要藏宝图)。");
            }
            LoreRoll("fish");
            Wear(DB.FishingRod);
        }

        // v0.45:下水用哪件工具 = 你点了哪件。产出按"是不是潜水装置"分两档,
        //        损坏只掷在这一件上(wear 的概率账本来就按工具分别累积,鱼叉坏了不影响浮镜)。
        void DoDive(ItemSO tool)
        {
            Spend(3);
            bool full = tool == DB.DiveGear;
            float mult = full ? 1f : B.weakToolChanceMultiplier;
            int food = 0, bait = 0;
            for (int i = 0; i < B.diveFoodRolls; i++) if (S.rng.NextDouble() < B.diveFoodChance * mult) food++;
            for (int i = 0; i < B.diveBaitRolls; i++) if (S.rng.NextDouble() < B.diveBaitChance * mult) bait++;
            if (NavigatorActive()) { food *= 2; bait *= 2; }
            if (food > 0) S.storage.Add(DB.Can, food);
            if (bait > 0) S.storage.Add(DB.Bait, bait);
            S.Log("带着 " + tool.displayName + " 下水:罐头 +" + food + "、鱼饵 +" + bait
                  + (full ? "" : "(非潜水装置:概率减半)"));
            D("dive", Sum(food, "份罐头", bait, "份鱼饵", 0, ""));

            if (!NavigatorActive() && S.rng.NextDouble() < B.diveInjuryChance) Damage(1, "潜水受伤");
            if (S.rng.NextDouble() < B.keyChanceDive && !S.hidden.hasKey)
            {
                S.hidden.hasKey = true; S.storage.Add(DB.Key, 1);
                S.Log("礁缝里摸到了 宝藏钥匙(15%,不需要藏宝图)。");
            }
            Wear(tool);
        }

        // 谈话:只动精神。v0.15 起它与"技能"是菜单里两条独立选项,各花各的精力,互不捎带。
        // v0.18:每天不限次;当天第一次回升 2 层,第二次起每次 1 层。v0.21:谈话 2 精力、技能仍 1 精力。
        int TalkMoodNow() { return talksToday == 0 ? B.talkMoodFirstTalk : B.talkMoodLater; }

        void DoTalk()
        {
            var ta = Act("talk");
            Spend(ta != null ? ta.cost : 2);      // v0.21:谈话 2 精力(原来是 1)
            int up = TalkMoodNow();
            talksToday++;
            if (S.mate.mood > MoodLevel.Good)
            {
                S.mate.mood = (MoodLevel)Mathf.Max((int)MoodLevel.Good, (int)S.mate.mood - up);
                S.Log("陪 " + S.mate.who.displayName + " 聊了一会儿(今天第 " + talksToday + " 次,回升 " + up + " 层):精神 → "
                      + MoodName(S.mate.mood) + "。");
                D("talk");
            }
            else S.Log("陪 " + S.mate.who.displayName + " 聊了一会儿(精神已经是良好)。");
            S.mate.moodDaysSinceTalk = 0;
            Refresh();
        }

        // 技能:只发动当天的技能效果,不动精神。飞行员的 +1 是纯上限 —— v0.15 起不再顺手补当前精力。
        void DoSkill()
        {
            if (!S.mate.present || !S.mate.skillOfferedToday || S.mate.skillActiveToday) return;
            Spend(1);
            S.mate.skillActiveToday = true;
            if (S.mate.who == DB.Pilot)
            {
                S.stats.staminaMax = B.staminaMax + 1;
                S.Log("飞行员技能生效:今天体力上限 " + S.stats.staminaMax + "(只抬上限,已经花掉的不补)。");
            }
            else S.Log("技能生效:" + S.mate.who.skillDesc);
            Refresh();
        }

        void DoFeed()
        {
            S.storage.Remove(DB.Can, 1);
            fedToday = true;
            S.mate.hunger = HungerLevel.Full;
            S.mate.hungerDaysSinceMeal = 0;
            S.Log("喂了 " + S.mate.who.displayName + " 1 份罐头(0 精力):饱食直接回到 饱食。");
            D("feed");
            Refresh();
        }

        void DoThrowBottle()
        {
            Spend(1);
            S.storage.Remove(DB.Bottle, 1);
            S.bottle.thrownBottles++;
            // ⚠ 幽灵船那条骰要 丢满 2 个瓶子 才开(§4.6)—— 第 1 个瓶子只打开轮船线,日志必须照实说
            S.Log("把漂流瓶扔进了海里(已丢 " + S.bottle.thrownBottles + " 个)。"
                  + (S.bottle.thrownBottles >= 2
                        ? "今晚起:轮船与幽灵船各掷各的独立 20%。"
                        : "今晚起每晚独立 20% 只掷 轮船 —— 再丢第 2 个瓶子才会另外开出 幽灵船(结局H)。"));
            D("throwbottle");
        }

        // 通用工具损坏:每次使用后掷一次(20% 起、+10%/次、上限 90%)
        public void Wear(ItemSO tool)
        {
            var t = tool as ToolSO;
            if (t == null || t.indestructible) return;
            if (!S.storage.Has(t, 1)) return;
            float p = ToolBreakChance(t);
            if (S.rng.NextDouble() < p)
            {
                S.storage.MarkBroken(t);
                S.Log(t.displayName + " 坏了(这一掷 " + Mathf.RoundToInt(p * 100) + "%)。修理:" + RepairLabel(t));
            }
        }

        readonly Dictionary<ToolSO, float> wear = new Dictionary<ToolSO, float>();
        float ToolBreakChance(ToolSO t)
        {
            float p;
            if (!wear.TryGetValue(t, out p)) p = t.breakChanceStart;
            wear[t] = Mathf.Min(0.9f, p + 0.1f);
            return p;
        }

        // v0.46(用户):"围墙也会损坏,按标准的损坏概率" ⇒ §2.3 那条累积制从此也管建筑。
        //   一次"使用" = 它挡下 涨潮 / 毒蛇 的那一晚(由 NightResolver 在挡住之后调用)。
        //   概率常量写死在代码里、没进 BalanceConfig:20% 起 / 每次 +10% / 上限 90% 是 §2.3 的既有口径,
        //   而"资产里缺这个键会静默读成 0"(§7.2 第一条)—— 一个读成 0 的起始概率 = 围墙永远不坏,正是这轮要修的毛病。
        public const float StructureBreakStart = 0.2f;
        public const float StructureBreakRamp = 0.1f;
        public const float StructureBreakCap = 0.9f;
        readonly Dictionary<string, float> wearStruct = new Dictionary<string, float>();

        public void WearStructure(string key)
        {
            if (!S.HasStructure(key)) return;      // 没建、或已经裂着 ⇒ 不再掷(修好之前不重复坏)
            float p = StructBreakChanceNext(key);
            if (S.rng.NextDouble() < p)
            {
                S.MarkStructureBroken(key);
                S.Log(StructureName(key) + " 被这一晚撞裂了(这一掷 " + Mathf.RoundToInt(p * 100) + "%)—— 它现在挡不住东西了。白天可以修:" + RepairLabel(key));
            }
        }

        // 读"下一次掷多少"并顺手把账抬上去(与 ToolBreakChance 同构)
        float StructBreakChanceNext(string key)
        {
            float p;
            if (!wearStruct.TryGetValue(key, out p)) p = StructureBreakStart;
            wearStruct[key] = Mathf.Min(StructureBreakCap, p + StructureBreakRamp);
            return p;
        }
        // 只读不抬:修好之后要把这一件的概率折回去一半
        float StructBreakChance(string key)
        {
            float p;
            return wearStruct.TryGetValue(key, out p) ? p : StructureBreakStart;
        }

        // 建筑的名字与修理价都从配方表反查 —— 不要再手写第二份常量。
        public string StructureName(string key)
        {
            foreach (var r in DB.recipes)
                if (r != null && r.isStructure && r.resultStructure == key) return r.displayName;
            return key;
        }
        public string RepairLabel(string structureKey)
        {
            int m, st;
            DB.RepairCost(structureKey, out m, out st);
            return m + " 材料 + " + st + " 体力";
        }

        public string RepairLabel(ItemSO item)
        {
            int m, st;
            DB.RepairCost(item, out m, out st);
            // v0.48(用户):"打火石修复也要一材料" ⇒ 这里那句写死的"0 材料 + 1 体力"(v0.5 定案)**删除**,
            //   改由 ToolSO 的 repairMaterials=1 / repairStamina=1 说话 —— 全项目只有 `RepairCost` 一个读价入口。
            return m + " 材料 + " + st + " 体力";
        }

        // ---------------- 弹层:修理 / 制作 / 吃 / 喝 / 用药 ----------------
        void OpenModal(string title, List<string> labels, List<Action> acts, List<bool> enabled)
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "box", new Color(0.13f, 0.15f, 0.19f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-330, -260), new Vector2(330, 260));
            Ui.Label(box, "title", title, 22, TextAnchor.UpperCenter, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(0, -8));
            float y = -56;
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var b = Ui.Button(box, "o" + i, labels[i], 15, () =>
                {
                    root.ClearModal();
                    if (acts[idx] != null) acts[idx]();
                    if (S.phase == RunPhase.Island) { CheckDeath(); Refresh(); }
                }, new Color(0.22f, 0.34f, 0.48f, 1f), new Vector2(0, 1), new Vector2(1, 1),
                  new Vector2(16, y - 32), new Vector2(-16, y));
                if (enabled != null) Ui.SetEnabled(b, enabled[i]);
                y -= 36;
            }
            Ui.Button(box, "close", "关闭", 16, () => root.ClearModal(), new Color(1, 1, 1, 0.12f),
                      new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-70, 14), new Vector2(70, 48));
        }

        // 制造与维修在同一张面板上(§11-39:材料是单一资源池,做与修本来就抢同一份库存)
        void OpenBench()
        {
            var labels = new List<string>(); var acts = new List<Action>(); var en = new List<bool>();
            labels.Add("— 制作(§6)—"); en.Add(false); acts.Add(null);
            foreach (var r in DB.recipes)
            {
                // v0.16:有上位替代在手 → 这一行根本不出现在面板上(不是置灰)。
                // 例外是 土制信号弹 与 自制药品:它们的 supersededBy 留空,所以永远在。
                if (r.supersededBy != null && S.storage.Has(r.supersededBy)) continue;
                // v0.19(§11-51 结案):没有 信号枪 就没有 土制信号弹 的配方 —— 做出来也打不出去,不如根本不给看
                if (r.requiresItemToShow != null && !S.storage.Has(r.requiresItemToShow, 1)) continue;
                // v0.18:已经有完好的同类 → 这一行也不出现。坏了的那件会在下面"修理"一段里,
                //        所以"只有破损件时才看得见制作/修理"这件事是同一套口径。
                //        消耗品(鱼饵/自制药品/土制信号弹/土制打火石)不会破损,也不受这条约束。
                // v0.46:建筑读的是"**建过没有**"而不是"还好不好" —— 围墙坏了也不该重新出现"再花 5 材料建一座",
                //        它走下面的修理段(材料3 + 1 体力)。这与工具那条口径完全一致。
                if (r.isStructure && S.structures.Contains(r.resultStructure)) continue;
                if (r.resultItem != null && !r.resultItem.consumable && S.storage.Count(r.resultItem) > 0) continue;
                string why;
                bool can = CanCraft(r, out why);
                // v0.22:被打断吃掉的素材(篝火吃打火石、自制药品吃药草)一律写进"价"里,
                //        不再以"需要 X"的面目出现 —— 面板上唯一还该出现"需要先建…"的只有 净水器(v0.19 你定的那道前置)。
                labels.Add(r.displayName + "  —  材料" + r.materials + " + " + r.stamina + " 体力" +
                           (r.consumesFlint ? " + 打火石" : "") +
                           (r.requiresExtraItem != null ? " + " + r.requiresExtraItem.displayName + r.requiresExtraItemCount : "") +
                           (can ? "" : "  — " + why));
                en.Add(can);
                var rr = r;
                acts.Add(() => DoCraft(rr));
            }
            var broken = BrokenList();
            var brokenStructs = BrokenStructures();
            // v0.40(用户):手摇充电 挪进维修面板 —— 它和"修理"本来就是同一件事:把一件工具从"用不了"变回"能用"。
            //        点沙滩上那支手电筒 那条路 **照旧留着**(§2.5 点即执行),这里是多一个入口,不是换掉。
            bool chargeable = S.storage.Has(DB.Flashlight) && S.flashlightCharge < B.flashlightChargeMax;
            if (broken.Count > 0 || chargeable || brokenStructs.Count > 0)
            {
                labels.Add(broken.Count > 0 ? "— 修理 / 充电 —" : "— 充电 —"); en.Add(false); acts.Add(null);
                foreach (var item in broken)
                {
                    int mat, st;
                    DB.RepairCost(item, out mat, out st);
                    if (MechanicActive()) { st = 0; mat = Mathf.Max(0, mat - 1); }
                    var it = item;
                    int m2 = mat, s2 = st;
                    labels.Add("修理 " + it.displayName + "  —  " + m2 + " 材料 + " + s2 + " 体力" +
                               (MechanicActive() ? "(机械师:体力 0、材料 -1)" : ""));
                    en.Add(S.storage.Has(DB.Material, m2) && S.stats.stamina >= s2);
                    acts.Add(() =>
                    {
                        S.storage.Remove(DB.Material, m2);
                        Spend(s2);
                        S.storage.Unbreak(it);
                        var t = it as ToolSO;
                        if (t != null) wear[t] = Mathf.Max(0.2f, ToolBreakChanceNow(t) * 0.5f);
                        S.Log("修好了 " + it.displayName + "(损坏概率减半,不低于 20%)。");
                        D("repair");
                    });
                }
                // v0.46(用户):"围墙也会损坏,按标准的损坏概率" ⇒ 它进同一段修理账。
                //   价走 **"制造量一半向上取整 + 1 体力"** 这条既有口径(围墙 材料5 → 修 材料3),
                //   机械师 的"当天修理体力归零、每件材料 -1"照旧吃得到它 —— 他是"所有修理"的省税,没理由漏掉建筑。
                foreach (var key in brokenStructs)
                {
                    var sk = key;
                    // v0.48(用户:"统一成一套,坏与不坏"):火的"坏"就是"熄了",它同样进这一行,
                    //   但它的"修理费"**不是材料,是一块火种**(0 材料 0 精力 —— 你这次的明文)。
                    //   所以这一段按"是不是火"分两种价,而不是给建筑再造第二套状态模型。
                    if (IsFireStructure(sk))
                    {
                        labels.Add("重新点燃 " + StructureName(sk) + "  —  一块火种(0 材料 0 精力)  烧 "
                                   + RunState.FireNightsPerLighting + " 晚"
                                   + (HasFireStarter() ? "" : "(手上没有火种)"));
                        en.Add(HasFireStarter());
                        acts.Add(() => { RelightFire(sk); Refresh(); });
                        continue;
                    }
                    int mat, st;
                    DB.RepairCost(key, out mat, out st);
                    if (MechanicActive()) { st = 0; mat = Mathf.Max(0, mat - 1); }
                    int m3 = mat, s3 = st;
                    labels.Add("修好 " + StructureName(sk) + "  —  " + m3 + " 材料 + " + s3 + " 体力" +
                               (MechanicActive() ? "(机械师:体力 0、材料 -1)" : ""));
                    en.Add(S.storage.Has(DB.Material, m3) && S.stats.stamina >= s3);
                    acts.Add(() =>
                    {
                        S.storage.Remove(DB.Material, m3);
                        Spend(s3);
                        S.RepairStructure(sk);
                        wearStruct[sk] = Mathf.Max(0.2f, StructBreakChance(sk) * 0.5f);   // 与工具同一句"修一次缓一段"
                        S.Log("把 " + StructureName(sk) + " 重新垒好了(下次损坏概率减半,不低于 20%)。");
                        D("repair");
                    });
                }
                if (chargeable)
                {
                    // v0.44:一格电 = 1 精力,一次只充一格(上限 2 格)                    labels.Add("手摇充电(手电筒)  —  1 体力  充 1 格(现在 " + S.flashlightCharge + "/" + B.flashlightChargeMax + ")");
                    en.Add(S.stats.stamina >= 1); acts.Add(DoCharge);
                }
            }
            OpenModal("制造 / 维修  面板    材料库存 ×" + S.storage.Total(DB.Material) +
                      "    药草 ×" + S.storage.Total(DB.Herb), labels, acts, en);
        }

        float ToolBreakChanceNow(ToolSO t)
        {
            float p;
            return wear.TryGetValue(t, out p) ? p : t.breakChanceStart;
        }

        bool CanCraft(RecipeSO r, out string why)
        {
            why = null;
            // v0.47(用户):"信号火堆 必须在有篝火后才能建造" ⇒ v0.28 起"制造完全没有前置"这条规则
            //        从"没有例外"变回 **有一个例外**(上一个例外是 v0.17 的 净水器,它随喝水系统一起删了)。
            //        它是真前置(解锁条件),不是被吃掉的素材 —— 后者走 requiresExtraItem,两条别混。
            //        ⚠ string 字段没填在 Unity 里是 "" 不是 null(§7.2 第一条铁律),判它必须 IsNullOrEmpty。
            if (!string.IsNullOrEmpty(r.requiresStructure) && !S.structures.Contains(r.requiresStructure))
            { why = "需要先建 " + StructureName(r.requiresStructure); return false; }
            if (r.oncePerRun && S.craftedThisRun.Contains(r.key)) { why = "每局限做 1 次"; return false; }
            if (r.isStructure && S.structures.Contains(r.resultStructure)) { why = "已经建好了"; return false; }
            // v0.28:净水器 与它那道"先建集水器"的前置一起删除 ⇒ 制造重新回到"没有任何前置"这条规则上。
            // 下面几条都是要被吃掉的价,不是门槛。
            if (r.consumesFlint && !Usable(DB.Flint) && !S.storage.Has(DB.CrudeFlint)) { why = "缺 打火石(或 土制打火石)"; return false; }
            if (r.requiresExtraItem != null && !S.storage.Has(r.requiresExtraItem, r.requiresExtraItemCount))
            {
                why = "缺 " + r.requiresExtraItem.displayName + " ×" + r.requiresExtraItemCount; return false;
            }
            if (!S.storage.Has(DB.Material, r.materials)) { why = "材料不足(需 " + r.materials + ")"; return false; }
            if (S.stats.stamina < r.stamina) { why = "体力不足"; return false; }
            return true;
        }

        void DoCraft(RecipeSO r)
        {
            string why;
            if (!CanCraft(r, out why)) { S.Log("做不了 " + r.displayName + ":" + why); return; }
            if (r.oncePerRun) S.craftedThisRun.Add(r.key);
            S.storage.Remove(DB.Material, r.materials);
            Spend(r.stamina);
            if (r.requiresExtraItem != null) S.storage.Remove(r.requiresExtraItem, r.requiresExtraItemCount);
            // v0.47:`consumesFlint` 那条"生篝火"分支整个删除 —— 篝火 升格成建筑(下面 isStructure 那一支),
            //        火种改由 **点燃** 时消耗(见 LightCampfire / LightSignalFire),不再是造的时候吃掉。
            if (r.isStructure)
            {
                S.Build(r.resultStructure);
                // v0.48(用户:"篝火不用点,存在则那几条分支都直接走"):**建造那一手 = 点燃**。
                //   `consumesFlint` 因此回来了,但含义换了:它不再是"造一件只值一晚的临时火",
                //   而是"这座灶建好即点着、从今晚起烧两晚"。熄灭之后要再点,走维修面板那一行(0 材料 0 精力 + 火种)。
                if (r.consumesFlint)
                {
                    S.Relight(r.resultStructure);
                    S.Log("建好了 " + r.displayName + ",并且当场点着(烧 " + RunState.FireNightsPerLighting
                          + " 个晚上,点燃当天算第 1 晚)。" + LightFireStarter());
                    return;
                }
                S.Log("建好了 " + r.displayName + "。");
                return;
            }
            S.storage.Add(r.resultItem, 1);
            S.hidden.everOwned.Add(r.resultItem);
            S.Log("做出了 " + r.displayName + "。");
            D("craft");
        }

        // v0.18:生火用的火种有两种 —— 自制的 土制打火石 只能用一次(直接消耗掉),
        // 拾荒带出的 打火石 是"上位"那件(可反复用,走 §2.3 的累积损坏、修理 0 材料 + 1 体力)。
        // 先烧一次性的那块,保住耐用的 —— 这是唯一合理的默认,所以不给玩家再开一个选择。
        // 白天生篝火 与 低温夜生火 都走这里,不要各写一套。
        public string LightFireStarter()
        {
            if (S.storage.Has(DB.CrudeFlint))
            {
                S.storage.Remove(DB.CrudeFlint, 1);
                return "土制打火石 用掉了(它只能用一次)。";
            }
            Wear(DB.Flint);   // §5.1:打火石 不许挂 breakChanceNight,它走累积损坏
            return "打火石 掷了一次常规损坏判定 —— 修它要 材料1 + 1 体力(v0.48 你把它从「0 材料」改成收费)。";
        }

        // ---------------- v0.48:火 = 建筑,建造即点燃,熄灭即"坏",修 = 重新点燃 ----------------
        // 用户这次的三句话合起来就是这套模型:
        //   「**篝火不用点,存在则那几条分支都直接走**」⇒ 不需要"每天点一次"这个动作,建好就烧着;
        //   「**统一成一套吧,坏与不坏(篝火熄灭也能当成一种"坏")**」⇒ 熄灭走 `brokenStructures`,
        //     所有读者只问 `HasStructure`,不再同时看计数器(那是 v0.47 的第二套模型,已废);
        //   「**点燃篝火不消耗精力,但是要消耗打火石(掷概率)**」⇒ "重新点燃"这一手 0 材料 0 精力,只花一块火种。
        public bool HasFireStarter() { return Usable(DB.Flint) || S.storage.Has(DB.CrudeFlint); }
        public static bool IsFireStructure(string key) { return Database.IsFireStructure(key); }

        // 白天点那两块灶:**没有"点"这个动作了**,只告诉你它的状态与去哪儿修(夜晚的点击归 target "fire",见 StructureProp)
        void FireInfo(string name, string key)
        {
            if (S.HasStructure(key)) S.Log(name + " 正烧着(还剩 " + S.NightsLeft(key) + " 个晚上),不用管它。");
            else if (IsFireStructure(key)) S.Log(name + " 熄了 —— 在 制造/维修 面板里「重新点燃」那一行,只花一块火种。");
        }
        void FireInfoCampfire() { FireInfo("篝火", Database.Campfire); }
        void FireInfoSignalFire() { FireInfo("信号火堆", Database.SignalFire); }

        // 维修面板里的"重新点燃":0 材料 0 精力,只花一块火种(火种自己那套优先烧 土制打火石 / 打火石掷累积损坏)
        void RelightFire(string key)
        {
            string name = StructureName(key);
            if (!S.structures.Contains(key)) { S.Log("还没有" + name + " —— 先在制造面板里把它建起来。"); return; }
            if (S.HasStructure(key)) { S.Log(name + " 还烧着,不用补。"); return; }
            if (!HasFireStarter()) { S.Log("没有火种," + name + " 点不着(土制打火石 材料1 可做;拾荒带的 打火石 全游戏只有一块)。"); return; }
            S.Relight(key);
            S.Log("重新点燃 " + name + ":烧 " + RunState.FireNightsPerLighting + " 个晚上。" + LightFireStarter());
            D("repair");
        }

        // 喝 / 吃 / 用药 的弹层已删(v0.15:点那件东西本身就用掉它)。见 EatCan / EatChocolate /
        // DrinkFresh / DrinkDirty / UseMedKit / UseMedicine。
        void ApplyMedicine(float chance, string name)
        {
            if (S.rng.NextDouble() < chance)
            {
                // v0.28:禁水期已随喝水系统删除,治好 生病 就是生病标记清掉
                if (S.stats.sick) { S.stats.sick = false; S.Log(name + " 生效:病好了。"); }
                else { Heal(1, name); S.Log(name + " 生效:+1 生命。"); }
                D("heal");
            }
            else { S.Log(name + " 失败了 —— 药照样消耗掉了。"); D("healfail"); }
            healedToday = true;
        }

        // §5.3/§5.4 彩蛋层的唯一入口。两条硬规则写死在这里:
        //   ①同一个 item 整局只给一次(loreFound 既是"已收集"也是那道闸);
        //   ②概率取各条 LoreDropSO.chance(v0.26 拍板:全部 0.01)。
        // 返回值是这一批的文本 —— 白天那屏读日志就够了,夜晚那屏要把它拼进结算里。
        public string LoreRoll(string source)
        {
            var sb = new StringBuilder();
            foreach (var l in DB.loreDrops)
            {
                if (l.source != source) continue;
                if (S.hidden.loreFound.Contains(l.item)) continue;
                if (S.rng.NextDouble() < l.chance)
                {
                    S.hidden.loreFound.Add(l.item);
                    S.storage.Add(l.item, 1);
                    S.Log("彩蛋:" + l.item.displayName + " —— " + l.loreText);
                    S.Diary(DiaryLines.Egg(l.item.displayName), DiaryLineKind.Egg);   // v0.30:玩家侧只给一句,全文在收集页
                    sb.Append("彩蛋 —— " + l.item.displayName + ":" + l.loreText + "\n");
                }
            }
            return sb.ToString();
        }

        int Rolls(float chance, int n)
        {
            int c = 0;
            for (int i = 0; i < n; i++) if (S.rng.NextDouble() < chance) c++;
            return c;
        }

        ItemSO RandomStorageItem()
        {
            var list = new List<ItemSO>();
            foreach (var kv in S.storage.ok)
            {
                if (kv.Key.lore) continue;      // 暗线道具不给猴子/事故(§3.4)
                for (int i = 0; i < kv.Value; i++) list.Add(kv.Key);
            }
            if (list.Count == 0) return null;
            return list[S.rng.Next(list.Count)];
        }

        // ---------------- 夜晚 ----------------
        // v0.14:夜晚不弹面板。还是这一张场景、这些物体,只是"只能用手上的东西",
        //       并且一晚只能做一件事(§2.3 夜用规则);"尝试睡去" = 这一晚的"什么都不做"。
        bool night;
        bool nightDone;
        // v0.30:nightLine 现在 **只** 装"点了但今晚帮不上忙"这类拒绝提示 ——
        //        夜晚的结算文本已经整体搬进日记(天亮时自动翻开那一页)。
        string nightLine;

        void BeginNight()
        {
            if (S.stats.staminaMax != B.staminaMax) S.stats.staminaMax = B.staminaMax;   // 飞行员 buff 只当天
            NightResolver.Begin(root);
            night = true; nightDone = false; nightLine = null;
            // v0.46(用户):信号火堆 在烧 ⇒ 救援事件不需要选道具,那一晚直接判定通过。
            //   这一晚因此"已经过去"了:再点任何物品都不该二次结算(否则同晚会出两个结局判定),
            //   屏幕上只留那颗 闭上眼 按钮(SleepLabel 读 HasPending 换的词)。
            if (NightResolver.HasPending)
            {
                nightDone = true;
                nightLine = "沙丘上那道火柱替你答了这一晚 —— 不需要你再动手。";
            }
            SetNightLook(true);
            Refresh();
        }

        void DoNight(GameChoiceSO c)
        {
            if (nightDone) return;
            // v0.30:夜晚不再显示"结算文本" —— 那一晚发生了什么,天亮时在日记里读。
            //        返回值仍然被 Log 到控制台(调试通道)。
            NightResolver.Choose(c);
            nightDone = true;
            EndNightOrRefresh();
        }

        // v0.15:夜晚没有"天亮 →"这一步。做了应对(或选了睡去)就直接进早上;
        // 唯一留在夜里的情形是"结局已定"(按钮变"闭上眼"→ 结局屏)。
        void EndNightOrRefresh()
        {
            if (nightDone && !NightResolver.HasPending) { OnDawn(); return; }
            Refresh();
        }

        void CalmTalkNow()
        {
            NightResolver.CalmTalk();        // v0.30:结算文本改由日记承担
            nightDone = true;
            EndNightOrRefresh();
        }

        // 尝试睡去 = 直接结束夜晚:没做过事就先结算"什么都不做",再让这一晚过去。
        // 只有结局已定时按钮才换词(闭上眼 → 进结局屏);其余一律"尝试睡去"。
        string SleepLabel() { return NightResolver.HasPending ? "闭上眼" : "尝试睡去"; }

        void OnSleep()
        {
            if (NightResolver.HasPending) { root.EndRun(NightResolver.Pending); return; }
            if (!nightDone)
            {
                NightResolver.Sleep();       // v0.30:结算文本改由日记承担
                nightDone = true;
                EndNightOrRefresh();
                return;
            }
            OnDawn();
        }

        void OnDawn()
        {
            night = false; nightDone = false; nightLine = null;
            NightResolver.End();
            SetNightLook(false);
            root.ClearModal();
            if (S.stats.hp <= 0) { root.EndRun(EndingResolver.DeathEnding(S)); return; }
            if (S.gull.presentCount >= B.gullEndingCount) { root.EndRun(EndingId.I); return; }
            // v0.30:先把"第 N 天"这一页封口(此时 S.day 还是 N,MorningTick 才 +1),再翻给他看。
            int page = S.day;
            S.SealDiary();
            MorningTick(false);
            if (S.phase != RunPhase.Island) return;      // 清晨结算里可能直接进结局,那时别再叠一层日记
            Refresh();
            OpenDiary(page);
        }

        void SetNightLook(bool on)
        {
            if (root.sun != null) root.sun.intensity = on ? 0.06f : 1.0f;
            // 白天的清屏色直接就是天空蓝:就算那块天空板因为材质/光照没画出来,
            // 屏幕也不会变成"看不见东西"的黑底 —— 相机的背景本身就是天。
            if (root.cam != null) root.cam.backgroundColor = on ? new Color(0.02f, 0.03f, 0.07f)
                                                                : new Color(0.55f, 0.78f, 0.95f);
        }

        // 进荒岛 0.4 秒后打一条镜头诊断:看向谁 + 视锥里有多少可见 Renderer。
        // 灰盒阶段这类"画面不对"的问题,靠猜不如靠这一行。
        bool diagDone;
        float diagAt;

        void DiagOnce()
        {
            if (diagDone || Time.unscaledTime < diagAt || root.cam == null) return;
            diagDone = true;
            var ray = root.cam.ViewportPointToRay(new Vector3(0.5f, 0.3f, 0f));
            RaycastHit h;
            string hit = Physics.Raycast(ray, out h, 400f)
                ? h.collider.name + " @" + h.distance.ToString("F1") + "m"
                : "这条视线 400 米内什么都没打到";
            int visible = 0;
            foreach (var r in FindObjectsOfType<Renderer>())
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(ray)) visible++;
            Debug.Log("[诊断] 镜头 " + root.cam.transform.position.ToString("F2") + " 俯仰 " +
                      root.cam.transform.eulerAngles.x.ToString("F0") + " | 视线:" + hit +
                      " | 视锥内可见 Renderer " + visible +
                      " | 父级=" + (root.cam.transform.parent != null ? root.cam.transform.parent.name : "无") +
                      " | 背景 " + root.cam.backgroundColor.ToString("F2") + " | 太阳 " +
                      (root.sun != null ? root.sun.intensity.ToString("F2") : "未接"));
        }

        void BuildNightHud(Transform rt)
        {
            var dim = Ui.Panel(rt, "nightdim", new Color(0.02f, 0.03f, 0.07f, 0.58f),
                              Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.GetComponent<Image>().raycastTarget = false;      // 遮罩不吃鼠标:沙滩上的东西照样能点

            var ev = NightResolver.Current;
            Ui.Label(rt, "nighthead", "第 " + S.day + " 夜 · " + (NightResolver.Calm ? "今夜无事发生" : ev.displayName),
                     26, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.5f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -46), new Vector2(0, -12));
            string desc = NightResolver.Calm ? "" : ev.description;
            if (ev == DB.MonkeyNaughty && NightResolver.StolenName != null) desc += "(它抢走的是:" + NightResolver.StolenName + ")";
            // v0.33:友善的猴子 也要把"换出哪件 / 换入哪件"写在屏幕上(这一对在本晚摊开时就已经定好,
            //        点"交易"只是执行它 —— 见 NightResolver.PickTrade)
            if (ev == DB.MonkeyFriendly && NightResolver.TradeGiveName != null)
                desc += "(它想收走你的:" + NightResolver.TradeGiveName + " —— 换给你:"
                        + (NightResolver.TradeGetName ?? "它没带新东西来,会给 2 份罐头") + ")";
            else if (ev == DB.MonkeyFriendly) desc += "(它能收的东西你一样也没有 —— 这一晚会换成 2 份罐头)";
            if (!string.IsNullOrEmpty(desc))
                Ui.Label(rt, "nightdesc", desc, 17, TextAnchor.UpperCenter, new Color(0.85f, 0.88f, 0.92f),
                         new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -86), new Vector2(-60, -48));
            if (!string.IsNullOrEmpty(nightLine))
                Ui.Label(rt, "nightres", nightLine, 17, TextAnchor.MiddleCenter, new Color(0.96f, 0.93f, 0.82f),
                         new Vector2(0, 0.42f), new Vector2(1, 0.42f), new Vector2(-430, -60), new Vector2(430, 40));

            // v0.32:夜晚这一屏不再有「日记」按钮 —— 日记是白天读的东西,夜里发生的写进当天那页,天亮封口后才翻得到。
            Ui.Button(rt, "sleep", SleepLabel(), 22, OnSleep,
                      new Color(0.30f, 0.34f, 0.46f, 1f), new Vector2(0, 0), new Vector2(0, 0),
                      new Vector2(16, 16), new Vector2(250, 76));

            // 没有对应物体的选项(涨潮的"转移到高处/抢救物资")+ v0.22:那些"既不绑物品也不绑物体"的选项
            // (影怪的"躲进被子"QTE、飞行员的"升临时信号帆"、没队友时小影怪的"直接睡觉") —— 全部排在睡去按钮上方,
            // 否则它们在场景界面里根本没有入口。
            var loose = new List<GameChoiceSO>(NightResolver.ChoicesByTarget("strip"));
            foreach (var c in NightResolver.ChoicesWithoutObject()) if (!loose.Contains(c)) loose.Add(c);
            float y = 90;
            foreach (var c in loose)
            {
                var cc = c;
                Ui.Button(rt, "strip_" + cc.key,
                          cc.label + (string.IsNullOrEmpty(cc.resultHint) ? "" : "\n(" + cc.resultHint + ")"),
                          15, () => DoNight(cc), new Color(0.20f, 0.30f, 0.44f, 1f),
                          new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, y), new Vector2(392, y + 52));
                y += 56;
            }
        }

        // 点一件东西 / 一堆火 / 海 / 队友:物品一律"直接做"(v0.15);
        // 队友/篝火这类"身上可能挂着两条夜用选项"的物体才列出来,否则同样直接做。
        void NightClick(ItemSO k)
        {
            // v0.46:点哪件 = 用哪件。这一位要带给结算(血月 的"下水"认的就是它)。
            NightResolver.NightTool = k;
            NightMenu(k.displayName, NightResolver.ChoicesFor(k), null, null, false);
        }

        void NightClickTarget(string target, string extraLabel = null, Action extraAct = null)
        {
            NightResolver.NightTool = null;      // 点的是地面物体(篝火/大海/队友/…),不是某件工具
            string title = target == "fire" ? "篝火" : target == "sea" ? "大海"
                         : target == "mate" ? (S.mate.present ? S.mate.who.displayName : "队友")
                         : target == "bones" ? "沙里露出来的那具尸骨"
                         : target == "monkey" ? "那只猴子" : "营地";
            NightMenu(title, NightResolver.ChoicesByTarget(target), extraLabel, extraAct, true);
        }

        void NightMenu(string title, List<GameChoiceSO> list, string extraLabel, Action extraAct, bool allowMenu)
        {
            if (nightDone) { nightLine = "这一晚已经过去,天亮之前什么都做不了。"; Refresh(); return; }
            if (list.Count == 0)
            {
                if (extraAct != null) { extraAct(); return; }
                nightLine = title + ":今晚它帮不上忙。";
                Refresh();
                return;
            }
            if (!allowMenu || (list.Count == 1 && extraLabel == null))
            {
                if (list.Count > 1) S.Log(title + " 今晚有 " + list.Count + " 条可做的事,按第一条\"" + list[0].label + "\"结算。");
                DoNight(list[0]);
                return;
            }
            var labels = new List<string>(); var acts = new List<Action>(); var en = new List<bool>();
            foreach (var c in list)
            {
                labels.Add(c.label + (string.IsNullOrEmpty(c.resultHint) ? "" : " — " + c.resultHint));
                en.Add(true);
                var cc = c;
                acts.Add(() => DoNight(cc));
            }
            if (extraLabel != null) { labels.Add(extraLabel); en.Add(true); acts.Add(extraAct); }
            OpenModal(title, labels, acts, en);
        }
    }
}
