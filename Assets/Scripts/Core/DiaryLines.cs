using System.Collections.Generic;

namespace SixtySLike
{
    // v0.30 日记文案。⚠ 这一份刻意 **不走资产**:它是纯叙述文本,没有任何数值与判定,
    // 而 §7.2 那三条铁律里两条都是"手改资产 YAML 翻车"。要搬进 SO 的话见 §11-64(等日志系统定型再说)。
    //
    // 三条写文案的规矩:
    //   1) 第一人称、短句、不喊叫、不解释机制 —— 玩家已经知道规则,日记只记他那一天的感受与损失;
    //   2) 凡是数值后果必须在句子里看得见(-1 生命就说疼、丢了东西就说没了),不许写成散文把代价藏起来;
    //   3) 轮换一律按 天数 取模,**绝不用 S.rng** —— 那会改变同一颗种子里的事件与损坏判定(§7.2 的确定性口径)。
    public static class DiaryLines
    {
        static string Pick(string[] opts, int day)
        {
            if (opts == null || opts.Length == 0) return null;
            return opts[PositiveMod(day, opts.Length)];
        }

        static int PositiveMod(int a, int b) { return ((a % b) + b) % b; }

        // ---- ①每日开场(答案 9 里那句"按天数固定文案":什么也没发生的那天也有东西可读)----
        static readonly string[] Quiet =
        {
            "今天没有别的事。我把能省的都省了。",
            "潮声吵了我半宿。白天什么都没发生,也算好事。",
            "我把昨天的东西数了一遍,又数了一遍。",
            "风往陆地上吹,岛上什么消息也不肯给我。",
            "今天很平静。我开始不信这种平静了。",
            "我在沙上画了一道,又擦掉。数日子这件事我不想再练。",
        };

        public static string QuietDay(int day) { return Pick(Quiet, day); }

        // ---- ②白天做过的事 ----
        static readonly Dictionary<string, string[]> Day_ = new Dictionary<string, string[]>
        {
            { "explore", new[] {
                "我进林子翻了一整天,累到坐在地上下不来。带回 {0}。",
                "林子深处什么都有点影子,又什么都没有。翻出来的只有 {0}。" } },
            { "fish", new[] {
                "我在礁石边守到手上起皱,钓上来 {0}。",
                "海还是肯给东西的。今天 {0}。" } },
            { "fishmiss", new[] {
                "礁石边守了半天,一根鳍都没见着。",
                "今天海不收我的钩。" } },
            { "dive", new[] {
                "我下水摸了一趟,上来时耳朵里全是咸的。{0}。" } },
            { "eat", new[] {
                "我打开一罐,慢慢吃完。瓶子空了,人没空。",
                "又少一罐。我开始把一口分成三口吃。" } },
            { "chocolate", new[] {
                "那根巧克力棒我留了很久,今天吃了。手指头回来得比脑子快。" } },
            { "coconut", new[] {
                "开了个椰子,汁顺着下巴流。饱了,伤也没那么疼。" } },
            { "heal", new[] {
                "我给自己上了药,然后等它起作用。" } },
            { "healfail", new[] {
                "药吃完了,病没走。那份药白扔了。" } },
            { "talk", new[] {
                "我和他/她说了会儿话。说得不好,但说了。",
                "今天有人回应我的声音。这件事比话本身有用。" } },
            { "feed", new[] {
                "我把一罐分给他/她,看着吃完才收手。" } },
            { "mateleft", new[] {
                "吊床空了。我没有喊名字 —— 喊也没用。" } },
            // v0.41:食用队友(第五种失去路径)。走 Bad 那支颜色,不粉饰、也不道德审判 —— 只记他做了什么和后果。
            // v0.46:后果换了一个 —— 回饱之外多了一条 "三天咽不下任何东西"(数值在 IslandPhase.MealFoodLockDays)。
            { "matemeal", new[] {
                "我吃了他/她。饱了。接下来三天我什么都咽不下去 —— 大概是身体先替我认了这件事。",
                "今天我不饿。往后三天我也吃不下去。从今每一天我都得记着为什么不饿。" } },
            { "matefamine", new[] {
                "他/她的吊床空了。我数过罐头,算出是我少给了一口 —— 但我也记得那天我自己也没吃饱。" } },
            { "matesuicide", new[] {
                "他/她不声不响地离开了。没有告别,也没有脚印。我把他的那份罐头收进箱子,手很稳。" } },
            { "craft", new[] {
                "我把材料磨成了能用的一样东西。手上多了两道新口子。" } },
            { "repair", new[] {
                "我把坏掉的那件修好了。它比我更值得活着。" } },
            { "charge", new[] {
                "摇把手摇到胳膊抬不起来。手电筒的灯又亮了一格。" } },
            { "throwbottle", new[] {
                "我把一个瓶子扔进海里,扔得很轻,像怕它碎。" } },
            { "egg", new[] {
                "我在沙上捡到一样不该在这儿的东西,收进箱子里了。" } },
            { "sick", new[] {
                "我又开始发抖。今天做什么都是两份累。" } },
            { "lowstamina", new[] {
                "天没黑我就动不了了。今天剩下的事只能明天做。" } },
        };

        public static string Day(string key, int day, params object[] args)
        {
            string[] opts;
            if (!Day_.TryGetValue(key, out opts)) return null;
            string t = Pick(opts, day);
            if (t == null) return null;
            return args == null || args.Length == 0 ? t : string.Format(t, args);
        }

        // ---- ②b 同类项合并(v0.40)----
        // 同一件白天行动一天里做 N 次,日记 **只留一行**,文案换成下面这句({0} = 次数)。
        // 用户原话:"玩家今天钓了三条鱼,直接出钓三条鱼的文案(喜悦/庆幸什么的),不要一条一条的出相同的文案"。
        // 只列 **真会重复** 的那几个 key:explore / dive / feed 一天最多一次,不需要合并句。
        static readonly Dictionary<string, string[]> Merge_ = new Dictionary<string, string[]>
        {
            { "fish", new[] {
                "今天下了 {0} 次钩,回回都有东西上钩。海还认我。",
                "{0} 次把手伸进海水,{0} 次都没白等 —— 我忍住没在滩上笑出声。" } },
            { "fishmiss", new[] {
                "今天白守了 {0} 回,一根鳍都没见着。海不收我的钩。" } },
            { "eat", new[] {
                "今天开了 {0} 罐。我开始一罐分三口吃。" } },
            { "coconut", new[] {
                "今天开了 {0} 个椰子。饱着的时候,我敢多想一点别的事。" } },
            { "heal", new[] {
                "上了 {0} 回药。总有一回该轮到我好起来。" } },
            { "healfail", new[] {
                "{0} 回药都没压住这个病。那份一份扔掉的,是我的运气。" } },
            { "talk", new[] {
                "今天和他/她说了 {0} 回话。这声音在岛上是有钱的东西,我舍不得省。" } },
            { "craft", new[] {
                "今天手里做成了 {0} 件东西。手上有活,心里就有点底。" } },
            { "repair", new[] {
                "修了 {0} 回。这些工具比我更值得心疼 —— 它们是我仅剩的证人。" } },
            { "charge", new[] {
                "摇充电摇到 {0} 回。反正不要力气,我就多摇了一会儿,好在夜里多看一眼。" } },
            { "throwbottle", new[] {
                "今天往海里丢了 {0} 个瓶子。每一个都是一封不知道有没有人读的信。" } },
        };

        // 没有合并句的 key 返回 null ⇒ RunState.Diary 保持第一次那句原文,只不再重复追加(宁可少说,不刷重复句)
        public static string MergeDay(string key, int n, int day)
        {
            if (n < 2) return null;
            string[] opts;
            if (!Merge_.TryGetValue(key, out opts)) return null;
            return string.Format(Pick(opts, day), n);
        }

        // ---- ③夜里发生的事:key = "事件:选项",退到 "事件" ----
        static readonly Dictionary<string, string[]> Night_ = new Dictionary<string, string[]>
        {
            // 涨潮
            { "tide:high", new[] {
                "水漫进营地。我带着人爬到高处,露天的东西被卷走了几件 —— 我听见它们碰在礁石上的声音。" } },
            { "tide:save", new[] {
                "我一夜没睡,把露天的东西全拖进庇护所。一样没丢,代价是第二天连站起来的力气都是借的。" } },
            { "tide", new[] { "潮水在夜里涨了。天亮时沙上多了一道新的线。" } },
            // 骸骨(彩蛋层)
            { "bones:look", new[] {
                "潮水把一个东西冲上来了。我走过去看了 —— 那具骸骨胸口的铭牌上,刻的是我的名字。" } },
            { "bones:ignore", new[] {
                "潮水冲上来一样东西。我没有走过去。天亮时沙上只剩一片湿。" } },
            { "bones", new[] {
                "海里推出一副骸骨,横在潮线上。我离它很近,一整夜没有动手。" } },
            // 血月
            { "bloodmoon:blanket", new[] {
                "月亮是红的。我把毯子裹在他/她身上,自己坐到天亮。毯子废了,人没废。" } },
            { "bloodmoon:dive", new[] {
                "血月退潮,我下海摸了一趟,拿到 {0}。第二天腿是软的 —— 上限被压掉一截。" } },
            { "bloodmoon:nothing", new[] {
                "整夜血红。他/她没睡,我也没睡。谁都没说话。" } },
            { "bloodmoon", new[] {
                "月亮升起来是红的。我在滩边坐到水退远,没有下海。" } },
            // 搜寻飞机(两阶段)
            { "searchplane:flare", new[] {
                "我看见机影了,打了一发。它绕了半圈就走了 —— 它要的是第二发。下一次它再来,我还得有一发。" } },
            { "searchplane:flare2", new[] {
                "第二发打出去了。这次机影没有绕圈。" } },
            { "searchplane:firepile", new[] {
                "我把信号火堆点着了。火光里那架飞机摆了一下翅膀,然后消失在海平线上。" } },
            // v0.46:火堆一直烧着的那一晚,不需要任何人再举东西 —— 它是"直接判定通过"的那条路
            { "searchplane:signalfire", new[] {
                "机影还没到就先看见了我:沙丘上那堆火是我白天点下的。这一次我什么都没做。" } },
            { "searchplane:flashlight", new[] {
                "我用手电筒照过去。灯空了,答案还没拿到。" } },
            // v0.43:命中与落空是两句话 —— 以前只有落空那句,结果获救那一晚也写"答案还没拿到"(文案 bug)。
            //        v0.42 那三条 flash1/flash2/flash2miss 随"两轮才掷 50%"一起撤销了。
            { "searchplane:flashhit", new[] {
                "我把光柱举到那架飞机身上。它没有绕圈 —— 它朝我下来了。" } },
            { "searchplane:ignore", new[] {
                "有飞机,我没有回应。它转了一圈就走了,像每天都会看见这座岛一样。" } },
            { "searchplane", new[] {
                "海平线上一明一暗。那架飞机绕了这座岛一圈,我留在沙上没有动。" } },
            // 轮船 / 幽灵船
            { "ship:sail", new[] { "有船靠过来。我上了船。" } },
            { "ship:ignore", new[] {
                "那条船在灯下停了一会儿,然后走了。海面上再也没有第二条 —— 我知道我把它用完了。" } },
            { "ship:flare", new[] { "一发打出去,船靠过来了。" } },
            // v0.46(用户):沙丘上那道 信号火堆 在烧 ⇒ 救援事件不用再挑道具,那一晚直接判定通过。
            //        三条各写一句 —— 它们是不同的船,不该读起来像同一件事。
            { "ship:signalfire", new[] {
                "火堆烧到天亮。船上的人看见了烟,也看见了我 —— 没人喊我做什么。" } },
            { "ship:flashlight", new[] { "我用光去赌,赌输了。船从此不再来。" } },
            { "ship", new[] {
                "一条船开进我能看清栏杆的距离,然后停在那儿。一整夜我都在看它离得有多近。" } },
            { "ghostship:ignore", new[] {
                "一艘没有灯也没有人的船靠上礁石。我一夜没敢动。天亮它退了,我活着,但也什么都没换来。" } },
            { "ghostship:flare", new[] { "我朝那艘没有灯的船打了一发。船上的人来接我了。" } },
            { "ghostship:signalfire", new[] {
                "火堆的光落在那艘船上。他们没有一个有影子,但还是停下来接我。" } },
            { "ghostship", new[] { "整条海平线暗下去,只那一艘船是亮的。" } },
            // 低温夜
            { "coldnight:fire", new[] {
                "冷得发抖,所以我烧了一堆。材料烧掉了两块,后半夜人是活的。" } },
            { "coldnight:blanket", new[] {
                "我把毯子裹紧熬过去。它这次废了。" } },
            { "coldnight", new[] {
                "这一夜我硬挨了下来。第二天我发着抖,知道自己生病了。" } },
            // 毒蛇 / 蟑螂
            { "snake:flashlight", new[] { "我用光照过去,那东西散了。灯也没电了。" } },
            { "snake:swat", new[] { "我把它赶走了。手上有两个洞,但不深。" } },
            { "snake:bite", new[] { "有东西钻进营地。我躲开了大部分,没躲开全部 —— 醒来时手上肿着。" } },
            { "snake", new[] { "有东西钻进营地。我躲开了大部分,没躲开全部。" } },
            // 鱼群 / 漂流瓶
            { "fishschool:net", new[] { "鱼群压过来,我下了网。三份罐头,网也磨旧了一格。" } },
            // v0.46:这一条现在 **必然是鱼叉**(钓竿 被从 鱼群 里拿掉了),所以句子可以点工具
            { "fishschool:hand", new[] { "鱼群近到我能听见水响。我举着叉子下去,留住了三条。" } },
            { "fishschool", new[] { "一整片银色从浅海里过,我错过了。" } },
            { "driftbottle:grab", new[] {
                "我在礁石边捞到一个瓶子。里面卷着一张纸 —— 我没敢看第二遍。" } },
            { "driftbottle", new[] { "有个瓶子从眼前转走了。我没伸手。" } },
            // 海鸥
            { "gull:feed", new[] {
                "我把手里的一份分给那只海鸥。它留下了。我开始数它们落在屋顶上的次数。" } },
            { "gull", new[] { "一只海鸥落在庇护所顶上,歪着头看我吃东西。" } },
            // 小影怪
            { "smallshadow:watch", new[] {
                "有个小一点的黑影绕着吊床蹭。我整夜睁着眼,它天亮前退了。我一天没力气。" } },
            { "smallshadow:fire", new[] {
                "火撑着那个黑影。它退了,火也熄了。" } },
            { "smallshadow", new[] {
                "我躲起来了。第二天吊床是空的 —— 是它把人拖走了,不是我。" } },
            // 调皮的猴子
            { "monkeynaughty:spear", new[] {
                "猴子抢了东西就跑,我用鱼叉把它捅回去。东西完好回来,还多一份罐头;叉子磨旧了一格。" } },
            { "monkeynaughty:flare", new[] {
                "我朝猴子打了一发。东西完好回来了 —— 那一发本来是留给船的。" } },
            { "monkeynaughty:fire", new[] {
                "火光把猴子吓跑了。东西抢回来了,但是坏的。" } },
            { "monkeynaughty:grab", new[] {
                "我扑上去空手抢。东西抢回来了,是破的,我少了一格血。" } },
            { "monkeynaughty:refuse", new[] {
                "我看着它跑掉。{0} 没了,永久没了。我告诉自己这不值得流血。" } },
            { "monkeynaughty", new[] { "一只猴子从林子里窜进来,抓起一样东西就跑。" } },
            // 友善的猴子
            { "monkeyfriendly:trade", new[] {
                "两只猴子摆了一排,把想换的那对推到我面前:我交出 {0},换回 {1}。不像交易,像上供。" } },
            { "monkeyfriendly:nogive", new[] {
                "它们想收的东西我一样也没有。它们留下 2 份罐头,自己走了。" } },
            { "monkeyfriendly:full", new[] {
                "它收走了 {0} —— 我手里已经没有它们没见过的东西了,于是它留下 2 份罐头。" } },
            { "monkeyfriendly:decline", new[] { "两只猴子坐在那儿等我。我没动。它们自己走了。" } },
            { "monkeyfriendly", new[] {
                "一只猴子把一件东西放在沙上,退开两步等我。我们都没出声。" } },
            // 雨三档 / 武器a
            { "rain", new[] {
                "下了一夜雨。天亮时庇护所外面那圈沙又软了一层。" } },   // 父事件兜底:正常永远显示的是三档变体
            { "rain_light:umbrella", new[] { "屋檐滴水。我把伞撑开,伞磨旧了些,人没病。" } },
            { "rain_heavy:umbrella", new[] { "暴雨糊了整屏。伞撑住了,我记住了它的代价。" } },
            { "rain_storm:umbrella", new[] { "一次白闪,雷声过后才落下来。伞废了,第二天我使不上劲。" } },
            { "rain_light", new[] { "下了一夜小雨。我淋着,第二天开始咳嗽。" } },
            { "rain_heavy", new[] { "暴雨下了一夜。我生了病,第二天腿是飘的。" } },
            { "rain_storm", new[] { "雷雨。白闪之后我躺了很久。病和空掉的体力,两样一起付。" } },
            { "weapona:umbrella", new[] {
                "月亮亮得不正常。我用伞挡着那束光,伞这次必废。标题就叫武器a,我不想它的名字。" } },
            { "weapona", new[] {
                "那轮月亮亮得不正常。我硬挨到天亮,第二天连眼睛都睁不开。" } },
            // 假队友
            { "fakemate:flashlight", new[] {
                "有个人影在暗处说他不舒服。我用光照过去 —— 是真的。他/她确实病了。" } },
            { "fakemate", new[] {
                "暗处有人影说他不舒服。我没有照,也没有应。第二天想起来还是后悔。" } },
            // 影怪 / 两点光
            { "shadow:offer", new[] { "我把箱子推过去。它像在看一张很久以前的借据。" } },
            { "shadow:spear", new[] {
                "庇护所外有黑影逼近,眼睛发光。我用鱼叉挡住了它 —— 我没事,叉子废了。" } },
            { "shadow:flashlight", new[] { "我把光顶在它面前。它退了,灯空了。" } },
            { "shadow:lure", new[] {
                "我把它引开了。我活着,吊床空了。这件事我这辈子都会记得是几点。" } },
            { "shadow:hide", new[] {
                "我钻进被子屏住呼吸。它从我身上过去了 —— 只拿走了一半的伤。" } },
            { "shadow:hidefail", new[] {
                "我钻进被子屏住呼吸,但它还是找到我了。那一夜拿走的是全部的伤。" } },
            { "shadow", new[] {
                "那东西终于来了。我什么都没做,让它过去了一遍。第二天我数着自己的伤口。" } },
            { "twolights:offer", new[] { "庇护所外两点光。我把箱子推过去。" } },
            { "twolights", new[] { "庇护所外两点光,不动。我没有过去。它熄灭了。" } },
            // 椰树(v0.28 新增)
            { "palmtree:take", new[] {
                "海风把椰树吹得很响,我过去看了看,抱回一个椰子。" } },
            { "palmtree:hit", new[] {
                "海风把椰树吹得很响。我抱回一个椰子,代价是头上挨了一下 —— 少了一格血。" } },
            { "palmtree", new[] { "椰树摇了一夜。我留着明天再过去。" } },
        };

        public static string Night(string evKey, string choiceKey, int day, params object[] args)
        {
            string[] opts;
            if (!string.IsNullOrEmpty(choiceKey) && Night_.TryGetValue(evKey + ":" + choiceKey, out opts))
                return Format(Pick(opts, day), args);
            if (Night_.TryGetValue(evKey, out opts)) return Format(Pick(opts, day), args);
            return null;
        }

        // 影怪那晚的"躲被子成功/失败"、蛇的"赶跑/被咬"这种同选项两结果的,由调用方给后缀
        public static string Suffix(string s) { return s; }

        static string Format(string t, object[] args)
        {
            if (t == null) return null;
            return args == null || args.Length == 0 ? t : string.Format(t, args);
        }

        // ---- ④彩蛋到手时的那一句(玩家侧只说"捡到一样不该在这儿的东西",细节在收集页读)----
        public static string Egg(string itemName)
        {
            return "我捡到一样东西:" + itemName + "。它不该在这座岛上。";
        }

        // ---- ⑤几个没有"事件:选项"可查的固定场合 ----
        public static string Calm(int day)
        {
            return Pick(new[] {
                "那一夜什么都没有发生。我睡得像块石头,这在这座岛上算奢侈品。",
                "安静的一夜。我半夜醒了一次,听见潮水在数我的日子。" }, day);
        }

        public static string CalmTalk(int day)
        {
            return Pick(new[] {
                "夜里无事,我和他/她说到很晚。不用省力气的时候,话就多起来了。" }, day);
        }

        // 开局那页(拾荒之后、第一个白天之前)
        public static string Opening(int carried, bool withMate)
        {
            return "我从飞机上活着下来,箱子里有 " + carried + " 件东西"
                   + (withMate ? ",还有一个人和我一起。" : "。只有我一个人。")
                   + " 从今天起我写这个,免得忘了自己是谁。";
        }

        public static string Death(string cause) { return cause; }
    }
}
