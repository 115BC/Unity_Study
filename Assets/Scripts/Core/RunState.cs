using System;
using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    public enum RunPhase { Title, Scavenging, Island, Ended }

    // §7.1:仓库回到 Dictionary<ItemSO,int>(v0.11 删掉 ItemInstance.origin);
    // v0.45:破损不再按"每一件"记 —— 同一种东西要么全好、要么全坏,所以 broken 这本账只是"这一种坏了几个"。
    public class Inventory
    {
        public readonly Dictionary<ItemSO, int> ok = new Dictionary<ItemSO, int>();
        public readonly Dictionary<ItemSO, int> broken = new Dictionary<ItemSO, int>();

        public int Count(ItemSO i) { int v; return ok.TryGetValue(i, out v) ? v : 0; }
        public int BrokenCount(ItemSO i) { int v; return broken.TryGetValue(i, out v) ? v : 0; }
        public bool Has(ItemSO i, int n = 1) { return Count(i) >= n; }
        public int Total(ItemSO i) { return Count(i) + BrokenCount(i); }

        public void Add(ItemSO i, int n = 1)
        {
            if (i == null || n <= 0) return;
            int v; ok.TryGetValue(i, out v); ok[i] = v + n;
        }

        public bool Remove(ItemSO i, int n = 1)
        {
            if (Count(i) < n) return false;
            ok[i] = Count(i) - n;
            if (ok[i] <= 0) ok.Remove(i);
            return true;
        }

        public void MarkBroken(ItemSO i)
        {
            // §2.3:只有"会坏的工具"进得了破损态。钓竿/手电筒 是 indestructible,
            // 消耗品与图鉴杂物根本不是 ToolSO —— 狐狸抢回它们时"没有破损这回事"。
            if (i == null || i.indestructible || !(i is ToolSO)) return;
            // v0.45(用户):一件东西只有两种状态 —— 坏了就是整堆坏,不会出现"还剩一把能用、
            //        另一把躺在维修面板里"那种同时存在两态的情况。
            int n = Count(i);
            if (n <= 0) return;
            ok.Remove(i);
            int v; broken.TryGetValue(i, out v); broken[i] = v + n;
        }

        // 破损件回炉:整堆一起修好(broken 里同一种东西永远只有一个数字)
        public bool Unbreak(ItemSO i)
        {
            int v;
            if (!broken.TryGetValue(i, out v) || v <= 0) return false;
            broken.Remove(i);
            Add(i, v);
            return true;
        }

        public bool HasBroken(ItemSO i) { return BrokenCount(i) > 0; }

        public IEnumerable<ItemSO> AllKinds()
        {
            var set = new HashSet<ItemSO>();
            foreach (var k in ok.Keys) set.Add(k);
            foreach (var k in broken.Keys) set.Add(k);
            return set;
        }

        public int TotalUnits()
        {
            int n = 0;
            foreach (var kv in ok) n += kv.Value;
            foreach (var kv in broken) n += kv.Value;
            return n;
        }
    }

    public enum MoodLevel { Good = 0, Lonely = 1, Depressed = 2, Broken = 3 }      // 精神 4 层
    public enum HungerLevel { Full = 0, Empty = 1, Hungry = 2, Famine = 3 }         // 饱食 4 层

    public class TeammateState
    {
        public TeammateSO who;
        public bool present;
        public MoodLevel mood;
        public HungerLevel hunger;
        public bool sick;
        public int moodDaysSinceTalk;
        public int hungerDaysSinceMeal;
        public bool skillOfferedToday;      // 40% 命中后挂"待触发",交流才发动
        public bool skillActiveToday;
        // v0.52(用户:"队友处于饥荒的当天不会死亡,第二天才会死亡"):进入 饥荒 那一天记下来 = 最后一天期限。
        //   -1 = 现在不在饥荒里。§2.4 从 v0.10 起写的就是"处于饥荒 **且当天没投喂** → 次日消失",
        //   以前代码在降进 饥荒 的那个清晨直接判死,玩家从来没有"那一天"可以去喂 ⇒ 是代码与文档不一致。
        public int famineDay = -1;

        public void Reset()
        {
            present = false; who = null; mood = MoodLevel.Good; hunger = HungerLevel.Full;
            sick = false; moodDaysSinceTalk = 0; hungerDaysSinceMeal = 0;
            skillOfferedToday = false; skillActiveToday = false;
            famineDay = -1;
        }
    }

    // v0.13:ExploreState 是 State 不是 flag(§12.6),存档、读档不重置
    public class ExploreState
    {
        public float refreshP = 0.30f;
        public bool availableToday;
        public bool exploredToday;          // 只有真的探索过才把 refreshP 回落
    }

    public class GullState
    {
        public bool gullFedOnce;            // v0.12:全游戏只需喂 1 次
        public int presentCount;
        public float refreshP = 0.20f;
    }

    public class BottleLogic
    {
        public int thrownBottles;
        public bool shipResolved;           // v0.12:错过即永久
        public bool ghostShipResolved;
    }

    public class EventBagState
    {
        public List<GameEventSO> queue = new List<GameEventSO>();
        public int roundIndex;
        public List<GameEventSO> insertQueue = new List<GameEventSO>();
    }

    public class HiddenLineFlags
    {
        public bool bonesSeen;          // v0.24:= "骸骨事件已经出现过"(整局一次的闸 + 幽灵船 30% 的开关)
        public bool nameTagTaken;
        public bool hasMap;
        public bool hasKey;
        public bool hasChest;
        public bool offeredThisRun;         // 局内 flag(≠ 跨局 trueEndingClaimed)
        public bool twoLightsSeen;
        public bool shadowNightResolved;
        public bool metShadow;
        public readonly HashSet<ItemSO> everOwned = new HashSet<ItemSO>();   // 友善狐狸货单
        public readonly HashSet<ItemSO> loreFound = new HashSet<ItemSO>();   // v0.26:彩蛋"整局只出一次"的那道闸
        // v0.26:彩蛋层里那三件"前一晚触发、第二天清晨才掷"的(A3 徽章 / B3 救生筏残片 / D1 狐狸的回礼)
        //        在这里登记来源键,清晨由 IslandPhase.MorningTick 排空。它是"延迟标记",不是收集状态。
        public readonly List<string> nextMorningEgg = new List<string>();
    }

    // 救援线两阶段(搜寻飞机):v0.27 起第二阶段不再靠插播,所以这里只剩"欠不欠第二发"
    public class RescueLine
    {
        public int planeStage;            // 0 = 没开始,1 = 已打第一发、欠第二发,2 = 这一局这条线关闭
                                          // (⚠ 不再有 planeSecondNight 那个"2~4 夜后插播"的字段:第二发等的就是 搜寻飞机 下一次从轮空池里轮上来)
        public bool flareUsedThisRun;     // 土制信号弹每局限 1 发
    }

    public class PlayerStats
    {
        public int hp = 5;
        public int staminaMax = 3;          // v0.28:5 → 3
        public int stamina = 3;
        public float fullness = 1f;         // 0~1
        // v0.28:hydration 与 noWaterDays 已删 —— 喝水系统整条移除,生存线上只剩 饱食 一条资源衰减
        public bool sick;
        public int nextDayStaminaPenalty;   // "次日体力 -N":一次性
        public int nextDayCapPenalty;       // "次日体力上限 -N":血月下水(领航员免),与上一行是两种东西
        public int capPenaltyToday;         // 当天生效的上限扣减,每天早上从 nextDayCapPenalty 结转
        public int noFoodDays;
        // v0.46(用户):吃掉队友之后 **三天吃不下东西**(吃罐头/开椰子/吃巧克力 一律挡)。
        //   与上面那个 noFoodDays 是两回事:noFoodDays = "饿到 0 的天数"(后果是掉生命),这个是"吃不进去的剩余天数"。
        public int foodLockDays;
    }

    // v0.30 日记系统:一天一条,里面写"那天白天做过的事 + 那天夜里发生的事"。
    // 它 **不是** RunState.log 的镜像 —— log 是调试通道(只进控制台),日记是玩家唯一能读的那一份。
    public enum DiaryLineKind { Day, Night, Egg, Bad }

    public class DiaryLine
    {
        public DiaryLineKind kind;
        public string text;
        // v0.40:同类项合并 —— 同一件白天行动做 N 次,日记里 **只留一行**,文案换成"做了 N 次"那一句。
        //        (用户:"玩家今天钓了三条鱼,直接出钓三条鱼的文案,不要一条一条的出相同的文案")
        public string mergeKey;
        public int n = 1;
    }

    public class DiaryEntry
    {
        public int day;                                   // 第几天(1 起)
        public readonly List<DiaryLine> lines = new List<DiaryLine>();
    }

    // 跨局字段只在 SaveData(§10 风险25:不许塞进 HiddenLineFlags)
    public static class SaveData
    {
        const string KeyTrueClaimed = "60slike.trueEndingClaimed";
        const string KeyLoopCount = "60slike.loopCount";
        const string KeyEndingsSeen = "60slike.endingsSeen";
        const string KeyEggsSeen = "60slike.eggsSeen";
        const string KeyDateMode = "60slike.dateMode";
        const string KeyName = "60slike.playerName";
        const string KeyVolume = "60slike.musicVolume";
        public static bool trueEndingClaimed { get { return PlayerPrefs.GetInt(KeyTrueClaimed, 0) == 1; } }
        public static void SetTrueClaimed(bool v) { PlayerPrefs.SetInt(KeyTrueClaimed, v ? 1 : 0); PlayerPrefs.Save(); }
        public static void ResetTrueClaimed() { SetTrueClaimed(false); }

        // v0.26:B2 刻着「正」字的木片 —— "每一笔是一次回环"。所以这个数记的是 **掉进 结局F 的次数**,不是总局数。
        //        设置里那条"重置真结局标记"不动它;它是计数,不是标记(§10 风险25:跨局的只放这里)。
        public static int loopCount { get { return PlayerPrefs.GetInt(KeyLoopCount, 0); } }
        public static void AddLoop() { PlayerPrefs.SetInt(KeyLoopCount, loopCount + 1); PlayerPrefs.Save(); }

        // ---- v0.30:主菜单「收集」页的两本账(跨局累计,不随单局重置)----
        static readonly HashSet<string> endingsSeen = LoadSet(KeyEndingsSeen);
        static readonly HashSet<string> eggsSeen = LoadSet(KeyEggsSeen);
        static readonly Dictionary<string, string> pending = new Dictionary<string, string>();

        static HashSet<string> LoadSet(string key)
        {
            var s = new HashSet<string>();
            string raw = PlayerPrefs.GetString(key, "");
            if (raw.Length > 0) foreach (var p in raw.Split(',')) if (p.Length > 0) s.Add(p);
            return s;
        }

        static void Remember(HashSet<string> set, string key, string val)
        {
            if (string.IsNullOrEmpty(val) || !set.Add(val)) return;
            var arr = new string[set.Count];
            set.CopyTo(arr);
            pending[key] = string.Join(",", arr);
        }

        public static bool HasSeenEnding(string id) { return endingsSeen.Contains(id); }
        public static bool HasSeenEgg(string key) { return eggsSeen.Contains(key); }
        public static int EndingsSeenCount { get { return endingsSeen.Count; } }
        public static int EggsSeenCount { get { return eggsSeen.Count; } }
        public static void MarkEndingSeen(string id) { Remember(endingsSeen, KeyEndingsSeen, id); }
        public static void MarkEggSeen(string key) { Remember(eggsSeen, KeyEggsSeen, key); }

        // 攒着一起写:PlayerPrefs.Save 不便宜,而一局里只会新增几条
        public static void Flush()
        {
            if (pending.Count == 0) return;
            foreach (var kv in pending) PlayerPrefs.SetString(kv.Key, kv.Value);
            pending.Clear();
            PlayerPrefs.Save();
        }

        // v0.30 设置页:日记日期的显示模式(0 = "D12" 紧凑,1 = "第 12 天")
        public static int dateMode { get { return PlayerPrefs.GetInt(KeyDateMode, 1); } }
        public static void ToggleDateMode() { PlayerPrefs.SetInt(KeyDateMode, dateMode == 0 ? 1 : 0); PlayerPrefs.Save(); }
        public static string Date(int day) { return dateMode == 0 ? "D" + day : "第 " + day + " 天"; }

        // v0.61(用户:"**开始菜单把姓名,重置结局,日记日期显示均放在设置的子页面里**"):
        //   姓名从"每局在标题屏现填"改成 **存进设置**(跨局记住)—— 不存的话搬进设置页就等于每局重填,
        //   比原来还麻烦。空串 = 没填过,开局时 `SanitizeName` 照旧兜成默认名(那条规则一个字没动)。
        public static string playerName { get { return PlayerPrefs.GetString(KeyName, ""); } }
        public static void SetPlayerName(string v) { PlayerPrefs.SetString(KeyName, v ?? ""); PlayerPrefs.Save(); }

        // v0.61:音乐音量(设置页里四档循环:关/低/中/高)。存 0~1 的浮点,`Music` 拿它乘淡入淡出的目标值。
        public static float musicVolume { get { return PlayerPrefs.GetFloat(KeyVolume, 0.7f); } }
        public static void SetMusicVolume(float v) { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(v)); PlayerPrefs.Save(); }
    }

    public class RunState
    {
        public string playerName = "Daylily";
        public RunPhase phase = RunPhase.Title;
        public int day;                     // 第 N 天(1 起)
        public int nightCount;              // 已结算的夜晚数(轮空池的 minNight 用它)
        public System.Random rng = new System.Random();

        public Inventory storage = new Inventory();   // 荒岛仓库(无限容量)
        public List<ItemSO> carried = new List<ItemSO>(); // 拾荒阶段的 4 格携带条(逐件)
        public readonly HashSet<string> structures = new HashSet<string>();
        // v0.17:每局限做一次的配方(土制信号弹 / 打火石)记在这本账上 —— 用"仓库里还有没有"去判是错的,
        // 因为产出物会被用掉、会坏、会修回来。
        public readonly HashSet<string> craftedThisRun = new HashSet<string>();
        // v0.48(用户):**建筑只有"坏 / 不坏"一套模型,火的"熄灭"就是它的"坏"**。
        //   ⇒ 所有"这件东西现在好不好用"的判定 **只有一条路**:`HasStructure(k)` = 建过 且 没坏。
        //     读者不再需要同时看计数(上一版 `fireLitTonight` 读的是两个计数器,那是第二套模型,已废)。
        //   计数器只剩一件事:**离熄灭还有几晚**。建造 = 点燃(用户:"篝火不用点,存在则那几条分支都直接走"),
        //   两晚烧完 / 被涨潮打湿 ⇒ `MarkStructureBroken` ⇒ 进入维修面板那一行("重新点燃",只花一块火种)。
        public int campfireNights;
        public int signalFireNights;
        public const int FireNightsPerLighting = 2;      // 用户原话:"篝火和信号火堆都持续两晚上"
        public int NightsLeft(string key)
        {
            if (!HasStructure(key)) return 0;            // 坏了(灭了)就没有"还剩几晚"
            return key == Database.SignalFire ? signalFireNights :
                   key == Database.Campfire ? campfireNights : 0;
        }
        public bool fireLitTonight { get { return HasStructure(Database.Campfire) || HasStructure(Database.SignalFire); } }
        // 信号火堆 是 篝火 的上位平替:它没坏的时候,篝火的作用一并成立(上面那条"或"就是这件事的全部实现)。
        public bool SignalFireBurning { get { return HasStructure(Database.SignalFire); } }
        // 烧完/被打湿 = 坏。两边一起处理:计数归零 + 进 brokenStructures(统一模型的关键一步)
        public void Extinguish(string key)
        {
            if (!structures.Contains(key)) return;
            if (key == Database.SignalFire) signalFireNights = 0;
            else campfireNights = 0;
            brokenStructures.Add(key);
        }
        // 重新点燃(= 修好这件"坏"):计数复位 + 从破损那本账里划掉
        public void Relight(string key)
        {
            if (key == Database.SignalFire) signalFireNights = FireNightsPerLighting;
            else campfireNights = FireNightsPerLighting;
            brokenStructures.Remove(key);
        }
        // 每个清晨:还在烧的减一晚,减到 0 就"坏"
        public void TickFiresAtDawn()
        {
            if (HasStructure(Database.Campfire) && --campfireNights <= 0) Extinguish(Database.Campfire);
            if (HasStructure(Database.SignalFire) && --signalFireNights <= 0) Extinguish(Database.SignalFire);
        }
        public bool useBait;                  // v0.18:钓鱼挂不挂鱼饵的开关(每局的态,默认不挂)
        // v0.44:电量从"有/没有"两态改成 **2 格存储**(用户:"手电筒有两点电量存储(开局随机0-2);一份电消耗一点精力充电")。
        //        每局的态,所以它在 RunState 而不是盘上的 FlashlightSO。初值在下面的构造函数里随机 0~2。
        public int flashlightCharge;

        public RunState()
        {
            // rng 是上面那个字段初始化器先建的(声明顺序),所以这里可以直接掷。
            flashlightCharge = rng.Next(0, 3);      // 开局 0 / 1 / 2 格
        }

        public PlayerStats stats = new PlayerStats();
        public TeammateState mate = new TeammateState();
        public ExploreState explore = new ExploreState();
        public GullState gull = new GullState();
        public BottleLogic bottle = new BottleLogic();
        public EventBagState bag = new EventBagState();
        public HiddenLineFlags hidden = new HiddenLineFlags();
        public RescueLine rescue = new RescueLine();

        public EndingId ending = EndingId.None;
        public EndingSO endingSo;
        public readonly List<string> log = new List<string>();

        // v0.30:diary = 玩家能读的那一份;today = 当天还没封口的那几行,清晨由 SealDiary 收进 diary。
        public readonly List<DiaryEntry> diary = new List<DiaryEntry>();
        readonly List<DiaryLine> today = new List<DiaryLine>();

        /// **玩家-facing 的日记通道。** 与 Log() 的分工是硬规矩:
        /// Log() = 机械文本,只进控制台(调试);Diary() = 叙述文本,进日记、玩家只看到这一份。
        /// ⚠ v0.40:传了 `mergeKey` 的行 **同一天内不重复追加** —— 第二次起只把次数 +1,
        ///   并换成 `DiaryLines.MergeDay` 那句"今天做了 N 次"的合并文案(没有合并文案的 key 就保持第一句,
        ///   宁可少说,也不要刷出一屏重复句)。控制台仍然一次不落,每回都打。
        public void Diary(string text, DiaryLineKind kind = DiaryLineKind.Day, string mergeKey = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[60s日记 D" + day + "] " + text);
            if (!string.IsNullOrEmpty(mergeKey))
            {
                for (int i = 0; i < today.Count; i++)
                {
                    if (today[i].mergeKey != mergeKey) continue;
                    today[i].n++;
                    string merged = DiaryLines.MergeDay(mergeKey, today[i].n, day);
                    if (!string.IsNullOrEmpty(merged)) today[i].text = merged;
                    return;
                }
            }
            today.Add(new DiaryLine { kind = kind, text = text, mergeKey = mergeKey });
        }

        // 天亮时封口:把"第 day 天"的这几行钉成一条。空白天也要留一条,不然翻页会跳号。
        public DiaryEntry SealDiary()
        {
            var e = new DiaryEntry { day = day };
            if (today.Count == 0)
                e.lines.Add(new DiaryLine { kind = DiaryLineKind.Day, text = DiaryLines.QuietDay(day) });
            else e.lines.AddRange(today);
            today.Clear();
            diary.Add(e);
            return e;
        }

        public bool HasStructure(string k) { return structures.Contains(k) && !brokenStructures.Contains(k); }
        // v0.48:**火这件建筑"刚建好"= 还没点着 = 坏**。这样"灶在、火不在"是一个安全且看得见的状态,
        //   而 DoCraft 那一支的 Relight 是把它的唯一途径 ⇒ 万一以后有人新增一座吃火种的建筑而忘了点,
        //   出现的是"没火"(玩家能看见、能修),不是 **假火**(悄悄按有火结算)。
        public void Build(string k)
        {
            structures.Add(k);
            // v0.64(用户:"**信号火堆直接替代火堆**" + 定案「**升级替换:篝火建到信号火堆就变成它**」)
            //   ⇒ **营地里始终只有一摊火**:升到信号档,篝火 那一档就从"建过"与"破损"两本账里退场,
            //     两档不会并排站在两处(原来的摆法是 篝火 在 fireAnchor、信号火堆 在另一格,那是两摊火)。
            //     "还剩几晚"按你选的「**升级 = 重新点燃**」:**不继承**剩余晚数 —— 它与任何一次建造一样,
            //     建好当场点着、烧满 `FireNightsPerLighting` 晚(这一条 v0.48 就是这么定的,所以**没有新账**,
            //     火种照旧在这一手里吃掉,熄灭了要再点也照旧一块火种)。
            if (k == Database.SignalFire)
            {
                structures.Remove(Database.Campfire);
                brokenStructures.Remove(Database.Campfire);
                campfireNights = 0;
            }
            if (Database.IsFireStructure(k)) brokenStructures.Add(k);
            else brokenStructures.Remove(k);
        }

        // v0.64:一摊火两档 ⇒ "这一档还算不算已经建过"要 **连着上下档一起看**。
        //   没有这一条,升级之后 `structures` 里就没有 campfire 了,制造面板会把「垒 篝火(材料3)」重新摆回玩家面前
        //   —— 那等于允许花 3 材料把一摊 信号火堆 **降级回篝火**。
        public bool StructureEverBuilt(string k)
        {
            if (k == Database.Campfire) return structures.Contains(Database.Campfire) || structures.Contains(Database.SignalFire);
            return structures.Contains(k);
        }

        // v0.46(用户):围墙 也会坏,按标准的累积损坏概率走(§2.3 那条 20% 起 / 每次 +10% / 上限 90%)。
        //   "建过"与"现在还在用"是两本账:建过的记在 structures,坏了的记在这里。
        //   分开的理由:修好它不需要重做一份配方(修 = 材料 + 体力,直接把它从这本账里划掉),
        //   而且 制造面板"已经建好了就不出现"那条闸读的是 structures —— 它不该被"坏了"重新打开。
        public readonly HashSet<string> brokenStructures = new HashSet<string>();
        public bool StructureIntact(string k) { return structures.Contains(k) && !brokenStructures.Contains(k); }
        public void MarkStructureBroken(string k) { if (structures.Contains(k)) brokenStructures.Add(k); }
        public bool RepairStructure(string k) { return brokenStructures.Remove(k); }

        public void Log(string s)
        {
            log.Add("D" + day + " " + s);
            if (log.Count > 400) log.RemoveAt(0);
            Debug.Log("[60s] " + s);
        }

        public int UsedCarrySlots()
        {
            int n = 0;
            foreach (var i in carried) n += i.slots;
            return n;
        }

        // §2.3:饱食度过低 → 次日体力恢复打折(demo:低饱食时睡眠只回一半)
        public int SleepStaminaRestore()
        {
            int max = stats.staminaMax;
            if (stats.sick) max = Mathf.Max(1, max - 1);          // 生病:体力上限 -1、睡眠不再回满
            max = Mathf.Max(1, max - stats.capPenaltyToday);      // 血月下水:次日体力上限 -1
            if (stats.fullness < 0.25f) max = Mathf.Max(1, max / 2);
            return max;
        }
    }
}
