using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixtySLike
{
    // 一次性把 demo 内容落成可保存的资产:
    //   Assets/Data/**              §5/§6/§2.3/§3/§4 的表,Inspector 里直接改数
    //   Assets/Materials/Greybox/**.mat
    //   Assets/Scenes/Demo.unity    GameRoot + Canvas(HUD/Modal)+ 主相机 + 太阳 + 机舱灰盒 Cabin
    // ⚠ 已有的资产默认不覆盖:资产是唯一真源,你在 Inspector / Scene 视图里的改动不能被这个脚本冲掉。
    //   要推倒重来用菜单 ③(它会先删掉这几目录)。
    public static class DemoAssetBaker
    {
        public const string DataRoot = "Assets/Data";
        public const string DatabaseAsset = DataRoot + "/Database.asset";
        public const string ScenePath = "Assets/Scenes/Demo.unity";
        public const string MatRoot = "Assets/Materials/Greybox";
        // 用户指定的全项目字体(华文行楷),放在 Scenes 里是他丢进去的位置
        public const string FontPath = "Assets/Scenes/STXINGKA.TTF";

        const string BalanceAsset = DataRoot + "/BalanceConfig.asset";
        const string Items = DataRoot + "/Items";
        const string Tools = DataRoot + "/Tools";
        const string Recipes = DataRoot + "/Recipes";
        const string Activities = DataRoot + "/Activities";
        const string Teammates = DataRoot + "/Teammates";
        const string Endings = DataRoot + "/Endings";
        const string Lore = DataRoot + "/Lore";
        const string Events = DataRoot + "/Events";
        const string Choices = Events + "/Choices";

        static int created;
        static bool batch;      // 批处理入口里不能弹"保存当前场景吗"的对话框

        // ── 菜单 ─────────────────────────────────────────────────
        [MenuItem("Tools/60slike/① 生成数据资产(已有的不覆盖)", false, 1)]
        static void MenuData() { BakeData(); }

        [MenuItem("Tools/60slike/② 生成 Demo 场景 + 机舱灰盒(已有的不覆盖)", false, 2)]
        static void MenuScene() { BakeScene(); }

        [MenuItem("Tools/60slike/④ 只重建荒岛层(机舱与你的其它改动都不动)", false, 3)]
        static void MenuRebuildIsland()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("[60slike] 还没有 " + ScenePath + ",先跑菜单 ②。");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = UnityEngine.Object.FindObjectOfType<GameRoot>();
            if (root == null) { Debug.LogError("[60slike] 场景里没有 GameRoot。"); return; }
            var old = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            root.islandStage = BuildIsland(root.db);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[60slike] 荒岛层已重建(几何 / 落点 / 标牌都回到烘焙器的版本)。机舱与其余物体未动。");
        }

        [MenuItem("Tools/60slike/③ 强制重建全部资产(丢弃 Inspector / Scene 里的改动)", false, 4)]
        static void MenuForce()
        {
            if (!EditorUtility.DisplayDialog("强制重建",
                    "会删除 Assets/Data、Assets/Materials/Greybox 和 Assets/Scenes/Demo.unity 再重新生成。\n" +
                    "你在 Inspector 里改过的数值、在 Scene 视图里挪过的座椅都会丢。确定?", "删掉重建", "取消"))
                return;
            DeleteAssetFolder(DataRoot);
            DeleteAssetFolder(MatRoot);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            BakeData();
            BakeScene();
        }

        // 批处理入口:Unity.exe -batchmode -executeMethod SixtySLike.DemoAssetBaker.BakeAll
        public static void BakeAll()
        {
            batch = true;
            BakeData();
            BakeScene();
            Debug.Log("[60slike] 烘焙完成:新建 " + created + " 个资产 → " + ScenePath);
        }

        static void DeleteAssetFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            if (!AssetDatabase.DeleteAsset(folder))
                Debug.LogWarning("[60slike] 删不掉 " + folder + "(可能被占用)");
        }

        // ── 数据表 ───────────────────────────────────────────────
        // 数值全部来自开发文档 §5/§6/§2.3/§3/§4 与 v0.12/v0.13 + 补录①~⑧ 的定案。
        static Database BakeData()
        {
            created = 0;
            var db = GetOrCreate<Database>(DatabaseAsset, out _);
            db.bal = GetOrCreate<BalanceConfig>(BalanceAsset, out _);

            // ---- §5.1 拾荒池:物品 ----
            Can = Item("can", "罐头", 1, 5, 6, false, "通用货币:回 25% 饱食。队友、海鸥、猴子都在抢同一个数字。",
                o => { o.consumable = true; o.fullnessRestore = 25; });
            // v0.28:淡水 与 脏水 两件物品连同 喝水系统 一起删除 ⇒ 舱内投放少 3~4 格(28~33 → 24~29)
            Material = Item("material", "材料", 1, 0, 0, false,
                "万能素材:制作 / 修理 / 生火 共用同一份库存(补录⑧)。v0.19:开局不刷在舱内,唯一来源是 探索荒岛(30%×6)。");
            Herb = Item("herb", "药草", 1, 0, 0, false,
                "v0.16:探索荒岛 20% 一次判定采到。它只有一个用途 —— 自制药品 的那一味素材(材料1 + 药草1)。不进机舱点位池、不进猴子货单。",
                o => { o.tradeable = false; });
            Coconut = Item("coconut", "椰子", 1, 0, 0, false,
                "耗 1 体力打开:回满饱食 + 1 生命。v0.28:探索荒岛 不再产它,它唯一的来源是夜晚事件「椰树」(走过去拿,30% 被椰子砸 -1 生命)。",
                o => { o.consumable = true; o.fullnessRestore = 100; o.hpRestore = 1; });
            Chocolate = Item("chocolate", "巧克力棒", 1, 1, 1, false,
                "特殊食物:回 3 点体力,不填饱食。v0.19:全舱只有 1 根(白天唯一能加精力的东西,所以它是独一份的)。",
                o => { o.consumable = true; o.staminaRestore = 3; });
            Bait = Item("bait", "鱼饵", 1, 1, 2, false,
                "v0.21:挂不挂由开关决定(手上有鱼饵时左下角才有那个开关)。挂上 → 只提高上钩率:至少 80%(本来就更高则按实际),钓上鱼后 80% 消耗掉这一份。不加产出。也是喂海鸥的替代货币。",
                o => { o.consumable = true; });
            FlareGun = Item("flaregun", "信号枪", 1, 1, 1, true,
                "v0.19:它取代了原来散刷的 信号弹 —— 枪不会被消耗、也不会坏,进箱时自带 1 发 信号弹。没有它就打不出任何弹(土制信号弹 也要用它)。",
                o => { o.tradeable = true; });
            Flare = Item("flare", "信号弹", 1, 0, 0, false,
                "v0.19:它现在是 信号枪 的弹药,开局不再散刷(枪自带 1 发)。每发只能用一次;搜寻飞机线要两发。",
                o => { o.consumable = true; });
            MedKit = Item("medkit", "医疗箱", 2, 1, 1, true, "一次性:治好 生病/受伤 或 回 1 生命。",
                o => { o.consumable = true; o.curesSickness = true; o.hpRestore = 1; });

            // ---- §5.2 工具(修理价 = 补录⑦ 定案;钓竿/手电筒永不挂损坏,v0.12)----
            FishingRod = Tool("rod", "钓鱼竿", 2, 1, 1, true, 0, 0, true, 0f,
                "解锁钓鱼。v0.12 定案:永不损坏。v0.21:它也能做了(材料4 + 2 体力),简易钓钩 已整件删除。");
            DiveGear = Tool("dive", "潜水装置", 4, 1, 1, true, 4, 3, false, 0.2f, "占满一整趟。解锁潜水;修理 = 材料4 + 3 体力(补录⑦)。");
            SimpleMask = Tool("mask", "简易浮镜", 1, 0, 0, false, 2, 2, false, 0.2f, "无潜水装置时低效潜水,产出概率减半。");
            Spear = Tool("spear", "鱼叉", 2, 1, 1, true, 2, 2, false, 0.2f,
                "唯一的武器:低效潜水 / 血月下水 / 影怪硬挡(必坏)/ 赶猴子(完好回收 +1 罐头,自身照常掷损坏)。");
            // v0.48(用户):**打火石 全游戏只有一块**("C4:打火石只有一个")⇒ cabinMax 2 → 1。
            //        并且 **修理开始收材料**(B6:"打火石修复也要一材料")⇒ 修 = 材料1 + 1 体力,
            //        v0.5 定案① 那句"修打火石 只耗 1 体力、0 材料"由你本人收回(当时是为了不让夜晚线被锁死)。
            //        ⚠ 与 土制打火石 的关系照旧:手上有 打火石 时那条配方不进面板(`supersededBy = Flint`);
            //          它坏了之后 `storage.Has(Flint)` 变 false ⇒ 配方重新出现,所以你 **总能退回自制那条路**。
            Flint = Tool("flint", "打火石", 1, 1, 1, false, 1, 1, false, 0.2f,
                "点燃 篝火/信号火堆 的必需件,可反复用(拾荒带出的都是上位物品)。**v0.48:全舱只刷一块;修理 材料1 + 1 体力**。自制的那块叫 土制打火石,只能用一次。");
            Blanket = Tool("blanket", "毯子", 1, 1, 1, false, 1, 1, false, 0.2f,
                "低温夜免疫 / 血月夜裹住自己(队友精神不降层)。本次必损。不能遮武器a。");
            // v0.46(用户):"伞在拾荒阶段最多只刷一把" ⇒ cabinMax 2 → 1。
            //        这条与"一件东西只有两种状态(v0.45)"是配套的:伞 是 挡灾必损 件,两把一起坏没有意义,
            //        而它 有配方(材料2)可补 —— 兜底从"多带一把"换成"回来再做一把"。
            Umbrella = Tool("umbrella", "雨伞", 1, 1, 1, false, 1, 1, false, 0.2f, "两场答卷:雨(50/80/100%)与 武器a(唯一解,必损)。机舱里最多只刷一把(v0.46)。");
            Net = Tool("net", "渔网", 2, 1, 1, false, 2, 1, false, 0.3f,
                "夜晚鱼群下网 / 捞漂流瓶。v0.19:只刷 1 件、占 2 格(原 3 格 1~2 件)。制作不再需要机械师技能(v0.16)。");
            Flashlight = Tab<FlashlightSO>(Tools + "/flashlight.asset", o =>
            {
                Seed(o, "flashlight", "手摇式充电手电筒", 1, 1, 1, true,
                     "永不损坏,改为电量(存储 2 格):任何夜晚用法用掉 1 格,白天 1 精力充 1 格;开局随机带 0~2 格。");
                o.indestructible = true;
                o.repairStamina = 0; o.repairMaterials = 0;
            });

            Medicine = Item("medicine", "自制药品", 1, 0, 0, false, "材料1 + 药草1 制作(v0.16:原来那一味淡水换成了药草)。掷 70% 治好生病或回 1 生命,失败也消耗。",
                o =>
                {
                    o.consumable = true; o.curesSickness = true; o.hpRestore = 1;
                    o.medicineSuccessChance = 0.7f; o.tradeable = false;
                });
            CrudeFlare = Item("crudeflare", "土制信号弹", 1, 0, 0, false,
                "材料3 制作,每局限 1 发。发射只有 60% 生效;哑火在船那晚 = 直接算错过。",
                o => { o.consumable = true; o.tradeable = false; });
            CrudeFlint = Item("crudeflint", "土制打火石", 1, 0, 0, false,
                "v0.18:材料1 自制的一块火种,只能用一次 —— 生火时优先烧掉它,保住可反复用的 打火石。不进机舱点位池、不进猴子货单。",
                o => { o.consumable = true; o.tradeable = false; });
            Bottle = Item("bottle", "漂流瓶", 1, 1, 1, false,
                "夜晚鱼群/漂流瓶事件可捞,也可能拾荒捡到(v0.19:舱内只有 1 个)。白天丢出:1 瓶 → 每晚独立 20% 轮船;"
                + "**丢满 2 瓶才会再开一条独立 20% 的幽灵船(结局H)** —— 所以第二个瓶子必须靠夜晚的漂流瓶事件捞。",
                o => { o.tradeable = false; });

            // ---- §5.3 暗线道具:全部 1 格、不可制作、不给数值收益 ----
            NameTag = LoreItem("nametag", "铭牌", "涨潮露出的尸骨胸口上那块。刻着你亲手输入的名字。只用于图鉴与结局F判定,不是真结局前置。");
            Map = LoreItem("map", "藏宝图", "血月夜下水 60%×30% 才掉。v0.13 之后它只剩一个用途:探索刷出的那天 100% 带回藏宝箱。");
            Key = LoreItem("key", "宝藏钥匙", "钓鱼 5% / 潜水 15%,两条都不需要藏宝图(v0.13)。没有它,宝箱打不开,也不给任何结局。");
            Chest = LoreItem("chest", "藏宝箱", "2 格。持图那天探索荒岛必定捡回。与钥匙一同持有才能在影怪之夜献宝。", o => o.slots = 2);
            Scrap = LoreItem("scrap", "残图", "钓鱼/探索杂物。与藏宝图合成清晰版(纯图鉴)。");
            Newspaper = LoreItem("news", "剪报", "钓鱼/探索杂物。海事保险公司的旧闻。");
            Photo = LoreItem("photo", "合影", "钓鱼/探索杂物。三个穿制服的人站在一架飞机前。");

            // ---- v0.26 §5.3 彩蛋层拍板的 8 件:全部 1 格、不给数值、不做任何结局的前置 ----
            Luggage = LoreItem("luggage", "泡水的行李牌",
                "钓鱼杂物(1%、整局一次)。一个不认识的姓,和同一个保险公司的理赔编号。");
            Claim = LoreItem("claim", "三张编号连续的理赔单",
                "钓鱼杂物(1%、整局一次)。编号 04/05/06,06 那行的名字被水洇开了;04 与 05 就是合影里站在飞机前那两个人。");
            Badge = LoreItem("badge", "压弯的公司徽章",
                "影怪之夜的次日清晨掷 1%(整局一次;那一晚献宝直接进 T 就不掷)。正面是海事保险公司的标。");
            Boarding = LoreItem("boarding", "湿透的登机牌",
                "探索荒岛杂物(1%、整局一次)。日期就是起飞那天,座位号对应着你身边那把空吊床。");
            Tally = LoreItem("tally", "刻着「正」字的木片",
                "跨局彩蛋:只有 这个存档已经掉进过 结局F(SaveData.loopCount>0)时,才在开局第一个清晨掷 1%。每一笔是一次回环。");
            Raft = LoreItem("raft", "无人认领的救生筏残片",
                "任一涨潮之后的次日清晨掷 1%(整局一次)。有人试过离开,而且没有走远。");
            TwoNames = LoreItem("twonames", "两个被划掉的名字",
                "任一队友永久消失之后掷 1%(整局一次)。饥荒 / 自杀 / 小影怪 / 引开影怪 四条路都算。");
            MonkeyGift = LoreItem("monkeygift", "猴子的回礼",
                "友善的猴子交易成立之后的次日清晨掷 1%(整局一次)。它拿走一样、留下一样 —— 不像交易,像上供。");

            // ---- §6 制作表 ----
            var recipes = new List<RecipeSO>
            {
                // v0.48(用户:"篝火不用点,存在则那几条分支都直接走"):consumesFlint 回到 true ——
                //        含义不再是"造一件只值一晚的临时火",而是 **建造那一手就把它点着**(建好即烧两晚)。
                //        熄灭 = 这座灶进入「坏」(与 围墙 同一套模型),恢复走维修面板那行"重新点燃"。
                Recipe("campfire", "庇护所篝火", 3, 2, null, true, true,
                    "v0.48:垒一座灶 = 材料3 + 2 体力 + 一块火种,**建好当场点着**,烧两个晚上(点燃当天算第 1 晚)。之后 **不用每天点**:它没坏的时候,那几条夜晚分支(低温夜点灶 / 小影怪硬撑 / 赶猴子 / 影怪之夜 -3 变 -2)直接走。两晚烧完或被 涨潮 打湿 ⇒ 进入「坏」(与 围墙 同一套模型),在同一个维修面板里 **重新点燃(一块火种,0 材料 0 精力)**。它是 信号火堆 的前置。"),
                Recipe("flint", "土制打火石", 1, 1, CrudeFlint, false, false,
                    "v0.18:材料1 自制一块火种,只能用一次(生火时优先烧它,保住可反复用的 打火石)。手上有 打火石 时这一行不进面板(上位替代)。",
                    o => o.supersededBy = Flint),
                // v0.28 曾把 净水器 那道前置删掉,于是"制造无前置"成了没有例外的规则。
                // ⚠ v0.47(用户)重新开 **一个例外**:`信号火堆` 必须先有 `篝火` 才建得起来。
                Recipe("wall", "庇护所围墙", 5, 3, null, true, false,
                    "v0.16:围墙在的时候 涨潮 与 毒蛇 直接不进夜。v0.24:尸骨已独立成 骸骨 事件,与这场无关。v0.46(用户):**它也会坏** —— 每挡下一场就算一次使用,按 §2.3 标准累积概率掷(20% 起、每次 +10%、上限 90%);裂了白天修(材料3 + 1 体力)。另外 影怪之夜躲被子 QTE 判定窗口 +20%,这条同样随它裂掉而失效。"),
                Recipe("signalfire", "信号火堆", 6, 2, null, true, true,
                    "v0.48:它是 **篝火的上位平替** —— 必须先有 篝火 才建得起来(§6 目前唯一的一道前置),价 材料6 + 2 体力,并且 **建好当场点着**、同样只烧两个晚上。烧着的那一晚 轮船/幽灵船/搜寻飞机 **直接判定通过**(结局A / H);两晚过或被 涨潮 打湿 ⇒ 它进入「坏」,在维修面板里花一块火种重新点燃 —— **所以「材料6 换一次必中」是不成立的,要的是「那一晚它得真在烧」**。熄灭时它连 篝火 的作用也不再提供(它是平替,不是叠加)。",
                    o => o.requiresStructure = Database.Campfire),
                Recipe("rod", "钓鱼竿", 4, 2, FishingRod, false, false,
                    "v0.21:钓竿也能做了(材料4 + 2 体力,你定的价)。它永不损坏,所以这是一次买断;简易钓钩 已整件删除。手上已经有一根时这一行不出现。"),
                Recipe("mask", "简易浮镜", 3, 2, SimpleMask, false, false,
                    "无潜水装置时解锁潜水(仍 3 体力),产出概率减半。v0.16:仓库里有 潜水装置 时这一行不进制造面板(高级平替)。",
                    o => o.supersededBy = DiveGear),
                Recipe("medicine", "自制药品", 1, 1, Medicine, false, false,
                    "材料1 + 药草1(v0.16:只有药草才能做,淡水那一味换掉了)。掷 70%,失败也消耗。",
                    o => o.requiresExtraItem = Herb),
                Recipe("umbrella", "雨伞", 2, 1, Umbrella, false, false, "雨 与 武器a 两场答卷,武器a 那一晚它是全游戏唯一解。"),
                Recipe("net", "渔网", 4, 1, Net, false, false, "v0.16:不再需要机械师技能当天生效(制造无前置)。"),
                Recipe("bait", "鱼饵", 1, 1, Bait, false, false,
                    "v0.21:挂上鱼饵 → 只提高上钩率:至少 80%(本来就更高则按实际),钓上鱼后 80% 消耗掉这一份。不加产出。也是喂海鸥的替代货币。"),
                Recipe("crudeflare", "土制信号弹", 3, 2, CrudeFlare, false, false,
                    "每局限 1 发,发射只有 60% 生效。补录⑤:材料4 → 材料3。v0.16:它有上位替代(信号弹),但你点名它留在面板里,所以不挂 supersededBy。"
                    + "v0.19:没有 信号枪 时这一行根本不出现(做出来也打不出去,§11-51 结案)。",
                    o => { o.oncePerRun = true; o.requiresItemToShow = FlareGun; }),
            };

            // ---- §2.2 白天行动 ----
            var activities = new List<ActivitySO>
            {
                Act("explore", "探索荒岛", 0, "树林", true, true,
                    "当天必须已经刷新(起始 30%,未刷新 +5%/天,只有真的探索才回落)。吃掉当天全部剩余体力:材料 30%×6 / 罐头 60%×6 / 药草 20%×1。v0.28 起 这里不再产 椰子(它改成夜晚事件「椰树」);v0.19 起 材料 在舱内不刷,这里仍是它唯一的来源。持藏宝图时必定捡回藏宝箱。"),
                Act("fish", "钓鱼", 1, "礁石", false, false,
                    "空军率 min(80%, 20% + 3%×(天数-1))。成功 = 1 份罐头;5% 掉宝藏钥匙(不需要图)。v0.21:挂鱼饵改成开关 —— 挂上则上鱼概率至少 80%(更高按实际)、钓上鱼后 80% 消耗掉这一份;只加上钩率,不加产出。"),
                Act("dive", "潜水", 3, "浅滩", false, false,
                    "罐头 60%×6 + 鱼饵 40%×3;用鱼叉或简易浮镜时概率减半。15% 掉宝藏钥匙(不需要图)。小概率受伤。"),
                Act("coconut", "开椰子", 0, "树林", false, false, "回满饱食 + 1 生命。v0.39:0 精力。"),
                Act("repair", "修理", 0, "营地", false, false,
                    "打火石 0材料+1体力 / 毯子 1+1 / 鱼叉 2+2 / 潜水装置 4+3 / 有配方的 = 制造量的一半。机械师当天:体力 0 且材料 -1。"),
                Act("craft", "制作", 0, "营地", false, false, "见 §6 制作表。"),
                Act("charge", "手摇充电", 1, "营地", false, false, "v0.44:1 精力充 1 格电(存储上限 2 格,一次只能充一格;开局随机带 0~2 格)。"),
                // v0.46(用户):"都行,改成'聊天'吧" ⇒ §11-68 结案:对外一律叫 **聊天**(key 仍是 talk,不改代码标识符)。
                Act("talk", "聊天", 2, "营地", false, false,
                    "v0.21:聊天涨到 2 精力(原 1)。每天不限次;当天第一次回升 2 层精神,第二次起每次 1 层。只动精神 —— 发动技能(面板上那颗叫「帮助」)是另一条,仍 1 精力。"),
                Act("feed", "喂食", 0, "营地", false, false, "1 份罐头 = 0 精力、每天最多 1 次、直接回到饱食并重置计时。"),
                // v0.28:Act("drink","喝水") 已删除 —— 喝水系统整条移除(淡水/脏水/海水/集水器/净水器 一起走)
                Act("eat", "吃东西", 0, "营地", false, false, "1 份罐头 = 回 25% 饱食、0 精力;满饱食时吃 = 浪费。"),
                Act("heal", "用药", 0, "营地", false, false, "医疗箱 100% / 自制药品 70%(失败也消耗)。v0.39:0 精力。"),
                Act("throwbottle", "丢出漂流瓶", 1, "礁石", false, false, "消耗 1 个瓶子。丢 1 → 每晚独立 20% 轮船;丢 2 → 再加一条独立 20% 幽灵船(骸骨出现过之后那条抬到 30%)。无上限、无代价。"),
                Act("endday", "结束白天", 0, "营地", false, false, "进入夜晚结算。"),
            };

            Pilot = Mate("pilot", "飞行员", "续航型:当天体力上限 +1(3→4),不补当前已花掉的。v0.15 起这是纯上限,不再顺手回 1 点;v0.28 精力上限从 5 降到 3,所以他是三家里唯一还能把上限抬回 4 的那个。", new Color(0.4f, 0.7f, 1f));
            Navigator = Mate("navigator", "领航员", "产出型:当天钓鱼/潜水必不空军且产出 ×2;血月下水免掉次日体力上限 -1。", new Color(0.5f, 1f, 0.6f));
            Mechanic = Mate("mechanic", "机械师", "省税型:当天所有修理体力归零且每件材料 -1(最低 0)。补录⑦ 之后他是三家里被加强最多的一个。", new Color(1f, 0.75f, 0.35f));

            var endingTable = new List<EndingSO>();
            endingTable.Add(Ending(EndingId.T, "T · 赎回",
                new[] { "影怪低头看着箱子,像在看一份很久以前的借据。", Real }, new[] { Real, Claimed }));
            // ⚠ v0.48(用户让我"按合适的逻辑改"§11-74):§4.8 那句 **"只改最后一句,前面所有专属描写照旧"**
            //   与原来这三行不符 —— 它们把 `epilogueIfTrueClaimed` 写成 [通用句, 点睛句],**专属描写那一行丢了**,
            //   于是"打过 T 的存档"反而读不到 A/B/H 各自那句专属描写。这里按文档那句改回来:
            //   **claimed 版 = [这一格自己的专属描写, Claimed]**(最后一句被换掉,前面照旧)。
            endingTable.Add(Ending(EndingId.A, "A · 获救",
                new[] { "船靠了岸。有人给你披上毯子,问你叫什么名字。", Fake },
                        new[] { "船靠了岸。有人给你披上毯子,问你叫什么名字。", Claimed }));
            endingTable.Add(Ending(EndingId.B, "B · 五十天",
                new[] { "第五十天的日出照常升起。你已经不再数日子了。", Fake },
                        new[] { "第五十天的日出照常升起。你已经不再数日子了。", Claimed }));
            endingTable.Add(Ending(EndingId.H, "H · 幽灵船的乘客",
                new[] { "船上的人没有影子,但他们很客气。", Fake },
                        new[] { "船上的人没有影子,但他们很客气。", Claimed }));
            // v0.45(用户):四翼的文案整个换成这一句 —— 它不再接那句通用的"你活下来了…寻宝的旅程",
            //        因为这句自己就把"闭眼→回到空难那一瞬"说完了(与 回环F 呼应)。
            // ⚠ v0.47 #4 定案:**打过 T 的存档不加那一句** ⇒ I 的 claimed 版 **保持 [Fake, Claimed]**,
            //        也就是它 **不跟着上面的"专属描写照旧"规则走** —— 这是你对这两个结局刻意的不同,别去"统一"它。
            endingTable.Add(Ending(EndingId.I, "I · 四翼",
                new[] { "四只海鸥吊起了一副骸骨,你缓缓地闭上了眼,再一次睁眼时,你发现你回到了空难的那一瞬间" }, new[] { Fake, Claimed }));
            endingTable.Add(Ending(EndingId.C, "C · 死在岛上", new[] { "潮水一遍遍漫过沙滩,把痕迹抹平。" }, null));
            endingTable.Add(Ending(EndingId.F, "F · 回环", new[] { "第 N 次。你又站在了那架飞机的舱门口。" }, null));

            var loreDrops = new List<LoreDropSO>
            {
                // v0.26:彩蛋层统一定价 1%,并且整局最多一次(闸 = RunState.hidden.loreFound)。
                //        下面 4 条是原有的,概率从 5%/4% 一起压到 1%。
                LoreDrop(Scrap, "fish", 0.01f, "半张海图,边缘被水泡烂了。剩下的一半在谁手里?"),
                LoreDrop(Newspaper, "fish", 0.01f, "一张旧剪报:百慕大 again。保险公司否认与勘查委托有关。"),
                LoreDrop(Photo, "explore", 0.01f, "合影背面写着日期,比你的登机日早了三年。"),
                LoreDrop(Scrap, "explore", 0.01f, "又一片残图。拼图的人显然不止你一个。"),
                // v0.26 拍板的 8 件:fish/explore 是每次行动各掷;
                //                 shadow/tide/matelost/monkeytrade/loop 是一次性来源,触发(或次日清晨)只掷一次。
                LoreDrop(Luggage, "fish", 0.01f, "行李牌上的姓不是你的姓,可那个编号和剪报里的理赔号是同一串。"),
                LoreDrop(Claim, "fish", 0.01f, "编号 04、05、06。06 那行的名字被水洇开了 —— 你是第 6 个。"),
                LoreDrop(Badge, "shadow", 0.01f, "徽章背面刻着公司的誓词:我们不找回人,我们找回账。"),
                LoreDrop(Boarding, "explore", 0.01f, "日期就是起飞那天。座位号你认得 —— 那是你身边那把空吊床的位置。"),
                LoreDrop(Tally, "loop", 0.01f, "一块木片,刻着 正,整好五笔。每一笔是一次回环。"),
                LoreDrop(Raft, "tide", 0.01f, "一截救生筏残片,割开的口子朝外。有人试过离开,而且没有走远。"),
                LoreDrop(TwoNames, "matelost", 0.01f, "一张签到表,两个名字被划掉了。划的人笔迹很稳。"),
                LoreDrop(MonkeyGift, "monkeytrade", 0.01f, "营地边多了一份东西,不是你的。它拿走一样、留下一样 —— 不像交易,像上供。"),
            };

            var bag = BuildEvents(db);

            // ---- Database 只是引用容器:每次烘焙都重接一遍,值仍然在各资产里 ----
            db.scavengedItems = new List<ItemSO>
            {
                Can, Material, Coconut, Chocolate, Bait, Flare, FlareGun, MedKit,
                FishingRod, DiveGear, Spear, Flint, Blanket, Umbrella, Net, Flashlight, Bottle
            };
            db.recipes = recipes;
            db.activities = activities;
            db.teammates = new List<TeammateSO> { Pilot, Navigator, Mechanic };
            db.endingTable = endingTable;
            db.loreDrops = loreDrops;
            db.bagEvents = bag;

            // ---- 具名引用:运行时代码写的是 DB.Can / DB.Shadow 这种字段,
            //      这里只重新接线(引用),数值仍然只在各自的 .asset 里 ----
            db.Can = Can; db.Coconut = Coconut;
            db.Chocolate = Chocolate; db.Bait = Bait; db.MedKit = MedKit; db.Flare = Flare; db.Material = Material;
            db.FlareGun = FlareGun;
            db.FishingRod = FishingRod; db.DiveGear = DiveGear; db.SimpleMask = SimpleMask;
            db.Spear = Spear; db.Flint = Flint; db.Blanket = Blanket; db.Umbrella = Umbrella; db.Net = Net;
            db.Flashlight = Flashlight;
            db.Medicine = Medicine; db.CrudeFlare = CrudeFlare; db.Bottle = Bottle;
            db.Herb = Herb;
            db.CrudeFlint = CrudeFlint;
            db.NameTag = NameTag; db.Map = Map; db.Key = Key; db.Chest = Chest;
            db.Scrap = Scrap; db.Newspaper = Newspaper; db.Photo = Photo;
            db.Luggage = Luggage; db.Claim = Claim; db.Badge = Badge; db.Boarding = Boarding;
            db.Tally = Tally; db.Raft = Raft; db.TwoNames = TwoNames; db.MonkeyGift = MonkeyGift;
            db.Pilot = Pilot; db.Navigator = Navigator; db.Mechanic = Mechanic;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[60slike] 数据资产就绪:" + DatabaseAsset + "(本次新建 " + created + " 个)");
            return db;
        }

        // ---- §3.2 事件表 ----
        static List<GameEventSO> BuildEvents(Database db)
        {
            // 常规轮空池:14 常驻
            var Tide = Ev("tide", "涨潮", "海水漫进营地,退去之后沙滩上留下一片新的东西。v0.16:建了围墙就挡得住(不丢露天物资)。v0.24:尸骨已经独立成『骸骨』事件,与这场无关了。", 10, true);
            Ch(Tide, "high", "转移到高处", "无条件;露天的东西被海水卷走 1~2 件 + 队友精神 -1");
            Ch(Tide, "save", "抢救露天物资", "保住仓库里的露天物资,一样不丢;代价:次日体力上限 -2",
                c => c.nextDayStaminaPenalty = 2);

            var BloodMoon = Ev("bloodmoon", "血月", "整夜血红。唯一的负面是:他/她害怕得睡不着。", 4, true, 5);
            // ⚠ 不要在这里挂 nextDayStaminaPenalty:血月的代价是"次日体力上限 -1",
            //   由 BloodMoonDive 走 nextDayCapPenalty 结算(那里才判领航员免除),挂两遍会扣成 -2。
            Ch(BloodMoon, "blanket", "裹毯子硬熬", "队友精神不降层;毯子本次必损",
                c => { c.requiresItem = Blanket; c.breakChanceNight = 1f; });      // §2.3 挡灾型:裹过血月 = 必坏
            Ch(BloodMoon, "dive", "趁退潮下水", "掷 60% → 1~3 份罐头;成功里再掷 30% → 藏宝图。代价:次日体力上限 -1(领航员免)");
            Ch(BloodMoon, "nothing", "什么都不做", "队友精神降 1 层", c => c.alwaysAvailable = true);

            var SearchPlane = Ev("searchplane", "搜寻飞机", "远处有机影掠过。这是手电筒唯一的营救判定窗口之一。", 12, true);
            Ch(SearchPlane, "flare", "打出信号弹", "消耗 1 发(土制弹只有 60% 生效)→ 第一发;第二发等它下一次从轮空池里轮上来(v0.27)", c => c.consumesItem = true);
            Ch(SearchPlane, "firepile", "点亮信号火堆", "只在 信号火堆 灭着的那一晚给:当场点着它(花火种)+ 记下第一发。烧着的那两晚它自己就免检(v0.47)", c => c.requiresStructure = Database.SignalFire);
            Ch(SearchPlane, "flashlight", "手电筒照射", "50% → 结局A;用掉 1 格电", c => c.drainsFlashlight = true);
            Ch(SearchPlane, "ignore", "不理会", "无累积惩罚(v0.10:结局E 已删)", c => c.alwaysAvailable = true);

            var ColdNight = Ev("coldnight", "低温夜", "气温掉得很快,庇护所里开始结霜。", 9, true);
            // v0.47:这一条不再收 材料2 —— 它改成"点燃自己垒好的灶",只花火种,而且一烧两晚。
            //        ⚠ 闸也跟着换了:要有 灶(篝火 或 信号火堆)+ 有火种,否则这一条根本不生成 ⇒ 没垒过灶的局只能裹毯子或硬挨。
            Ch(ColdNight, "fire", "点燃自己的灶", "只花火种(0 材料 0 精力),烧两晚;需要先垒过 篝火 或 已建 信号火堆", c => c.requiresItem = Flint);
            Ch(ColdNight, "blanket", "裹毯子", "毯子本次必损",
                c => { c.requiresItem = Blanket; c.breakChanceNight = 1f; });      // §2.3 挡灾型:抗低温 = 必坏
            Ch(ColdNight, "硬挨", "硬挨一夜", "-1 生命 + 生病", c => { c.applySick = true; c.alwaysAvailable = true; });

            var Snake = Ev("snake", "毒蛇 / 蟑螂", "营地里有细小的爬行声。v0.16:建了围墙这一晚它根本进不来(事件不出现,改抽别的)。", 3, true);
            Ch(Snake, "swat", "点击驱赶", "小窗口:60% 成功");
            Ch(Snake, "flashlight", "手电筒照射驱赶", "不损坏;用掉 1 格电", c => c.drainsFlashlight = true);
            Ch(Snake, "不管它", "不管它", "-1 生命 或 损失 1 份罐头", c => c.alwaysAvailable = true);

            var FishSchool = Ev("fishschool", "鱼群", "浅海里压过来一大片银色,水面炸开。", 8, true);
            // v0.46(用户):"鱼群不能使用鱼竿,只有鱼叉与渔网" ⇒ 第二条从"鱼叉或钓竿徒手搞"收成 **纯鱼叉**,
            //        产出与 渔网 同为 3 份,两件都照常掷累积损坏。这一场从此没有"零损耗的备选"。
            Ch(FishSchool, "net", "下渔网", "2 体力 → 3 份罐头;渔网走累积概率(30% 起),会坏", c => c.requiresItem = Net);
            Ch(FishSchool, "hand", "用鱼叉下水叉鱼", "3 份罐头;鱼叉照常掷累积概率(20% 起)—— 会坏");
            Ch(FishSchool, "nothing", "啥也不做", "纯机会事件,无惩罚", c => c.alwaysAvailable = true);

            var Driftbottle = Ev("driftbottle", "漂流瓶", "一个玻璃瓶在礁石边上下翻滚,里面卷着一张纸。", 7, true);
            Ch(Driftbottle, "grab", "渔网捞 / 潜水装置取", "2 体力 → 得到 1 个漂流瓶");
            Ch(Driftbottle, "nothing", "空手够不到", "无惩罚,只是少一次求救投递机会", c => c.alwaysAvailable = true);

            var Gull = Ev("gull", "海鸥", "一只海鸥落在庇护所顶上,歪着头看你吃东西。", 6, true);
            Ch(Gull, "feed", "给它 1 份罐头或鱼饵", "全游戏只需喂 1 次;之后来的直接落", c => c.requiresItem = Can);
            Ch(Gull, "ignore", "不理它", "它飞走,下轮还会来", c => c.alwaysAvailable = true);

            var SmallShadow = Ev("smallshadow", "小影怪", "比影怪小一号的黑影,只有一点眼光,绕着营地转。", 5, true);
            Ch(SmallShadow, "watch", "守夜", "队友 100% 存活;次日体力 -2", c => c.nextDayStaminaPenalty = 2);
            Ch(SmallShadow, "fire", "点着火硬撑", "队友 70% 存活;火本次必熄", c => c.requiresFire = true);
            Ch(SmallShadow, "nothing", "啥也不做 / 躲起来", "队友被杀,永久消失(唯一硬杀队友的事件)", c => c.alwaysAvailable = true);
            Ch(SmallShadow, "sleepalone", "直接睡觉", "没有队友:次日体力 -1,不丢罐头不坏工具", c => c.requiresNoTeammate = true);

            var MonkeyNaughty = Ev("monkeynaughty", "调皮的猴子", "一只猴子从树林窜进来,抓起仓库里随机 1 件东西就跑。", 8, true);
            Ch(MonkeyNaughty, "spear", "用鱼叉驱赶", "被抢那件完好回来,+1 份罐头;鱼叉自己按正常概率掷损坏",
                c => { c.requiresItem = Spear; c.breakChanceNight = 0f; c.rewardFood = 1; });
            Ch(MonkeyNaughty, "flare", "用信号枪打一发驱赶", "被抢那件完好回来;枪不坏也不消耗,但弹药照旧少 1 发",
                c => { c.requiresItem = FlareGun; c.breakChanceNight = 0f; c.consumesItem = true; });
            Ch(MonkeyNaughty, "fire", "用火堆驱赶", "能赶,但抢回来的那件是破损状态(打火石不坏)",
                c => { c.requiresFire = true; c.breakChanceNight = 0f; c.recoversDamaged = true; });
            // v0.27:空手抢夺 不再挂 alwaysAvailable —— 它绑在夜晚那只猴子身上(NightResolver.BoundTarget → "monkey"),
            //        只有点它才走到这一条。于是"尝试睡去(什么都不做)"落到下面那条 不给它 上,与 §3.2 的口径一致。
            //        ⚠ 改之前 sleep = 空手抢夺(-1 生命 + 破损回收),那是这条 alwaysAvailable 造成的,不是设计。
            Ch(MonkeyNaughty, "grab", "空手抢夺", "-1 生命,而且抢回来的是破损状态(点那只猴子才做得到)",
                c => c.recoversDamaged = true);
            Ch(MonkeyNaughty, "refuse", "不给它", "那件物品被它带走,永久丢失(= 什么都不做 / 尝试睡去)", c => c.alwaysAvailable = true);

            var MonkeyFriendly = Ev("monkeyfriendly", "友善的猴子", "两只猴子坐成一排,把它们想换的推给你。", 6, true);
            Ch(MonkeyFriendly, "trade", "交易", "给它 1 件你当前持有的拾荒物,换 1 件你整局从未持有过的", c => c.alwaysAvailable = true);
            Ch(MonkeyFriendly, "decline", "不换", "它自己走掉,无任何负面", c => c.alwaysAvailable = true);

            // v0.28 新增:椰树 —— 椰子的唯一来源(探索荒岛不再产它)。零惩罚的机会事件,但"去看"自带 30% 被砸。
            //        "走过去看看" 不绑任何物品/物体 ⇒ 它由 NightResolver.ChoicesWithoutObject() 兜底,排在睡去按钮上方。
            var PalmTree = Ev("palmtree", "椰树", "海风吹得椰树摇晃。", 5, true);
            Ch(PalmTree, "take", "走过去看看", "获得 1 个椰子;30% 概率被椰子砸 -1 生命");
            Ch(PalmTree, "ignore", "不去看", "没收获,也没惩罚(= 什么都不做 / 尝试睡去)", c => c.alwaysAvailable = true);

            var Rain = Ev("rain", "下雨", "云压得很低。同一事件内部按 3:2:1 掷档。", 7, true);
            var rainVariants = new List<GameEventSO>
            {
                Ev("rain_light", "小雨", "屋檐开始滴水。", 0, false),
                Ev("rain_heavy", "暴雨", "画面整片水纹,雨点糊屏。", 0, false),
                Ev("rain_storm", "雷雨", "一次白闪,延迟的雷声。", 0, false),
            };
            for (int i = 0; i < rainVariants.Count; i++)
            {
                var v = rainVariants[i];
                float bc = i == 0 ? 0.5f : (i == 1 ? 0.8f : 1.0f);
                Ch(v, "umbrella", "撑雨伞", "伞损坏 " + Mathf.RoundToInt(bc * 100) + "%" +
                    (i == 2 ? ";雷雨:伞必坏且次日体力 -1" : "") + " → 免于生病",
                    c => { c.requiresItem = Umbrella; c.breakChanceNight = bc; if (i == 2) c.nextDayStaminaPenalty = 1; });
                Ch(v, "nothing", "啥也不做(淋一夜)", "生病 + 次日体力 -2",
                    c => { c.applySick = true; c.nextDayStaminaPenalty = 2; c.alwaysAvailable = true; });
            }
            AddVariants(Rain, rainVariants, new[] { 3, 2, 1 });   // Rain 已有变体就不再写,保留 Inspector 里的权重改动

            var WeaponA = Ev("weapona", "武器a", "月亮亮得不正常,光直直地照进庇护所,躺下就头疼。(标题就是这三个字符,不解释)", 6, true);
            Ch(WeaponA, "umbrella", "用雨伞遮挡", "全游戏唯一解:毯子不能遮。本次必损",
                c => { c.requiresItem = Umbrella; c.breakChanceNight = 1f; });
            Ch(WeaponA, "硬挨", "硬挨", "次日体力 -2(不提供第三选项)",
                c => { c.nextDayStaminaPenalty = 2; c.alwaysAvailable = true; });

            var FakeMate = Ev("fakemate", "假队友", "一个看不清的人影站在营地边,说他不舒服。v0.45:营地里没有队友 ⇒ 这一晚它根本不会出现。", 5, true);
            Ch(FakeMate, "flashlight", "用手电筒照他", "可辨真伪:60% 为真 → 队友必定进入生病。用掉 1 格电",
                c => { c.drainsFlashlight = true; c.breakChanceNight = 0f; });
            Ch(FakeMate, "nothing", "不照", "真伪其实早已决定,手电筒只是告诉你答案", c => c.alwaysAvailable = true);

            // ---- 不入轮:独立掷 / 强制插播 ----
            // v0.24:骸骨 —— 你要求的"不要和普通的涨潮放一起"。整局一次,每晚掷一次直到它出现;
            //        走过去看就必定取走铭牌(没有第二层分支),不掩埋,天亮它自己消失。
            //        它出现过之后,幽灵船那条独立 20% 抬到 30%(仍然必须先丢过 2 个漂流瓶)。
            // v0.26:概率从 20% 压到 1% —— 它现在和 剪报/合影/残图 一样是"彩蛋层"的一员,同一套定价。
            var Bones = Ev("bones", "骸骨", "今晚你看见潮水似乎将什么东西卷上了沙滩。", 0, false,
                0, o => { o.nightlyIndependentRoll = 0.01f; o.oncePerRun = true; });
            Ch(Bones, "look", "走过去看", "必定取走铭牌(整局只有这一晚的机会;天亮海水就把它带走)");
            Ch(Bones, "ignore", "不理会", "你没有靠近 —— 铭牌就此错过(它只影响图鉴与结局F判定)",
                c => c.alwaysAvailable = true);
            // ⚠ "整局一次 / 独立掷"必须写在 Ev 的 fill 里:fill 只在资产首次创建时跑,
            //   放在外面就等于每次烘焙都把用户在 Inspector 里改的值按回原样。
            var Ship = Ev("ship", "路过的轮船", "海平线上有灯光。由漂流瓶引来:单发即结局A。", 0, false,
                0, o => o.nightlyIndependentRoll = 0.2f);
            Ch(Ship, "flare", "打出信号弹", "单阶段:1 发即结局A(土制弹 60% 生效,哑火 = 错过)", c => c.consumesItem = true);
            Ch(Ship, "flashlight", "手电筒照射", "50% → 结局A;落空 = 错过,这条船永久不再来", c => c.drainsFlashlight = true);
            Ch(Ship, "sail", "升起临时信号帆", "飞行员技能生效当天才有:这条线唯一的免费出口");
            Ch(Ship, "ignore", "什么都不做", "⚠ 错过即永久:shipResolved 置位,海面上再也没有船了", c => c.alwaysAvailable = true);

            var Ghost = Ev("ghostship", "幽灵船", "整条海平线暗下去。一艘没有灯、也没有人的船靠上礁石,甲板上摆着整齐的缆绳。", 0, false,
                0, o => { o.nightlyIndependentRoll = 0.2f; o.oncePerRun = true; });
            Ch(Ghost, "flare", "打出信号弹", "→ 结局H·幽灵船的乘客(土制弹 60%,哑火 = 错过)", c => c.consumesItem = true);
            Ch(Ghost, "flashlight", "手电筒照射", "50% → 结局H;未命中 → 它天亮自己走,且永久不再来", c => c.drainsFlashlight = true);
            Ch(Ghost, "ignore", "不做任何事", "它自己退走。⚠ 错过即永久:H 在这一局彻底关闭", c => c.alwaysAvailable = true);

            var Shadow = Ev("shadow", "影怪", "庇护所外出现黑影,眼睛发光,逐渐逼近。整局最重的一场演出(第 20~25 天必出 1 次)。", 0, false,
                0, o => o.oncePerRun = true);
            Ch(Shadow, "offer", "把藏宝箱推给它", "需要 箱 + 钥匙 都在手,且这个存档没打过 T → 真结局 T");
            Ch(Shadow, "spear", "鱼叉硬挡", "玩家免伤,鱼叉本次必坏",
                c => { c.requiresItem = Spear; c.breakChanceNight = 1f; });     // §2.3 挡灾型:硬挡影怪 = 必坏(v0.12:唯一的会坏硬挡件)
            Ch(Shadow, "flashlight", "手电筒硬挡", "玩家免伤;用掉 1 格电(不触发营救判定)", c => c.drainsFlashlight = true);
            Ch(Shadow, "hide", "躲进被子里屏息", "QTE 单次:成功 = 伤害减半并向上取整");
            Ch(Shadow, "lure", "引开影怪", "有队友时可选:该队友永久消失,玩家免伤", c => c.requiresTeammate = true);
            Ch(Shadow, "nothing", "什么都不做", "篝火在烧 -2 生命;没有火 -3 生命", c => c.alwaysAvailable = true);

            var TwoLights = Ev("twolights", "两点光", "影怪过后的夜晚,庇护所外出现两点光。(补救窗口,§11-9)", 0, false);
            Ch(TwoLights, "offer", "把箱子推过去", "需要 箱 + 钥匙 → 真结局 T");
            Ch(TwoLights, "ignore", "不理它", "它熄灭。这一局不会再有第三次", c => c.alwaysAvailable = true);

            // ⚠ v0.27:这里原来是 Planesecond 那个"2~4 夜后插播"的专属事件,整个删除。
            //        两阶段线仍然要两发,但第二发等的就是 搜寻飞机 下一次从常规轮空池里轮上来
            //        (它本来就在池里,优先级 12),所以不需要第二个事件、也不需要计时字段。

            // ---- 事件引用接到 Database(值在各 .asset 里) ----
            db.Tide = Tide; db.BloodMoon = BloodMoon; db.SearchPlane = SearchPlane; db.ColdNight = ColdNight;
            db.Snake = Snake; db.FishSchool = FishSchool; db.Driftbottle = Driftbottle; db.Gull = Gull;
            db.SmallShadow = SmallShadow; db.MonkeyNaughty = MonkeyNaughty; db.MonkeyFriendly = MonkeyFriendly;
            db.Rain = Rain; db.WeaponA = WeaponA; db.FakeMate = FakeMate;
            db.Ship = Ship; db.Ghost = Ghost; db.Shadow = Shadow; db.TwoLights = TwoLights;
            db.Bones = Bones;
            db.PalmTree = PalmTree;     // v0.28:椰树 —— 椰子的唯一来源

            return new List<GameEventSO>
            {
                Tide, BloodMoon, SearchPlane, ColdNight, Snake, FishSchool, Driftbottle,
                Gull, SmallShadow, MonkeyNaughty, MonkeyFriendly, Rain, WeaponA, FakeMate, PalmTree
            };
        }

        // ── 场景 + 机舱灰盒 ───────────────────────────────────────
        // 这些常数就是原来 Play 时代码生成那架飞机的那一份尺寸,一个都没改:
        // 改的是"谁持有它们"—— 现在它们落在 Assets/Scenes/Demo.unity 的 GameObject 上,
        // 颜色落在 Assets/Materials/Greybox/*.mat 上,你在 Scene 视图里的挪动就是新的真源。
        const float HalfW = 2.9f;           // 侧壁中心 |z|
        const float CeilBottom = 2.59f;     // 天花板板底面 = 2.68 - 0.18/2,分区牌贴这块面
        static readonly string[] ZoneNames = { "驾驶舱", "前客舱", "舱门区", "后客舱", "货舱" };
        static readonly float[] SeatXs = { -5.6f, -4.4f, -3.2f, -2.0f, 2.0f, 3.2f, 4.4f, 5.6f };
        static readonly float[] SeatZs = { -2.5f, -2.0f, -1.5f, 1.5f, 2.0f, 2.5f };

        static void BakeScene()
        {
            var db = AssetDatabase.LoadAssetAtPath<Database>(DatabaseAsset);
            if (db == null) db = BakeData();
            if (db.bal == null)
            {
                // 出舱圈的半径要从 BalanceConfig 资产里读,没有它就摆不出那个圆
                Debug.LogError("[60slike] " + DatabaseAsset + " 里 bal 是空的,先跑菜单 ① 生成数据资产。");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                // 场景是唯一真源:已经存在就绝不重烘,只把它打开给你继续编辑。
                // 但"缺哪一层补哪一层":后来加的荒岛层用 ExtendExistingScene 补进去,不让你推倒重来。
                if (!batch)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    ExtendExistingScene();
                }
                Debug.Log("[60slike] " + ScenePath + " 已存在,只补缺的层、不覆盖已有内容。要推倒重来用菜单 ③。");
                return;
            }
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = BuildCamera();
            var sun = BuildSun();
            RectTransform hud, modal;
            BuildCanvas(out hud, out modal);
            var cabin = BuildCabin(db);
            var stage = BuildIsland(db);
            BuildGameRoot(db, cam, hud, modal, cabin, stage, sun);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[60slike] 存不了 " + ScenePath + "(路径被占用?)");
                return;
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[60slike] 场景就绪:" + ScenePath + " —— 机舱灰盒、材质、HUD/Modal 全在里面,可直接编辑。累计新建 " + created + " 个资产");
        }

        // 已有场景里补建缺失的层。只动"没有的东西",已有的一律不碰(你在 Inspector / Scene 视图里改过的都保留)。
        static void ExtendExistingScene()
        {
            var root = UnityEngine.Object.FindObjectOfType<GameRoot>();
            if (root == null)
            {
                Debug.LogError("[60slike] " + ScenePath + " 里没有 GameRoot,补不了层。要整份重来用菜单 ③。");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            bool changed = false;

            if (root.islandStage == null) root.islandStage = UnityEngine.Object.FindObjectOfType<IslandStage>();
            if (root.islandStage == null)
            {
                root.islandStage = BuildIsland(root.db);
                Debug.Log("[60slike] 补建了荒岛层 Island(天空/大海/沙滩/植被/篝火 + 18 个道具落点)。");
                changed = true;
            }
            if (root.cabin == null)
            {
                root.cabin = UnityEngine.Object.FindObjectOfType<CabinAnchors>();
                changed = root.cabin != null;
            }
            if (root.cam == null) root.cam = UnityEngine.Camera.main;
            if (root.sun == null) { root.sun = UnityEngine.Object.FindObjectOfType<Light>(); changed = root.sun != null || changed; }
            if (root.uiFont == null) { root.uiFont = ProjectFont(); changed = root.uiFont != null || changed; }
            if (root.islandStage != null && (root.islandStage.seaAnchor == null || root.islandStage.propSlots.Count < 24))
                Debug.Log("[60slike] 场景里的荒岛层是旧版(缺 SeaAnchor 或落点不足 24 个)。" +
                          "夜晚点不到 大海、仓库东西多了会重叠 → 请跑菜单 ④ 只重建荒岛层。");
            if (changed)
            {
                EditorUtility.SetDirty(root);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) Debug.LogError("[60slike] 存不了 " + ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[60slike] " + ScenePath + " 已补层并保存(累计本次新建 " + created + " 个资产)。");
            }
        }

        static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";            // FaceCamera 与夜晚的镜头都走 Camera.main 找它
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            cam.fieldOfView = 65f;
            go.AddComponent<AudioListener>();
            return cam;
        }

        static Light BuildSun()
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.0f;
            l.color = new Color(1f, 0.96f, 0.9f);
            l.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            return l;
        }

        static void BuildCanvas(out RectTransform hud, out RectTransform modal)
        {
            var go = new GameObject("DemoCanvas");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // 两层容器留在场景里,里面的内容每个阶段自己重建
            hud = Ui.Panel(go.transform, "HUD", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modal = Ui.Panel(go.transform, "Modal", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        static void BuildGameRoot(Database db, Camera cam, RectTransform hud, RectTransform modal,
                                  CabinAnchors cabin, IslandStage islandStage, Light sun)
        {
            var r = new GameObject("GameRoot").AddComponent<GameRoot>();
            r.db = db; r.cam = cam; r.hud = hud; r.modal = modal; r.cabin = cabin;
            r.islandStage = islandStage; r.sun = sun; r.uiFont = ProjectFont();
            EditorUtility.SetDirty(r);
        }

        static Font ProjectFont()
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (f == null) Debug.LogError("[60slike] 找不到 " + FontPath + " —— UI 会退回系统字体。把 TTF 放回原位再跑一次本菜单。");
            return f;
        }

        static CabinAnchors BuildCabin(Database db)
        {
            var carpet = GreyboxMat("Carpet", new Color(0.22f, 0.23f, 0.27f));
            var aisleCol = GreyboxMat("AisleRunner", new Color(0.14f, 0.17f, 0.24f));
            var panel = GreyboxMat("Panel", new Color(0.78f, 0.76f, 0.70f));
            var ceilCol = GreyboxMat("Ceiling", new Color(0.85f, 0.84f, 0.80f));
            var bin = GreyboxMat("Bin", new Color(0.70f, 0.68f, 0.62f));
            var seat = GreyboxMat("Seat", new Color(0.19f, 0.26f, 0.42f));
            var glass = GreyboxMat("Window", new Color(0.55f, 0.75f, 0.95f));
            var glow = GreyboxMat("CoveLight", new Color(0.95f, 0.95f, 0.85f));
            var metal = GreyboxMat("Metal", new Color(0.42f, 0.44f, 0.48f));
            var dark = GreyboxMat("Dark", new Color(0.13f, 0.14f, 0.17f));
            var doorC = GreyboxMat("Door", new Color(0.9f, 0.75f, 0.3f));
            var boxC = GreyboxMat("StorageBox", new Color(0.35f, 0.6f, 0.4f));
            var ringC = GreyboxMat("ExitZoneRing", new Color(0.9f, 0.85f, 0.3f, 0.35f));

            var T = new GameObject("Cabin").transform;
            var seatPans = new List<Renderer>();
            var signs = new List<CabinSign>();

            // ── 壳体:单通道宽体机的截面(斜肩 → 八边形,这是"机身"和"房间"最大的区别)──
            var shell = Group(T, "Shell");
            Hull(shell, "Floor", new Vector3(0, -0.05f, 0), new Vector3(21f, 0.1f, 5.9f), carpet, true);
            Hull(shell, "AisleRunner", new Vector3(0, 0.006f, 0), new Vector3(20.6f, 0.01f, 2.5f), aisleCol);
            Hull(shell, "Ceil", new Vector3(0, CeilBottom + 0.09f, 0), new Vector3(20.8f, 0.18f, 2.8f), ceilCol);
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "Port" : "Stbd";
                Hull(shell, "Wall" + tag, new Vector3(0, 0.95f, s * HalfW), new Vector3(21.2f, 1.9f, 0.2f), panel, true);
                // 斜肩内表面下沿必须正好落在侧壁顶内缘 (y1.90, |z|2.80):2.454/1.846 是按 64° 反推的。
                // 摆低了这块斜板会横插进过道,把门板上半截和门牌一起盖掉(自检的"埋进"断言抓到过它)。
                Hull(shell, "Shoulder" + tag, new Vector3(0, 2.454f, s * 1.846f), new Vector3(21.2f, 2.2f, 0.16f), panel,
                     false, new Vector3(s < 0 ? 64f : -64f, 0f, 0f));
                Hull(shell, "CoveLight" + tag, new Vector3(0, 2.5f, s * 0.62f), new Vector3(19.5f, 0.05f, 0.2f), glow);
            }
            Hull(shell, "NoseWall", new Vector3(-10.1f, 1.2f, 0), new Vector3(0.25f, 2.4f, 5.8f), panel, true);
            Hull(shell, "TailWall", new Vector3(10.1f, 1.2f, 0), new Vector3(0.25f, 2.4f, 5.8f), panel, true);

            // ── 舷窗 ──
            var win = Group(T, "Windows");
            for (int s = -1; s <= 1; s += 2)
            {
                int i = 0;
                for (float x = -9.2f; x <= 9.2f; x += 1.35f, i++)
                    Hull(win, "Window" + (s < 0 ? "P" : "S") + i, new Vector3(x, 1.32f, s * 2.76f),
                         new Vector3(0.32f, 0.44f, 0.06f), glass);
            }

            // ── 行李架:在每个舱门/厨房站位前断开,和真机一样,所以只有这两段 ──
            var bins = Group(T, "Bins");
            foreach (var seg in new[] { -3.9f, 3.9f })
                for (int s = -1; s <= 1; s += 2)
                    Hull(bins, "Bin" + (s < 0 ? "P" : "S") + seg, new Vector3(seg, 1.6f, s * 2.05f),
                         new Vector3(5.2f, 0.4f, 0.9f), bin);

            // ── 座椅排:只留中央过道。靠过道的两面椅面交给 CabinAnchors.seatPans ──
            var seats = Group(T, "Seats");
            foreach (var x in SeatXs)
                foreach (var z in SeatZs)
                {
                    var cushion = Hull(seats, "SeatCushion_" + x + "_" + z, new Vector3(x, 0.42f, z),
                                       new Vector3(0.5f, 0.12f, 0.48f), seat, true);
                    Hull(seats, "SeatBack_" + x + "_" + z, new Vector3(x + 0.24f, 0.72f, z),
                         new Vector3(0.12f, 0.6f, 0.48f), seat, true);
                    if (Mathf.Abs(z) == 1.5f) seatPans.Add(cushion.GetComponent<Renderer>());
                }

            var service = Group(T, "Service");
            Hull(service, "Galley", new Vector3(-0.9f, 0.45f, 2.35f), new Vector3(1.6f, 0.9f, 0.7f), bin, true);
            Hull(service, "CoffeeMaker", new Vector3(-0.55f, 1.05f, 2.35f), new Vector3(0.4f, 0.3f, 0.4f), dark);
            Hull(service, "Lavatory", new Vector3(0.95f, 1.0f, 2.4f), new Vector3(0.9f, 2.0f, 0.7f), bin, true);

            // 隔舱壁:只留中央过道宽 2.4 的开口,驾驶舱和货舱因此是"走进去"的而不是"穿过去"的
            var deck = Group(T, "FlightDeck");
            Bulkhead(T, -6.5f, panel);
            Hull(deck, "InstrumentPanel", new Vector3(-9.65f, 1.2f, 0), new Vector3(0.6f, 0.8f, 3.0f), dark, true);
            Hull(deck, "Glareshield", new Vector3(-9.4f, 1.68f, 0), new Vector3(0.35f, 0.2f, 3.0f), metal);
            for (int i = 0; i < 4; i++)
                Hull(deck, "Windshield" + i, new Vector3(-9.9f, 2.05f, -1.05f + i * 0.7f),
                     new Vector3(0.1f, 0.5f, 0.6f), glass);
            foreach (var z in new[] { -1.5f, 1.5f })
            {
                Hull(deck, "PilotSeat", new Vector3(-8.6f, 0.5f, z), new Vector3(0.55f, 0.12f, 0.55f), seat, true);
                Hull(deck, "PilotSeatBack", new Vector3(-8.35f, 0.9f, z), new Vector3(0.12f, 0.7f, 0.55f), seat, true);
            }

            var cargo = Group(T, "CargoDeck");
            Bulkhead(T, 6.5f, panel);
            foreach (var z in new[] { -1.85f, 1.85f })
                Hull(cargo, "Container", new Vector3(7.8f, 0.55f, z), new Vector3(1.5f, 1.1f, 1.0f), metal, true);
            Hull(cargo, "ContainerAft", new Vector3(9.1f, 0.55f, -2.0f), new Vector3(1.3f, 1.1f, 1.0f), metal, true);

            // ── 门与储物箱:左侧(port)L1 塞式舱门,真机约 1.1 宽 × 1.9 高 ──
            var door = Group(T, "Door");
            var frame = Hull(door, "DoorFrame", new Vector3(0, 0.98f, -2.82f), new Vector3(1.15f, 1.95f, 0.14f), doorC);
            Hull(door, "DoorWindow", new Vector3(0, 1.42f, -2.74f), new Vector3(0.36f, 0.36f, 0.06f), glass);
            var box = Hull(door, "Box", new Vector3(1.5f, 0.45f, -2.15f), new Vector3(1.2f, 0.9f, 1.1f), boxC);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ExitZone";
            ring.transform.SetParent(door, false);
            ring.transform.position = new Vector3(0, 0.02f, -2.6f);
            ring.transform.localScale = new Vector3(db.bal.exitZoneRadius * 2f, 0.01f, db.bal.exitZoneRadius * 2f);
            ring.GetComponent<Renderer>().sharedMaterial = ringC;
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());

            // ── 玩法要读的两个位置 ──
            var anchors = Group(T, "Anchors");
            var doorAnchor = new GameObject("DoorAnchor");     // 出舱判定圆心(舱门内侧地面点)
            doorAnchor.transform.SetParent(anchors, false);
            doorAnchor.transform.position = new Vector3(0, 0, -2.6f);
            var playerSpawn = new GameObject("PlayerSpawn");   // 开局站立点(脚下)
            playerSpawn.transform.SetParent(anchors, false);
            playerSpawn.transform.position = new Vector3(0, 0.05f, -1.2f);

            // ── 标牌:文字/挂点/朝向都是数据,运行时由 CabinAnchors 印出来 ──
            // 分区牌印在天花板底面:过道里没有任何东西挡它,而且抬头看头顶面板本来就是机上读区域的方式。
            // 侧壁那一条被行李架吃掉了,不能贴。
            for (int z = 0; z < ScavengingPhase.Zones.Length; z++)
            {
                float cx = (ScavengingPhase.Zones[z][0] + ScavengingPhase.Zones[z][1]) * 0.5f;
                signs.Add(new CabinSign
                {
                    text = ZoneNames[z],
                    host = T,
                    localPos = new Vector3(cx, CeilBottom - World.LabelGap, 0f),
                    facingLocal = Vector3.down,
                    color = new Color(0.2f, 0.24f, 0.3f),
                });
            }
            // EXIT 牌印在门板上(门心 + 半厚,再抬一丝防 z-fighting),
            // y=1.75 在舷窗(1.24~1.60)上方、门顶(1.955)下方 —— 真机的舱门指示牌就在这个位置
            signs.Add(new CabinSign
            {
                text = "舱门 / 出舱口",
                host = frame.transform,
                localPos = new Vector3(0f, 1.75f - 0.98f, 0.082f),
                facingLocal = Vector3.forward,
                color = new Color(0.85f, 0.6f, 0.15f),
            });
            // 箱名贴朝过道的那一面(x=0.9 面):站在出舱圈里正好是迎面,不用低头找飘着的字
            signs.Add(new CabinSign
            {
                text = "储物箱(无限)",
                host = box.transform,
                localPos = new Vector3(0.888f - 1.5f, 0.5f - 0.45f, 0f),
                facingLocal = Vector3.left,
                color = new Color(0.85f, 1f, 0.9f),
            });

            var a = T.gameObject.AddComponent<CabinAnchors>();
            a.doorAnchor = doorAnchor.transform;
            a.playerSpawn = playerSpawn.transform;
            a.seatPans = seatPans;
            a.signs = signs;
            EditorUtility.SetDirty(a);
            return a;
        }

        // ── 荒岛那一层:不是文字冒险,是一张能看见的岛 ─────────────────
        // 天空 / 大海 / 沙滩 / 一些海边植被 / 篝火 是几何体 + 印在面上的字;
        // 仓库里的每一件在运行时摆到 propSlots 上,点它就是与它有关的白天行动。
        static IslandStage BuildIsland(Database db)
        {
            var sky = GreyboxMat("IslandSky", new Color(0.55f, 0.78f, 0.95f), true);   // 背景板必须不受光
            var sea = GreyboxMat("IslandSea", new Color(0.10f, 0.32f, 0.52f));
            var sand = GreyboxMat("IslandSand", new Color(0.87f, 0.80f, 0.62f));
            var trunk = GreyboxMat("IslandTrunk", new Color(0.42f, 0.30f, 0.18f));
            var leaf = GreyboxMat("IslandLeaf", new Color(0.20f, 0.46f, 0.24f));
            var stone = GreyboxMat("IslandStone", new Color(0.44f, 0.45f, 0.47f));
            var log = GreyboxMat("IslandLog", new Color(0.35f, 0.24f, 0.14f));

            var T = new GameObject("Island").transform;
            var signs = new List<CabinSign>();
            var slots = new List<Transform>();

            var camPose = new GameObject("CameraPose");
            camPose.transform.SetParent(T, false);
            camPose.transform.position = new Vector3(0f, 5.6f, 13.5f);
            // Unity 是左手系:Euler 的 X 为正 = 往下俯视(拾荒阶段的 HandleLook 也是这个约定,
            // 鼠标上移 pitch 变小 = 抬头)。之前写 -26 结果是"抬头朝天",整屏一个 Renderer 都不在视锥里。
            // 相机站在沙滩外侧的 +z,要看的是 -z 方向,所以 yaw = 180。
            camPose.transform.rotation = Quaternion.Euler(26f, 180f, 0f);

            var backdrop = Group(T, "Backdrop");
            var skyGo = Hull(backdrop, "Sky", new Vector3(0f, 7f, -15f), new Vector3(40f, 20f, 0.2f), sky);
            var seaGo = Hull(backdrop, "Sea", new Vector3(0f, 0.02f, -6.5f), new Vector3(40f, 0.12f, 12f), sea);
            var sandGo = Hull(backdrop, "Sand", new Vector3(0f, -0.04f, 4f), new Vector3(40f, 0.12f, 17f), sand, true);
            // 海平线:一条压扁的浅带,免得大海与沙滩之间是硬切
            Hull(backdrop, "WetSand", new Vector3(0f, 0.03f, -0.6f), new Vector3(40f, 0.02f, 1.6f),
                 GreyboxMat("IslandWetSand", new Color(0.72f, 0.66f, 0.52f)));

            // 一些海边植被:两棵椰子树 + 一丛灌木。相机 yaw=180 之后"屏幕左 = 世界 +x",
            // 所以这些位置全部取 +x,才和你那张示例图的左右一致。
            // "休息小憩"整条行动已删(v0.15:白天加精力只剩巧克力棒),所以这里不再有树荫落点。
            var veg = Group(T, "Vegetation");
            Palm(veg, "PalmA", new Vector3(9.5f, 0f, 1.2f), trunk, leaf);
            Palm(veg, "PalmB", new Vector3(11.6f, 0f, 3.6f), trunk, leaf);
            Hull(veg, "Bush", new Vector3(7.6f, 0.28f, 4.4f), new Vector3(1.5f, 0.55f, 1.5f), leaf, false);

            // 篝火:石圈 + 两根柴,火苗由运行时按"今晚有没有生火"生成
            var fire = Group(T, "Campfire");
            var fireAnchor = new GameObject("FireAnchor");
            fireAnchor.transform.SetParent(T, false);
            fireAnchor.transform.position = new Vector3(0f, 0f, 1.2f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Hull(fire, "Stone" + i, new Vector3(fireAnchor.transform.position.x + Mathf.Cos(a) * 0.95f, 0.1f,
                                                    fireAnchor.transform.position.z + Mathf.Sin(a) * 0.95f),
                     new Vector3(0.34f, 0.2f, 0.34f), stone, false, new Vector3(0f, i * 45f, 0f));
            }
            for (int i = 0; i < 2; i++)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                l.name = "Log" + i;
                l.transform.SetParent(fire.transform, false);
                l.transform.position = new Vector3(0f, 0.16f, 1.2f);
                l.transform.localScale = new Vector3(0.22f, 0.75f, 0.22f);
                l.transform.rotation = Quaternion.Euler(90f, i * 90f, 0f);
                l.GetComponent<Renderer>().sharedMaterial = log;
                UnityEngine.Object.DestroyImmediate(l.GetComponent<Collider>());
            }

            // 道具落点:三排,越靠前越靠近镜头(仓库里的东西按顺序摆上来)
            // 道具落点:四排 × 6,越靠前越靠近镜头。仓库种类数能到 20 上下(16 件 + 队友 + 尸骨),
            // 18 个会绕回第一排互相穿插,所以留到 24。
            float[] rowZ = { 3.4f, 5.0f, 6.6f, 8.2f };
            for (int r = 0; r < rowZ.Length; r++)
                for (int i = 0; i < 6; i++)
                {
                    var s = new GameObject("Slot_" + r + "_" + i);
                    s.transform.SetParent(T, false);
                    s.transform.position = new Vector3(-5.5f + i * 2.2f, 0f, rowZ[r]);
                    slots.Add(s.transform);
                }

            var mateA = new GameObject("MateAnchor");
            mateA.transform.SetParent(T, false);
            mateA.transform.position = new Vector3(-6.8f, 0f, 1.6f);
            var gullA = new GameObject("GullAnchor");
            gullA.transform.SetParent(T, false);
            gullA.transform.position = new Vector3(-8.5f, 2.6f, -3.5f);
            var bonesA = new GameObject("BonesAnchor");
            bonesA.transform.SetParent(T, false);
            bonesA.transform.position = new Vector3(3.2f, 0f, 0.4f);
            var seaA = new GameObject("SeaAnchor");
            seaA.transform.SetParent(T, false);
            seaA.transform.position = new Vector3(0f, 0.06f, -3.4f);

            // 标牌:全部印在面上(天空/大海 朝镜头,沙滩/植被/篝火 朝上贴着沙)。
            // x 全部按 yaw=180 的镜像取号,让"屏幕左"对应世界 +x,与示例图一致。
            signs.Add(new CabinSign
            {
                text = "天空",
                host = skyGo.transform,
                localPos = new Vector3(0.32f, 0.22f, 0.52f),
                facingLocal = Vector3.forward,
                size = 0.4f,
                color = new Color(0.16f, 0.28f, 0.4f),
            });
            signs.Add(new CabinSign
            {
                text = "大海",
                host = seaGo.transform,
                localPos = new Vector3(-0.12f, 0.55f, 0.1f),
                facingLocal = Vector3.up,
                size = 0.38f,
                color = new Color(0.82f, 0.92f, 1f),
            });
            signs.Add(new CabinSign
            {
                text = "沙滩",
                host = sandGo.transform,
                localPos = new Vector3(-0.3f, 0.55f, 0.32f),
                facingLocal = Vector3.up,
                size = 0.36f,
                color = new Color(0.42f, 0.36f, 0.24f),
            });
            signs.Add(new CabinSign
            {
                text = "一些海边植被",
                host = sandGo.transform,
                localPos = new Vector3(9.5f / 40f, 0.55f, (2.6f - 4f) / 17f),
                facingLocal = Vector3.up,
                size = 0.33f,
                color = new Color(0.16f, 0.32f, 0.18f),
            });
            signs.Add(new CabinSign
            {
                text = "篝火",
                host = sandGo.transform,
                localPos = new Vector3(0f, 0.55f, (2.1f - 4f) / 17f),
                facingLocal = Vector3.up,
                size = 0.33f,
                color = new Color(0.85f, 0.5f, 0.16f),
            });

            var st = T.gameObject.AddComponent<IslandStage>();
            st.cameraPose = camPose.transform;
            // 顺手把场景里的主相机摆到这个位姿:编辑器视口 = 玩家看到的那一屏,
            // 免得"运行时代码才把镜头搬过去",在 Scene 视图里对着一堆原点物体猜。
            var mc = UnityEngine.Camera.main;
            if (mc != null)
            {
                mc.transform.position = camPose.transform.position;
                mc.transform.rotation = camPose.transform.rotation;
                EditorUtility.SetDirty(mc.transform);
            }
            st.propSlots = slots;
            st.mateAnchor = mateA.transform;
            st.gullAnchor = gullA.transform;
            st.bonesAnchor = bonesA.transform;
            st.seaAnchor = seaA.transform;
            st.fireAnchor = fireAnchor.transform;
            st.signs = signs;
            EditorUtility.SetDirty(st);
            return st;
        }

        static Transform Palm(Transform parent, string name, Vector3 at, Material trunk, Material leaf)
        {
            var g = Group(parent, name);
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Trunk";
            t.transform.SetParent(g, false);
            t.transform.position = at + new Vector3(0f, 1.35f, 0f);
            t.transform.localScale = new Vector3(0.42f, 1.35f, 0.42f);
            t.transform.rotation = Quaternion.Euler(0f, 0f, 7f);
            t.GetComponent<Renderer>().sharedMaterial = trunk;
            UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());
            for (int i = 0; i < 5; i++)
            {
                var l = Hull(g, "Leaf" + i, at + new Vector3(0f, 2.75f, 0f), new Vector3(2.3f, 0.12f, 0.5f), leaf, false,
                             new Vector3(0f, i * 72f, -22f));
                l.transform.position = at + new Vector3(0f, 2.75f, 0f)
                                       + new Vector3(Mathf.Cos(i * 72f * Mathf.Deg2Rad) * 0.9f, 0f,
                                                     Mathf.Sin(i * 72f * Mathf.Deg2Rad) * 0.9f);
            }
            return g;
        }

        static void Bulkhead(Transform parent, float x, Material panel)        {
            var g = Group(parent, "Bulkhead@" + x);
            for (int s = -1; s <= 1; s += 2)
                Hull(g, "Bulkhead" + (s < 0 ? "P" : "S"), new Vector3(x, 1.2f, s * 2.05f),
                     new Vector3(0.18f, 2.4f, 1.7f), panel, true);
            Hull(g, "BulkheadLintel", new Vector3(x, 2.4f, 0f), new Vector3(0.18f, 0.5f, 2.5f), panel);
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        // 编辑模式下 Destroy 是非法的,拆 Collider 只能用 DestroyImmediate
        static GameObject Hull(Transform parent, string name, Vector3 pos, Vector3 scale, Material m,
                               bool collider = false, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            // 斜切的机身板要靠旋转摆:先 SetParent(false) 再给世界旋转,顺序反了会被父级旋转带偏
            if (euler.x != 0f || euler.y != 0f || euler.z != 0f) go.transform.rotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        // 颜色进 .mat 资产:同一个色号在 Scene 视图里改一次,整架飞机跟着改。
        // unlit=true 走 "Unlit/Color":背景那块"天空"朝向镜头的那一面是背光的,
        // 用受光的 Standard + 平面环境光(0.21)会被算成一片近黑,整个画面就"看不见东西"。
        static Material GreyboxMat(string file, Color c, bool unlit = false)
        {
            Material m;
            if (matCache.TryGetValue(file, out m) && m != null) return m;
            EnsureFolder(MatRoot);
            string path = MatRoot + "/" + file + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(ShaderFor(unlit));
                m.color = c;
                if (!unlit && c.a < 0.99f)
                {
                    // Standard 做半透明必须给整套 keyword,只改 _Mode 没有效果
                    m.SetFloat("_Mode", 3f);
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.renderQueue = 3000;
                }
                AssetDatabase.CreateAsset(m, path);
                created++;
            }
            else
            {
                // 已有的 .mat 只校正 shader(比如这次把天空改成 Unlit),颜色仍然尊重你在 Inspector 里的改动
                var want = ShaderFor(unlit);
                if (want != null && m.shader != want)
                {
                    m.shader = want;
                    m.color = c;
                    EditorUtility.SetDirty(m);
                    Debug.Log("[60slike] " + path + " 的 shader 改成 " + want.name);
                }
            }
            matCache[file] = m;
            return m;
        }

        static Shader ShaderFor(bool unlit)
        {
            if (!unlit) return Shader.Find("Standard");
            var u = Shader.Find("Unlit/Color");
            if (u == null) u = Shader.Find("UI/Default");     // 极端情况下至少它不受光照
            if (u == null) { Debug.LogError("[60slike] 找不到 Unlit/Color,天空会继续受光偏黑。"); return Shader.Find("Standard"); }
            return u;
        }

        // ── 建表小工具:已有的资产只补引用,不覆盖值 ──────────────
        const string Fake = "你活下来了,但内心依旧不安。几个月后,你再一次踏上了寻宝的旅程。";
        const string Real = "你活下来了。这一次,你是为自己而活。你发誓再也不去寻宝了。";
        const string Claimed = "你又一次上了那架飞机。这一次,你知道自己在替谁数钱。";

        static ItemSO Can, Coconut, Chocolate, Bait, MedKit, Flare, Material, FlareGun;   // v0.28:FreshWater / DirtyWater 已删
        static ToolSO FishingRod, DiveGear, SimpleMask, Spear, Flint, Blanket, Umbrella, Net;
        static FlashlightSO Flashlight;
        static ItemSO Medicine, CrudeFlare, Bottle, Herb, CrudeFlint;
        static ItemSO NameTag, Map, Key, Chest, Scrap, Newspaper, Photo;
        static ItemSO Luggage, Claim, Badge, Boarding, Tally, Raft, TwoNames, MonkeyGift;   // v0.26 彩蛋层拍板 8 件
        static TeammateSO Pilot, Navigator, Mechanic;

        static void Seed(ItemSO o, string key, string name, int slots, int min, int max, bool high, string desc)
        {
            o.key = key; o.displayName = name; o.slots = slots;
            o.cabinMin = min; o.cabinMax = max; o.highValue = high; o.desc = desc;
            o.spawnPoints = Mathf.Clamp(3 + max, 3, 6);
        }

        static ItemSO Item(string key, string name, int slots, int min, int max, bool high, string desc,
                           Action<ItemSO> extra = null)
        {
            return Tab<ItemSO>(Items + "/" + key + ".asset", o =>
            {
                Seed(o, key, name, slots, min, max, high, desc);
                if (extra != null) extra(o);
            });
        }

        static ToolSO Tool(string key, string name, int slots, int min, int max, bool high,
                           int repairMat, int repairSta, bool indestructible, float breakStart, string desc)
        {
            return Tab<ToolSO>(Tools + "/" + key + ".asset", o =>
            {
                Seed(o, key, name, slots, min, max, high, desc);
                o.repairMaterials = repairMat; o.repairStamina = repairSta;
                o.indestructible = indestructible; o.breakChanceStart = breakStart;
                o.repairFreeOfMaterials = repairMat == 0;
            });
        }

        static ItemSO LoreItem(string key, string name, string desc, Action<ItemSO> extra = null)
        {
            return Tab<ItemSO>(Items + "/" + key + ".asset", o =>
            {
                Seed(o, key, name, 1, 0, 0, false, desc);
                o.lore = true; o.tradeable = false; o.consumable = false;
                if (extra != null) extra(o);
            });
        }

        static RecipeSO Recipe(string key, string name, int materials, int stamina, ItemSO result,
                               bool isStructure, bool consumesFlint, string desc, Action<RecipeSO> extra = null)
        {
            return Tab<RecipeSO>(Recipes + "/" + key + ".asset", o =>
            {
                o.key = key; o.displayName = name; o.materials = materials; o.stamina = stamina;
                o.desc = desc; o.consumesFlint = consumesFlint; o.isStructure = isStructure;
                if (isStructure) o.resultStructure = result == null ? key : null;
                else o.resultItem = result;
                if (extra != null) extra(o);
            });
        }

        static ActivitySO Act(string key, string name, int cost, string loc, bool oncePerDay, bool needsRoll, string desc)
        {
            return Tab<ActivitySO>(Activities + "/" + key + ".asset", o =>
            {
                o.key = key; o.displayName = name; o.cost = cost; o.location = loc;
                o.oncePerDay = oncePerDay; o.requiresDailyRoll = needsRoll; o.desc = desc;
                o.costAllRemaining = key == "explore";
            });
        }

        static TeammateSO Mate(string key, string name, string skill, Color c)
        {
            return Tab<TeammateSO>(Teammates + "/" + key + ".asset", o =>
            {
                o.key = key; o.displayName = name; o.profession = name;
                o.skillDesc = skill; o.skillChance = 0.4f; o.color = c;
            });
        }

        static EndingSO Ending(EndingId id, string title, string[] lines, string[] claimedLines)
        {
            return Tab<EndingSO>(Endings + "/" + id + ".asset", o =>
            {
                o.id = id; o.title = title;
                o.epilogueLines.AddRange(lines);
                if (claimedLines != null) o.epilogueIfTrueClaimed.AddRange(claimedLines);
                // C 与 F 不读"已宣读完"的尾注
                o.usesClaimedEpilogue = !(claimedLines == null);
            });
        }

        static LoreDropSO LoreDrop(ItemSO item, string source, float chance, string text)
        {
            return Tab<LoreDropSO>(Lore + "/" + item.key + "_" + source + ".asset", o =>
            {
                o.item = item; o.source = source; o.chance = chance; o.loreText = text;
            });
        }

        static GameEventSO Ev(string key, string name, string desc, int priority, bool inBag, int minNight = 0,
                              Action<GameEventSO> extra = null)
        {
            return Tab<GameEventSO>(Events + "/" + key + ".asset", o =>
            {
                o.key = key; o.displayName = name; o.description = desc;
                o.priority = priority; o.inEventBag = inBag; o.neverInBag = !inBag; o.minNight = minNight;
                if (extra != null) extra(o);
            });
        }

        static GameChoiceSO Ch(GameEventSO e, string key, string label, string hint, Action<GameChoiceSO> extra = null)
        {
            var c = Tab<GameChoiceSO>(Choices + "/" + e.key + "_" + key + ".asset", o =>
            {
                o.key = key; o.label = label; o.resultHint = hint;
                if (extra != null) extra(o);
            });
            if (c == null || e.choices.Contains(c)) return c;
            e.choices.Add(c);
            EditorUtility.SetDirty(e);
            return c;
        }

        static void AddVariants(GameEventSO e, List<GameEventSO> variants, int[] weights)
        {
            if (e.variants.Count > 0) return;   // 已经接过:权重和成员以 Inspector 里的为准
            e.variants.AddRange(variants);
            e.variantWeights.AddRange(weights);
            EditorUtility.SetDirty(e);
        }

        static T Tab<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            bool fresh;
            var o = GetOrCreate<T>(path, out fresh);
            if (o != null && fresh)
            {
                if (fill != null) fill(o);
                EditorUtility.SetDirty(o);
                created++;
            }
            return o;
        }

        static T GetOrCreate<T>(string path, out bool fresh) where T : ScriptableObject
        {
            int slash = path.LastIndexOf('/');
            EnsureFolder(slash > 0 ? path.Substring(0, slash) : "Assets");
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            fresh = o == null;
            if (fresh)
            {
                o = ScriptableObject.CreateInstance<T>();
                o.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(o, path);
            }
            CheckScriptRef<T>(path);
            return o;
        }

        // Unity 只会把一个 .cs 的"主类"(文件名同名、否则是第一个类)注册成可引用的 MonoScript。
        // 同文件里的其它 ScriptableObject 类型会被写成 m_Script: {fileID: 0} —— 资产照样存在、引用照样非空,
        // 但读回来的字段全是 0(就是这么把 拾荒 60 秒 变成 0 秒的)。这里把它变成一条响亮的错误。
        static void CheckScriptRef<T>(string path)
        {
            if (!File.Exists(path)) return;
            if (!File.ReadAllText(path).Contains("m_Script: {fileID: 0}")) return;
            Debug.LogError("[60slike] " + path + " 的 m_Script 是 {fileID: 0} → " + typeof(T).Name +
                " 的数值读回来全是 0。修法:每个 ScriptableObject 类型单独一个 .cs 文件,并且它要是文件里的第一个类" +
                "(已拆到 Assets/Scripts/Core/Data/)。拆完重开 Unity 让它重编译,再跑一次本菜单。");
        }

        static void EnsureFolder(string folder)
        {
            if (folder == "Assets" || AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(slash > 0 ? folder.Substring(0, slash) : "Assets");
            AssetDatabase.CreateFolder(slash > 0 ? folder.Substring(0, slash) : "Assets", Path.GetFileName(folder));
        }
    }
}
