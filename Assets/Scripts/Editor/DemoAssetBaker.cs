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
        // ⚠ 字体(v0.50 换 思源黑体):烘焙器 **不再管字体**。
        //   唯一入口是 `Ui.BootFont()` 按 Resources 名字取(`Resources/Fonts/NotoSansSC-Regular`),
        //   编辑器与打包同一条路。旧行楷那支文件还在 `Assets/Scenes/STXINGKA.TTF`(用户说留着不删),
        //   只在 Resources 那份没导入时由 `Ui` 兜一下底。
        //   原来这里是 `FontPath` + `ProjectFont()` + `GameRoot.uiFont` 三件套 —— 那是第二条加载路,
        //   Inspector 里拖的那份会在 Awake 把新字体覆盖回旧的,所以字段与这两个一起删了。

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

        [MenuItem("Tools/60slike/⑥ 生成物品栏缩略图(携带条每格的小贴图)", false, 6)]
        public static void MenuIcons() { BakeIcons(); }

        // ── v0.62(用户:"**拾取的物品的小模型/贴图显示在物品栏里**")──────────────────
        //   做法 = **离屏渲染每件物品的模型,存成 `Resources/Icons/<key>.png`**(128×128):
        //   运行时携带条每格读一张贴图 + 名字,占几格就重复几格(他给的例子:潜水装置 ×3)。
        //   三条口径:① 没模型的件渲一颗方块当剪影(与灰盒兜底同一思路),**不报错、不空格**;
        //             ② 队友也烘(`mate_<key>`,吊床/携带条里背着人时那三格用);
        //             ③ 这是 **生成出来的资产**,换模型/加物品后重跑一次本菜单即可,不进 git 的手改名单。
        //   ⚠ 编辑模式渲染:临时相机 + 临时灯 + 临时物体,用完一律 DestroyImmediate(编辑模式 Destroy 是非法的,
        //     这条 v0.51 那轮栽过);RenderTexture.active 用完要归 null,不然 Scene 视图会黑一块。
        public static void BakeIcons()
        {
            var db = AssetDatabase.LoadAssetAtPath<Database>(DatabaseAsset);
            if (db == null) { Debug.LogError("[60slike] 还没有数据资产,先跑菜单 ①。"); return; }
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Icons");

            var camGo = new GameObject("IconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.fieldOfView = 40f;
            var lightGo = new GameObject("IconLight");
            var li = lightGo.AddComponent<Light>();
            li.type = LightType.Directional;
            li.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var stage = new GameObject("IconStage");
            var rt = new RenderTexture(128, 128, 24);

            var keys = new List<string>();
            foreach (var it in db.scavengedItems) if (it != null) keys.Add(it.key);
            keys.Add("mate_" + db.Pilot.key); keys.Add("mate_" + db.Navigator.key); keys.Add("mate_" + db.Mechanic.key);

            int n = 0;
            foreach (var key in keys)
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/" + key + ".fbx");
                var go = pref != null ? UnityEngine.Object.Instantiate(pref) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Icon_" + key;
                go.transform.SetParent(stage.transform, false);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                // 归一到 1.6 米再按包围盒中心摆相机 ⇒ 大件小件在格子里差不多大
                Vector3 mn, mx;
                World.WorldAABB(go, out mn, out mx);
                var size = mx - mn;
                float m = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (m > 0.0001f) go.transform.localScale = go.transform.localScale * (1.6f / m);
                World.WorldAABB(go, out mn, out mx);
                var center = (mn + mx) * 0.5f;
                camGo.transform.position = center + new Vector3(1.7f, 1.2f, -1.7f);
                camGo.transform.LookAt(center);
                cam.targetTexture = rt;
                RenderTexture.active = rt;
                cam.Render();
                var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes("Assets/Resources/Icons/" + key + ".png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(go);
                n++;
            }
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(stage);
            AssetDatabase.Refresh();
            Debug.Log("[60slike] 物品栏缩略图 " + n + " 张 → Assets/Resources/Icons/(携带条每格读 Icons/<key>;换模型后重跑本菜单)。");
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
            Can = Item("can", "罐头", 1, 5, 6, false, "通用货币:回 25% 饱食。队友、海鸥、狐狸都在抢同一个数字。",
                o => { o.consumable = true; o.fullnessRestore = 25; });
            // v0.28:淡水 与 脏水 两件物品连同 喝水系统 一起删除 ⇒ 舱内投放少 3~4 格(28~33 → 24~29)
            Material = Item("material", "材料", 1, 0, 0, false,
                "万能素材:制作 / 修理 / 生火 共用同一份库存(补录⑧)。v0.19:开局不刷在舱内,唯一来源是 探索荒岛(30%×6)。");
            Herb = Item("herb", "药草", 1, 0, 0, false,
                "v0.16:探索荒岛 20% 一次判定采到。它只有一个用途 —— 自制药品 的那一味素材(材料1 + 药草1)。不进机舱点位池、不进狐狸货单。",
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
            // v0.62(用户:"**背包改为3格**" ⇒ 4 格的 潜水装置 在 3 格背包里永远带不走,潜水线会死):
            //   占格 4 → 3,正好装满一整趟,与 v0.x 那条"满趟独占"的原意一致(他裁的:改 3 格)。
            DiveGear = Tool("dive", "潜水装置", 3, 1, 1, true, 4, 3, false, 0.2f, "占满一整趟(3 格)。解锁潜水;修理 = 材料4 + 3 体力(补录⑦)。");
            SimpleMask = Tool("mask", "简易浮镜", 1, 0, 0, false, 2, 2, false, 0.2f, "无潜水装置时低效潜水,产出概率减半。");
            Spear = Tool("spear", "鱼叉", 2, 1, 1, true, 2, 2, false, 0.2f,
                "唯一的武器:低效潜水 / 血月下水 / 影怪硬挡(必坏)/ 赶狐狸(完好回收 +1 罐头,自身照常掷损坏)。");
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
                "v0.18:材料1 自制的一块火种,只能用一次 —— 生火时优先烧掉它,保住可反复用的 打火石。不进机舱点位池、不进狐狸货单。",
                o => { o.consumable = true; o.tradeable = false; });
            Bottle = Item("bottle", "漂流瓶", 1, 1, 1, false,
                "夜晚鱼群/漂流瓶事件可捞,也可能拾荒捡到(v0.19:舱内只有 1 个)。白天丢出:1 瓶 → 每晚独立 20% 轮船;"
                + "**丢满 2 瓶才会再开一条独立 20% 的幽灵船(结局H)** —— 所以第二个瓶子必须靠夜晚的漂流瓶事件捞。",
                o => { o.tradeable = false; });

            // ---- §5.3 暗线道具:全部 1 格、不可制作、不给数值收益 ----
            NameTag = LoreItem("nametag", "铭牌", "涨潮露出的尸骨胸口上那块。刻着你亲手输入的名字。只用于图鉴与结局F判定,不是真结局前置。");
            Map = LoreItem("map", "藏宝图", P("map"));
            Key = LoreItem("key", "宝藏钥匙", P("key"));
            Chest = LoreItem("chest", "藏宝箱", "2 格。持图那天探索荒岛必定捡回。与钥匙一同持有才能在影怪之夜献宝。", o => o.slots = 2);
            Scrap = LoreItem("scrap", "残图", "钓鱼/探索杂物。与藏宝图合成清晰版(纯图鉴)。");
            Newspaper = LoreItem("news", "剪报", "钓鱼/探索杂物。海事保险公司的旧闻。");
            Photo = LoreItem("photo", "合影", "钓鱼/探索杂物。三个穿制服的人站在一架飞机前。");

            // ---- v0.26 §5.3 彩蛋层拍板的 8 件:全部 1 格、不给数值、不做任何结局的前置 ----
            Luggage = LoreItem("luggage", "泡水的行李牌", P("luggage"));
            Claim = LoreItem("claim", "三张编号连续的理赔单", P("claim"));
            Badge = LoreItem("badge", "压弯的公司徽章", P("badge"));
            Boarding = LoreItem("boarding", "湿透的登机牌", P("boarding"));
            Tally = LoreItem("tally", "刻着「正」字的木片", P("tally"));
            Raft = LoreItem("raft", "无人认领的救生筏残片", P("raft"));
            TwoNames = LoreItem("twonames", "两个被划掉的名字", P("twonames"));
            MonkeyGift = LoreItem("monkeygift", "狐狸的回礼", P("monkeygift"));

            // ---- §6 制作表 ----
            var recipes = new List<RecipeSO>
            {
                // v0.48(用户:"篝火不用点,存在则那几条分支都直接走"):consumesFlint 回到 true ——
                //        含义不再是"造一件只值一晚的临时火",而是 **建造那一手就把它点着**(建好即烧两晚)。
                //        熄灭 = 这座灶进入「坏」(与 围墙 同一套模型),恢复走维修面板那行"重新点燃"。
                Recipe("campfire", "庇护所篝火", 3, 2, null, true, true,
                    "v0.48:垒一座灶 = 材料3 + 2 体力 + 一块火种,**建好当场点着**,烧两个晚上(点燃当天算第 1 晚)。之后 **不用每天点**:它没坏的时候,那几条夜晚分支(低温夜点灶 / 小影怪硬撑 / 赶狐狸 / 影怪之夜 -3 变 -2)直接走。两晚烧完或被 涨潮 打湿 ⇒ 进入「坏」(与 围墙 同一套模型),在同一个维修面板里 **重新点燃(一块火种,0 材料 0 精力)**。它是 信号火堆 的前置。"),
                Recipe("flint", "土制打火石", 1, 1, CrudeFlint, false, false,
                    "v0.18:材料1 自制一块火种,只能用一次(生火时优先烧它,保住可反复用的 打火石)。手上有 打火石 时这一行不进面板(上位替代)。",
                    o => o.supersededBy = Flint),
                // v0.28 曾把 净水器 那道前置删掉,于是"制造无前置"成了没有例外的规则。
                // ⚠ v0.47(用户)重新开 **一个例外**:`信号火堆` 必须先有 `篝火` 才建得起来。
                Recipe("wall", "庇护所围墙", 5, 3, null, true, false,
                    "v0.16:围墙在的时候 涨潮 与 毒蛇 直接不进夜。v0.24:尸骨已独立成 骸骨 事件,与这场无关。v0.46(用户):**它也会坏** —— 每挡下一场就算一次使用,按 §2.3 标准累积概率掷(20% 起、每次 +10%、上限 90%);裂了白天修(材料3 + 1 体力)。另外 影怪之夜躲被子 QTE 判定窗口 +20%,这条同样随它裂掉而失效。"),
                Recipe("signalfire", "信号火堆", 6, 2, null, true, false,
                    "v0.64:它 **不是 篝火 之外多出来的一摊**,而是把那一堆垒高 —— 建起它,篝火 那一档从账上退场,营地里永远只有一堆火(同一格、身量 2.4 米、v0.65 复用 篝火 的模型放大一档)。必须先有 篝火(§6 唯一的一道前置),价 材料6 + 2 体力;**v0.65:这一手不吃火种**(它是垒高,不是重新点火),建好照样当场点着、烧两个晚上。烧着的那几晚 轮船/幽灵船/搜寻飞机 直接判定通过(结局A / H);两晚过或被 涨潮 打湿 ⇒ 进入「坏」,维修面板里「重新点燃」= 一块火种(那才是「点火」)。",
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

            // v0.62 迁移:菜单① 的约定是"已有的不覆盖",所以 **已烘出来的 dive.asset 占格还是 4** ——
            //   而 3 格背包装不下 4 格件(潜水线会死)。这里只改这一个字段并打日志,不动别的资产;
            //   不想走迁移就删掉 Assets/Data 重跑①(数据资产全是生成的,删了不丢任何东西)。
            if (db.DiveGear != null && db.DiveGear.slots != 3)
            {
                Debug.Log("[60slike] v0.62 迁移:潜水装置 占格 " + db.DiveGear.slots + " → 3(背包改 3 格,4 格件永远带不走)。");
                db.DiveGear.slots = 3;
                db.DiveGear.desc = "占满一整趟(3 格)。解锁潜水;修理 = 材料4 + 3 体力(补录⑦)。";
                EditorUtility.SetDirty(db.DiveGear);
            }

            // v0.65 迁移(**同一类坑,同一个做法**):菜单① 的约定是"已有的不覆盖",所以已经烘出来的
            //   `signalfire.asset` 里 `consumesFlint` 仍是 true —— 不迁的话这轮那句「**升级免火种**」
            //   只改了代码字面量、运行时读到的还是资产里那份 true(两本账,升级照样吃掉一块火种)。
            //   ⚠ 只改这一个字段 + 打日志;`desc` 不动(配方说明不是玩家可见文本,面板那行是拼出来的)。
            if (db.recipes != null)
                for (int i = 0; i < db.recipes.Count; i++)
                {
                    var rf = db.recipes[i];
                    if (rf == null || rf.key != Database.SignalFire || !rf.consumesFlint) continue;
                    Debug.Log("[60slike] v0.65 迁移:信号火堆 这一手 吃火种 → 不吃(升级 = 把那一堆垒高,不是重新点火;熄灭后再点仍然要一块火种)。");
                    rf.consumesFlint = false;
                    EditorUtility.SetDirty(rf);
                }

            // v0.66 迁移(用户:"**小影怪的直接睡觉和正常的尝试睡去合并**"):这一条选项 **整件不再列出**。
            //   它原来只在 **没有队友** 的那一晚出现,而结算里 `smallshadow` 的第一条判据就是 `!mate.present`
            //   ⇒ 「直接睡觉」与通用「尝试睡去」走的是 **同一个分支**(次日体力 -1,不丢罐头不坏工具),
            //     所以那一晚的面板上是 **两行同一件事**。合并 = 留「尝试睡去」那一条。
            //   ⚠ 盘上那个 `smallshadow_sleepalone.asset` 我 **没删**(删文件不在这一轮的必要里);
            //     要清干净就删掉 `Assets/Data` 整个目录重跑 ① —— 数据资产全是生成的,删了不丢任何东西。
            if (db.SmallShadow != null && db.SmallShadow.choices != null)
            {
                int n0 = db.SmallShadow.choices.Count;
                db.SmallShadow.choices.RemoveAll(c => c != null && c.key == "sleepalone");
                if (db.SmallShadow.choices.Count != n0)
                {
                    Debug.Log("[60slike] v0.66 迁移:小影怪 的选项列表里摘掉「直接睡觉」(它与「尝试睡去」本来就同一件事)。");
                    EditorUtility.SetDirty(db.SmallShadow);
                }
            }

            // v0.69 迁移(用户:"**所有概率文案都隐藏**"):玩家看得见的文本有 **三个出处在资产里** ——
            //   夜晚选项按钮括号那句(`resultHint`,18 条)、夜晚屏的事件描述(`description`,1 条)、
            //   收集页右栏的彩蛋说明(`desc`,10 条)。菜单① 的约定是"已有的不覆盖" ⇒ **不刷这一步,
            //   你盘上那 29 条旧文案一个字都不会变**(与 v0.62 占格、v0.65 免火种、v0.66 删选项同一类坑)。
            //   文字的唯一出处是上面那张 `PlayerText`,所以这里不重复写字面量,新增一条只要往表里加一行。
            int refreshed = RefreshPlayerText();
            if (refreshed > 0)
                Debug.Log("[60slike] v0.69 迁移:刷新玩家可见文案 " + refreshed +
                          " 条(机会有多大 / 掷 / 概率 这类写法已从 夜晚选项括号、事件描述、彩蛋说明 里摘掉;" +
                          "代价与收益的数字照旧留着)。");

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[60slike] 数据资产就绪:" + DatabaseAsset + "(本次新建 " + created + " 个)");
            return db;
        }

        // 按 **资产文件名** 认(= `Ch()` 落盘的 `<事件key>_<选项key>.asset`,事件与物品就是它们自己的 key),
        // 所以这张表不需要知道 key 与资产路径的对应关系。返回改动了几个资产(0 = 盘上已经是新文字)。
        internal static int RefreshPlayerText()
        {
            int n = 0;
            foreach (var typeName in new[] { "GameChoiceSO", "GameEventSO", "ItemSO" })
                foreach (var gu in AssetDatabase.FindAssets("t:" + typeName))
                {
                    var path = AssetDatabase.GUIDToAssetPath(gu);
                    string want;
                    if (!PlayerText.TryGetValue(System.IO.Path.GetFileNameWithoutExtension(path), out want)) continue;
                    var o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    var c = o as GameChoiceSO; var e = o as GameEventSO; var it = o as ItemSO;
                    if (c != null) { if (c.resultHint == want) continue; c.resultHint = want; }
                    else if (e != null) { if (e.description == want) continue; e.description = want; }
                    else if (it != null) { if (it.desc == want) continue; it.desc = want; }
                    else continue;
                    EditorUtility.SetDirty(o); n++;
                }
            return n;
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
            Ch(BloodMoon, "dive", "趁退潮下水", P("bloodmoon_dive"));
            Ch(BloodMoon, "nothing", "什么都不做", "队友精神降 1 层", c => c.alwaysAvailable = true);

            var SearchPlane = Ev("searchplane", "搜寻飞机", "远处有机影掠过。这是手电筒唯一的营救判定窗口之一。", 12, true);
            Ch(SearchPlane, "flare", "打出信号弹", P("searchplane_flare"), c => c.consumesItem = true);
            Ch(SearchPlane, "firepile", "点亮信号火堆", "只在 信号火堆 灭着的那一晚给:当场点着它(花火种)+ 记下第一发。烧着的那两晚它自己就免检(v0.47)", c => c.requiresStructure = Database.SignalFire);
            Ch(SearchPlane, "flashlight", "手电筒照射", P("searchplane_flashlight"), c => c.drainsFlashlight = true);
            Ch(SearchPlane, "ignore", "不理会", "无累积惩罚(v0.10:结局E 已删)", c => c.alwaysAvailable = true);

            var ColdNight = Ev("coldnight", "低温夜", "气温掉得很快,庇护所里开始结霜。", 9, true);
            // v0.47:这一条不再收 材料2 —— 它改成"点燃自己垒好的灶",只花火种,而且一烧两晚。
            //        ⚠ 闸也跟着换了:要有 灶(篝火 或 信号火堆)+ 有火种,否则这一条根本不生成 ⇒ 没垒过灶的局只能裹毯子或硬挨。
            Ch(ColdNight, "fire", "点燃自己的灶", "只花火种(0 材料 0 精力),烧两晚;需要先垒过 篝火 或 已建 信号火堆", c => c.requiresItem = Flint);
            Ch(ColdNight, "blanket", "裹毯子", "毯子本次必损",
                c => { c.requiresItem = Blanket; c.breakChanceNight = 1f; });      // §2.3 挡灾型:抗低温 = 必坏
            Ch(ColdNight, "硬挨", "硬挨一夜", "-1 生命 + 生病", c => { c.applySick = true; c.alwaysAvailable = true; });

            var Snake = Ev("snake", "毒蛇 / 蟑螂", "营地里有细小的爬行声。v0.16:建了围墙这一晚它根本进不来(事件不出现,改抽别的)。", 3, true);
            Ch(Snake, "swat", "点击驱赶", P("snake_swat"));
            Ch(Snake, "flashlight", "手电筒照射驱赶", "不损坏;用掉 1 格电", c => c.drainsFlashlight = true);
            Ch(Snake, "不管它", "不管它", "-1 生命 或 损失 1 份罐头", c => c.alwaysAvailable = true);

            var FishSchool = Ev("fishschool", "鱼群", "浅海里压过来一大片银色,水面炸开。", 8, true);
            // v0.46(用户):"鱼群不能使用鱼竿,只有鱼叉与渔网" ⇒ 第二条从"鱼叉或钓竿徒手搞"收成 **纯鱼叉**,
            //        产出与 渔网 同为 3 份,两件都照常掷累积损坏。这一场从此没有"零损耗的备选"。
            Ch(FishSchool, "net", "下渔网", P("fishschool_net"), c => c.requiresItem = Net);
            Ch(FishSchool, "hand", "用鱼叉下水叉鱼", P("fishschool_hand"));
            Ch(FishSchool, "nothing", "啥也不做", "纯机会事件,无惩罚", c => c.alwaysAvailable = true);

            var Driftbottle = Ev("driftbottle", "漂流瓶", "一个玻璃瓶在礁石边上下翻滚,里面卷着一张纸。", 7, true);
            Ch(Driftbottle, "grab", "渔网捞 / 潜水装置取", "2 体力 → 得到 1 个漂流瓶");
            Ch(Driftbottle, "nothing", "空手够不到", "无惩罚,只是少一次求救投递机会", c => c.alwaysAvailable = true);

            var Gull = Ev("gull", "海鸥", "一只海鸥落在庇护所顶上,歪着头看你吃东西。", 6, true);
            // v0.50(用户:"8.海鸥也可以使用鱼饵触发"):标签一直写着"罐头或鱼饵"、结算也一直两边都收,
            //   缺的只是 **第二个引用** —— 现在 `requiresItemAlt = Bait`,闸门与绑定都认两件。
            Ch(Gull, "feed", "给它 1 份罐头或鱼饵", "全游戏只需喂 1 次;之后来的直接落",
               c => { c.requiresItem = Can; c.requiresItemAlt = Bait; });
            Ch(Gull, "ignore", "不理它", "它飞走,下轮还会来", c => c.alwaysAvailable = true);

            var SmallShadow = Ev("smallshadow", "小影怪", "比影怪小一号的黑影,只有一点眼光,绕着营地转。", 5, true);
            Ch(SmallShadow, "watch", "守夜", P("smallshadow_watch"), c => c.nextDayStaminaPenalty = 2);
            Ch(SmallShadow, "fire", "点着火硬撑", P("smallshadow_fire"), c => c.requiresFire = true);
            Ch(SmallShadow, "nothing", "啥也不做 / 躲起来", "队友被杀,永久消失(唯一硬杀队友的事件)", c => c.alwaysAvailable = true);
            // v0.66(用户:"**小影怪的直接睡觉和正常的尝试睡去合并**")⇒ 这一条整件删除:
            //   原来那条 `sleepalone`(requiresNoTeammate)与通用「尝试睡去」走的是 **同一个结算分支**
            //   (`!S.mate.present` ⇒ 次日体力 -1,不丢罐头不坏工具),所以在"没有队友"的那一晚面板上会出现 **两行同一件事**。
            //   合并 = 留 睡去 那一条(它每个事件都有,是"什么都不做"的正式入口),删掉这一行重复的。
            //   ⚠ 已经烘出来的 `smallshadow_sleepalone.asset` 由 菜单① 里的迁移从 小影怪 的选项列表里摘掉。

            var MonkeyNaughty = Ev("monkeynaughty", "调皮的狐狸", "一只狐狸从树林窜进来,抓起仓库里随机 1 件东西就跑。", 8, true);
            Ch(MonkeyNaughty, "spear", "用鱼叉驱赶", P("monkeynaughty_spear"),
                c => { c.requiresItem = Spear; c.breakChanceNight = 0f; c.rewardFood = 1; });
            Ch(MonkeyNaughty, "flare", "用信号枪打一发驱赶", "被抢那件完好回来;枪不坏也不消耗,但弹药照旧少 1 发",
                c => { c.requiresItem = FlareGun; c.breakChanceNight = 0f; c.consumesItem = true; });
            Ch(MonkeyNaughty, "fire", "用火堆驱赶", "能赶,但抢回来的那件是破损状态(打火石不坏)",
                c => { c.requiresFire = true; c.breakChanceNight = 0f; c.recoversDamaged = true; });
            // v0.27:空手抢夺 不再挂 alwaysAvailable —— 它绑在夜晚那只狐狸身上(NightResolver.BoundTarget → "monkey"),
            //        只有点它才走到这一条。于是"尝试睡去(什么都不做)"落到下面那条 不给它 上,与 §3.2 的口径一致。
            //        ⚠ 改之前 sleep = 空手抢夺(-1 生命 + 破损回收),那是这条 alwaysAvailable 造成的,不是设计。
            Ch(MonkeyNaughty, "grab", "空手抢夺", "-1 生命,而且抢回来的是破损状态(点那只狐狸才做得到)",
                c => c.recoversDamaged = true);
            Ch(MonkeyNaughty, "refuse", "不给它", "那件物品被它带走,永久丢失(= 什么都不做 / 尝试睡去)", c => c.alwaysAvailable = true);

            var MonkeyFriendly = Ev("monkeyfriendly", "友善的狐狸", "两只狐狸坐成一排,把它们想换的推给你。", 6, true);
            Ch(MonkeyFriendly, "trade", "交易", "给它 1 件你当前持有的拾荒物,换 1 件你整局从未持有过的", c => c.alwaysAvailable = true);
            Ch(MonkeyFriendly, "decline", "不换", "它自己走掉,无任何负面", c => c.alwaysAvailable = true);

            // v0.28 新增:椰树 —— 椰子的唯一来源(探索荒岛不再产它)。零惩罚的机会事件,但"去看"自带 30% 被砸。
            //        "走过去看看" 不绑任何物品/物体 ⇒ 它由 NightResolver.ChoicesWithoutObject() 兜底,排在睡去按钮上方。
            var PalmTree = Ev("palmtree", "椰树", "海风吹得椰树摇晃。", 5, true);
            Ch(PalmTree, "take", "走过去看看", P("palmtree_take"));
            Ch(PalmTree, "ignore", "不去看", "没收获,也没惩罚(= 什么都不做 / 尝试睡去)", c => c.alwaysAvailable = true);

            var Rain = Ev("rain", "下雨", P("rain"), 7, true);
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
                Ch(v, "umbrella", "撑雨伞", P(v.key + "_umbrella"),
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
            Ch(FakeMate, "flashlight", "用手电筒照他", P("fakemate_flashlight"),
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
            Ch(Ship, "flare", "打出信号弹", P("ship_flare"), c => c.consumesItem = true);
            Ch(Ship, "flashlight", "手电筒照射", P("ship_flashlight"), c => c.drainsFlashlight = true);
            Ch(Ship, "sail", "升起临时信号帆", "飞行员技能生效当天才有:这条线唯一的免费出口");
            Ch(Ship, "ignore", "什么都不做", "⚠ 错过即永久:海面上再也没有船了", c => c.alwaysAvailable = true);

            var Ghost = Ev("ghostship", "幽灵船", "整条海平线暗下去。一艘没有灯、也没有人的船靠上礁石,甲板上摆着整齐的缆绳。", 0, false,
                0, o => { o.nightlyIndependentRoll = 0.2f; o.oncePerRun = true; });
            Ch(Ghost, "flare", "打出信号弹", P("ghostship_flare"), c => c.consumesItem = true);
            Ch(Ghost, "flashlight", "手电筒照射", P("ghostship_flashlight"), c => c.drainsFlashlight = true);
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
            // ⚠ `uiFont` 这个字段已经从 GameRoot 上删掉了(v0.50 换 思源黑体 的时候):
            //   字体的唯一入口是 `Ui.BootFont()` 按 Resources 名字取,编辑器与打包同一条路。
            if (root.islandStage != null && (root.islandStage.seaAnchor == null || root.islandStage.propSlots.Count < 24))
                Debug.Log("[60slike] 场景里的荒岛层是旧版(缺 SeaAnchor 或落点不足 24 个)。" +
                          "夜晚点不到 大海、仓库东西多了会重叠 → 请跑菜单 ④ 只重建荒岛层。");
            // v0.57:岛成形这一轮改的是 **烘焙器**,场景里那三块板不会自己变 ⇒ 没网格就是还没重烘,直接说破它
            //   (不然他只会看到"还是那三块板",然后以为是代码没生效 —— 这类"代码已改、场景未烘"本项目说过三次)。
            else if (root.islandStage != null && AssetDatabase.LoadAssetAtPath<Mesh>(IslandDiscAsset) == null)
                Debug.Log("[60slike] 场景里的荒岛还是 v0.56 那三块板(工程里没有 " + IslandDiscAsset +
                          ")→ 请跑菜单 ④ 只重建荒岛层,才会变成\"一个岛、周围全是海\"。");
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
            r.islandStage = islandStage; r.sun = sun;
            EditorUtility.SetDirty(r);
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
            var grass = GreyboxMat("IslandGrass", new Color(0.26f, 0.44f, 0.19f));     // 小山包的海拔线上那一层

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

            // ── v0.57(用户:"**荒岛建模为一个岛,周围全是海(固定资产)**";两条定案:「**大圆岛:现有东西全不动**」
            //    +「**一张大海面包围 + 天空板留着**」)────────────────────────────────
            //   原来那一屏是 **三块板**:沙滩 40×17(z -4.5~12.5)、大海 40×12(z -12.5~-0.5)、天空板 z=-15。
            //   ⇒ 海只是"岛后面的一条带",怎么走都走不到海边。现在:
            //     · 岛 = 一张按 `IslandTerrain.SurfaceY` 烘的 **实心圆面**(岛心是平沙,出了干岛面沿沙滩斜着入水)
            //     · 海 = **一块 400 米见方的海面**,顶面正好落在 `IslandTerrain.SeaY`,把岛整个围住
            //     · 天空板 **留着**(他裁的),但必须挪到岛外、并且挪到海的最远边之外(z=-210,440×120)——
            //       原来那块立在 z=-15,现在正对着海水中间,不挪就是一堵墙
            //   ⚠ **这一屏的构图因此变了,我说明白,不是偷偷改的**:岛要装得下 **现有全部内容**
            //     (24 格道具、山包网格到 x 21、海鸥挂点 x -8.5)⇒ 干岛面半径只能取 20 米,而固定镜头在 z=13.5
            //     ⇒ **镜头现在是"站在岛中间往外看"**:画面下半是一大片沙,海成了靠近地平线的那一条带
            //     (按 65° 竖直视场算:沙到屏幕约 **73%** 高是浪线,海带到约 **86%**,再往上是天空)。
            //     想"海多一点"只有两个旋钮:`IslandTerrain.FlatR`(岛多大)与 `BeachR / BeachDeepY`(沙滩多长多陡)——
            //     但 `FlatR` 一旦小于 19,山包与 海鸥 就伸到岛外去了,那要先挪东西,而他这轮的话是"现有东西全不动"。
            var backdrop = Group(T, "Backdrop");
            // 三样几何体都不再是标牌的宿主(见下面那一组 `host = backdrop`),所以这里不接返回值
            //   ⚠ 天空板为什么要放到 212 米外、放大到 440×120:它是 **一块平面**,而海现在铺到 200 米开外 ——
            //     板放近了就会 **切掉海面**(在板前那一条水平线以上就是天空,海被它挡住,看着像"海里立着一堵墙")。
            //     放在海的最远边之外,画面才是:沙(到浪线)→ 海(一条带)→ 天。
            //     (相机清屏色与这块板的颜色本来就是同一个蓝,所以从别的方向看出去也不会有"板外"的黑。)
            Hull(backdrop, "Sky", new Vector3(0f, 20f, IslandTerrain.Center.y - 212f),
                 new Vector3(440f, 120f, 0.2f), sky);
            // 海:Cube 是"中心 + 整尺寸",所以顶面 = 位置 y + 半个厚度 ⇒ 把中心往下挪半个厚度,顶面才 **正好** 是 SeaY
            Hull(backdrop, "Sea", new Vector3(IslandTerrain.Center.x, IslandTerrain.SeaY - 0.03f, IslandTerrain.Center.y),
                 new Vector3(400f, 0.06f, 400f), sea);
            IslandDisc(T, sand);
            // 原来这里还有一条 `WetSand`(40×1.6 的浅带,糊在两块板的水线接缝上):岛成形之后接缝是 **一整圈**
            //   `IslandTerrain.WaterR`,一条横着的带子盖不住它,所以这块板直接删掉(沙滩网格自己斜着入水,不需要再糊)。

            // v0.49(用户:"5.重新建立一些地形,玩家所处的荒岛有个小山包,有草地/稀树林")
            //   高度场的 **定义** 在运行时代码 `IslandTerrain` 里(两个高斯包),这里只把它烘成网格 ——
            //   这样"山有多高"只有一个出处:摆树、摆草、以及将来第一人称采样都问同一个函数。
            //   ⚠ 山包整体在道具区(x -5.5~5.5、z 3.4~8.2)与各锚点之外,所以 **24 个落点一个都没动** ——
            //     这是 §11-83 ⑤ 那条"落点 y 要不要按地形采样"的前置:它现在还不需要被采样。
            Hill(T, grass);
            GrassPatch(T, grass);

            // 一些海边植被:两棵椰子树 + 一丛灌木。相机 yaw=180 之后"屏幕左 = 世界 +x",
            // 所以这些位置全部取 +x,才和你那张示例图的左右一致。
            // "休息小憩"整条行动已删(v0.15:白天加精力只剩巧克力棒),所以这里不再有树荫落点。
            // v0.49(用户:"替换成真模型吧"):这三件先试 `Assets/Art/` 里的真模型,
            //   **文件不在就退回原来那套灰盒几何**(与运行时 `World.PlaceModel` 同一条"纯增量"口径)。
            // v0.49(用户:"⑩植被白模不用再去下:图集就在本地那个 449MB 的 zip 里 …只提用到的几张接上即可")
            //   ⇒ 图集已经从 `_packs` 里提出来了(3 张 PNG 进 `Assets/Art/quaternius/stylized_nature/Textures/`),
            //     上一轮"无引用⇒内嵌"的判断错在哪、这次怎么按材质名配贴图,都写在 `VegTex` 的注释里。
            //     **改回 true 了,所以要跑一次菜单 ④ 才会进场景。**
            var veg = Group(T, "Vegetation");
            // ⚠ 这三件的落点现在 **按高度场采**:山包就立在椰子树后面,写死 y=0 的话那两棵会埋进坡里一米多。
            //   `VegPos` 是同一条约定的唯一出处(灰盒兜底那两条也跟着走,别只改一边)。
            var pa = VegPos(9.5f, 1.2f);
            var pb = VegPos(11.6f, 3.6f);
            var bu = VegPos(8.5f, -0.5f);      // v0.59:挪进草地(原来 (7.6,4.4) 落在营地沙地空地里)
            if (BakeVegetationModels &&
                !ArtProp(veg, "PalmA", "quaternius/stylized_nature/PalmTree_2.fbx", pa, 5.2f, new Vector3(0f, 24f, 0f), VegTrunk, VegPalmLeaf))
                Palm(veg, "PalmA", pa, trunk, leaf);
            if (BakeVegetationModels &&
                !ArtProp(veg, "PalmB", "quaternius/stylized_nature/PalmTree_4.fbx", pb, 4.1f, new Vector3(0f, -37f, 0f), VegTrunk, VegPalmLeaf))
                Palm(veg, "PalmB", pb, trunk, leaf);
            if (BakeVegetationModels &&
                !ArtProp(veg, "Bush", "quaternius/stylized_nature/Bush_Large.fbx", bu, 1.6f, Vector3.zero, VegBushLeaf))
                Hull(veg, "Bush", bu + new Vector3(0f, 0.28f, 0f), new Vector3(1.5f, 0.55f, 1.5f), leaf, false);
            // 稀树林 + 草地(山包上那几棵与一丛丛的草,高度全部按 `IslandTerrain.Height` 采)
            if (BakeVegetationModels) Grove(veg);

            // 篝火:石圈 + 两根柴,火苗由运行时按"今晚有没有生火"生成
            var fire = Group(T, "Campfire");
            var fireAnchor = new GameObject("FireAnchor");
            fireAnchor.transform.SetParent(T, false);
            fireAnchor.transform.position = new Vector3(0f, 0f, 1.2f);
            // v0.49:石圈与柴堆**不再摆** —— 运行时那一格已经有 `Bonfire`/`Bonfire_Fire` 真模型(它自带石圈),
            //   两套叠在一起就是截图里那种"火堆周围一圈小方块"。夜里那块 Ø1.9 的可点地面照旧在,
            //   所以"营地火堆位"这个信息并没有丢(红线 60:那块落点不能被任何模型压小)。
            if (BakeFirePitRocks)
            {
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
            // v0.57:**这一块跟着水线走**(它是"海"这个信息的位置,不是道具格里那 24 件之一)。
            //   旧位置 (0, 0.06, -3.4) 是按"海在沙滩后面那条带里"量的;岛成形之后那一点在岛 **正中央**(离浪线约 15 米),
            //   再留着就是一块立在干沙上的半透明蓝板 —— 所以我挪了它,并且把它钉在 `IslandTerrain.WaterR` 上:
            //   中心落在浪线外 1.5 米,运行时那块 8×3 的板正好盖住"水刚开始"的那一圈(它的近边 = 浪线外侧)。
            //   ⚠ 代价说清楚:固定镜头里它现在很小(31 米外,画面靠上那一条),夜里点它比原来费眼力 ——
            //     这是"大圆岛 + 现有东西全不动"这两条凑在一起必然的结果,不是我另外选的路。
            seaA.transform.position = new Vector3(IslandTerrain.Center.x, IslandTerrain.SeaY + 0.06f,
                                                  IslandTerrain.Center.y - IslandTerrain.WaterR - 1.5f);

            // v0.58(用户:"**物品放在沙滩上,沙滩上最好有一些贝类/海带等物品**")
            //   ⇒ 装饰物 **在营地与锚点都摆好之后再撒**,因为它要避开这些落点(下面那份 keepOut 名单)。
            var keepOut = new List<Transform>(slots);
            keepOut.Add(mateA.transform); keepOut.Add(gullA.transform); keepOut.Add(bonesA.transform);
            keepOut.Add(fireAnchor.transform); keepOut.Add(seaA.transform);
            Decor(T, keepOut, GreyboxMat("ShoreShell", new Color(0.93f, 0.88f, 0.79f)),
                        GreyboxMat("ShoreKelp", new Color(0.15f, 0.29f, 0.16f)));

            // 标牌:全部印在面上(天空/大海 朝镜头,沙滩/植被/篝火 朝上贴着沙)。
            // x 全部按 yaw=180 的镜像取号,让"屏幕左"对应世界 +x,与示例图一致。
            // ⚠ v0.57:**五块牌子的宿主统一挂在 `Backdrop` 上,`localPos` 直接写世界坐标**。
            //   以前挂在那些 **被缩放过的板** 上(Sky 40×20、Sand 40×17),而 `World.LabelAt` 是挂成子物体的
            //   ⇒ 文字会跟着宿主一起被放大 40 倍。这一组牌子从 v0.49 起就是关着的(`IslandStage.GroundSignsVisible`),
            //   所以那条一直没人看见;而这一轮换了几何体(岛是网格、海 160 米),不改宿主就等着哪天有人把开关拨回去。
            signs.Add(new CabinSign
            {
                text = "天空",
                host = backdrop,
                localPos = new Vector3(0f, 14f, IslandTerrain.Center.y - 210f),
                facingLocal = Vector3.forward,
                size = 0.4f,
                color = new Color(0.16f, 0.28f, 0.4f),
            });
            signs.Add(new CabinSign
            {
                text = "大海",
                host = backdrop,
                localPos = new Vector3(IslandTerrain.Center.x, 0.35f, IslandTerrain.Center.y - IslandTerrain.WaterR - 3.5f),
                facingLocal = Vector3.up,
                size = 0.38f,
                color = new Color(0.82f, 0.92f, 1f),
            });
            signs.Add(new CabinSign
            {
                text = "沙滩",
                host = backdrop,
                localPos = new Vector3(-3f, 0.55f, 1.5f),
                facingLocal = Vector3.up,
                size = 0.36f,
                color = new Color(0.42f, 0.36f, 0.24f),
            });
            signs.Add(new CabinSign
            {
                text = "一些海边植被",
                host = backdrop,
                localPos = new Vector3(9.5f, 0.55f, 2.6f),
                facingLocal = Vector3.up,
                size = 0.33f,
                color = new Color(0.16f, 0.32f, 0.18f),
            });
            signs.Add(new CabinSign
            {
                text = "篝火",
                host = backdrop,
                localPos = new Vector3(0f, 0.55f, 2.1f),
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

        // v0.49(用户:"替换成真模型吧"):把 `Assets/Art/<rel>` 里的 FBX 直接摆进场景。
        //   量尺寸用 `World.WorldAABB`(与运行时同一个算法,不读 renderer.bounds),
        //   底面落到 pos 的高度、最长边收进 target;**文件不存在就返回 false,调用方退回灰盒几何**。
        //   Collider 一律拆掉:植被不该挡路,而荒岛层这些装饰物本来也没有鼠标交互。
        static bool ArtProp(Transform parent, string name, string rel, Vector3 pos, float target, Vector3 euler,
                            params string[] texRels)
        {
            var pref = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/" + rel);
            if (pref == null) return false;
            // 用普通克隆而不是 PrefabUtility.Instantiate:这些是场景装饰物,不需要保持 prefab 关联,
            // 而且 UnityEditor 的程序集拆分让 PrefabUtility 在离线编译那条路上解析不到。
            var go = UnityEngine.Object.Instantiate(pref);
            go.name = name;
            go.transform.SetParent(parent, false);
            Vector3 mn, mx;
            if (World.WorldAABB(pref, out mn, out mx))
            {
                var s = mx - mn;
                var m = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (m > 0.00001f) go.transform.localScale = pref.transform.localScale * (target / m);
            }
            // v0.60(用户:"**树倒了,要旋转90度**"):这批 Quaternius 植被 FBX 是 **Z-up 作者坐标** ——
            //   探针直接读 FBX 顶点表实测:树与草的 **根部在 z≈0、高度沿 +Z**(PalmTree_2 的 5.28 米在 z 上),
            //   而 Unity 按文件头当 Y-up 导入 ⇒ 进场景就是 **躺着的**。立起来 = 绕 X **-90°**(模型 +Z → 世界 +Y),
            //   你给的 yaw 再乘在它外面(先立后转;顺序反了树会歪着倒)。
            //   ⚠ 符号是照几何证据定的;烘完若看见"头朝下插进沙里",把 `VegUpFix` 的 -90 改成 +90(一个数)。
            go.transform.rotation = Quaternion.Euler(euler) * Quaternion.Euler(VegUpFix);
            if (World.WorldAABB(go, out mn, out mx))
            {
                var c = (mn + mx) * 0.5f;
                go.transform.position += new Vector3(pos.x - c.x, pos.y - mn.y, pos.z - c.z);
            }
            foreach (var col in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(col);
            if (texRels.Length > 0) VegTex(go, texRels);
            return true;
        }

        // v0.49(用户:"⑩植被白模不用再去下:图集就在本地那个 449MB 的 zip 里"):
        //   Quaternius 的 FBX **不内嵌贴图**,但它把贴图名写进了**材质名**里(实测这几个文件的材质就是
        //   `PalmTree_Trunk` / `PalmTree_Leaves` / `Bush_Leaves` / `Grass_Large`)⇒ 拿材质名去配图集最省事,
        //   也不用给模型改导入设置。每配上一个**打一行日志**:"一大片白几何体"就是这层没接上的症状,
        //   上一轮我凭"文件里没有贴图引用"就推断"贴图内嵌了",判断是错的,这次让它自己说话。
        //   ⚠ 叶冠那几张 PNG 带 alpha ⇒ 材质一律走 **Cutout**(不这么做透明那部分会成实心的一片黑);
        //     `Grass.png` 是不透明的(RGB,色型 2),草本身是实心几何体而不是面片,所以 Cutout 对它无副作用。
        static void VegTex(GameObject go, params string[] rels)
        {
            foreach (var rd in go.GetComponentsInChildren<Renderer>())
            {
                var shared = rd.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    var nm = shared[i].name;
                    string best = null; int bestScore = 0;
                    bool guessed = false;
                    foreach (var rel in rels)
                    {
                        var s = TexMatch(nm, System.IO.Path.GetFileNameWithoutExtension(rel));
                        if (s > bestScore) { bestScore = s; best = rel; }
                    }
                    if (best == null)
                    {
                        // **这具模型根本没带材质名**(实测 `Grass_Large.fbx` 的材质槽叫 `Default-Material` —— 那是
                        //   Unity 给"FBX 里没有材质"的兜底名,不是作者起的),名字自然谁也匹配不上。
                        //   ⇒ 只在 **调用方只给了一张候选图** 时才兜底用它:这块地只可能是那张图,不是瞎配。
                        //   给了两张(椰树 = 树干 + 叶冠)的时候仍然留白模并打日志 —— 那种情况下猜错就是"给树干刷叶子"。
                        if (rels.Length != 1 || !nm.StartsWith("Default", System.StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log("[植被] " + go.name + " 的材质 " + nm + " 谁都不像 ⇒ 留着白模(0 分比瞎配好)。");
                            continue;
                        }
                        best = rels[0];
                        guessed = true;                   // 这件是"靠唯一候选图兜底"配上的,最后一行要说清
                    }
                    var m = VegMat(best);
                    if (m == null) continue;
                    shared[i] = m;
                    Debug.Log("[植被] " + go.name + " 的材质 " + nm + " → " +
                              System.IO.Path.GetFileNameWithoutExtension(best) +
                              (guessed ? "(模型没有作者起的材质名,按唯一候选图配)" : ""));
                }
                rd.sharedMaterials = shared;
            }
        }

        // 材质名 ↔ 贴图名 的匹配分:整名相等 > 分词相等 > 分词互相包含,再加一条关键词加分
        //   (trunk/bark/wood 认树干,leaf/grass/bush/flower 认叶子)。
        // ⚠ **0 分就留白模,不瞎配**:上一轮我凭"包里没有贴图引用"推断"贴图内嵌了",判断是错的 ——
        //   这次宁可白一片,也不要给树干刷一层叶子还看不出来。
        static int TexMatch(string matName, string texName)
        {
            var a = SplitName(matName);
            var b = SplitName(texName);
            int s = 0;
            foreach (var t in a)
                foreach (var u in b)
                {
                    if (t == u) s += 2;
                    else if (t.Contains(u) || u.Contains(t)) s += 1;
                }
            if (matName.ToLowerInvariant() == texName.ToLowerInvariant()) s += 5;
            var m = matName.ToLowerInvariant();
            var k = texName.ToLowerInvariant();
            bool trunkish = m.Contains("trunk") || m.Contains("bark") || m.Contains("wood");
            bool leafy = m.Contains("leaf") || m.Contains("grass") || m.Contains("bush") || m.Contains("flower");
            if (trunkish && (k.Contains("trunk") || k.Contains("bark"))) s += 3;
            if (leafy && (k.Contains("leaf") || k.Contains("grass") || k.Contains("bush"))) s += 3;
            if (trunkish && leafy) s -= 2;
            return s;
        }

        static string[] SplitName(string s)
        {
            return s.ToLowerInvariant().Split(new[] { '_', '-', '.', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        static readonly Dictionary<string, Material> _vegMats = new Dictionary<string, Material>();

        static Material VegMat(string rel)
        {
            Material m;
            if (_vegMats.TryGetValue(rel, out m)) return m;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + rel);
            if (tex == null) { Debug.LogError("[植被] 贴图不在库里:" + rel + "(先从 _packs 里把那张 PNG 提进 Assets/Art/…)"); return null; }
            var dir = rel.Substring(0, rel.LastIndexOf('/') + 1);
            var pack = dir.EndsWith("/Textures/") ? dir.Substring(0, dir.Length - "/Textures/".Length + 1) : dir;
            var folder = "Assets/" + pack + "Mats";
            EnsureFolder(folder);
            var path = folder + "/M_" + tex.name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                m.mainTexture = tex;
                m.SetFloat("_Mode", 1f);                        // 1 = Cutout
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Glossiness", 0.04f);               // 植物不要高光,不然白天像塑料
                // ⚠ 只设 `_Mode` 是**菜单里那个下拉框**的行为,shader 走不走镂空分支看**关键字**;
                //   脚本建材质时没人帮你点,得自己写上。漏了它 = 叶冠糊成一整片黑。
                m.shaderKeywords = new[] { "_ALPHATEST_ON" };
                // ⚠ 同理:镂空那一档 belongs 在 Geometry 队列(2450)。新建材质的 renderQueue 还是 -1(跟 shader 默认),
                //   Unity 会打一条 "Render queue value outside of the allowed range (2450 - 2500) … resetting to default"
                //   然后自己改回去 —— 写死成它要改的那个值,这条提示就没声音了,而我们也确切知道排在哪儿。
                m.renderQueue = 2450;
                AssetDatabase.CreateAsset(m, path);
                Debug.Log("[植被] 建材质 " + path + "(" + tex.name + ",Cutout)");
            }
            // 已经建过的那几张(这次之前留下的)补齐同一个队列 —— 不然要人手删资产才会消掉那条提示。
            else if (m.renderQueue != 2450)
            {
                m.renderQueue = 2450;
                EditorUtility.SetDirty(m);
                Debug.Log("[植被] " + path + " 的 renderQueue 补成 2450(镂空那一档该在的位置)");
            }
            _vegMats[rel] = m;
            return m;
        }

        // ── 小山包:一张按 `IslandTerrain.Height` 抬起来的三角网 ────────────────────
        //   低多边形那种"一面一面"的平切 = **顶点不共享**(共享了 `RecalculateNormals` 会给你糊成平滑果冻)。
        //   v0.60:**山包整块只吃草地材质** —— 沙/草的分界不再靠"每个格子选材质"(那条路边界是 0.9 米的方块台阶,
        //     用户原话"**不要棱棱角角的**"),改由一张边界平滑的草地贴片 `GrassPatch` 负责(见下)。
        //     山包露出沙面的部分全在草地之内,埋在沙面以下的裙边反正看不见 ⇒ 单一材质不会穿帮。
        //   ⚠ 网格要 `CreateAsset` 存成资产,场景里的 MeshFilter 才有得引用;顺手上了 MeshCollider ——
        //     将来第一人称(§11-84)要能走上去,这一步现在就把路铺好,而它不影响点选那一套(山包上没有落点)。
        // v0.59:格子跟着那个 1.1 米的缓包收小(包心 (9,-2)、3σ=9 ⇒ 四边各留到边界处高度 <1 毫米),
        //   所以 **网格边界不再有断面**(v0.57 记的那笔旧账到此结掉)。
        const float HillX0 = 0f, HillX1 = 18f, HillZ0 = -11f, HillZ1 = 7f, HillStep = 0.9f;

        static void Hill(Transform parent, Material grassMat)
        {
            int nx = Mathf.CeilToInt((HillX1 - HillX0) / HillStep);
            int nz = Mathf.CeilToInt((HillZ1 - HillZ0) / HillStep);
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x0 = HillX0 + ix * HillStep, x1 = Mathf.Min(HillX0 + (ix + 1) * HillStep, HillX1);
                    float z0 = HillZ0 + iz * HillStep, z1 = Mathf.Min(HillZ0 + (iz + 1) * HillStep, HillZ1);
                    Quad(v, tri, new[] { new Vector3(x0, Top(x0, z0), z0), new Vector3(x1, Top(x1, z0), z0),
                                            new Vector3(x1, Top(x1, z1), z1), new Vector3(x0, Top(x0, z1), z1) });
                }

            var mesh = new Mesh { name = "IslandHill" };
            mesh.SetVertices(v);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // 自己验一遍绕序:法向平均必须朝上。朝下 = 整个山包背对相机 = 屏幕上"一块空地",
            // 而它不报错、不进自检,是最难查的那一类(与 UI 那个"负尺寸矩形"同一类事故)。
            float sum = 0f;
            foreach (var n in mesh.normals) sum += n.y;
            if (sum < 0f) Debug.LogError("[地形] IslandHill 的绕序反了(法向平均朝下)⇒ 山包会整个看不见。");

            EnsureFolder("Assets/Art/generated");
            const string path = "Assets/Art/generated/IslandHill.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("Hill");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = grassMat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Debug.Log("[地形] 小山包:" + mesh.triangles.Length / 3 + " 个三角,最高点 " +
                      HeightAtGrid().ToString("F2") + " 米(网格已存成 " + path + ")");
        }

        // ── v0.57 岛本体:一张按 `IslandTerrain.SurfaceY` 烘的实心圆面 ─────────────────
        //   为什么是网格而不是一块板:他要的是"荒岛是一个岛,周围全是海" ⇒ 岛面必须 **自己入水**
        //   (岛心平沙 0.02 → 出了 `FlatR` 沿沙滩斜降到 `BeachDeepY`),一块水平的板做不到这件事。
        //   三个口径记在这里:
        //   ① **形状只有一个出处** = `IslandTerrain.SurfaceY`;这张网、自由视角那面"不能下水"的墙、
        //      自检"每样东西都在岛上吗"问的都是同一个函数(见 `IslandTerrain` 顶部那条注释)。
        //   ② 半径铺到 **水下 4 米**(`BeachR + 4`)⇒ 不用做侧壁:海面那块板(`SeaY`)会把它整个切在水下,
        //      玩家能看到的是"沙滩斜着走进海里",看不到岛的边。
        //   ③ **不给 MeshCollider**:走路是解析采样(`IslandTerrain.GroundY`),没有刚体要落在它上面;
        //      而整张岛面一旦有了碰撞体,`OnMouseUpAsButton` 那条射线就可能被地面抢先命中 —— 白加一层风险。
        //   绕序自检与 `Hill` 同一条(法向平均必须朝上;反了不报错、不进自检,是最难查的那一类)。
        //   ⚠ 这条路径 `DemoChecks.V057()` 也在读(它靠"网格存不存在"判断场景烘过没烘过)⇒ 提到外面当唯一名字。
        public const string IslandDiscAsset = "Assets/Art/generated/IslandDisc.asset";

        static GameObject IslandDisc(Transform parent, Material sandMat)
        {
            const float R = IslandTerrain.BeachR + 4f;
            const int Rings = 22, Sectors = 56;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < Rings; i++)
            {
                float r0 = R * i / Rings, r1 = R * (i + 1) / Rings;
                for (int s = 0; s < Sectors; s++)
                {
                    float a0 = Mathf.PI * 2f * s / Sectors, a1 = Mathf.PI * 2f * (s + 1) / Sectors;
                    // 极坐标 (r, a) 到 (x, z) 的行列式 = r > 0 ⇒ 保向,所以 `Quad` 那套绕序直接可用
                    Quad(v, tri, new[] { Ring(r0, a0), Ring(r1, a0), Ring(r1, a1), Ring(r0, a1) });
                }
            }
            var mesh = new Mesh { name = "IslandDisc" };
            mesh.SetVertices(v);
            mesh.subMeshCount = 1;              // v0.60:整张岛面就是沙;草地是另一张贴上去的网(见 `GrassPatch`)
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            float sum = 0f;
            foreach (var n in mesh.normals) sum += n.y;
            if (sum < 0f) Debug.LogError("[地形] IslandDisc 的绕序反了(法向平均朝下)⇒ 整座岛会看不见。");

            EnsureFolder("Assets/Art/generated");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(IslandDiscAsset) != null) AssetDatabase.DeleteAsset(IslandDiscAsset);
            AssetDatabase.CreateAsset(mesh, IslandDiscAsset);

            var go = new GameObject("Sand");     // 名字沿用旧的那块板:Scene 视图里找"沙滩"还是找它
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = sandMat;
            Debug.Log("[地形] 岛:半径 " + R.ToString("F1") + " 米的实心圆面(全沙)," + mesh.triangles.Length / 3 +
                      " 个三角(浪线 r=" + IslandTerrain.WaterR.ToString("F2") + ",沙滩到 r=" +
                      IslandTerrain.BeachR + " 后入水到 " + IslandTerrain.BeachDeepY + " 米)。网格存成 " + IslandDiscAsset);
            return go;
        }

        // ── v0.60(用户:"**草坪边缘能线性化吗,不要棱棱角角的**")草地贴片 ─────────────
        //   v0.58/v0.59 的草地是"每个 0.9 米格子二选一材质"⇒ 边界是 **0.9 米的台阶**(弦高差到 0.9 米),
        //   这就是他说的"棱棱角角"。改法:**草地单独一张网**,边界直接取 `OnGrass` 那两条解析曲线
        //   (外沿的摆动圆 + 营地挖空圆)按 128 个扇区采样 ⇒ 边界偏差 = 弦高 ≈ 6 毫米,肉眼是 **一条滑的曲线**。
        //   高度取 `max(岛面, 山包面) + 4 毫米`:平地上比沙面高 4 毫米、坡上比山包高 4 毫米,
        //   两层同色相接处是一条等高线,看不出缝;4 毫米在 30 米外是亚像素,不会闪也不会看见"浮着一层"。
        //   ⚠ **不给 MeshCollider**:它只是颜色层,走路采的是 `IslandTerrain.GroundY`(山包那张网的 collider 在它下面 4 毫米)。
        public const string GrassPatchAsset = "Assets/Art/generated/IslandGrass.asset";

        static void GrassPatch(Transform parent, Material grassMat)
        {
            const int Sectors = 128, Steps = 12;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int s = 0; s < Sectors; s++)
            {
                float a0 = Mathf.PI * 2f * s / Sectors, a1 = Mathf.PI * 2f * (s + 1) / Sectors;
                float o0 = GrassOuter(a0), o1 = GrassOuter(a1);
                float i0 = GrassInner(a0), i1 = GrassInner(a1);
                for (int k = 0; k < Steps; k++)
                {
                    float t0 = (float)k / Steps, t1 = (float)(k + 1) / Steps;
                    // 角序与 `Quad` 的约定一致:(r0,a0)(r1,a0)(r1,a1)(r0,a1) —— 反了绕序自检会响
                    Quad(v, tri, new[] { Patch(i0 + (o0 - i0) * t0, a0), Patch(i0 + (o0 - i0) * t1, a0),
                                         Patch(i1 + (o1 - i1) * t1, a1), Patch(i1 + (o1 - i1) * t0, a1) });
                }
            }
            var mesh = new Mesh { name = "IslandGrass" };
            mesh.SetVertices(v);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            float sum = 0f;
            foreach (var n in mesh.normals) sum += n.y;
            if (sum < 0f) Debug.LogError("[地形] IslandGrass 的绕序反了(法向平均朝下)⇒ 草地会整个看不见。");

            EnsureFolder("Assets/Art/generated");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(GrassPatchAsset) != null) AssetDatabase.DeleteAsset(GrassPatchAsset);
            AssetDatabase.CreateAsset(mesh, GrassPatchAsset);

            var go = new GameObject("GrassPatch");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = grassMat;
            Debug.Log("[地形] 草地贴片:" + mesh.triangles.Length / 3 + " 个三角,边界 128 段采样(弦高 ≈6 毫米)" +
                      " —— v0.58 那版 0.9 米台阶到此为止。网格存成 " + GrassPatchAsset);
        }

        // 外沿 = `IslandTerrain.GrassBoundaryR`(同一条曲线,不写第二份数)
        static float GrassOuter(float a) { return IslandTerrain.GrassBoundaryR(a); }

        // 内沿 = 营地挖空圆:从岛心出发的射线与它的 **远交点**(射线不碰到挖空圆时内沿 = 0)
        static float GrassInner(float a)
        {
            var o = IslandTerrain.Center;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var oc = o - IslandTerrain.CampClear;
            float b = Vector2.Dot(d, oc), c = oc.sqrMagnitude - IslandTerrain.CampClearR * IslandTerrain.CampClearR;
            float disc = b * b - c;
            if (disc <= 0f) return 0f;
            float t = -b + Mathf.Sqrt(disc);
            return t > 0f ? t : 0f;
        }

        // 贴片顶点:高度 = max(岛面, 山包面) + 4 毫米(见 `GrassPatch` 顶部那笔账)
        static Vector3 Patch(float r, float a)
        {
            float x = IslandTerrain.Center.x + Mathf.Cos(a) * r;
            float z = IslandTerrain.Center.y + Mathf.Sin(a) * r;
            float y = Mathf.Max(IslandTerrain.SurfaceY(x, z), Top(x, z)) + 0.004f;
            return new Vector3(x, y, z);
        }

        static Vector3 Ring(float r, float a)
        {
            return new Vector3(IslandTerrain.Center.x + Mathf.Cos(a) * r,
                               IslandTerrain.SurfaceY(IslandTerrain.Center.x + Mathf.Cos(a) * r,
                                                      IslandTerrain.Center.y + Mathf.Sin(a) * r),
                               IslandTerrain.Center.y + Mathf.Sin(a) * r);
        }

        // 网格顶点的高度:整体压低 5 厘米,让平的裙边 **埋进沙滩那块板里**
        // (沙滩顶面在 y=0.02,共面就会 z-fighting,闪成一片花)
        static float Top(float x, float z) { return Mathf.Max(0f, IslandTerrain.Height(x, z)) - 0.05f; }

        static float HeightAtGrid()
        {
            float m = 0f;
            for (float x = HillX0; x <= HillX1; x += HillStep)
                for (float z = HillZ0; z <= HillZ1; z += HillStep) m = Mathf.Max(m, Top(x, z) + 0.05f);
            return m;
        }

        static void Quad(List<Vector3> v, List<int> tri, Vector3[] c)
        {
            int i = v.Count;
            // 绕序照 Unity 自己那块 Quad 的三角形顺序搬过来的(它的顶点序是 左下/右下/左上/右上,
            // 三角形是 [0,2,1] 与 [2,3,1]),映射到地面 (x,z) 就是下面这两条。反了就看上一行那个自检。
            v.Add(c[0]); v.Add(c[3]); v.Add(c[1]);
            v.Add(c[3]); v.Add(c[2]); v.Add(c[1]);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i + 3); tri.Add(i + 4); tri.Add(i + 5);
        }

        // 植被的地面点:山包上就按高度场抬,平地仍是 y≈0。
        //   ⚠ `-0.045` 不是随手写的:网格整体埋在沙滩顶面 **下** 5 厘米(`Top`),所以植被要比网格面高一点、
        //     又比沙面低一点 —— 落在中间,脚底既不悬空也不扎穿。
        static Vector3 VegPos(float x, float z)
        {
            return new Vector3(x, IslandTerrain.Height(x, z) - 0.045f, z);
        }

        // 稀树林 + 草地:树与草都 **按 `IslandTerrain.Height` 找自己的地面高度**,所以挪山包它们跟着走。
        //   ⚠ 位置是写死的定点(不是随机)⇒ 每次重烘一模一样;你在 Scene 视图里挪过的会被覆盖。
        static void Grove(Transform veg)
        {
            // 屏幕左 = 世界 +x。四棵绕着山包铺开(两棵在坡上、两棵在山脚),高度/朝向各差一点才不像复制粘贴
            var trees = new float[,]
            {
                { 11.0f, 3.4f, 4.6f, 18f }, { 15.4f, 1.2f, 5.4f, -42f },
                { 18.2f, -2.6f, 4.2f, 96f }, { 12.6f, -1.4f, 5.0f, -15f }
            };
            for (int i = 0; i < trees.GetLength(0); i++)
            {
                float x = trees[i, 0], z = trees[i, 1], h = trees[i, 2], yaw = trees[i, 3];
                ArtProp(veg, "Palm" + (char)('C' + i), "quaternius/stylized_nature/PalmTree_" + (3 + i % 2) + ".fbx",
                        VegPos(x, z), h, new Vector3(0f, yaw, 0f), VegTrunk, VegPalmLeaf);
            }
            // 草:定点撒(用 i 的函数,不用 Random ⇒ 重烘稳定),只落在 **草地圆之内** 且不陡的地方
            //   (v0.58:判据从"海拔高于 `GrassFrom`"换成 `OnGrass` —— 分区现在按位置不按高度,见 `IslandTerrain`)
            int n = 0;
            for (int i = 0; i < 40 && n < 12; i++)
            {
                float x = 8.5f + (i % 8) * 1.55f + (i / 8) * 0.6f;
                float z = 4.2f - (i / 8) * 1.5f + (i % 5) * 0.35f;
                if (!IslandTerrain.OnGrass(x, z) || !IslandTerrain.Placeable(x, z)) continue;
                ArtProp(veg, "Grass" + i, "quaternius/stylized_nature/Grass_Large.fbx",
                        VegPos(x, z), 0.55f + (i % 3) * 0.12f, new Vector3(0f, i * 47f, 0f), VegGrass);
                n++;
            }
            // v0.61(用户:"**增加一下灌木植被的分布情况**"):v0.60 那 6 丛定点表换成 **撒点循环**(目标 16 丛)——
            //   黄金角绕岛心转、半径 9.5~15.5 米按 i 抖动(正好是"营地空地外沿到草地外沿"那一圈绿带),
            //   再逐条过滤:在草地内(`OnGrass`)/ 坡度够缓(`Placeable`)/ 离已摆的树与灌木 ≥2.2 米。
            //   ⇒ 分布匀、不聚堆、**不用 Random**(重烘一模一样,与草/贝壳同一口径);Scene 里手挪的会被覆盖。
            var placed = new List<Vector2>();
            for (int i = 0; i < trees.GetLength(0); i++) placed.Add(new Vector2(trees[i, 0], trees[i, 1]));
            int nb = 0;
            for (int i = 0; i < 240 && nb < 16; i++)
            {
                float a = i * 2.39996f;
                float r = 9.5f + ((i * 53) % 61) / 61f * 6.0f;
                float x = IslandTerrain.Center.x + Mathf.Cos(a) * r;
                float z = IslandTerrain.Center.y + Mathf.Sin(a) * r;
                if (!IslandTerrain.OnGrass(x, z) || !IslandTerrain.Placeable(x, z)) continue;
                bool clear = true;
                foreach (var p in placed)
                    if ((p - new Vector2(x, z)).magnitude < 2.2f) { clear = false; break; }
                if (!clear) continue;
                placed.Add(new Vector2(x, z));
                float h = 1.2f + ((i * 29) % 5) * 0.15f;
                if (BakeVegetationModels &&
                    ArtProp(veg, "BushG" + nb, "quaternius/stylized_nature/Bush_Large.fbx",
                            VegPos(x, z), h, new Vector3(0f, i * 47f, 0f), VegBushLeaf))
                { nb++; continue; }
                Hull(veg, "BushG" + nb, VegPos(x, z) + new Vector3(0f, h * 0.35f, 0f),
                     new Vector3(h, h * 0.7f, h), VegMat(VegBushLeaf), false);
                nb++;
            }
            Debug.Log("[地形] 稀树林 4 棵 + 灌木 " + nb + " 丛 + 草 " + n + " 丛(植被只落在草地之内(外沿 r=" +
                      IslandTerrain.GrassOuterR + "、营地挖空 r=" + IslandTerrain.CampClearR + ")且坡度够缓的地方)。");
        }

        // ── v0.58 沙滩装饰:贝壳与海带 ──────────────────────────────────────────────
        //   用户:"**沙滩上最好有一些贝类/海带等物品**",而他这一轮把身份定成「**纯装饰,不可点**」⇒
        //   三条口径:① **不带 collider**(`Hull` 默认就拆、球那颗也手动拆)—— 它既不该抢走一块可点面,
        //              也不该挡住身后那一格道具的悬浮(红线 60 那一类:落点不能被任何模型压小);
        //            ② **定点撒**(黄金角 + i 的函数,**不用 Random**)⇒ 每次重烘一模一样,与 `Grove` 同一口径,
        //              你在 Scene 视图里挪过的会被覆盖;
        //            ③ 落点判据只用现成的两条:`!OnGrass`(只在沙上)+ 离每个既有落点 ≥1.1 米。
        //   ⚠ 以后要"能捡",那 **不是加个 collider** 就完事:要走 §5.1 物品表 + 占几格 + 能不能吃/算不算材料,
        //     那些数值必须他逐条给 —— 我不替他填(与"渔网占 2 格"那轮同一个道理)。
        static void Decor(Transform parent, List<Transform> keepOut, Material shellMat, Material kelpMat)
        {
            var g = Group(parent, "ShoreDecor");
            int shells = 0, kelps = 0;
            for (int i = 0; i < 120 && (shells < 16 || kelps < 9); i++)
            {
                float a = i * 2.39996f;                                   // 黄金角:不聚堆、不规律得看得出来是撒的
                float t = ((i * 37) % 100) / 100f;
                float r = 18.0f + t * 1.4f;                              // v0.60:草地外沿摆动最大到 17.85 ⇒ 从 18 起撒,
                                                                         //   贝类不会长在草里(`V058()` ④ 也钉着这条)
                float x = IslandTerrain.Center.x + Mathf.Cos(a) * r;
                float z = IslandTerrain.Center.y + Mathf.Sin(a) * r;
                if (IslandTerrain.OnGrass(x, z)) continue;                 // 只摆沙滩,草地留给草和树
                bool clear = true;
                foreach (var k in keepOut)
                    if (k != null && new Vector2(k.position.x - x, k.position.z - z).magnitude < 1.1f)
                    { clear = false; break; }
                if (!clear) continue;
                var at = VegPos(x, z);                                     // 与植被同一条"贴地"约定(山包尾巴压过来也不会悬空)
                if (i % 3 == 0)
                {
                    for (int s = 0; s < 3; s++)                            // 海带 = 两三片压扁的长条交叉叠着
                        Hull(g, "Kelp" + kelps + "_" + s, at + new Vector3(0f, 0.02f + s * 0.008f, 0f),
                             new Vector3(0.10f, 0.02f, 0.95f + (s % 2) * 0.3f), kelpMat, false,
                             new Vector3(0f, i * 41f + s * 37f, 0f));
                    kelps++;
                }
                else
                {
                    var sh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sh.name = "Shell" + shells;
                    sh.transform.SetParent(g, false);
                    sh.transform.position = at + new Vector3(0f, 0.02f, 0f);
                    sh.transform.localScale = new Vector3(0.30f, 0.11f, 0.30f);
                    sh.transform.rotation = Quaternion.Euler(0f, i * 53f, 0f);
                    sh.GetComponent<Renderer>().sharedMaterial = shellMat;
                    UnityEngine.Object.DestroyImmediate(sh.GetComponent<Collider>());
                    shells++;
                }
            }
            Debug.Log("[地形] 沙滩装饰:贝壳 " + shells + " 枚 + 海带 " + kelps + " 撮" +
                      "(纯装饰:不带 collider、不进仓库、不参与任何判定)。");
        }

        // v0.49:篝火位的石圈与柴堆不再烘进场景 —— 运行时那一格已经有 `Bonfire`/`Bonfire_Fire` 真模型
        //   (它自带石圈),两套叠在一起就是截图里"火堆周围一圈小方块"。想退回灰盒对照就把这个改成 true。
        static readonly bool BakeFirePitRocks = false;

        // v0.49(用户:"⑩植被白模不用再去下:图集就在本地那个 449MB 的 zip 里(43 张 PNG,去重约 145MB)
        //   —— 上一轮我"只提 .fbx"才导致白模。只提用到的几张接上即可。")
        //   ⇒ 三张贴图已提进 `Assets/Art/quaternius/stylized_nature/Textures/`(合计 0.6MB,原始 449MB 的包照旧留在 `_packs/` 里不进 git)。
        //   **改回 true 了 ⇒ 要跑一次菜单 ④ 才会进场景。**
        static readonly bool BakeVegetationModels = true;

        const string VegTrunk = "Art/quaternius/stylized_nature/Textures/PalmTree_Trunk.png";
        const string VegPalmLeaf = "Art/quaternius/stylized_nature/Textures/PalmTree_Leaves.png";
        const string VegBushLeaf = "Art/quaternius/stylized_nature/Textures/Bush_Leaves.png";
        const string VegGrass = "Art/quaternius/stylized_nature/Textures/Grass.png";
        // v0.60:植被 FBX 的"立起来"补偿(绕 X -90°)。理由与实测证据写在 `ArtProp` 里;要改符号只改这一处。
        static readonly Vector3 VegUpFix = new Vector3(-90f, 0f, 0f);

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

        // ==== v0.69(用户:"**所有概率文案都隐藏**")====
        // 玩家看得见的文本有 **三个出处在资产里**:夜晚选项按钮括号那句 = `GameChoiceSO.resultHint`、
        //   夜晚屏的事件 = `GameEventSO.description`、收集页右栏的彩蛋说明 = `ItemSO.desc`。
        // **这张表是这三处文字的唯一出处**:`P("id")` 在调用点取它、菜单 ① 的迁移按 **资产文件名** 刷它
        //   ⇒ 既不会出现"改了代码字面量、盘上还是旧文案"(§7.2 那条"已有的不覆盖"),也不会有两份不同措辞。
        // ⚠ **键 = 资产文件名**:`Ch(事件, 选项key, …)` 落成 `<事件key>_<选项key>.asset`(与 `Tab()` 用的同一个名字),
        //   事件与物品就是它们自己的 key。表里没有的条目 ⇒ 用调用点传进来的那段文字。
        // ⚠ **不在这张表里的 `desc`(行动 / 配方 / 非彩蛋物品的说明)全工程没有读者** ⇒ 那是给 Inspector 看的
        //   设计文档,里面写概率是写给做设计的人看的,不上屏(v0.66 那条"只摘玩家看得见的"就按这条边界执行)。
        internal static readonly Dictionary<string, string> PlayerText = new Dictionary<string, string>
        {
            // —— 夜晚选项按钮上那一句(18 条;代价与收益的数字都留着,摘掉的只有"机会有多大")——
            { "bloodmoon_dive",         "有机会捞回 1~3 份罐头,运气好还能摸到藏宝图。代价:次日体力上限 -1(领航员免)" },
            { "fakemate_flashlight",    "照下去才分得清真伪;那确实是个病人的话,队友必定进入生病。用掉 1 格电" },
            { "fishschool_hand",        "3 份罐头;鱼叉照常会坏 —— 用得越多越容易坏" },
            { "fishschool_net",         "2 体力 → 3 份罐头;渔网会坏,而且它比 鱼叉 更容易坏" },
            { "ghostship_flare",        "→ 结局H·幽灵船的乘客(土制弹可能哑火 = 错过)" },
            { "ghostship_flashlight",   "照得着就上船(结局H);照不到它天亮自己走,且永久不再来" },
            { "monkeynaughty_spear",    "被抢那件完好回来,+1 份罐头;鱼叉照常会坏" },
            { "palmtree_take",          "获得 1 个椰子;可能被砸下来的那颗 -1 生命" },
            { "rain_light_umbrella",    "免于生病;伞有可能因此坏掉" },
            { "rain_heavy_umbrella",    "免于生病;伞多半会坏掉" },
            { "rain_storm_umbrella",    "免于生病;伞必坏,还搭上次日体力 -1" },
            { "searchplane_flare",      "消耗 1 发(土制弹可能哑火)→ 第一发;第二发等它下一次从轮空池里轮上来(v0.27)" },
            { "searchplane_flashlight", "照得着就把人换回来(结局A);用掉 1 格电" },
            { "ship_flare",             "单阶段:1 发即结局A(土制弹可能哑火 = 错过)" },
            { "ship_flashlight",        "照得着就走(结局A);落空 = 错过,这条船永久不再来" },
            { "smallshadow_fire",       "队友不一定撑得住;火本次必熄" },
            { "smallshadow_watch",      "队友必定存活;次日体力 -2" },
            { "snake_swat",             "窗口很短,赶得上才打得走" },
            // —— 夜晚屏那句事件描述 ——
            { "rain",                   "云压得很低。" },     // 原来后面还挂着"内部按 3:2:1 掷档"= 机制,不给玩家看
            // —— 收集页右栏的彩蛋说明(10 条;"整局最多一次"这种次数上限不是概率,留着)——
            { "badge",      "影怪之夜的第二天清晨,它可能被冲上来(整局最多一次;那一晚如果直接献宝结束就没有它)。正面是海事保险公司的标。" },
            { "boarding",   "探索荒岛的杂物(整局最多一次)。日期就是起飞那天,座位号对应着你身边那把空吊床。" },
            { "claim",      "钓鱼捞上来的杂物(整局最多一次)。编号 04/05/06,06 那行的名字被水洇开了;04 与 05 就是合影里站在飞机前那两个人。" },
            { "key",        "钓鱼或潜水都可能捞到它,两条都不需要藏宝图(v0.13)。没有它,宝箱打不开,也不给任何结局。" },
            { "luggage",    "钓鱼捞上来的杂物(整局最多一次)。一个不认识的姓,和同一个保险公司的理赔编号。" },
            { "map",        "只有血月夜下水、运气好才摸得到。v0.13 之后它只剩一个用途:探索刷出的那天必定带回藏宝箱。" },
            { "monkeygift", "友善的狐狸做成交易之后的第二天清晨,它可能被留下(整局最多一次)。它拿走一样、留下一样 —— 不像交易,像上供。" },
            { "raft",       "任一次涨潮之后的第二天清晨,它可能被冲上来(整局最多一次)。有人试过离开,而且没有走远。" },
            { "tally",      "跨局彩蛋:只有 这个存档已经掉进过 结局F 时,才可能在开局第一个清晨出现。每一笔是一次回环。" },
            { "twonames",   "任一队友永久消失之后,它可能出现(整局最多一次)。饥荒 / 自杀 / 小影怪 / 引开影怪 四条路都算。" },
        };

        static string P(string id)
        {
            string v; return PlayerText.TryGetValue(id, out v) ? v : "";
        }

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
