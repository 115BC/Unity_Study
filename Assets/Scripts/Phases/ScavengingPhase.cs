using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // M1:第一人称灰盒拾荒(§2.1)。几何体 + 文字标注,不下载任何模型。
    public class ScavengingPhase : MonoBehaviour
    {
        public GameRoot root;
        RunState S { get { return root.state; } }
        Database DB { get { return root.db; } }

        float timeLeft;
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
        }
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<GameObject> hazards = new List<GameObject>();
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
            timeLeft = DB.bal.scavengerSeconds;
            if (!BindCabin()) { enabled = false; return; }
            SpawnContents();
            BuildPlayer();
            Physics.SyncTransforms();   // 同帧建好 Collider 就走路,第一次 SimpleMove 会漏检地面
            BuildHud();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            S.Log("拾荒开始:60 秒。搬走的东西必须投进舱门口的储物箱才算落袋。");
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
            AddHazard("火焰", new Vector3(-3.6f, 0.4f, 0f), new Color(1f, 0.35f, 0.15f));
            AddHazard("漏电", new Vector3(3.2f, 0.5f, 0f), new Color(1f, 0.95f, 0.2f));
            AddHazard("火焰", new Vector3(7.6f, 0.4f, 0f), new Color(1f, 0.35f, 0.15f));
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

        void AddPickup(ItemSO item, TeammateSO mate, Vector3 p, int zone, Color color)
        {
            GameObject go;
            if (mate != null)
            {
                // 胶囊原语高 2 格,scale 0.9 → 实际 1.8,p 是脚下的面,所以要抬半个身子
                go = World.Primitive(PrimitiveType.Capsule, "Mate_" + mate.key, p + Vector3.up * 0.9f, Vector3.one * 0.9f, color, transform, mate.displayName, false);
            }
            else
            {
                var s = Vector3.one * (0.3f + 0.12f * item.slots);
                // 不挂 Collider:靠近判定走 Nearest 的距离,拾荒时不该被物品挡住走路
                go = World.Primitive(item.slots >= 4 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Item_" + item.key,
                                     p + Vector3.up * (s.y * 0.5f), s, color, transform, item.displayName, false);
            }
            pickups.Add(new Pickup { go = go, item = item, mate = mate, zone = zone });
        }

        void AddHazard(string name, Vector3 p, Color c)
        {
            var go = World.Primitive(PrimitiveType.Cube, "Hazard_" + name, p, Vector3.one * 0.7f, c, transform, name + "(-2秒)", false);
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
            camGo.transform.localPosition = new Vector3(0, 1.55f, 0);
            camT = camGo.transform;
            root.cam.transform.SetParent(camT, false);
            root.cam.transform.localPosition = Vector3.zero;
            root.cam.transform.localRotation = Quaternion.identity;
        }

        void BuildHud()
        {
            var rt = root.hud;
            timerText = Ui.Label(rt, "timer", "60", 72, TextAnchor.UpperCenter, Color.white,
                                 new Vector2(0, 1), new Vector2(1, 1), new Vector2(-100, -96), new Vector2(100, -10));
            timerText.font = Ui.DigitFont;   // 现在全项目都是 STXINGKA(华文行楷),这一行只是保持"倒计时走 Ui.DigitFont"这个口子
            carryText = Ui.Label(rt, "carry", "", 20, TextAnchor.UpperLeft, new Color(0.9f, 0.95f, 1f),
                                 new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -170), new Vector2(430, -20));
            storageText = Ui.Label(rt, "storage", "", 18, TextAnchor.UpperRight, new Color(0.8f, 0.9f, 0.8f),
                                   new Vector2(1, 1), new Vector2(1, 1), new Vector2(-430, -170), new Vector2(-16, -20));
            promptText = Ui.Label(rt, "prompt", "", 26, TextAnchor.LowerCenter, Color.yellow,
                                  new Vector2(0, 0), new Vector2(1, 0), new Vector2(-400, 90), new Vector2(400, 140));
            hintText = Ui.Label(rt, "hint", "WASD 移动 / 鼠标 转向 / E 拾取·投递 / Q 在舱门跳伞 / 碰到火焰与漏电 -2 秒",
                                16, TextAnchor.LowerCenter, new Color(0.7f, 0.75f, 0.8f),
                                new Vector2(0, 0), new Vector2(1, 0), new Vector2(-560, 12), new Vector2(560, 44));
            flashText = Ui.Label(rt, "flash", "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.3f),
                                 new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-300, 60), new Vector2(300, 110));
            BuildCarryBar(rt);
        }

        readonly List<Image> slotImages = new List<Image>();
        readonly List<Text> slotTexts = new List<Text>();

        void BuildCarryBar(Transform rt)
        {
            slotImages.Clear(); slotTexts.Clear();
            var bar = Ui.Panel(rt, "carryBar", new Color(0, 0, 0, 0.45f), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                               new Vector2(-230, 150), new Vector2(230, 230));
            Ui.Label(bar, "title", "携带条(单次搬运上限 4 格,不堆叠)", 14, TextAnchor.UpperCenter,
                     new Color(0.8f, 0.85f, 0.9f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -22), new Vector2(0, -2));
            for (int i = 0; i < 4; i++)
            {
                float x = -220 + i * 110;
                // ⚠ x 是"以条的中心为原点"算的(-220…+220),所以锚点必须是 (0.5,0)。
                //   原来挂的是 (0,0) = 条的 **左下角**,整排格子因此往左溢出面板 230 像素(截图里那个错位)。
                var s = Ui.Panel(bar, "slot" + i, new Color(1, 1, 1, 0.10f), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                 new Vector2(x, 6), new Vector2(x + 100, 52));
                slotImages.Add(s.GetComponent<Image>());
                slotTexts.Add(Ui.Label(s, "t", "", 15, TextAnchor.MiddleCenter, Color.white,
                                       Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-4, -2)));
            }
        }

        void Update()
        {
            if (S.phase != RunPhase.Scavenging) return;
            HandleLook();
            HandleMove();
            HandleHazards();

            timeLeft -= Time.unscaledDeltaTime;
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
            yaw += Input.GetAxis("Mouse X") * 2.2f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, -80f, 80f);
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
            float speed = 4.2f * (carryingTeammate ? (1f - DB.bal.teammateSpeedPenalty) : 1f);
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var dir = (camT.forward * v + camT.right * h);
            dir.y = 0;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            cc.SimpleMove(dir * speed);
        }

        void HandleHazards()
        {
            foreach (var hz in hazards)
            {
                if (hz == null) continue;
                if (Flat2(hz.transform.position, playerT.position) < 1.1f)
                {
                    timeLeft = Mathf.Max(0, timeLeft - DB.bal.hazardSecondsCost);
                    Flash("碰到" + hz.name.Replace("Hazard_", "") + ":-2 秒");
                    Destroy(hz);
                }
            }
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

        void LateUpdate()
        {
            if (S.phase != RunPhase.Scavenging) return;
            if (Time.unscaledTime > flashUntil) flashText.text = "";

            var near = Nearest(1.9f);
            bool inExit = InExitZone();
            string prompt = "";

            if (near != null)
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
            if (p.mate != null)
            {
                if (carryingTeammate || S.carried.Count > 0)
                {
                    if (S.carried.Count > 0) Flash("抓取队友会占满 4 格:先把手里的东西投进储物箱");
                    else Flash("舱门吊带只够一人:你已经抓住 " + carriedMate.displayName + " 了");
                    return;
                }
                carryingTeammate = true;
                carriedMate = p.mate;
                Destroy(p.go);
                pickups.Remove(p);
                Flash("抓住了 " + p.mate.displayName + "(移速 -30%)");
                return;
            }
            if (carryingTeammate) { Flash("背着人,携带条已经满了"); return; }
            if (S.UsedCarrySlots() + p.item.slots > DB.bal.carrySlots) { Flash("携带条放不下(还剩 " + (DB.bal.carrySlots - S.UsedCarrySlots()) + " 格)"); return; }
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

            for (int i = 0; i < 4; i++)
            {
                string label = "";
                if (carryingTeammate)
                {
                    label = i == 0 ? carriedMate.displayName : "(队友占用)";
                }
                else
                {
                    int acc = 0;
                    foreach (var it in S.carried)
                    {
                        // 只显示名称;slots 仍然要参与累加,不然 2 格/4 格的东西会不知道占了哪几格
                        if (acc == i) label = it.displayName;
                        acc += it.slots;
                        if (acc > i) break;
                    }
                }
                slotTexts[i].text = label;
                slotImages[i].color = label == "" ? new Color(1, 1, 1, 0.08f) : new Color(0.4f, 0.7f, 1f, 0.35f);
            }

            carryText.text = "携带 " + (carryingTeammate ? 4 : S.UsedCarrySlots()) + "/4 格" +
                             (InExitZone() ? "   ✅ 在舱门口(可投递 / 跳伞)" : "   ⚠ 不在舱门口(归零 = 坠机死亡)");
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
