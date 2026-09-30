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
        public Font uiFont;             // 全项目字体(用户指定 Assets/Scenes/STXINGKA.TTF),没接上就退回系统字体

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

            // 字体:编辑器里 Ui 在场景加载前就按路径装好了(见 Ui.BootFont),这里再用 Inspector 上
            // 拖的那份覆盖一次 —— 这条是打包后仍然生效的路径,也让你能不换代码就换字体。
            if (uiFont != null) Ui.Use(uiFont);
            else if (Ui.ProjectFont == null)
                Debug.Log("[字体] 既没在 GameRoot.uiFont 里指定,也没能按路径加载 " + Ui.ProjectFontPath + ",继续用系统字体。");

            EnterTitle();
        }

        public void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
        }

        public void ClearHud() { Clear(hud); }
        public void ClearModal() { Clear(modal); }

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
            if (islandStage == null || islandStage.cameraPose == null || cam == null) return;
            if (cam.transform.parent != islandStage.cameraPose) { ApplyIslandView("KeepIslandView"); return; }
            if (cam.transform.localPosition != Vector3.zero) cam.transform.localPosition = Vector3.zero;
            if (cam.transform.localRotation != Quaternion.identity) cam.transform.localRotation = Quaternion.identity;
        }

        // §12.6:名字输入的安全 = 长度上限 + 过滤 < > 与控制字符;渲染一律不用 richText
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Daylily";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in raw)
            {
                if (ch == '<' || ch == '>') continue;
                if (char.IsControl(ch)) continue;
                if (sb.Length >= 8) break;
                sb.Append(ch);
            }
            var s = sb.ToString().Trim();
            return string.IsNullOrEmpty(s) ? "Daylily" : s;
        }
    }

    // ---------- 标题 / 名字输入(§2.0) ----------
    public class TitlePhase : MonoBehaviour
    {
        public GameRoot root;
        InputField nameInput;
        Text claimedHint;
        UnityEngine.UI.Button dateModeBtn;

        void Start()
        {
            var rt = root.hud;
            Ui.Panel(rt, "bg", new Color(0.06f, 0.08f, 0.11f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Ui.Label(rt, "title", "六十秒 · 荒岛", 54, TextAnchor.MiddleCenter, Color.white,
                     new Vector2(0, 1), Vector2.one, new Vector2(0, -200), new Vector2(0, -90));
            Ui.Label(rt, "sub", "登记表:写下你的名字(≤8 字)。\n它在 HUD、涨潮露出的铭牌、合影、结局字幕与真结局碑林上都会出现。",
                     18, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.85f),
                     new Vector2(0, 1), Vector2.one, new Vector2(0, -300), new Vector2(0, -220));

            // InputField
            var fieldRt = Ui.Panel(rt, "nameField", new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   new Vector2(-160, -400), new Vector2(160, -350));
            nameInput = fieldRt.gameObject.AddComponent<InputField>();
            var text = Ui.Label(fieldRt, "text", "Daylily", 26, TextAnchor.MiddleLeft, Color.white,
                                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
            text.supportRichText = false;
            nameInput.targetGraphic = fieldRt.GetComponent<Image>();
            nameInput.textComponent = text;
            nameInput.characterLimit = 8;
            nameInput.text = "Daylily";
            nameInput.onValueChanged.AddListener(v => nameInput.text = GameRoot.SanitizeName(v));

            Ui.Button(rt, "start", "登上飞机(开始 60 秒拾荒)", 24, () => root.EnterScavenging(nameInput.text),
                      new Color(0.85f, 0.35f, 0.25f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                      new Vector2(-200, -490), new Vector2(200, -430));

            claimedHint = Ui.Label(rt, "claimed", "", 16, TextAnchor.MiddleCenter, new Color(0.9f, 0.8f, 0.5f),
                                   new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 120), new Vector2(0, 160));
            RefreshClaimed();

            Ui.Button(rt, "resetClaimed", "再要一次账(重置真结局标记)", 16, () =>
            {
                SaveData.ResetTrueClaimed();
                RefreshClaimed();
            }, new Color(1, 1, 1, 0.10f), new Vector2(0, 0), new Vector2(1, 0),
              new Vector2(-190, 60), new Vector2(190, 100));

            // ---- v0.30:设置项(日记日期显示模式)+ 主菜单「收集」页 ----
            dateModeBtn = Ui.Button(rt, "dateMode", "", 16, () =>
            {
                SaveData.ToggleDateMode();
                RefreshDateMode();
            }, new Color(1, 1, 1, 0.10f), new Vector2(0, 0), new Vector2(1, 0),
               new Vector2(-190, 14), new Vector2(190, 54));
            RefreshDateMode();

            Ui.Button(rt, "collect", "收集(结局 / 彩蛋)", 16, OpenCollection,
                      new Color(0.34f, 0.28f, 0.46f, 1f), new Vector2(0, 0), new Vector2(1, 0),
                      new Vector2(-190, -34), new Vector2(190, 8));

            Ui.Label(rt, "ver", "demo:几何体 + 文字标注 | 开发文档 v0.48(尚未正式版本)", 14, TextAnchor.LowerRight,
                     new Color(0.5f, 0.55f, 0.6f), new Vector2(0, 0), new Vector2(1, 0),
                     new Vector2(-330, 8), new Vector2(-10, 30));
        }

        void RefreshClaimed()
        {
            claimedHint.text = SaveData.trueEndingClaimed
                ? "这个存档已经打过真结局 T:献宝选项与两点光都不会再生成。"
                : "真结局 T 每个存档只能打出一次(设置里可重置)。";
        }

        void RefreshDateMode()
        {
            Ui.SetButtonText(dateModeBtn, SaveData.dateMode == 0
                ? "日记日期显示:D12(点一下换成「第 12 天」)"
                : "日记日期显示:第 12 天(点一下换成 D12)");
        }

        // v0.30 收集页:结局收集 + 彩蛋收集。跨局累计(读 SaveData),没拿到的显示 ???
        void OpenCollection()
        {
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "box", new Color(0.12f, 0.14f, 0.18f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-460, -330), new Vector2(460, 330));
            var db = root.db;

            Ui.Label(box, "title", "收集", 24, TextAnchor.UpperLeft, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -50), new Vector2(-20, -18));

            // 左栏:结局
            int seenE = 0;
            if (db.endingTable != null)
                for (int i = 0; i < db.endingTable.Count; i++)
                    if (db.endingTable[i] != null && SaveData.HasSeenEnding(db.endingTable[i].id.ToString())) seenE++;
            Ui.Label(box, "eh", "结局 " + seenE + " / " + (db.endingTable == null ? 0 : db.endingTable.Count), 18,
                     TextAnchor.UpperLeft, new Color(0.9f, 0.8f, 0.5f),
                     new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(20, -90), new Vector2(-10, -62));
            float y = -96;
            if (db.endingTable != null)
                for (int i = 0; i < db.endingTable.Count; i++)
                {
                    var e = db.endingTable[i];
                    if (e == null) continue;
                    bool got = SaveData.HasSeenEnding(e.id.ToString());
                    Ui.Label(box, "e" + i, got ? e.title : "??? 未曾抵达", 15, TextAnchor.UpperLeft,
                             got ? new Color(0.9f, 0.92f, 0.96f) : new Color(0.45f, 0.48f, 0.55f),
                             new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(20, y - 22), new Vector2(-10, y));
                    y -= 24;
                }

            // 右栏:彩蛋层(§5.3 那 15 件;没拿到只给个 ???,拿到之后连说明一起读)
            var eggs = new[] { db.NameTag, db.Map, db.Key, db.Chest, db.Scrap, db.Newspaper, db.Photo,
                               db.Luggage, db.Claim, db.Badge, db.Boarding, db.Tally, db.Raft, db.TwoNames, db.MonkeyGift };
            int seenG = 0, total = 0;
            for (int i = 0; i < eggs.Length; i++)
                if (eggs[i] != null) { total++; if (SaveData.HasSeenEgg(eggs[i].key)) seenG++; }
            Ui.Label(box, "gh", "彩蛋 " + seenG + " / " + total, 18, TextAnchor.UpperLeft, new Color(0.72f, 0.58f, 0.92f),
                     new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(10, -90), new Vector2(-20, -62));
            y = -96;
            for (int i = 0; i < eggs.Length; i++)
            {
                var it = eggs[i];
                if (it == null) continue;
                bool got = SaveData.HasSeenEgg(it.key);
                Ui.Label(box, "g" + i, got ? it.displayName + " — " + it.desc : "??? 没在这座岛上见过", 14,
                         TextAnchor.UpperLeft, got ? new Color(0.86f, 0.82f, 0.94f) : new Color(0.45f, 0.48f, 0.55f),
                         new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(10, y - 34), new Vector2(-20, y));
                y -= 38;
            }

            Ui.Button(box, "close", "合上", 16, () => root.ClearModal(), new Color(1, 1, 1, 0.12f),
                      new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-70, 16), new Vector2(70, 52));
        }
    }
}
