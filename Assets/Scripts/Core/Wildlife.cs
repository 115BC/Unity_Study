using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // v0.75(用户 v0.71 ⑧⑪):「海鸥绕岛」+「海鸥要扇翅膀」。
    //   v0.76(用户:"现在是直立的" / "海鸥就是那四只海鸥线的海鸥,不需要一直挂着一只"):
    //   扇翅只服务 **海鸥线那几只**(`IslandPhase` 按 `S.gull.presentCount` 摆的 0~4 只);
    //   天上那支"常驻绕岛编队"已整个删除 —— 海鸥不是环境装饰,是那条线的角色,没有线就没有海鸥。
    //   姿势修正那条 +90°X 在 `World.Placement["gull"]`。
    //   v0.77(用户:"白天海鸥绕岛飞,夜晚海鸥停留在陆地上"):白天那几只由 `GullCircle` 带着绕岛飞,
    //   夜里落回营地边不动(`IslandPhase` 按 `night` 分两支)。**编号仍只有 `S.gull.presentCount` 这一处**。
    //   扇翅有两条路:
    //   ① **Animator**(第 6 章那个缺口):菜单 ① 会生成 `Assets/Resources/Anim/GullFlap.controller`(两状态 + Bool `Flap`)。
    //      没有它的时候走 ②,并不是"没做"—— ② 是运行时兜底,保证盘上还没跑菜单① 时翅膀也在动。
    //   ② **程序化兜底**:直接转肩/上臂骨骼(local Z,左右反相)。模型骨架里那两根"手臂"就是翅膀
    //      (`gull.fbx` 的 `Shoulder / UpperArm / LowerArm`,没有叫 wing 的骨骼)。
    //   ⚠ 这是 **纯演出**,不碰任何数值;它不会与地面那几只"事件海鸥"抢点击(天上海鸥没有 collider)。
    public class GullFlap : MonoBehaviour
    {
        Transform[] arms;
        Quaternion[] rest;
        float[] side;
        bool procedural;

        void Start()
        {
            var ctrl = Resources.Load<RuntimeAnimatorController>("Anim/GullFlap");
            // ⚠ 必须连 **avatar** 一起判:FBX 是 Generic 骨架,模型实例上那份 Animator 自带导入时生成的 Avatar;
            //   若哪天导入设置没了 Avatar,只挂 controller 会得到一个"不动"的 Animator ⇒ 不如走程序化兜底。
            var anim = GetComponent<Animator>();
            if (ctrl != null && anim != null && anim.avatar != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.SetBool("Flap", true);
                return;
            }

            procedural = true;
            var all = GetComponentsInChildren<Transform>(true);
            var list = new List<Transform>();
            var shoulder = new List<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == transform) continue;
                if (t.name.IndexOf("UpperArm", System.StringComparison.OrdinalIgnoreCase) >= 0) list.Add(t);
                else if (t.name.IndexOf("Shoulder", System.StringComparison.OrdinalIgnoreCase) >= 0) shoulder.Add(t);
            }
            // 只在没有 UpperArm 时才退 Shoulder —— 两层一起转会叠成两倍角度(与烘焙器 `Wings` 同一条)。
            if (list.Count == 0) list = shoulder;
            arms = list.ToArray();
            rest = new Quaternion[arms.Length];
            side = new float[arms.Length];
            for (int i = 0; i < arms.Length; i++)
            {
                rest[i] = arms[i].localRotation;
                side[i] = arms[i].localPosition.x >= 0f ? 1f : -1f;
            }
        }

        void Update()
        {
            if (!procedural || arms == null || arms.Length == 0) return;
            float a = Mathf.Sin(Time.time * 7f) * 28f;
            for (int i = 0; i < arms.Length; i++)
                arms[i].localRotation = rest[i] * Quaternion.Euler(0f, 0f, a * side[i]);
        }
    }

    // v0.78(用户:"新增海鸥线结局动画:四只海鸥抓起一副骸骨飞向天边"):
    //   §4.7 早就写好了这一镜("四只海鸥从四个方向落下,叼起一副骨架飞走;沙滩只剩拖痕"),
    //   v0.89 用户把后半句收回:"不需要这个,直接被海鸥拉着起飞即可" ⇒ 不再拖行、不留拖痕。
    //   但 `EndingPhase` 是 **纯 UI** —— 它一进场就把 Island 层关掉、把相机也一起关在里头,
    //   所以 **过场必须在荒岛那层还活着的时候播**:播完再 `EndRun(I)`。触发点因此在 `IslandPhase`。
    //   这里只负责"演":四只海鸥 + 一具新骨架 + 淡出;不碰任何数值、不新增结算。
    //   ⚠ 表演里的海鸥 **不是** `S.gull.presentCount` 那四只的引用 —— 那四只在开演前会被 `IslandPhase`
    //     收起来(否则白天绕岛那 4 只 + 这 4 只会同屏变成 8 只)。数量仍是 4(`B.gullEndingCount`),
    //     自检 `V078()` 钉住"这里数 == 数据里的数"。
    public class GullEndingCutscene : MonoBehaviour
    {
        public const int GullCount = 4;              // 与 `B.gullEndingCount` 同值,自检钉住
        public const float Duration = 8.4f;          // 整镜长度(秒,unscaled)
        public const float ArriveAt = 2.6f;          // 四只落定、叼住
        // v0.89(用户:"沙上仍看得出被拖过的沟 / 不需要这个,直接被海鸥拉着起飞即可"):
        //   **拖行阶段整段删除** —— 骨架原地躺到被叼住,然后直接从原地离地飞走;
        //   地面上不再铺任何拖痕(那段 `DragMark` / `MarkColor` 也一起删掉)。
        public const float LiftEnd = 4.4f;           // 离地(从被叼住算 1.8 秒)
        public const float FadeFrom = 7.4f;          // 开始压黑
        // v0.83:镜头在 2.0 秒内靠近骸骨,比海鸥落定(2.6 秒)略早 —— 先看清它,再被抓走。
        // v0.86(用户:"海鸥线的摄像头只用靠近,仍然是一个俯视视角,不需要跟随海鸥起飞"):
        //   到位后 **固定不动** 到压黑,不跟骨架被叼起/飞出岛。
        // v0.87(用户:"海鸥线结局要在斜上方观看,至少能看到骸骨全貌"):
        //   v0.86 的 7 米 / 26° 离得太近,海鸥落在镜头前把骸骨挤到画边(截图里只剩一小块)。
        //   ⇒ 退到 12 米、俯角压到 42°(仍沿 +z 那一侧看,不换方向),焦点回到路线正中(50%);
        //     65° 纵 FOV 下这一带横向可见约 27 米,骸骨与四只海鸥都在画面内;v0.89 焦点收到骨架上。
        //   ⚠ 旋转只在 `CameraAt` 这 2 秒里从固定机位插值到新俯角,之后 **定死** —— 不跟海鸥起飞。
        public const float CameraAt = 2.0f;
        public const float CamDistance = 12f;        // 沿视线退多远(越大越远)
        public const float CamPitch = 42f;           // 最终俯角(度)
        public const float CamFocusHeight = 0.45f;   // 焦点抬离地面这么多

        // v0.88(用户:"海鸥向大海的方向且向天空飞"):离地高度与飞行终点。
        //   终点方向不再写死 -z —— 由"岛心 → 拾起点"这条径向决定(= 朝外海),水平 36 米、抬高 24 米;
        //   离地 2.4 米先越过沙丘,再斜着爬升(不会擦着沙丘顶飞出去)。
        public const float LiftHeight = 2.4f;
        public const float FlightOut = 36f;
        public const float FlightUp = 24f;

        // 过场期间镜头归这里管:`GameRoot` 的两处每帧校正要让位(与船 / 直升机同一条闸)。
        public static bool Active;

        public System.Action onDone;

        Transform skel;
        Transform[] gulls = new Transform[GullCount];
        Vector3[] gullLast = new Vector3[GullCount];
        UnityEngine.UI.Image fade;
        Vector3 spot, skyTarget;
        Vector3 skelOffset;              // 枢轴相对"底面中心落点"的偏移(见 Build)
        Camera cam;
        Vector3 camStart, camGoal;
        Quaternion camStartRot, camEndRot;
        float t;
        bool done;

        // §4.7 的"按 flag 分镜":埋骨那条已随掩埋一起删除(v0.24 / §11-55),
        //   只剩 `nameTagTaken` —— 拿过铭牌 ⇒ 骨架胸口那块是空的,镜头不解释。
        public static bool ShowsTag(bool nameTagTaken) { return !nameTagTaken; }

        // 骨架只有一个落点、一条飞行终点(自检也读它)。
        //   v0.89:拖行阶段删除 ⇒ 不再有"起拖点 / 拾起点"两个点,只剩骨架躺着的那一格。
        public static void Route(out Vector3 spot, out Vector3 skyTarget)
        {
            var dune = IslandTerrain.DuneCenter;
            spot = new Vector3(dune.x + 2.4f, 0f, dune.y - 4.2f);
            spot.y = IslandTerrain.GroundY(spot.x, spot.z);
            // v0.88(用户:"海鸥向大海的方向且向天空飞"):方向由
            //   **岛心 → 骨架落点** 这条径向决定(从岛心往外 = 一定是海),水平 `FlightOut`、抬高 `FlightUp`;
            //   岛形只有 `IslandTerrain.Center` 一个出处,骨架挪了,海的方向也不会跟着写死错。
            var out2 = new Vector2(spot.x - IslandTerrain.Center.x,
                                   spot.z - IslandTerrain.Center.y);
            if (out2.sqrMagnitude < 0.0001f) out2 = Vector2.down;
            out2.Normalize();
            skyTarget = spot + new Vector3(out2.x, 0f, out2.y) * FlightOut + Vector3.up * FlightUp;
        }

        // v0.89:焦点就是骨架躺着的那一格(v0.86 起是"路线 60%",拖行没了就不需要那个百分比)。
        public static Vector3 CameraFocus(Vector3 spot)
        {
            return spot + Vector3.up * CamFocusHeight;
        }

        // 固定观察方向:仍从岛南侧(+z)往 -z 看,只是俯角压到 `CamPitch`。
        public static Quaternion CameraRotation()
        {
            return Quaternion.Euler(CamPitch, 180f, 0f);
        }

        public static Vector3 CameraGoal(Vector3 spot)
        {
            return CameraFocus(spot) - (CameraRotation() * Vector3.forward) * CamDistance;
        }

        // 四个方向:每只从相隔 90° 的方位落下,高度略错开(不排成一条线)。
        public static void Slot(int i, out Vector3 dir, out float height)
        {
            float a = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
            dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            height = 4.2f + (i % 2) * 0.9f;
        }

        public static GullEndingCutscene Play(GameRoot root, Transform parent, bool nameTagTaken, System.Action onDone)
        {
            var go = new GameObject("GullEndingCutscene");
            go.transform.SetParent(parent, false);      // 挂在阶段身上 ⇒ EndRun 拆阶段时一起收掉
            var c = go.AddComponent<GullEndingCutscene>();
            c.Build(root, nameTagTaken, onDone);
            return c;
        }

        void Build(GameRoot root, bool nameTagTaken, System.Action onDone)
        {
            Active = true;
            this.onDone = onDone;
            Route(out spot, out skyTarget);

            // v0.83:镜头先记下固定机位,再搬出来独立驱动;2 秒后挪到斜上方。
            cam = root != null ? root.cam : null;
            if (cam != null)
            {
                camStart = cam.transform.position;
                camStartRot = cam.transform.rotation;
                camGoal = CameraGoal(spot);
                camEndRot = CameraRotation();
                cam.transform.SetParent(root.transform, true);
            }

            // ---- 骸骨:一具"新的"人类骸骨(不复用玩家已经见过的那堆骨头;位置也不在 bonesAnchor) ----
            //   v0.83(用户:"骸骨有人类骸骨吗"):`bones.fbx` 是 Environment_LargeBones(一堆骨头),
            //   换成 Quaternius Pirate Kit 的 `Characters_Skeleton.fbx`(6,975 顶点 / 13,766 三角)。
            //   摆好之后 **绕 X 放平**,再把底面贴回沙面 —— 之后是"躺着被海鸥叼走",不是站着飞。
            var skelGo = World.PlaceModel(transform, "skeleton", false, spot, Vector3.one * 1.8f);
            if (skelGo == null)
            {
                skelGo = World.Primitive(PrimitiveType.Capsule, "bones", spot + Vector3.up * 0.35f,
                                         new Vector3(0.7f, 1.3f, 0.7f), new Color(0.86f, 0.84f, 0.76f),
                                         transform, null, false, new Vector3(90f, 0f, 0f));
            }
            else
            {
                skelGo.transform.rotation *= Quaternion.Euler(90f, 0f, 0f);
                Vector3 a, b;
                if (World.WorldAABB(skelGo, out a, out b))
                    skelGo.transform.position += new Vector3(0f, spot.y - a.y, 0f);
            }
            skel = skelGo.transform;
            // `PlaceModel` 贴的是 **底面**、居中到落点的是 **包围盒中心**(枢轴不等于落点)
            //   ⇒ 之后每帧赋位置时要整段带偏移,否则骨架会先陷进沙里再被拎起。
            skelOffset = skel.position - spot;
            StripColliders(skel.gameObject);

            // `nameTagTaken == false` 才在胸口那面留一块铭牌;拿走过 ⇒ 空着,不解释。
            if (ShowsTag(nameTagTaken))
            {
                Vector3 mn, mx;
                if (World.WorldAABB(skel.gameObject, out mn, out mx))
                {
                    var c = (mn + mx) * 0.5f;
                    var tag = World.PlaceModel(transform, "nametag", false,
                                               new Vector3(c.x, mx.y + 0.01f, c.z), Vector3.one * 0.32f,
                                               World.UnlitMat(new Color(0.82f, 0.86f, 0.9f)));
                    if (tag != null)
                    {
                        StripColliders(tag);
                        tag.transform.SetParent(skel, true);   // 跟着骨架一起被拎走
                    }
                }
            }

            // ---- 四只海鸥:空 holder 负责位置/朝向,模型是子物体(+90°X 才不会被盖掉) ----
            for (int i = 0; i < GullCount; i++)
            {
                Vector3 dir; float h;
                Slot(i, out dir, out h);
                var holder = new GameObject("EndingGull" + i);
                holder.transform.SetParent(transform, false);
                holder.transform.position = spot + dir * (9f + i) + Vector3.up * h;

                var body = World.PlaceModel(holder.transform, "gull", false, holder.transform.position,
                                            Vector3.one * 1.6f);
                if (body == null)
                    body = World.Primitive(PrimitiveType.Sphere, "gull", holder.transform.position,
                                           new Vector3(1.2f, 0.35f, 0.6f), new Color(0.94f, 0.95f, 0.97f),
                                           holder.transform, null, false);
                else StripColliders(body);
                if (body.GetComponent<GullFlap>() == null) body.AddComponent<GullFlap>();
                gulls[i] = holder.transform;
                gullLast[i] = holder.transform.position;
            }

            // ---- 压黑用的是 HUD 最上层的一张纯色 ----
            //   v0.89:地面拖痕整段删除(用户:"不需要这个,直接被海鸥拉着起飞即可")。
            if (root != null && root.hud != null)
            {
                fade = Ui.Panel(root.hud, "gullfade", new Color(0f, 0f, 0f, 0f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<UnityEngine.UI.Image>();
                fade.raycastTarget = false;
            }
        }

        void Update()
        {
            if (done) return;
            t += Time.unscaledDeltaTime;

            // 骨架:原地躺着 → 被叼住后直接离地 → 朝外海的方向飞进天空(v0.89 无拖行)。
            float liftK = Mathf.Clamp01((t - ArriveAt) / (LiftEnd - ArriveAt));
            Vector3 pos = spot;
            pos.y = Mathf.Lerp(spot.y, spot.y + LiftHeight, Smooth(liftK));
            float flyK = Mathf.Clamp01((t - LiftEnd) / (Duration - LiftEnd));
            if (flyK > 0f)
                pos = Vector3.Lerp(new Vector3(spot.x, spot.y + LiftHeight, spot.z),
                                   skyTarget, flyK * flyK);
            skel.position = pos + skelOffset;

            // v0.87:镜头 0 → 2 秒从固定机位挪到斜上方(camStart → camGoal + 俯角 26° → 42°),
            //   之后 **定死**:位置与旋转都不再跟着 `pos` 走 —— 海鸥抓起骨架飞走时镜头不抬头、不追。
            if (cam != null)
            {
                float k = Smooth(Mathf.Clamp01(t / CameraAt));
                cam.transform.position = Vector3.Lerp(camStart, camGoal, k);
                cam.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, k);
            }

            for (int i = 0; i < GullCount; i++)
            {
                Vector3 dir; float h;
                Slot(i, out dir, out h);
                float arriveK = Smooth(Mathf.Clamp01(t / ArriveAt));
                Vector3 grab = pos + dir * 0.9f + Vector3.up * 0.55f;
                Vector3 p = t < ArriveAt
                    ? Vector3.Lerp(spot + dir * (9f + i) + Vector3.up * h, grab, arriveK)
                      + Vector3.up * Mathf.Sin(arriveK * Mathf.PI) * 1.2f
                    : grab;
                if (gulls[i] == null) continue;
                gulls[i].position = p;
                // v0.88(用户:"向天空飞"):转向按 **整条速度**(含 y)—— 爬升时海鸥跟着抬头,
                //   不再只认水平分量(以前一路平着飞出画面)。水平分量太小的那一两帧保持上一帧朝向,
                //   避开 `LookRotation` 的 forward 与 up 共线退化。
                Vector3 vel = p - gullLast[i];
                var flat = new Vector3(vel.x, 0f, vel.z);
                if (flat.sqrMagnitude > 0.0001f)
                    gulls[i].rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
                gullLast[i] = p;
            }

            if (fade != null)
            {
                float a = Mathf.Clamp01((t - FadeFrom) / (Duration - FadeFrom));
                var c = fade.color;
                c.a = a;
                fade.color = c;
            }

            if (t >= Duration)
            {
                done = true;
                if (onDone != null) onDone();
            }
        }

        static float Smooth(float k) { return k * k * (3f - 2f * k); }

        void OnDestroy() { Active = false; }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;                 // Destroy 是延迟的 ⇒ 先关掉,这一帧也点不到
                UnityEngine.Object.Destroy(c);
            }
        }
    }

    // 白天绕岛飞的那几只(用户 v0.77:「触发海鸥线后,白天海鸥绕岛飞」)。
    //   **不是环境装饰**:实例数仍由 `S.gull.presentCount` 决定,线没触发就没有海鸥;
    //   夜里 `IslandPhase` 不再用这个组件,改成落回营地边。
    //   挂在一个 **空 holder** 上,模型是它的子物体:这个组件每帧赋 `transform.rotation`,
    //   若把 +90°X 的修正挂在同一层会被它盖掉(海鸥又会直立)。
    //   绕的是 `IslandTerrain.Center`(岛心的唯一出处);半径全部在水线之外,所以它们不横穿营地,
    //   飞行中也没有 collider ⇒ 不抢任何可点落点(红线 60)。
    public class GullCircle : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 36f;
        public float height = 3f;
        public float speed = 0.08f;
        public float phase;

        void Update()
        {
            float a = phase + Time.time * speed;
            transform.position = center + new Vector3(Mathf.Cos(a) * radius,
                                                      height + Mathf.Sin(a * 2.3f) * 0.5f,
                                                      Mathf.Sin(a) * radius);
            var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            if (tangent.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }

}
