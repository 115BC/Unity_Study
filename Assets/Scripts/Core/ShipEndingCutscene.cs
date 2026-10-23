using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // v0.79(用户:「邮轮/幽灵船结局都是邮轮靠岸,然后摄像头向邮轮的甲板上移动
    //                 只不过邮轮上存在NPC,而幽灵船空无一人」)
    //   ⇒ A(获救 / 邮轮)与 H(幽灵船)共用同一镜:船横着靠到岛外海面,镜头从固定机位飞向甲板;
    //     **唯一的差别是甲板上有没有人** —— A 摆 4 个, H 一个都不摆。
    //   v0.90:B(活满五十天)也复用这一镜,甲板同样是 4 个人(用户:"五十天使用轮船获救结局")。
    //   ⚠ 这一镜必须演在世界里(Island 阶段还活着),`EndingPhase` 是纯 UI,一进场相机和 Island 层全收走。
    //     所以 `GameRoot.EndRun` 只在 `onDone` 里发生一次(与 v0.78 的海鸥过场同一条口径)。
    //   ⚠ 相机在过场期间不归 `CameraPose` 管:`Active` 这个静态闸让 `ApplyIslandView` / `KeepIslandView`
    //     两处都让位,否则每帧都会被拽回固定机位(v0.53 §11-84⑤ 同一条)。
    public class ShipEndingCutscene : MonoBehaviour
    {
        // 时间轴(秒,unscaled)。顺序由 `V079()` 钉住:靠岸 < 镜头出发 < 镜头到位 < 压黑 < 结束。
        public const float DockAt = 5f;              // 船靠到泊位
        public const float CamFrom = 5.4f;           // 镜头开始飞向甲板
        public const float CamAt = 10.6f;            // 镜头停在甲板上方
        public const float FadeFrom = 12.4f;         // 开始压黑
        public const float Duration = 13.8f;

        public const int NpcCountA = 4;              // A 的甲板上站着几个人
        public const float ShipLength = 30f;         // 船体最长边(米)
        public const float DockOffshore = 6f;        // 泊位离浪线的距离:船宽一半之外还留一点海面
        public const float Approach = 30f;           // 从多远开始驶来(沿岸线方向,不是横着漂)
        public const float CameraAboveDeck = 4.2f;   // 镜头停在甲板上方多高
        public const float CameraBackOff = 6f;       // 镜头在船岛侧退开多少(退到船宽之外)

        // v0.85(用户:"幽灵船能把它变成半透明的荧光绿吗,如果不行就全黑可以吗")
        //   ⇒ H 那一支把整条船换成半透明荧光绿;连透明 shader 都拿不到时退回 **纯黑**(他给的二选一)。
        //   ⚠ 只换材质:模型、航线、镜头、时间轴、甲板上"有没有人"一个数都没动;A 不经过这一支。
        // v0.87(用户:"幽灵船再深绿一点,加点鬼火可以吗"):颜色整体压到深绿(红/蓝都再降,
        //   自发光跟着收暗一档 ⇒ 仍是荧光,但不是薄荷绿);鬼火另见 `AddGhostFires`。
        public static readonly Color GhostTint = new Color(0.10f, 0.62f, 0.28f, 0.50f);
        public static readonly Color GhostEmission = new Color(0.05f, 0.48f, 0.18f) * 0.85f;
        public const int GhostFireCount = 5;                 // H 的甲板上几团鬼火

        public static bool Active;

        public System.Action onDone;

        Camera cam;
        Vector3 camStart, camEnd, camLook;
        Quaternion camStartRot, camEndRot;
        Transform ship;
        Vector3 shipStart, shipDock;
        Image fade;
        float t;
        bool done;

        // v0.90(用户:"五十天使用轮船获救结局"):B 也走这一镜 —— 活满 50 天,邮轮靠岸接人。
        //   与 A 的区别只在"怎么走到这里"(A 是夜晚事件/飞机;B 是清晨天数判定),甲板同样是 4 个人。
        //   T / C / F / I 仍各有各的收尾(见 EndingResolver / BonesEndingCutscene / GullEndingCutscene)。
        public static bool Handles(EndingId id) { return id == EndingId.A || id == EndingId.B || id == EndingId.H; }

        // A / B = 有人来救;H = 幽灵船,甲板上一个人都没有。
        public static int NpcCountFor(EndingId id) { return id == EndingId.H ? 0 : NpcCountA; }

        // 泊位在岛的外侧正对固定镜头的那一点:仍用 `IslandTerrain.WaterR` 推,不写死 z。
        public static Vector3 DockPos()
        {
            var c = IslandTerrain.Center;
            return new Vector3(c.x, IslandTerrain.SeaY, c.y - IslandTerrain.WaterR - DockOffshore);
        }

        // 船从泊位沿岸线切线退开 `Approach` 米起步 —— 这样它是"开过来",不是横着漂过来。
        public static Vector3 StartPos()
        {
            Vector3 outward, tangent;
            DockAxes(out outward, out tangent);
            return DockPos() + tangent * Approach;
        }

        // 世界位 → **靠岸后**的船体局部坐标。
        //   ⚠ 不能用 `ship.InverseTransformPoint`:摆甲板乘客 / 甲板灯的时候船还停在 `shipStart`
        //     (沿岸线 30 米外),那一整段航程会被原样算进局部坐标 ⇒ 人和灯一起偏到船外面去
        //     (用户报的「船员和邮轮的相对位置错了」就是这一条)。
        public static Vector3 DockLocal(Vector3 world, Quaternion shipRot)
        {
            return Quaternion.Inverse(shipRot) * (world - DockPos());
        }

        // 岸线这一点的径向(朝海)与切线。两者都在水平面上,切线 = 绕 y 转 90°。
        public static void DockAxes(out Vector3 outward, out Vector3 tangent)
        {
            var c = IslandTerrain.Center;
            var d = DockPos();
            outward = new Vector3(d.x - c.x, 0f, d.z - c.y);
            if (outward.sqrMagnitude < 0.0001f) outward = Vector3.back;
            outward.Normalize();
            tangent = Vector3.Cross(Vector3.up, outward);
            tangent.Normalize();
        }

        // `longAlongZ` = 模型自己的最长边在原朝向下是 z(v0.79 的 CruiseShip.fbx 就是这一种:
        //   原始 FBX 是 Z-up,y 18 / x 4 / z 6.6,导入 Unity 后长边落在 z 上)。
        //   船体长边要 **和岸线切线同向**(横靠),所以基线角 = 切线角,长边是 z 就直接用,是 x 再加 90°。
        // v0.83(用户:"船绕z轴旋转180度,现在是倒着开向岸边的(倒车)"):
        //   船从 `StartPos = 泊位 + 切线 × Approach` 开来,航向是 **−切线**;原来的 yaw 只对齐了切线,
        //   船头因此朝后(倒车)。模型根节点带 −90°X 的导入修正 ⇒ 它的 **局部 Z 就是世界竖轴**,
        //   用户说的"绕 z 转 180"就是这里再加 180° 的 yaw。
        public static float ShipYaw(bool longAlongZ)
        {
            Vector3 outward, tangent;
            DockAxes(out outward, out tangent);
            float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            return tangentYaw + (longAlongZ ? 0f : 90f) + 180f;
        }

        // 镜头终点:甲板上方、船的岛侧 —— 正对着"甲板上有没有人"那一面,不隔着船体看。
        //   船宽由调用方量出来(`halfWidth`),这里只补高度与后退量,自检可以脱离场景直接算。
        public static Vector3 CameraEnd(float deckY, float halfWidth)
        {
            var dock = DockPos();
            Vector3 outward, tangent;
            DockAxes(out outward, out tangent);
            return new Vector3(dock.x, deckY + CameraAboveDeck, dock.z)
                   + (-outward) * (halfWidth + CameraBackOff);
        }

        public static Vector3 CameraLook(float deckY)
        {
            var dock = DockPos();
            return new Vector3(dock.x, deckY + 1.1f, dock.z);
        }

        // v0.87(用户:"幽灵船再深绿一点,加点鬼火可以吗")
        //   鬼火的世界位:沿甲板长边等分 5 团、左右交替(像一串飘过去的东西),全留在船宽内。
        //   摆进船体时仍走 `DockLocal`(与 NPC / 甲板灯同一条),否则会按 `shipStart` 算偏 30 米。
        public static Vector3 GhostFireAt(int i, Vector3 dock, float deckY,
                                          Vector3 tangent, Vector3 inward, float halfWidth)
        {
            float slot = GhostFireCount == 1 ? 0f : Mathf.Lerp(-9f, 9f, i / (float)(GhostFireCount - 1));
            float side = (i % 2 == 0) ? 1f : -1f;
            return new Vector3(dock.x, deckY + 0.12f, dock.z)
                   + tangent * slot + inward * (halfWidth * 0.35f * side);
        }

        public static ShipEndingCutscene Play(GameRoot root, Transform parent, EndingId id, System.Action onDone)
        {
            var go = new GameObject("ShipEndingCutscene");
            go.transform.SetParent(parent, false);      // 挂在阶段身上 ⇒ EndRun 拆阶段时一起收掉
            var c = go.AddComponent<ShipEndingCutscene>();
            c.Build(root, id, onDone);
            return c;
        }

        void Build(GameRoot root, EndingId id, System.Action onDone)
        {
            Active = true;
            this.onDone = onDone;
            shipDock = DockPos();
            shipStart = StartPos();

            cam = root.cam;
            if (cam != null)
            {
                // 从固定机位起手:先记下 CameraPose 那一帧的世界位姿,再把相机搬到 root 底下独立驱动。
                camStart = cam.transform.position;
                camStartRot = cam.transform.rotation;
                cam.transform.SetParent(root.transform, true);
            }

            // ---- 船:一个空 holder 负责位置/朝向,模型放进去后再量一次包围盒 ----
            var holder = new GameObject("CruiseShip");
            holder.transform.SetParent(transform, false);
            holder.transform.position = shipStart;
            ship = holder.transform;

            var model = World.PlaceModel(ship, "cruiseship", false, shipStart, Vector3.one * ShipLength);
            if (model == null) BuildFallbackShip(shipStart);
            StripColliders(ship.gameObject);
            // 幽灵船:模型与灰盒兜底一起换材质(灰盒也是 ship 的子物体 ⇒ 同一次遍历覆盖到)。
            if (id == EndingId.H) GhostLook(ship.gameObject);

            Vector3 mn, mx;
            if (!World.WorldAABB(ship.gameObject, out mn, out mx))
            {
                Debug.LogWarning("[60slike] 邮轮模型量不到包围盒 ⇒ 过场退回一组保守尺寸(船还会动,只是甲板高度是估的)。");
                mn = shipDock;
                mx = shipDock + new Vector3(ShipLength, 7f, 6f);
            }
            var size = mx - mn;
            // 长边不在切线方向就转 90°(用变换后的包围盒判断,不猜模型的导入朝向)。
            bool longAlongZ = size.z >= size.x;
            ship.rotation = Quaternion.Euler(0f, ShipYaw(longAlongZ), 0f);
            if (!World.WorldAABB(ship.gameObject, out mn, out mx))
            {
                mn = shipDock;
                mx = shipDock + new Vector3(ShipLength, 7f, 6f);
            }
            size = mx - mn;

            float halfWidth = Mathf.Min(size.x, size.z) * 0.5f;
            float deckY = Mathf.Lerp(mn.y, mx.y, 0.45f);   // 甲板:包围盒下 45% 处(船顶还有烟囱/桅杆)
            Vector3 outward, tangent;
            DockAxes(out outward, out tangent);
            Vector3 inward = -outward;

            // 镜头终点:甲板上方、船的岛侧 —— 正对着"甲板上有没有人"那一面,不隔着船体看。
            camEnd = CameraEnd(deckY, halfWidth);
            camLook = CameraLook(deckY);
            if (cam != null) camEndRot = Quaternion.LookRotation(camLook - camEnd, Vector3.up);

            // ---- A 的甲板:4 个人;H 一个都不摆(这就是两个结局在这一镜里的唯一区别) ----
            var dockRot = ship.rotation;
            int n = NpcCountFor(id);
            for (int i = 0; i < n; i++)
            {
                float slot = Mathf.Lerp(-7f, 7f, n == 1 ? 0.5f : i / (float)(n - 1));
                bool male = (i % 2) == 0;
                var pos = new Vector3(shipDock.x, deckY, shipDock.z) + tangent * slot
                          + inward * (halfWidth * 0.35f);
                var npc = World.PlaceModel(transform, male ? "npc_man" : "npc_woman", false, pos,
                                           new Vector3(1.2f, 1.8f, 1.2f));
                if (npc == null)
                    npc = World.Primitive(PrimitiveType.Capsule, male ? "npc_man" : "npc_woman",
                                          pos + Vector3.up * 0.9f, new Vector3(0.5f, 0.9f, 0.5f),
                                          new Color(0.82f, 0.80f, 0.74f), transform, null, false);
                // 位置按"靠岸后"的甲板算,局部坐标也按 **泊位位姿** 换算(`DockLocal`)——
                //   不能用 `ship.InverseTransformPoint`:此刻船还在 30 米外的起点上。
                var local = DockLocal(npc.transform.position, dockRot);
                npc.transform.SetParent(ship, false);
                npc.transform.localPosition = local;
                // 朝向仍是世界 +z = 岛这一侧 = 镜头(船转了多少,这里就反着补多少)。
                npc.transform.rotation = Quaternion.identity;
                StripColliders(npc);
            }

            // 甲板灯:A/H 同样一盏、同样亮 —— 灯本身不让两个结局分家;
            //   v0.85 起 H 的整条船是荧光绿,灯跟着偏绿(否则一条绿船上挂一盏暖白灯,比"空无一人"还怪)。
            var lamp = new GameObject("DeckLight");
            lamp.transform.SetParent(ship, false);
            lamp.transform.localPosition =
                DockLocal(new Vector3(shipDock.x, deckY + 5.5f, shipDock.z), dockRot);
            var l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = id == EndingId.H ? new Color(0.42f, 1f, 0.65f) : new Color(0.82f, 0.86f, 0.95f);
            l.intensity = 1.1f;
            l.range = 22f;
            l.shadows = LightShadows.None;

            // 鬼火:A 没有,只有 H。
            if (id == EndingId.H) AddGhostFires(shipDock, deckY, tangent, inward, halfWidth, dockRot);

            // ---- 压黑:挂在 HUD 最上层,跟 `ClearHud` 一起收 ----
            if (root != null && root.hud != null)
            {
                fade = Ui.Panel(root.hud, "shipfade", new Color(0f, 0f, 0f, 0f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                fade.raycastTarget = false;
            }
        }

        // 每团鬼火 = 一个 **空 holder**(船的子物体)+ 点光 + 火粒子。
        //   holder 没有 MeshFilter ⇒ `Ui.PointLight` / `Ui.Sparks` 量不到 AABB,退回 holder 的世界位;
        //   而这两个函数内部都会把粒子缩回世界尺度 1,不会被船体那 ~200 倍 localScale 放大(v0.83 同一条)。
        void AddGhostFires(Vector3 dock, float deckY, Vector3 tangent, Vector3 inward,
                           float halfWidth, Quaternion dockRot)
        {
            var flame = new Color(0.20f, 1f, 0.38f);
            for (int i = 0; i < GhostFireCount; i++)
            {
                var go = new GameObject("GhostFire" + i);
                go.transform.SetParent(ship, false);
                go.transform.localPosition =
                    DockLocal(GhostFireAt(i, dock, deckY, tangent, inward, halfWidth), dockRot);
                go.transform.localRotation = Quaternion.identity;
                World.PointLight(go, Vector3.up * 0.45f, flame, 1.15f, 5f, true);
                World.Sparks(go, flame, true);
            }
        }

        void Update()
        {
            if (done) return;
            t += Time.unscaledDeltaTime;

            // 靠岸:前 `DockAt` 秒从起点滑到泊位,之后只剩一点浮动。
            float sail = Smooth(Mathf.Clamp01(t / DockAt));
            ship.position = Vector3.Lerp(shipStart, shipDock, sail)
                            + Vector3.up * (Mathf.Sin(t * 1.3f) * 0.06f);

            // 镜头:先停在固定机位看船进来,`CamFrom` 之后飞到甲板上方,`CamAt` 之后停住。
            if (cam != null)
            {
                float k = Smooth(Mathf.Clamp01((t - CamFrom) / (CamAt - CamFrom)));
                cam.transform.position = Vector3.Lerp(camStart, camEnd, k);
                cam.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, k);
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

        void OnDestroy() { Active = false; }

        static float Smooth(float k) { return k * k * (3f - 2f * k); }

        // 模型缺失时的灰盒兜底:船体 + 上层甲板 + 驾驶室 + 烟囱,尺寸与归一化后的真模型同量级。
        void BuildFallbackShip(Vector3 at)
        {
            World.Primitive(PrimitiveType.Cube, "Hull", at + new Vector3(0f, 1.6f, 0f),
                            new Vector3(30f, 3.2f, 7f), new Color(0.24f, 0.27f, 0.32f),
                            ship, null, false);
            World.Primitive(PrimitiveType.Cube, "Deck", at + new Vector3(0f, 3.3f, 0f),
                            new Vector3(27f, 0.3f, 6.2f), new Color(0.72f, 0.70f, 0.64f),
                            ship, null, false);
            World.Primitive(PrimitiveType.Cube, "Bridge", at + new Vector3(-9f, 5.1f, 0f),
                            new Vector3(7f, 3.4f, 5f), new Color(0.86f, 0.86f, 0.84f),
                            ship, null, false);
            World.Primitive(PrimitiveType.Cylinder, "Funnel", at + new Vector3(5f, 6.4f, 0f),
                            new Vector3(1.4f, 2.2f, 1.4f), new Color(0.55f, 0.22f, 0.18f),
                            ship, null, false);
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;                 // Destroy 是延迟的 ⇒ 先关掉,这一帧也点不到
                UnityEngine.Object.Destroy(c);
            }
        }

        // 半透明荧光绿:走内置 Standard 的 Transparent 模式(`_Mode=3`)+ 自发光。
        //   `_Mode=3` 的 alpha 只压暗漫反射,自发光仍按原强度加色 ⇒ 才有"荧光"而不是"暗绿玻璃"。
        //   拿不到 Standard(理论上不会)就返回 null,由 `GhostLook` 走他给的备用方案:不透明纯黑。
        public static Material GhostMat()
        {
            var sh = Shader.Find("Standard");
            if (sh == null) return null;
            var m = new Material(sh);
            m.name = "ghost-ship";
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_EMISSION");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetColor("_Color", GhostTint);
            m.SetColor("_EmissionColor", GhostEmission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;   // 别把幽灵船烘进光照贴图
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        static void GhostLook(GameObject shipGo)
        {
            var ghost = GhostMat();
            if (ghost == null)
            {
                Debug.LogWarning("[60slike] 找不到 Standard shader ⇒ 幽灵船退回纯黑(用户给的备用方案)。");
                Paint(shipGo, World.Mat(Color.black), false);
                return;
            }
            ghost.hideFlags = HideFlags.DontSave;      // 只活在运行时,别被谁当资产烘进项目
            Paint(shipGo, ghost, true);
        }

        // 子网格可能不止一个 ⇒ 每个材质槽都要换(只写 `sharedMaterial` 只会换掉第 0 个)。
        static void Paint(GameObject go, Material m, bool ghost)
        {
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
            {
                int n = Mathf.Max(1, rd.sharedMaterials.Length);
                var arr = new Material[n];
                for (int i = 0; i < n; i++) arr[i] = m;
                rd.sharedMaterials = arr;
                if (ghost)
                {
                    // 半透明的船还投一块实心影子会穿帮;幽灵本来也不该压住沙滩。
                    rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rd.receiveShadows = false;
                }
            }
        }
    }
}
