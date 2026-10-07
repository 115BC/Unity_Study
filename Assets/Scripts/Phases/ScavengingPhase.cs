using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // M1:第一人称拾荒(§2.1)。舱体仍是灰盒几何 + 贴面文字;
    //   而**能捡的东西**(物品与队友)从 v0.49 起走 `World.PlaceModel` 的真模型,没装模型的自动退回原语。
    public class ScavengingPhase : MonoBehaviour
    {
        public GameRoot root;
        RunState S { get { return root.state; } }
        Database DB { get { return root.db; } }

        float timeLeft;
        // v0.53(§11-84 第一人称共存):**这几个数不再只属于机舱那一层** —— 荒岛的自由视角(由 `IslandWalk` 管)
        //   直接读它们,所以"手感"只有一个出处:改一个数 = 两层一起改,不会漂。
        //   用户的裁定原话:"**直接复用机舱那一套**"。
        public const float LookSensitivity = 2.2f;     // 鼠标每单位 → 度
        public const float LookPitchClamp = 80f;       // 俯仰夹 ±80°
        public const float WalkSpeed = 4.2f;           // 米/秒
        public const float Reach = 1.9f;               // "够得着"的水平距离(拾取与按 E 共用)
        public const float EyeHeight = 1.55f;          // 眼睛离脚

        // v0.62(用户:"**开局拾荒时间改为15+30s,前15s自由活动查看,后30s才能拾取物品(15s后才出现漏电/起火)**")
        //   拾荒节奏拆成两段:**前 `LookSeconds` 只能走/看**(不能拾取、不能投递,漏电/起火不出现),
        //   之后 `PickSeconds` 才是拾取窗口。总时长 = 两者之和(45 秒)。
        //   ⚠ 这两个数 **从 BalanceConfig 搬成了代码常量**(原来那里是 `scavengerSeconds=60` / `carrySlots=4` 两个 SO 字段):
        //     节奏与背包格数是这一轮 **定死的设计**,不是留给 Inspector 拧的旋钮;留在 SO 里就会出现
        //     "资产里还是 60/4、代码读的是新数"这种两本账(菜单① 不覆盖已有资产,那条坑这项目踩过几次)。
        public const float LookSeconds = 15f;
        public const float PickSeconds = 30f;
        public const float TotalSeconds = LookSeconds + PickSeconds;
        // v0.62(用户:"**背包改为3格**"):携带条 3 格。⚠ 连带裁定:潜水装置 4 格 → **3 格**(否则 3 格背包永远带不走它,
        //   潜水线在拾荒层就死了);3 格 = 装满一整趟,与 v0.x 那条"满趟独占"的原意一致。
        public const int CarrySlots = 3;

        CharacterController cc;
        Transform playerT;      // ⚠ 距离判定一律用它:本 MonoBehaviour 挂在 phase 根上,它永远在 (0,0,0)
        Transform camT;
        float yaw, pitch;
        Vector3 doorPos;
        Text timerText, carryText, storageText, promptText, hintText, flashText;
        float flashUntil;

        class Pickup
        {
            public GameObject go;
            public ItemSO item;
            public TeammateSO mate;
            public int zone;
            // 下面三条只服务"挤开"(§11-83 ②⑧):底面高度、原点相对底面的抬升、水平半径
            public float foot, lift, radius;
        }
        readonly List<Pickup> pickups = new List<Pickup>();
        bool carryingTeammate;
        TeammateSO carriedMate;

        // 机舱分区:x 从 -10 到 10,舱门居中(x=0)。烘焙器按同一份常量摆椅面与分区牌,
        // 改了分区再烘一次即可(标牌本身在场景资产里,那份才是显示的唯一真源)。
        public static readonly float[][] Zones =
        {
            new[] { -10f, -6f }, new[] { -6f, -2f }, new[] { -2f, 2f }, new[] { 2f, 6f }, new[] { 6f, 10f }
        };

        CabinAnchors a;       // 场景里那架机舱的接口

        void Start()
        {
            timeLeft = TotalSeconds;
            if (!BindCabin()) { enabled = false; return; }
            SpawnContents();
            BuildPlayer();
            Physics.SyncTransforms();   // 同帧建好 Collider 就走路,第一次 SimpleMove 会漏检地面
            BuildHud();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Music.Scavenge();          // v0.49(用户:"拾荒阶段的音乐要越来越急促,最好只有1min左右")
            S.Log("拾荒开始:前 " + (int)LookSeconds + " 秒只能查看(不能拾取),之后 " + (int)PickSeconds +
                  " 秒可以拾取与投递;搬走的东西必须投进舱门口的储物箱才算落袋。");
        }

        // 机身几何 / 材质 / 标牌都在 Assets/Scenes/Demo.unity 的 Cabin 里(参考 747 · Air Force One 的中段客舱:
        // 窄机身 + 中央过道 + 座椅排 + 行李架 + 舷窗 + 斜肩八边形截面 + 隔舱壁)。
        // 这里只把它的位置读出来:在 Scene 视图里挪舱门、储物箱、座椅,玩法判定跟着走。
        bool BindCabin()
        {
            a = root.cabin != null ? root.cabin : FindObjectOfType<CabinAnchors>();
            if (a == null || a.doorAnchor == null || a.playerSpawn == null)
            {
                Debug.LogError("[60slike] 场景里没有 Cabin(或 CabinAnchors 的 doorAnchor / playerSpawn 没接)。" +
                               "跑菜单 Tools/60slike/② 生成 Demo 场景 + 机舱灰盒,然后打开 Assets/Scenes/Demo.unity。");
                S.Log("机舱场景资产缺失,拾荒阶段起不来。");
                return false;
            }
            a.gameObject.SetActive(true);
            doorPos = a.doorAnchor.position;
            return true;
        }

        // 落点两处:过道地板(y=0)和椅面。椅面从 CabinAnchors.seatPans 里按 x 落在本区挑一块,
        // 高度直接用它包围盒顶面 —— 所以在 Scene 视图里把座椅抬高/挪排,物品就跟着改落点。
        // 不上行李架:斜肩机身在架面上方就往内收,3~4 格的大件放架上会捅进蒙皮。
        Vector3 PointInZone(int z, int slots)
        {
            float x = UnityEngine.Random.Range(Zones[z][0] + 0.7f, Zones[z][1] - 0.7f);
            if (slots <= 2 && UnityEngine.Random.value < 0.45f)
            {
                Renderer best = null; float bd = float.MaxValue;
                for (int i = 0; i < a.seatPans.Count; i++)
                {
                    var r = a.seatPans[i];
                    if (r == null) continue;
                    var c = r.bounds.center;
                    if (c.x < Zones[z][0] || c.x > Zones[z][1]) continue;     // 只要本区的椅面
                    float d = Mathf.Abs(c.x - x) + Mathf.Abs(c.z);            // 同排里靠过道那侧优先
                    if (d < bd) { bd = d; best = r; }
                }
                if (best != null)
                {
                    var c = best.bounds.center;
                    return new Vector3(c.x, best.bounds.max.y, c.z);          // 接触面,抬半个身子交给 AddPickup
                }
            }
            return new Vector3(x, 0f, UnityEngine.Random.Range(-0.7f, 0.7f));
        }

        void SpawnContents()
        {
            var highValuePerZone = new int[Zones.Length];
            var placed = new List<Vector3>();

            // 人、危险物先落位并占位:物品循环里的 0.74 间距只比对 placed,
            // 不先把它们塞进去就会刷出"物品嵌在队友肚子里/标签压在火焰上"
            // 三名队友全部刷出,但整局只能背走 1 名(抓取占满 4 格)。
            // p 是脚下的面(y=0 地板),AddPickup 会自己把人立起来;都站在中央过道上,
            // 两侧现在是座椅排,不能再把人卡进座椅里
            AddPickup(null, DB.Pilot, new Vector3(-8.6f, 0f, 0f), 0, DB.Pilot.color);
            AddPickup(null, DB.Navigator, new Vector3(4.4f, 0f, 0f), 3, DB.Navigator.color);
            AddPickup(null, DB.Mechanic, new Vector3(8.9f, 0f, 0f), 4, DB.Mechanic.color);
            placed.Add(new Vector3(-8.6f, 0f, 0f));
            placed.Add(new Vector3(4.4f, 0f, 0f));
            placed.Add(new Vector3(8.9f, 0f, 0f));

            // 火焰 / 漏电:碰到扣 2 秒,摆在过道上才拦得住人
            AddHazard("火焰", new Vector3(-3.6f, 0.4f, 0f), new Color(1f, 0.35f, 0.15f), true);
            AddHazard("漏电", new Vector3(3.2f, 0.5f, 0f), new Color(1f, 0.95f, 0.2f), false);
            AddHazard("火焰", new Vector3(7.6f, 0.4f, 0f), new Color(1f, 0.35f, 0.15f), true);
            placed.Add(new Vector3(-3.6f, 0f, 0f));
            placed.Add(new Vector3(3.2f, 0f, 0f));
            placed.Add(new Vector3(7.6f, 0f, 0f));

            foreach (var item in DB.scavengedItems)
            {
                int copies = UnityEngine.Random.Range(item.cabinMin, item.cabinMax + 1);
                if (copies <= 0) continue;

                // 候选点位池:高价值物横跨 3 个以上区域
                var pool = new List<int>();
                if (item.highValue)
                {
                    var all = new List<int> { 0, 1, 2, 3, 4 };
                    for (int i = all.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); var t = all[i]; all[i] = all[j]; all[j] = t; }
                    pool.AddRange(all.GetRange(0, Mathf.Max(3, Mathf.Min(5, item.spawnPoints))));
                }
                else
                {
                    int a = UnityEngine.Random.Range(0, Zones.Length);
                    pool.Add(a);
                    if (item.spawnPoints >= 4) pool.Add((a + 1) % Zones.Length);
                }

                for (int c = 0; c < copies; c++)
                {
                    Vector3 p = Vector3.zero; int zone = 0; bool ok = false;
                    for (int tries = 0; tries < 24 && !ok; tries++)
                    {
                        zone = pool[UnityEngine.Random.Range(0, pool.Count)];
                        if (item.highValue && highValuePerZone[zone] >= 2) continue;    // 每区高价值预算 1~2 件
                        p = PointInZone(zone, item.slots);
                        ok = true;
                        foreach (var q in placed) if ((q - p).sqrMagnitude < 0.55f) { ok = false; break; }
                    }
                    if (!ok) continue;
                    if (item.highValue) highValuePerZone[zone]++;
                    placed.Add(p);
                    AddPickup(item, null, p, zone, item.highValue ? new Color(1f, 0.85f, 0.35f) : new Color(0.55f, 0.75f, 0.95f));
                }
            }

        }

        // v0.49(用户:"开局拾荒的时候也把文字隐藏了")⇒ 舱内物品头顶的名字**不再显示**。
        //   这不是丢信息:靠近到 1.9 米时 HUD 中央本来就有 `[E] 拾取 罐头` 那条提示(`promptText`),
        //   而这一层没有鼠标悬浮,飘着的名字只是挡视线的噪声。
        //   ⚠ 只收"头顶飘着的名牌";舱壁/舱门/储物箱上那些**印在表面上的标牌照旧保留**(它们管的是"这是哪儿")。
        //   ⚠ `static readonly` 而不是 `const`:const false 会让那两句被判"永不可达",每次编译刷 CS0162 警告。
        static readonly bool CabinPickupLabels = false;

        void AddPickup(ItemSO item, TeammateSO mate, Vector3 p, int zone, Color color)
        {
            GameObject go;
            // v0.49:舱内这些**能捡的东西**也走真模型 —— 同一个 `World.PlaceModel` 缝,按 key 取,
            //        没装模型的照旧是那颗球/方块。⚠ 这条路上有一件事和荒岛层**相反**,别照着抄:
            //        名字牌走 `CabinPickupLabels` 这个常量,不是"悬浮才亮"—— 这一层没有悬浮这回事。
            // v0.49(用户:"2.拾荒物品加入实体碰撞,不允许直接从物品中穿过去"):**物品这一侧的 Collider 留下来了**
            //        (上一版是我主动 `StripCollider` 拆掉的)。队友仍然不挂 —— 裁定说的是"拾荒物品",
            //        而且背上那位不能被堵住,这一条请确认。
            float w = mate != null ? 1.2f : 0.55f + 0.16f * item.slots;      // 水平那条边(半径按它算)
            if (mate != null)
            {
                var ms = Vector3.one * 0.9f;
                // 模型按 **身高 1.8** 归一(那颗胶囊原语本来就是 0.9 宽 × 1.8 高 —— 胶囊自带 2 倍高度)
                go = World.PlaceModel(transform, "mate_" + mate.key, false, p, new Vector3(1.2f, 1.8f, 1.2f));
                if (go != null)
                {
                    StripCollider(go);
                    if (CabinPickupLabels)
                        World.LabelAt(go.transform, mate.displayName,
                                      new Vector3(p.x, World.TopOf(go) + 0.06f, p.z), 0.12f, Color.white, TextAnchor.LowerCenter);
                }
                else
                {
                    // 胶囊原语高 2 格,scale 0.9 → 实际 1.8,p 是脚下的面,所以要抬半个身子
                    go = World.Primitive(PrimitiveType.Capsule, "Mate_" + mate.key, p + Vector3.up * 0.9f, ms, color, transform, mate.displayName, false);
                }
            }
            else
            {
                // v0.49(用户:"还是小了点"):0.3+0.12×格 → **0.55+0.16×格**(1 格件 0.71、4 格件 1.19)
                var s = Vector3.one * w;
                // v0.53(渔网 教出来的):**这个模型自己没带材质** ⇒ 压上项目的物品色,别让它成了 Unity 内置白模。
                //   渔网 就在拾荒池里(§5.1 第 17 项),所以这一层也会碰到它;带材质的那 30 多件 **一律不压**(照旧吃自己的贴图)。
                go = World.PlaceModel(transform, item.key, false, p, s,
                                      World.ModelHasMaterial(item.key, false) ? null : World.ItemMat(item, false));   // 模型按底面立在 p 上
                if (go != null)
                {
                    if (CabinPickupLabels)
                        World.LabelAt(go.transform, item.displayName,
                                      new Vector3(p.x, World.TopOf(go) + 0.06f, p.z), 0.12f, Color.white, TextAnchor.LowerCenter);
                }
                else
                {
                    go = World.Primitive(item.slots >= 4 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Item_" + item.key,
                                         p + Vector3.up * (s.y * 0.5f), s, color, transform,
                                         CabinPickupLabels ? item.displayName : null, true);
                }
            }
            // foot / lift / radius 三条是给 `PushItems` 用的:
            //   foot = 这件东西**底面**现在在多高(椅面上的东西被挤下来要自己落,因为没有刚体);
            //   lift = 物体原点相对底面的偏移 —— 模型是 0(`PlaceModel` 把底面贴到 p),原语是半个身子。
            pickups.Add(new Pickup { go = go, item = item, mate = mate, zone = zone,
                                      foot = p.y, lift = go.transform.position.y - p.y, radius = w * 0.5f });
        }

        static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);
        }

        // v0.49(用户:"3.触发器写在几个漏电/起火的地方,触碰到之后除了惩罚还有相应的贴图特效")
        //   判定从"每帧算距离"改成物理触发:方块自己那份 BoxCollider 转成 `isTrigger`,
        //   边长放大到世界 ~1.5 米 —— 与原来那条 `Flat2 < 1.1f` 同量级(0.735 半宽 + 玩家 0.35 半径 ≈ 1.08 接触),
        //   所以**惩罚的触发范围一个数都没改**,变的只是"谁来算"。碰撞与那一下的粒子都在 `HazardTrigger` 里。
        //   ⚠ 方块仍然是灰盒那颗,这轮只加特效与触发,不动 §11-53 的舱体重做。
        // v0.62:漏电/起火 **开局不出现**(用户:"15s后才出现漏电/起火" + 这一轮定案"**危险均是15s后才出现**")
        //   ⇒ 建好先整套关掉,等查看期结束再一起点亮。关掉 = SetActive(false):触发器与粒子都不工作,
        //     所以 **查看期里根本没有"扣 2 秒"这件事**(不是"扣不到",是那块方块连同判定都不在场景里)。
        // v0.63(用户:"**漏电/火焰…等文字都不要出现了**")⇒ 方块上那句 "火焰(-2秒)" 不再印字。
        readonly List<GameObject> hazards = new List<GameObject>();
        bool hazardsOn;

        void AddHazard(string name, Vector3 p, Color c, bool fire)
        {
            var go = World.Primitive(PrimitiveType.Cube, "Hazard_" + name, p, Vector3.one * 0.7f, c, transform, null);
            var box = go.GetComponent<BoxCollider>();
            if (box != null) { box.isTrigger = true; box.size = Vector3.one * 2.1f; }
            var t = go.AddComponent<HazardTrigger>();
            t.fx = World.Sparks(go, c, fire);
            t.onHit = () =>
            {
                timeLeft = Mathf.Max(0, timeLeft - DB.bal.hazardSecondsCost);
                Flash("碰到" + name + ":-" + DB.bal.hazardSecondsCost + " 秒");
            };
            go.SetActive(false);
            hazards.Add(go);
        }

        void BuildPlayer()
        {
            var p = new GameObject("Player");
            p.transform.SetParent(transform, false);   // 阶段回收时一起走(相机已由 GameRoot 先搬出去)
            // 站位点来自场景资产 Cabin/Anchors/PlayerSpawn(地板顶面 + 一丝余量):
            // 胶囊底边 = position.y + center.y - height/2 = position.y,必须贴着地板
            p.transform.position = a.playerSpawn.position;
            playerT = p.transform;
            cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            var camGo = new GameObject("Eye");
            camGo.transform.SetParent(p.transform, false);
            camGo.transform.localPosition = new Vector3(0, EyeHeight, 0);
            camT = camGo.transform;
            root.cam.transform.SetParent(camT, false);
            root.cam.transform.localPosition = Vector3.zero;
            root.cam.transform.localRotation = Quaternion.identity;
        }

        void BuildHud()
        {
            var rt = root.hud;
            timerText = Ui.Label(rt, "timer", ((int)TotalSeconds).ToString(), 72, TextAnchor.UpperCenter, Color.white,
                                 new Vector2(0, 1), new Vector2(1, 1), new Vector2(-100, -96), new Vector2(100, -10));
            timerText.font = Ui.DigitFont;   // 现在全项目都是 STXINGKA(华文行楷),这一行只是保持"倒计时走 Ui.DigitFont"这个口子
            carryText = Ui.Label(rt, "carry", "", 20, TextAnchor.UpperLeft, new Color(0.9f, 0.95f, 1f),
                                 new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -170), new Vector2(430, -20));
            storageText = Ui.Label(rt, "storage", "", 18, TextAnchor.UpperRight, new Color(0.8f, 0.9f, 0.8f),
                                   new Vector2(1, 1), new Vector2(1, 1), new Vector2(-430, -170), new Vector2(-16, -20));
            promptText = Ui.Label(rt, "prompt", "", 26, TextAnchor.LowerCenter, Color.yellow,
                                  new Vector2(0, 0), new Vector2(1, 0), new Vector2(-400, 90), new Vector2(400, 140));
            hintText = Ui.Label(rt, "hint", "前 15 秒查看:WASD 移动 / 鼠标 转向 · 15 秒后:E 拾取·投递 / Q 在舱门跳伞 / 碰到火焰与漏电 -2 秒",
                                16, TextAnchor.LowerCenter, new Color(0.7f, 0.75f, 0.8f),
                                new Vector2(0, 0), new Vector2(1, 0), new Vector2(-560, 12), new Vector2(560, 44));
            flashText = Ui.Label(rt, "flash", "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.3f),
                                 new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-300, 60), new Vector2(300, 110));
            BuildCarryBar(rt);
        }

        readonly List<Image> slotImages = new List<Image>();
        readonly List<Text> slotTexts = new List<Text>();
        readonly List<Image> slotIcons = new List<Image>();

        // v0.62(用户:"**拾取的物品的小模型/贴图显示在物品栏里**"):缩略图由菜单 ⑥ 离屏渲染烘成
        //   `Resources/Icons/<key>.png`(没烘或没模型的件 = 没有贴图,格子只显名字,不报错)。
        //   Sprite 建一次缓存:每帧 `Sprite.Create` 会漏 Texture 且垃圾一堆。
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        static Sprite IconFor(string key)
        {
            Sprite s;
            if (icons.TryGetValue(key, out s)) return s;
            var tex = Resources.Load<Texture2D>("Icons/" + key);
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
            icons[key] = s;
            return s;
        }

        void BuildCarryBar(Transform rt)
        {
            slotImages.Clear(); slotTexts.Clear(); slotIcons.Clear();
            // v0.62:4 格 → 3 格 ⇒ 条的宽度和格的 x 都跟着重算过(**格宽 140、间距 10、整排居中**)。
            var bar = Ui.Panel(rt, "carryBar", new Color(0, 0, 0, 0.45f), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                               new Vector2(-230, 150), new Vector2(230, 230));
            Ui.Label(bar, "title", "携带条(单次搬运上限 " + CarrySlots + " 格,不堆叠)", 14, TextAnchor.UpperCenter,
                     new Color(0.8f, 0.85f, 0.9f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -22), new Vector2(0, -2));
            for (int i = 0; i < CarrySlots; i++)
            {
                float x = -220 + i * 150;
                // ⚠ x 是"以条的中心为原点"算的,所以锚点必须是 (0.5,0)。
                //   原来挂的是 (0,0) = 条的 **左下角**,整排格子因此往左溢出面板(截图里那个错位,v0.35 修过一回)。
                var s = Ui.Panel(bar, "slot" + i, new Color(1, 1, 1, 0.10f), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                 new Vector2(x, 6), new Vector2(x + 140, 52));
                slotImages.Add(s.GetComponent<Image>());
                // v0.62:左半格放缩略图,右半格放名字(占几格就重复几格,如 潜水装置 潜水装置 潜水装置)。
                //   ⚠ **不要再 `AddComponent<Image>()`**:`Ui.Panel` 建的就是带 Image 的物体,同一个物体上
                //     再加一份 Image 会被 Unity 拒掉并 **返回 null**(实测:下一行就 NRE,整条携带条因此只有 1 格)。
                //     带图的这块口子是 `Ui.Icon` —— 它已经把 preserveAspect 与 raycastTarget=false 都设好了。
                slotIcons.Add(Ui.Icon(s, "icon", null, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                                      new Vector2(4, -21), new Vector2(54, 21)));
                slotTexts.Add(Ui.Label(s, "t", "", 13, TextAnchor.MiddleLeft, Color.white,
                                       new Vector2(0, 0), new Vector2(1, 1), new Vector2(58, 2), new Vector2(-4, -2)));
            }
        }

        void Update()
        {
            if (S.phase != RunPhase.Scavenging) return;
            HandleLook();
            HandleMove();

            timeLeft -= Time.unscaledDeltaTime;
            // v0.49(用户:"拾荒阶段的音乐要越来越急促"):整曲不动,随倒计时把 pitch 抬到 1.25(在 `Music` 里)
            Music.Tension(1f - timeLeft / TotalSeconds);
            // v0.62:查看期结束的那一刻把漏电/起火点亮(只点一次)
            if (!hazardsOn && !Looking)
            {
                hazardsOn = true;
                for (int i = 0; i < hazards.Count; i++) if (hazards[i] != null) hazards[i].SetActive(true);
                S.Log("机舱里开始漏电与起火:碰到扣 2 秒。从现在起可以拾取了。");
            }
            if (timeLeft <= 0)
            {
                timeLeft = 0;
                if (InExitZone())
                {
                    S.Log("倒计时归零:你站在舱门口,身上没投出去的东西丢了,但你活着离开了。");
                    DropCarried(false);
                    root.EnterIsland();
                }
                else
                {
                    S.Log("倒计时归零,人不在舱门前 → 坠机死亡(结局C 的专属分支,不是第 8 个结局)。");
                    root.EndRun(EndingId.C);
                }
                return;
            }
            RefreshHud();
        }

        void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                return;
            }
            yaw += Input.GetAxis("Mouse X") * LookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * LookSensitivity, -LookPitchClamp, LookPitchClamp);
            camT.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        // 水平距离:架子上的东西(y≈1.05)和地上的该有一样的拾取半径
        static float Flat2(Vector3 a, Vector3 b)
        {
            a.y = 0; b.y = 0;
            return (a - b).sqrMagnitude;
        }

        bool InExitZone()
        {
            float r = carryingTeammate ? DB.bal.exitZoneRadiusCarrying : DB.bal.exitZoneRadius;
            return Flat2(playerT.position, doorPos) <= r * r;
        }

        void HandleMove()
        {
            float speed = WalkSpeed * (carryingTeammate ? (1f - DB.bal.teammateSpeedPenalty) : 1f);
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var dir = (camT.forward * v + camT.right * h);
            dir.y = 0;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            cc.SimpleMove(dir * speed);
            PushItems();
        }

        // v0.49(用户三条原话:"2.拾荒物品加入实体碰撞,不允许直接从物品中穿过去" /
        //   "1.能推动,不同体积的物品被推动后移动速度不同" / "2.不要丢弃")⇒ **挤开**。
        //   不上 Rigidbody(给物品上刚体会飘走、会穿墙,而 CharacterController 撞刚体本来就推不动),
        //   而是自己按 `slots` 分档搬位置。四条口径:
        //   ①只有 **物品** 是实体,队友仍然可以穿过去;
        //   ②全都推得动,大件只是慢 —— 这样才不存在"手满 + 被挡住"的死局,所以 **不需要丢弃** 那条路;
        //   ③推之前沿推开方向打一条短射线:前面是舱体(舱壁/座椅/集装箱)就 **不推** ——
        //     物品钻进墙里比堵住更难查。射线撞上别的物品 **不算**(它们反正会一起被推开);
        //   ④被挤出椅面的那件 **落到地板上** —— 没有刚体,这一跳得自己写,不然它会悬在过道半空。
        //   ⚠ 距离与"能不能捡"那套判定(`Nearest(1.9)`)完全无关,它只搬位置,不改任何数值。
        void PushItems()
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                var pk = pickups[i];
                if (pk.go == null || pk.item == null) continue;
                var d = pk.go.transform.position - playerT.position;
                d.y = 0f;
                float reach = 0.35f + pk.radius;        // 玩家胶囊半径 + 物品半径:贴上才算"被挤"
                if (d.sqrMagnitude > reach * reach) continue;
                var dir = d.sqrMagnitude < 0.0001f ? -camT.forward : d.normalized;
                dir.y = 0f;
                float step = PushSpeed(pk.item.slots) * Time.unscaledDeltaTime;
                var to = pk.go.transform.position + dir * step;
                // ③ 水平射线要从 **身体中部** 打:在地面高度上它会一路蹭地板,于是"什么都推不动"
                var origin = new Vector3(pk.go.transform.position.x, pk.foot + pk.radius * 0.8f, pk.go.transform.position.z);
                RaycastHit hit;
                if (Physics.Raycast(origin, dir, out hit, pk.radius + step + 0.06f, ~0, QueryTriggerInteraction.Ignore)
                    && !IsPickup(hit.collider)) continue;
                pk.foot = Support(to, pk.foot);          // ④ 先定这一帧它站在哪个面上,再按 lift 摆回原点高度
                pk.go.transform.position = new Vector3(to.x, pk.foot + pk.lift, to.z);
            }
        }

        bool IsPickup(Collider c)
        {
            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i].go == c.gameObject) return true;
            return false;
        }

        // 还压在原来那块椅面上就留在原高度,否则落回地板(地板顶面 y=0,与 `PointInZone` 同一条约定)。
        // 椅面的包围盒直接来自场景资产 `CabinAnchors.seatPans` ⇒ 在 Scene 里挪座椅,这条判定跟着走。
        float Support(Vector3 at, float foot)
        {
            if (foot <= 0.01f) return 0f;
            foreach (var r in a.seatPans)
                if (r != null && Mathf.Abs(foot - r.bounds.max.y) < 0.06f &&
                    at.x > r.bounds.min.x && at.x < r.bounds.max.x &&
                    at.z > r.bounds.min.z && at.z < r.bounds.max.z) return foot;
            return 0f;
        }

        // 1 格件一碰就走,4 格件(医疗箱 / 潜水装置 / 藏宝箱)推着明显迟钝但不为零。
        // ⚠ 这是 **纯表现层的档位**,不写成 BalanceConfig 字段:SO 里缺 key 的数值字段读回来是 0(§7.2 第一条),
        //   那样就成了"所有东西都不动"。要改手感只改这一行。
        static float PushSpeed(int slots)
        {
            return Mathf.Max(0.6f, 2.8f / (0.5f + 0.5f * Mathf.Max(1, slots)));
        }

        void Flash(string s) { flashText.text = s; flashUntil = Time.unscaledTime + 1.2f; }

        Pickup Nearest(float maxDist)
        {
            Pickup best = null; float bd = maxDist * maxDist;
            foreach (var p in pickups)
            {
                if (p.go == null) continue;
                float d = Flat2(p.go.transform.position, playerT.position);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        // v0.62:查看期(前 15 秒)判据 —— 只剩的时间比拾取窗口长 = 还在查看期。
        //   用"剩多少"而不是"过了多少"算,是因为倒计时是唯一被扣秒(碰到漏电 -2 秒)影响的那本账:
        //   扣秒会让查看期 **变长** 而不是把拾取窗口吃掉,这与"前 15 秒安全"的读法一致。
        bool Looking { get { return timeLeft > PickSeconds; } }

        void LateUpdate()
        {
            if (S.phase != RunPhase.Scavenging) return;
            if (Time.unscaledTime > flashUntil) flashText.text = "";

            var near = Nearest(Reach);
            bool inExit = InExitZone();
            string prompt = "";

            if (near != null && !Looking)
            {
                if (near.mate != null)
                    prompt = carryingTeammate ? "" : "[E] 抓住 " + near.mate.displayName + "(" + near.mate.profession + ") —— 整局只能带走 1 名";
                else
                    prompt = "[E] 拾取 " + near.item.displayName;
            }
            if (inExit)
            {
                string extra = "";
                if (S.carried.Count > 0) extra += "[E] 投进储物箱(" + S.carried.Count + " 件)  ";
                if (carryingTeammate) extra += "[E] 把 " + carriedMate.displayName + " 送上吊床  ";
                prompt = (prompt == "" ? "" : prompt + "\n") + extra + "[Q] 跳伞离开 → 荒岛";
            }
            promptText.text = prompt;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (inExit && (S.carried.Count > 0 || carryingTeammate)) Deposit();
                else if (near != null) TryPick(near);
            }
            if (Input.GetKeyDown(KeyCode.Q) && inExit)
            {
                S.Log("主动跳伞:带走 玩家 + 队友 + 储物箱全部;手上没投的东西丢了。");
                DropCarried(false);
                root.EnterIsland();
            }
        }

        void TryPick(Pickup p)
        {
            // v0.62:查看期里按 E 不给拾取,只给一句"还要等几秒"(不留沉默,也不让他以为键坏了)
            if (Looking)
            {
                Flash("查看期:还有 " + Mathf.CeilToInt(timeLeft - PickSeconds) + " 秒才能拾取");
                return;
            }
            if (p.mate != null)
            {
                if (carryingTeammate || S.carried.Count > 0)
                {
                    if (S.carried.Count > 0) Flash("抓取队友会占满 " + CarrySlots + " 格:先把手里的东西投进储物箱");
                    else Flash("舱门吊带只够一人:你已经抓住 " + carriedMate.displayName + " 了");
                    return;
                }
                carryingTeammate = true;
                carriedMate = p.mate;
                Destroy(p.go);
                pickups.Remove(p);
                Flash("抓住了 " + p.mate.displayName + "(移速 -30%)");   // 允许:这是代价数值,不是概率(v0.66 裁定只摘概率数字)
                return;
            }
            if (carryingTeammate) { Flash("背着人,携带条已经满了"); return; }
            if (S.UsedCarrySlots() + p.item.slots > CarrySlots) { Flash("携带条放不下(还剩 " + (CarrySlots - S.UsedCarrySlots()) + " 格)"); return; }
            S.carried.Add(p.item);
            S.hidden.everOwned.Add(p.item);
            Destroy(p.go);
            pickups.Remove(p);
        }

        void Deposit()
        {
            if (carryingTeammate)
            {
                S.mate.who = carriedMate;
                S.mate.present = true;
                S.Log("把 " + carriedMate.displayName + " 送上了吊床。另外两个人不能再抓了。");
                carryingTeammate = false;
                carriedMate = null;
                Flash("队友已入箱:" + S.mate.who.displayName);
            }
            int n = S.carried.Count;
            if (n > 0)
            {
                DropCarried(true);
                Flash("投进储物箱 " + n + " 件");
            }
        }

        void DropCarried(bool toStorage)
        {
            if (toStorage)
                foreach (var i in S.carried)
                {
                    S.storage.Add(i, 1);
                    // v0.19:信号枪自带一发信号弹 —— 枪进箱的时候那一发跟着进箱(它不占携带格,是装在枪里的)
                    if (i == DB.FlareGun && S.storage.Total(DB.Flare) == 0)
                    {
                        S.storage.Add(DB.Flare, 1);
                        S.Log("信号枪里还压着一发 信号弹(它跟着枪一起进了储物箱)。");
                    }
                }
            S.carried.Clear();
        }

        void RefreshHud()
        {
            timerText.text = Mathf.CeilToInt(timeLeft).ToString();
            timerText.color = timeLeft < 10 ? new Color(1f, 0.35f, 0.3f) : Color.white;

            // v0.62:每格 = "占着这一格的那件东西"(缩略图 + 名字);占几格就重复几格
            //   (用户给的例子:潜水装置 占满 3 格 ⇒ 三格都写 潜水装置)。队友 = 占满整条。
            for (int i = 0; i < CarrySlots; i++)
            {
                ItemSO cell = null;
                bool mate = false;
                if (carryingTeammate)
                {
                    mate = true;
                }
                else
                {
                    int acc = 0;
                    foreach (var it in S.carried)
                    {
                        if (i >= acc && i < acc + it.slots) { cell = it; break; }
                        acc += it.slots;
                    }
                }
                string label = mate ? carriedMate.displayName : cell != null ? cell.displayName : "";
                slotTexts[i].text = label;
                slotImages[i].color = label == "" ? new Color(1, 1, 1, 0.08f) : new Color(0.4f, 0.7f, 1f, 0.35f);
                var icon = mate ? IconFor("mate_" + carriedMate.key) : cell != null ? IconFor(cell.key) : null;
                slotIcons[i].sprite = icon;
                slotIcons[i].gameObject.SetActive(icon != null);
            }

            carryText.text = "携带 " + (carryingTeammate ? CarrySlots : S.UsedCarrySlots()) + "/" + CarrySlots + " 格" +
                             (Looking ? "   · 查看期:还有 " + Mathf.CeilToInt(timeLeft - PickSeconds) + " 秒才能拾取"
                                      : InExitZone() ? "   ✅ 在舱门口(可投递 / 跳伞)" : "   ⚠ 不在舱门口(归零 = 坠机死亡)");
            storageText.text = StorageSummary();
        }

        string StorageSummary()
        {
            var sb = new System.Text.StringBuilder("储物箱(无限容量)\n");
            int total = 0;
            foreach (var kv in S.storage.ok)
            {
                sb.Append(kv.Key.displayName).Append(" ×").Append(kv.Value).Append("  ");
                total += kv.Key.slots * kv.Value;
            }
            sb.Append("\n已落袋 ").Append(total).Append(" 格 | 队友:").Append(S.mate.present ? S.mate.who.displayName : "无");
            return sb.ToString();
        }
    }
}
