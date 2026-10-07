using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // 一切都来自场景资产:Assets/Scenes/Demo.unity 里有 GameRoot(挂着 db 资产引用)、
    // DemoCanvas(HUD / Modal 两层)、主相机、太阳、以及机舱灰盒 Cabin。
    // 这里不再有"按 Play 自动生成"的 RuntimeInitializeOnLoad —— 缺资产就直接报错提示去跑菜单。
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot I;

        [Header("资产引用(由 Tools/60slike 烘焙写进场景,可在 Inspector 里改)")]
        public Database db;
        public Camera cam;
        public RectTransform hud;       // 每个阶段自己重建,容器在场景里
        public RectTransform modal;     // 事件 / 结局 弹层
        public CabinAnchors cabin;      // 机舱灰盒:只有拾荒阶段打开它
        public IslandStage islandStage; // 荒岛灰盒:只有荒岛阶段打开它,主相机搬到它的镜头位姿
        public Light sun;               // 夜晚把那盏太阳压暗(不遮罩材质资产,所以不会污染 .mat)
        // ⚠ v0.50 删掉了 `public Font uiFont`:字体的唯一入口是 `Ui.BootFont()`(Resources 按名字取)。
        //   留着这个字段 = 留着"Inspector 里拖的那份会在 Awake 覆盖回来"这条第二真源。
        //   场景文件里那一格 `uiFont: {guid…}` 会变成孤儿数据,Unity 下次存场景时自己丢掉(不用手改 YAML)。

        // 局内状态不进场景文件:Awake 里 EnterTitle() 第一件事就是 new 一个,
        // 序列化进 Demo.unity 只会让那本账被编辑器的旧副本挡着
        [System.NonSerialized] public RunState state = new RunState();
        public IslandPhase island;      // 夜晚结算要回调白天的工具损坏账
        GameObject phaseGo;

        void Awake()
        {
            I = this;
            if (db == null || cam == null || hud == null || modal == null)
            {
                Debug.LogError("[60slike] 场景资产没接好:db / cam / hud / modal 有空引用。" +
                               "请先跑菜单 Tools/60slike/① 生成数据资产,再跑 ② 生成 Demo 场景,然后打开 Assets/Scenes/Demo.unity。");
                enabled = false;
                return;
            }
            if (cabin == null) cabin = FindObjectOfType<CabinAnchors>();
            if (islandStage == null) islandStage = FindObjectOfType<IslandStage>();
            if (sun == null) sun = FindObjectOfType<Light>();

            // 字体不在这里管了(v0.50):`Ui.BootFont` 用 Resources 按名字取,编辑器与打包同一条路。
            //   原来这个 Awake 里还有一句 `Ui.Use(uiFont)` —— 它是"第二真源",会把字体覆盖回 Inspector 上
            //   拖的那份(现在那一格拖的还是行楷),所以字段连同这句一起删掉。

            EnterTitle();
        }

        public void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
        }

        public void ClearHud() { Clear(hud); }
        public void ClearModal() { Clear(modal); }

        // v0.56(用户:"**队友页面的鼠标在第一人称模式下消失了**"):自由视角期间光标是锁住的,
        //   而 **任何一个弹层**(队友面板 / 制造面板 / 日记 / ≥2⚡ 确认窗 / Tab 行动面板)都要用鼠标点。
        //   这些弹层全都开在 `modal` 底下 ⇒ 判据只有这一条,不再一个一个列名单
        //   (我上一版就是只认了 Tab 面板那一个,所以队友面板一开光标就"消失"了 —— 名单式判据必漏)。
        public bool ModalOpen { get { return modal != null && modal.childCount > 0; } }

        void DestroyPhase()
        {
            if (phaseGo != null)
            {
                // 拾荒阶段把相机挂在玩家的 Eye 下面,而 Eye/Player 现在都在 phase 层级里:
                // 不先把相机搬出来,Destroy 会顺着层级把它一起拆掉,进荒岛就黑屏了
                if (cam != null && cam.transform.parent != transform) cam.transform.SetParent(transform, true);
                Destroy(phaseGo);
            }
            phaseGo = null;
            island = null;
        }

        T NewPhase<T>(string name) where T : MonoBehaviour
        {
            DestroyPhase();
            ClearHud();
            ClearModal();
            // 机舱灰盒是场景资产,不随阶段生灭:只有拾荒阶段把它打开。
            // 荒岛那一层同理 —— 两层都是场景里真实的 GameObject,靠开关切换,不再是一屏文字。
            if (cabin != null) cabin.gameObject.SetActive(typeof(T) == typeof(ScavengingPhase));
            if (islandStage != null) islandStage.gameObject.SetActive(typeof(T) == typeof(IslandPhase));
            phaseGo = new GameObject(name);
            return phaseGo.AddComponent<T>();
        }

        public void EnterTitle()
        {
            state = new RunState();
            state.phase = RunPhase.Title;
            Music.Stop();
            var p = NewPhase<TitlePhase>("TitlePhase");
            p.root = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void EnterScavenging(string name)
        {
            state.playerName = SanitizeName(name);
            state.phase = RunPhase.Scavenging;
            var p = NewPhase<ScavengingPhase>("ScavengingPhase");
            p.root = this;
        }

        public void EnterIsland()
        {
            state.phase = RunPhase.Island;
            var p = NewPhase<IslandPhase>("IslandPhase");
            p.root = this;
            island = p;
            ApplyIslandView("EnterIsland");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 把主相机交给荒岛那层的 CameraPose 管:挂成它的子物体,而不是"拷一次位姿"。
        // 挂上去之后,任何脚本或你在 Scene 视图里拖一下 gizmo,都不可能把镜头留在荒岛外面。
        public void ApplyIslandView(string why)
        {
            // v0.53(§11-84 第一人称共存):**自由视角期间镜头归 `IslandWalk` 管,这里一条都不动**。
            //   不挡会怎样:`Refresh()` 每重画一次就调一次本函数,相机被一次次拽回 CameraPose ⇒ 人一走镜头就弹回去,
            //   正是 §11-84⑤ 点名的"这条每帧校正必须让位"。
            //   回固定视角那条路仍然是这里 —— `IslandWalk.Off()` 先关 `Active` 再调过来,所以顺序不会把自己挡死。
            if (IslandWalk.Active) return;
            if (islandStage == null || islandStage.cameraPose == null || cam == null)
            {
                Debug.LogError("[60slike] 荒岛镜头摆不出来:islandStage=" + (islandStage != null) +
                    " cameraPose=" + (islandStage != null && islandStage.cameraPose != null) +
                    " cam=" + (cam != null) + "。跑菜单 Tools/60slike/② 或 ④ 补建荒岛层。");
                return;
            }
            islandStage.gameObject.SetActive(true);
            bool moved = cam.transform.parent != islandStage.cameraPose;
            cam.transform.SetParent(islandStage.cameraPose, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            if (moved)
                Debug.Log("[相机] " + why + " → 挂到 " + islandStage.cameraPose.name + ",世界位 " +
                          cam.transform.position.ToString("F2") + " 俯仰/偏航 " + cam.transform.eulerAngles.ToString("F0") +
                          " | Island 激活=" + islandStage.gameObject.activeInHierarchy +
                          " | 落点 " + islandStage.propSlots.Count + " | 标牌 " + islandStage.signs.Count +
                          " | 背景色 " + cam.backgroundColor.ToString("F2"));
        }

        public void EndRun(EndingId id)
        {
            if (state.phase != RunPhase.Ended)
            {
                // v0.26:回环计数只读 F(Damage→EndRun 与清晨检查可能同帧各叫一次,只算一次)
                if (id == EndingId.F) SaveData.AddLoop();
                // v0.31:死掉那天不写日记 —— 未封口的当天那几行直接跟着这局一起丢掉,结局屏前不翻日记。
                // 主菜单「收集」页的两本账在这里落盘:结局 + 这一局拿到的彩蛋件
                SaveData.MarkEndingSeen(id.ToString());
                foreach (var it in state.hidden.loreFound) SaveData.MarkEggSeen(it.key);
                if (state.hidden.nameTagTaken) SaveData.MarkEggSeen(db.NameTag.key);
                if (state.hidden.hasMap) SaveData.MarkEggSeen(db.Map.key);
                if (state.hidden.hasKey) SaveData.MarkEggSeen(db.Key.key);
                if (state.hidden.hasChest) SaveData.MarkEggSeen(db.Chest.key);
                SaveData.Flush();
            }
            state.phase = RunPhase.Ended;
            state.ending = id;
            Music.Stop();       // v0.49:结局屏不该还响着"生存阶段"那首
            if (db.endings.ContainsKey(id)) state.endingSo = db.endings[id];
            if (id == EndingId.T && !SaveData.trueEndingClaimed) SaveData.SetTrueClaimed(true);
            var p = NewPhase<EndingPhase>("EndingPhase");
            p.root = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 相机已经挂在 CameraPose 底下了,这里只把"被谁拖走的局部值"归零。静默:每帧跑。
        public void KeepIslandView()
        {
            if (IslandWalk.Active) return;      // v0.53 §11-84⑤:自由视角期间不要每帧把相机按回 CameraPose
            if (islandStage == null || islandStage.cameraPose == null || cam == null) return;
            if (cam.transform.parent != islandStage.cameraPose) { ApplyIslandView("KeepIslandView"); return; }
            if (cam.transform.localPosition != Vector3.zero) cam.transform.localPosition = Vector3.zero;
            if (cam.transform.localRotation != Quaternion.identity) cam.transform.localRotation = Quaternion.identity;
        }

        // §12.6:名字输入的安全 = 长度上限 + 过滤 < > 与控制字符;渲染一律不用 richText
        //   v0.61:拆成两条 —— `StripName` 只做过滤/截断(**空串保持空**,设置页要能清空),
        //   `SanitizeName` = 过滤 + 兜默认名(开局那条路,规则一个字没动)。
        public static string StripName(string raw)
        {
            var sb = new System.Text.StringBuilder();
            if (raw != null)
                foreach (var ch in raw)
                {
                    if (ch == '<' || ch == '>') continue;
                    if (char.IsControl(ch)) continue;
                    if (sb.Length >= 8) break;
                    sb.Append(ch);
                }
            return sb.ToString().Trim();
        }

        public static string SanitizeName(string raw)
        {
            var s = StripName(raw);
            return string.IsNullOrEmpty(s) ? "Daylily" : s;
        }
    }

    // ---------- 标题 / 名字输入(§2.0) ----------
    public class TitlePhase : MonoBehaviour
    {
        public GameRoot root;

        void Start()
        {
            var rt = root.hud;
            Ui.Panel(rt, "bg", new Color(0.06f, 0.08f, 0.11f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Ui.Label(rt, "title", "六十秒 · 荒岛", 54, TextAnchor.MiddleCenter, Color.white,
                     new Vector2(0, 1), Vector2.one, new Vector2(0, -200), new Vector2(0, -90));
            // v0.61 文案收口:主屏不再摆"登记表/≤8 字"那套填表口吻(姓名进设置页),副标题只留一句话
            Ui.Label(rt, "sub", "空难之后的第六十秒,你落在了一座无人记得的岛上。",
                     18, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.85f),
                     new Vector2(0, 1), Vector2.one, new Vector2(0, -300), new Vector2(0, -220));

            // v0.61:姓名从主屏搬走 ⇒ 开局读 **设置里存的那个**(`SaveData.playerName`,空 = 没填过,兜默认名)
            Ui.Button(rt, "start", "登上飞机", 24, () => root.EnterScavenging(SaveData.playerName),
                      new Color(0.85f, 0.35f, 0.25f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                      new Vector2(-200, -420), new Vector2(200, -360));

            // v0.51(用户:"开始界面加一个'收集'页面,可以看到所有的已获得的彩蛋物品")
            //   ⇒ 页 **从 v0.30 就在**(`OpenCollection`:左栏 7 个结局、右栏 15 件彩蛋,拿到才显名字+说明全文),
            //     你看不见它的原因是那颗按钮的位置:它挂在底部锚点 `(0,0)` 上而 `offsetMin.y = -34`
            //     ⇒ **下半截在屏幕之外**(与 v0.32 那批"负尺寸矩形"是同一类事故:不报错、不进自检、Hierarchy 里看着正常)。
            //     修法是提上来当第二级主按钮,并且 **把进度写在按钮上** —— 一眼就知道里面有什么、还差多少。
            Ui.Button(rt, "collect", CollectLabel(), 20, OpenCollection,
                      new Color(0.34f, 0.28f, 0.46f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                      new Vector2(-250, -490), new Vector2(250, -440));

            // v0.61(用户:"**开始菜单把姓名,重置结局,日记日期显示均放在设置的子页面里,设置里还有其他的设置信息(音量,键位…)**")
            //   ⇒ 主屏只留 开始 / 收集 / 设置 三颗;姓名·日期格式·重置真结局·音量·键位 全进 `OpenSettings` 那一页。
            // v0.67(用户:"**署名页怎么做**")⇒ 主屏第四颗:**致谢**(素材许可里有两条义务 ——
            //   音乐 CC BY 4.0 要署名、字体 OFL 1.1 要随包带许可文本;前者必须出现在玩家能看到的界面上)。
            Ui.Button(rt, "settings", "设置", 20, OpenSettings,
                      new Color(0.22f, 0.26f, 0.32f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                      new Vector2(-250, -560), new Vector2(-10, -510));
            Ui.Button(rt, "credits", "致谢", 20, OpenCredits,
                      new Color(0.24f, 0.34f, 0.3f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                      new Vector2(10, -560), new Vector2(250, -510));
            // v0.61 文案收口:右下角那行 "demo:…| 开发文档 v0.x(尚未正式版本)" **删掉** —— 它是给我们看的调试戳,
            //   不是给玩家看的文案;版本号只留在开发文档里。
        }

        // ---- v0.61 设置子页 ----
        InputField setNameInput;
        UnityEngine.UI.Button setDateBtn, setVolBtn;
        Text claimedHint;

        static readonly float[] VolSteps = { 0f, 0.35f, 0.7f, 1f };
        static readonly string[] VolNames = { "关", "低", "中", "高" };

        static int VolIndex()
        {
            int best = 0; float bd = 9f;
            for (int i = 0; i < VolSteps.Length; i++)
            {
                float d = Mathf.Abs(VolSteps[i] - Music.Volume);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void OpenSettings()
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "box", new Color(0.12f, 0.14f, 0.18f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-360, -300), new Vector2(360, 300));
            Ui.Label(box, "title", "设置", 24, TextAnchor.UpperLeft, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -50), new Vector2(-20, -18));

            // 姓名(存进 SaveData,跨局记住;空 = 没填过,开局兜默认名)
            Ui.Label(box, "nl", "姓名(不超过 8 字;会出现在铭牌、合影与结局字幕上)", 15, TextAnchor.MiddleLeft,
                     new Color(0.75f, 0.8f, 0.85f), new Vector2(0, 1), new Vector2(1, 1),
                     new Vector2(20, -96), new Vector2(-20, -70));
            var fieldRt = Ui.Panel(box, "nameField", new Color(1, 1, 1, 0.12f), new Vector2(0, 1), new Vector2(0, 1),
                                   new Vector2(20, -140), new Vector2(300, -100));
            setNameInput = fieldRt.gameObject.AddComponent<InputField>();
            var text = Ui.Label(fieldRt, "text", "", 20, TextAnchor.MiddleLeft, Color.white,
                                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
            text.supportRichText = false;
            setNameInput.targetGraphic = fieldRt.GetComponent<Image>();
            setNameInput.textComponent = text;
            setNameInput.characterLimit = 8;
            setNameInput.text = SaveData.playerName;
            setNameInput.onValueChanged.AddListener(v =>
            {
                var s = GameRoot.StripName(v);          // 与开局同一条过滤,但 **空串保持空**(空 = 没填)
                if (s != v) setNameInput.text = s;
                SaveData.SetPlayerName(s);
            });

            setDateBtn = Ui.Button(box, "dateMode", "", 16, () => { SaveData.ToggleDateMode(); RefreshDateMode(); },
                                   new Color(1, 1, 1, 0.10f), new Vector2(0, 1), new Vector2(0, 1),
                                   new Vector2(20, -190), new Vector2(340, -150));
            RefreshDateMode();

            setVolBtn = Ui.Button(box, "volume", "", 16, () =>
            {
                Music.SetVolume(VolSteps[(VolIndex() + 1) % VolSteps.Length]);
                RefreshVolume();
            }, new Color(1, 1, 1, 0.10f), new Vector2(0, 1), new Vector2(0, 1),
               new Vector2(20, -240), new Vector2(340, -200));
            RefreshVolume();

            Ui.Button(box, "resetClaimed", "重置真结局记录", 16, () =>
            {
                SaveData.ResetTrueClaimed();
                RefreshClaimed();
            }, new Color(1, 1, 1, 0.10f), new Vector2(0, 1), new Vector2(0, 1),
              new Vector2(20, -290), new Vector2(340, -250));
            claimedHint = Ui.Label(box, "claimed", "", 14, TextAnchor.MiddleLeft, new Color(0.9f, 0.8f, 0.5f),
                                   new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -318), new Vector2(-20, -294));
            RefreshClaimed();

            // 键位:**只读说明表**。改绑是另一轮的活(移动/视角现在走 Unity 的 Axis 名,改绑要把输入换成按键轮询)
            Ui.Label(box, "keys",
                     "键位(暂不支持改绑)\n拾荒:W A S D 移动 · 鼠标 转视野 · 左键 锁定/放开光标 · E 拾取与使用 · Q 在舱门投递\n" +
                     "荒岛:鼠标 点选物体 · E 走近交互 · V 固定/自由视角 · 左 Shift 奔跑(自由视角) · Tab 行动面板 · Esc 关闭面板",
                     14, TextAnchor.UpperLeft, new Color(0.7f, 0.74f, 0.8f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -470), new Vector2(-20, -330));

            Ui.Button(box, "close", "返回", 16, () => root.ClearModal(), new Color(1, 1, 1, 0.12f),
                      new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-70, 16), new Vector2(70, 52));
        }

        void RefreshClaimed()
        {
            if (claimedHint == null) return;
            claimedHint.text = SaveData.trueEndingClaimed
                ? "此存档已达成真结局:献宝选项不再生成。"
                : "真结局每个存档仅可达成一次,可在此重置。";
        }

        void RefreshDateMode()
        {
            Ui.SetButtonText(setDateBtn, SaveData.dateMode == 0
                ? "日记日期格式:D12(点击切换为「第 12 天」)"
                : "日记日期格式:第 12 天(点击切换为 D12)");
        }

        void RefreshVolume()
        {
            Ui.SetButtonText(setVolBtn, "音乐音量:" + VolNames[VolIndex()] + "(点击切换)");
        }

        // §5.3 那 15 件彩蛋/暗线件 —— **唯一一份名单**。按钮上的进度与页里的列表都读它,
        //   不然加一件彩蛋就会有一处数字说谎(这条项目里已经栽过:写死的第二份清单从不自己更新)。
        //   ⚠ 它是 **static** 的:`DemoChecks` 也要读同一份来断言"名单里每件都是 lore 件"。
        public static ItemSO[] EggItems(Database db)
        {
            return new[] { db.NameTag, db.Map, db.Key, db.Chest, db.Scrap, db.Newspaper, db.Photo,
                           db.Luggage, db.Claim, db.Badge, db.Boarding, db.Tally, db.Raft,
                           db.TwoNames, db.MonkeyGift };
        }

        // 按钮上直接写进度(跨局累计,与页里那两栏同一本账)
        string CollectLabel()
        {
            var db = root.db;
            var eggs = EggItems(root.db);
            int got = 0, total = 0;
            for (int i = 0; i < eggs.Length; i++)
            {
                if (eggs[i] == null) continue;
                total++;
                if (SaveData.HasSeenEgg(eggs[i].key)) got++;
            }
            int eg = 0, et = 0;
            if (db.endingTable != null)
                for (int i = 0; i < db.endingTable.Count; i++)
                {
                    var e = db.endingTable[i];
                    if (e == null) continue;
                    et++;
                    if (SaveData.HasSeenEnding(e.id.ToString())) eg++;
                }
            return "收集:彩蛋 " + got + " / " + total + "   结局 " + eg + " / " + et;
        }

        // ---------------- 「左选右读」这一套(收集页 v0.66 + 致谢页 v0.67 共用一份实现) ----------------
        // v0.66(用户:"**彩蛋界面可以选择并查看详情,不要直接把描述和物品放一起(结局也这么处理)**")
        // v0.67(用户:"**署名页怎么做**")⇒ 这一页不只属于收集:左栏每行只写 **一个名字**,右栏出正文。
        //   ⚠ 一套 UI 两种内容,不是两处各写一遍(项目里"名单式判据/两份文案"栽过很多次)。
        class DetailRow
        {
            public string label;      // 左栏那一行写的(收集页没拿到的写 ???)
            public string head;       // 右栏标题
            public string body;       // 右栏正文
            public bool dim;          // 灰显 = 没有内容(没拿到 / 还没抵达)
            public string group;      // 非空且与上一行不同 ⇒ 在这行前插一个小标题(计数写在里面)
        }

        // 详情页那四块的位置都写成 **具名常量**,而不是散在调用里的字面量:
        //   v0.68 那次排版 bug(正文压住标题)之所以没被 `Ui` 的"负尺寸"警告抓到,是因为矩形方向是对的、
        //   只是 **上边放得太高** —— 这种"位置错但形状对"只有把上下边写成有名字的数,自检才能判顺序。
        public const float DetailRowTop = -78f;        // 左栏第一块(组标题)的上边
        public const float DetailRowPitch = 24f, DetailRowHeight = 22f;   // 行距 = 行高 + 缝
        public const float DetailHeadTop = -12f, DetailHeadBottom = -42f;
        public const float DetailBodyTop = -48f, DetailBodyBottom = -560f;
        public const float DetailPaneBottom = -670f;   // 右栏框的底;左栏最后一行不许越过它

        Text detailHead, detailBody;
        readonly System.Collections.Generic.List<Text> detailRowTexts = new System.Collections.Generic.List<Text>();
        readonly System.Collections.Generic.List<Image> detailRowBgs = new System.Collections.Generic.List<Image>();
        readonly System.Collections.Generic.List<DetailRow> detailRows = new System.Collections.Generic.List<DetailRow>();

        // 这一页 = 名单 + 详情。`rows` 由调用方拼好(收集页拼结局/彩蛋,致谢页拼 txt)
        void OpenDetailPage(string pageTitle, System.Collections.Generic.List<DetailRow> rows)
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "box", new Color(0.12f, 0.14f, 0.18f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-480, -350), new Vector2(480, 350));
            detailRowTexts.Clear(); detailRowBgs.Clear(); detailRows.Clear();

            Ui.Label(box, "title", pageTitle, 24, TextAnchor.UpperLeft, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -50), new Vector2(-20, -18));

            float y = DetailRowTop;
            string lastGroup = null;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r == null) continue;
                if (!string.IsNullOrEmpty(r.group) && r.group != lastGroup)
                {
                    lastGroup = r.group;
                    Ui.Label(box, "g" + y, r.group, 18, TextAnchor.UpperLeft, GroupColor(r.group),
                             new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y - DetailRowHeight), new Vector2(400, y));
                    y -= 26;
                }
                y = DetailButton(box, r, y);
            }

            var pane = Ui.Panel(box, "detail", new Color(1, 1, 1, 0.05f), new Vector2(0, 1), new Vector2(0, 1),
                                new Vector2(430, DetailPaneBottom), new Vector2(930, DetailRowTop));
            detailHead = Ui.Label(pane, "h", "", 20, TextAnchor.UpperLeft, Color.white,
                                  new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, DetailHeadBottom), new Vector2(-16, DetailHeadTop));
            // ⚠ v0.68 修的那处排版:legacy `Ui.Label` 的第四、五个参数是 **(左下角, 右上角)**,
            //   而我上一版给正文写的是 `(-66, -14)` ⇒ 矩形的 **上边在 -14**,正好压在标题(-12 ~ -42)上,
            //   于是截图里「音乐」和正文第一行叠成一片。正文的上边必须在标题下边之下(-48),
            //   下边给到框底附近(-560)。这条与 §11 里"负尺寸矩形"是同一族:不报错、不进自检,只有画面会说。
            detailBody = Ui.Label(pane, "b", "", 15, TextAnchor.UpperLeft, new Color(0.86f, 0.88f, 0.92f),
                                  new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, DetailBodyBottom), new Vector2(-16, DetailBodyTop));

            Ui.Button(box, "close", "合上", 16, () => root.ClearModal(), new Color(1, 1, 1, 0.12f),
                      new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-70, 16), new Vector2(70, 52));

            // 默认选中"第一个有内容的";一个都没有就选第一行 —— 右边不能是空白(空白页看起来像坏了)
            if (detailRows.Count > 0)
            {
                int first = 0;
                for (int i = 0; i < detailRows.Count; i++) if (!detailRows[i].dim) { first = i; break; }
                SelectDetail(first);
            }
        }

        static Color GroupColor(string group)
        {
            if (group.Contains("结局")) return new Color(0.9f, 0.8f, 0.5f);
            if (group.Contains("彩蛋")) return new Color(0.72f, 0.58f, 0.92f);
            return new Color(0.7f, 0.82f, 0.9f);
        }

        void OpenCollection()
        {
            var db = root.db;
            var rows = new System.Collections.Generic.List<DetailRow>();

            int seenE = 0, totalE = 0;
            if (db.endingTable != null)
                for (int i = 0; i < db.endingTable.Count; i++)
                {
                    var e = db.endingTable[i];
                    if (e == null) continue;
                    totalE++;
                    bool got = SaveData.HasSeenEnding(e.id.ToString());
                    if (got) seenE++;
                    rows.Add(new DetailRow { dim = !got,
                        label = got ? e.title : "??? 未曾抵达",
                        head = got ? e.title : "??? 未曾抵达",
                        body = got ? JoinEpilogue(e) : "这一种走法你还没有抵达过。" });
                }
            if (totalE > 0) rows[0].group = "结局 " + seenE + " / " + totalE;      // 计数要算完整组才有意义 ⇒ 组标题在数完之后才写

            int firstEgg = rows.Count, seenG = 0;
            var eggs = EggItems(db);
            for (int i = 0; i < eggs.Length; i++)
            {
                var it = eggs[i];
                if (it == null) continue;
                bool got = SaveData.HasSeenEgg(it.key);
                if (got) seenG++;
                rows.Add(new DetailRow { dim = !got,
                    label = got ? it.displayName : "??? 没在这座岛上见过",
                    head = got ? it.displayName : "??? 没在这座岛上见过",
                    body = got ? it.desc : "还没在这座岛上见过它。" });
            }
            if (rows.Count > firstEgg) rows[firstEgg].group = "彩蛋 " + seenG + " / " + (rows.Count - firstEgg);

            OpenDetailPage("收集", rows);
        }

        // v0.67(用户:"**署名页怎么做**")⇒ 玩家可见的署名文本 **只有一份**:
        //   `Assets/Resources/Text/Credits.txt`,以 `## 小节名` 分条,下面到下一条之前的全部文字是正文。
        //   ⚠ 法律台账在 `Assets/Art/ATTRIBUTION.md`(记许可依据/URL/取证方式),它 **不再抄这段文本**,
        //     免得改了 txt 而台账还留一份旧署名(那正是这个项目反复栽的"两份名单")。
        void OpenCredits()
        {
            var rows = new System.Collections.Generic.List<DetailRow>();
            var ta = Resources.Load<TextAsset>("Text/Credits");
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogError("[60slike] 致谢页读不到 `Assets/Resources/Text/Credits.txt`(TextAsset 为空)。" +
                               "打包时要确认它在 `Assets/Resources/` 下(只有 Resources 目录才会进包)。");
                rows.Add(new DetailRow { group = "素材来源", label = "署名文本没带进来", head = "致谢", dim = true,
                    body = "这个发行包里没找到署名文本。\n请开发侧检查 Assets/Resources/Text/Credits.txt。" });
                OpenDetailPage("致谢", rows);
                return;
            }

            string cur = null;
            System.Text.StringBuilder sb = null;
            var lines = ta.text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                if (raw.StartsWith("## "))
                {
                    if (cur != null) rows.Add(CreditRow(cur, sb));
                    cur = raw.Substring(3).Trim();
                    sb = new System.Text.StringBuilder();
                }
                else if (cur != null) { sb.Append(raw); sb.Append('\n'); }
            }
            if (cur != null) rows.Add(CreditRow(cur, sb));
            if (rows.Count > 0) rows[0].group = "素材来源";
            OpenDetailPage("致谢 · 素材与许可", rows);
        }

        static DetailRow CreditRow(string head, System.Text.StringBuilder sb)
        {
            return new DetailRow { label = head, head = head, body = sb.ToString().Trim(), dim = false };
        }

        // 一行 = 一颗按钮(只写 label),返回下一行的 y
        float DetailButton(Transform parent, DetailRow r, float y)
        {
            // ⚠ 行号要 **当场抓住** 再交给闭包:把 `detailRows.Count` 直接写进 lambda 是点击那一刻才求值,
            //   结果每一行都会选中最后一行(与 §11 里那条 idx 同族)。
            int row = detailRows.Count;
            var b = Ui.Button(parent, "r" + row, r.label, 14, () => SelectDetail(row),
                              new Color(1, 1, 1, 0.06f), new Vector2(0, 1), new Vector2(0, 1),
                              new Vector2(20, y - DetailRowHeight), new Vector2(400, y),
                              r.dim ? new Color(0.45f, 0.48f, 0.55f) : new Color(0.9f, 0.92f, 0.96f));
            detailRowTexts.Add(b.GetComponentInChildren<Text>());
            detailRowBgs.Add(b.GetComponent<Image>());
            // v0.68 排版:左栏是 **名单**,不是按钮阵列 ⇒ 文字得左对齐(`Ui.Button` 默认居中,一排居中看起来像表格)
            var t = b.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.alignment = TextAnchor.MiddleLeft;
                var trt = t.rectTransform;
                trt.offsetMin = new Vector2(12, trt.offsetMin.y);
                trt.offsetMax = new Vector2(-8, trt.offsetMax.y);
            }
            detailRows.Add(r);
            return y - DetailRowPitch;
        }

        void SelectDetail(int row)
        {
            if (row < 0 || row >= detailRows.Count) return;
            var r = detailRows[row];
            if (detailHead != null) detailHead.text = r.head;
            if (detailBody != null) detailBody.text = r.body;
            // 选中态:文字颜色 + **底色一起变**(v0.68:截图里只靠字色区分,选中那行几乎看不出来),
            //   不靠字符串猜、也不额外加图形资产
            for (int i = 0; i < detailRowTexts.Count; i++)
            {
                var sel = i == row;
                var t = detailRowTexts[i];
                if (t != null)
                    t.color = sel ? new Color(1f, 0.86f, 0.4f)
                            : (detailRows[i].dim ? new Color(0.45f, 0.48f, 0.55f) : new Color(0.9f, 0.92f, 0.96f));
                if (i < detailRowBgs.Count && detailRowBgs[i] != null)
                    detailRowBgs[i].color = sel ? new Color(0.28f, 0.33f, 0.42f, 1f) : new Color(1, 1, 1, 0.05f);
            }
        }

        static string JoinEpilogue(EndingSO e)
        {
            if (e.epilogueLines == null || e.epilogueLines.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < e.epilogueLines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(e.epilogueLines[i]);
            }
            return sb.ToString();
        }
    }
}
