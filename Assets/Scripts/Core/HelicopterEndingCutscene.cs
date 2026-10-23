using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // v0.81(用户:「直接落到岛屿正中央(不用考虑下方的东西,摄像头移到舱门即可」)
    //   ⇒ 搜寻直升机 救走的那一支 A,不再"当场换屏":一架真直升机从岛外飞进来、
    //     停在 **岛心**(`IslandTerrain.Center`,不看底下摆着什么),停稳后镜头飞到机身侧面的舱门。
    //   ⚠ 这一镜与 v0.78 的海鸥、v0.79 的邮轮同一条口径:必须演在世界里(Island 阶段还活着),
    //     所以 `GameRoot.EndRun(A)` 只在 `onDone` 里发生一次。
    //   ⚠ 相机在过场期间不归 `CameraPose` 管:`Active` 这个静态闸让 `ApplyIslandView` / `KeepIslandView`
    //     两处都让位,否则每帧都会被拽回固定机位(§11-84⑤)。
    public class HelicopterEndingCutscene : MonoBehaviour
    {
        // 时间轴(秒,unscaled)。顺序由 `V081()` 钉住:飞抵悬停(4.8)< 镜头出发(5.6)
        //   < 开始下降(6.0)< 落地(8.4)< 镜头到位(9.8)< 压黑(11.2)< 结束(12.4)。
        //   ⚠ 镜头在 **落地前** 就已经在飞:出发时还在下降,落地后才停稳。
        public const float HoverAt = 4.8f;           // 飞到岛心上方、停在悬停高度
        public const float DescendFrom = 6.0f;       // 开始垂直下降
        public const float LandAt = 8.4f;            // 机腹/起落架触地
        public const float CamFrom = 5.6f;           // 镜头开始飞向舱门
        public const float CamAt = 9.8f;             // 镜头停在舱门外
        public const float FadeFrom = 11.2f;         // 开始压黑
        public const float Duration = 12.4f;

        public const float Size = 9f;                // 归一化后的最长边(主旋翼直径)≈ 9 米
        // 航线:从岛的外海(-z,固定镜头那一侧的反面)低空飞进来。
        //   ⚠ 不能从画面左侧横着飞进来:固定机位俯 26°、竖直视场 65° ⇒ 画面顶端只比水平线高
        //     `arcTan(6.5°)` 左右;从侧后方进场时,机顶会被画面上沿切掉(量过:岛心处的可视顶端 ≈ 7 米)。
        //     从 -z 迎面飞进来,高度一路压在 6.5 米以下,全程在画内。
        public const float StartBack = 42f;          // 起手点在岛心 -z 方向多远
        public const float StartUp = 6.5f;           // 起手时机腹离地高度
        public const float HoverHeight = 3.2f;       // 悬停时机腹离地高度(再高就超出画面顶端)

        // 舱门在机身侧面(+z = 固定镜头这一侧)。
        //   比例来自 FBX 实测:主旋翼直径 4172、机身宽 1056 ⇒ 机身半宽 ≈ 0.14 × 旋翼直径;
        //   舱门高度取整机包围盒的 45%(机身顶上是尾梁/旋翼,不能用满高)。
        public const float DoorZRatio = 0.14f;
        public const float DoorHeightRatio = 0.45f;
        public const float DoorEyeLift = 0.10f;      // 镜头比舱门中心再高一点(俯看舱门)
        public const float DoorBackOff = 0.42f;      // 镜头退开距离 = 0.42 × 机身长(大于旋翼半径,不会插进旋翼)

        public static bool Active;

        public System.Action onDone;

        Camera cam;
        Vector3 camStart, camEnd, camLook;
        Quaternion camStartRot, camEndRot;
        Transform heli;
        Vector3 landing;
        Image fade;
        float t;
        bool done;

        // 落点 = 岛心。x/z 取 `IslandTerrain.Center`,y 取那里的地表高度 —— 底下有什么一律不管(用户原话)。
        public static Vector3 LandingPos()
        {
            var c = IslandTerrain.Center;
            return new Vector3(c.x, IslandTerrain.GroundY(c.x, c.y), c.y);
        }

        public static Vector3 StartPos() { return LandingPos() + new Vector3(0f, StartUp, -StartBack); }

        public static Vector3 HoverPos() { return LandingPos() + Vector3.up * HoverHeight; }

        // 镜头终点:机身 +z 侧面(舱门)外,看向舱门。纯函数,自检可以脱离场景算。
        public static void DoorView(Vector3 landingPos, Vector3 size, out Vector3 camPos, out Vector3 look)
        {
            float doorY = landingPos.y + size.y * DoorHeightRatio;
            float doorZ = landingPos.z + size.z * DoorZRatio;
            look = new Vector3(landingPos.x, doorY, doorZ);
            camPos = look + new Vector3(0f, size.y * DoorEyeLift, size.x * DoorBackOff);
        }

        public static HelicopterEndingCutscene Play(GameRoot root, Transform parent, System.Action onDone)
        {
            var go = new GameObject("HelicopterEndingCutscene");
            go.transform.SetParent(parent, false);      // 挂在阶段身上 ⇒ EndRun 拆阶段时一起收掉
            var c = go.AddComponent<HelicopterEndingCutscene>();
            c.Build(root, onDone);
            return c;
        }

        void Build(GameRoot root, System.Action onDone)
        {
            Active = true;
            this.onDone = onDone;
            landing = LandingPos();

            cam = root.cam;
            if (cam != null)
            {
                // 从固定机位起手:先记下 CameraPose 那一帧的世界位姿,再把相机搬到 root 底下独立驱动。
                camStart = cam.transform.position;
                camStartRot = cam.transform.rotation;
                cam.transform.SetParent(root.transform, true);
            }

            // ---- 直升机:空 holder 负责位置/朝向,模型做子物体 ----
            var holder = new GameObject("RescueHelicopter");
            holder.transform.SetParent(transform, false);
            holder.transform.position = StartPos();
            heli = holder.transform;

            var model = World.PlaceModel(heli, "helicopter", false, heli.position, Vector3.one * Size);
            if (model == null) BuildFallback(heli.position);
            else if (model.GetComponent<RotorSpin>() == null) model.AddComponent<RotorSpin>();   // v0.83:再近也要转
            // 机头 +X 是 `helicopter.fbx` 的导入朝向(Y-up;FBX 里机头在 -x,Unity 翻 X 后到 +x),
            //   所以这里 **不再补旋转** —— `PlaceModel` 没动过根节点的旋转,正好是我们要的横停位。
            StripColliders(heli.gameObject);

            // ---- 舱门镜头:按实际包围盒算(模型缺失时灰盒也有自己的尺寸) ----
            Vector3 mn, mx;
            if (!World.WorldAABB(heli.gameObject, out mn, out mx))
            {
                Debug.LogWarning("[60slike] 直升机量不到包围盒 ⇒ 舱门镜头退回一组保守尺寸。");
                mn = landing;
                mx = landing + new Vector3(Size, Size * 0.32f, Size * 0.9f);
            }
            var size = mx - mn;
            DoorView(landing, size, out camEnd, out camLook);
            if (cam != null) camEndRot = Quaternion.LookRotation(camLook - camEnd, Vector3.up);

            // ---- 夜里要看得见:一盏朝下的探照灯(跟着机身走)+ 舱门那一侧的暖光 ----
            var spot = new GameObject("SearchLight");
            spot.transform.SetParent(heli, false);
            spot.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 灯的前方 = 局部 +z ⇒ 转成朝下
            var sl = spot.AddComponent<Light>();
            sl.type = LightType.Spot;
            sl.color = new Color(1f, 0.96f, 0.86f);
            sl.intensity = 2.6f;
            sl.range = 34f;
            sl.spotAngle = 38f;
            sl.shadows = LightShadows.None;
            World.PointLight(heli.gameObject, new Vector3(0f, 1.5f, size.z * DoorZRatio),
                             new Color(1f, 0.84f, 0.66f), 1.3f, 9f, false);

            // ---- 压黑:挂在 HUD 最上层,跟 `ClearHud` 一起收 ----
            if (root != null && root.hud != null)
            {
                fade = Ui.Panel(root.hud, "helifade", new Color(0f, 0f, 0f, 0f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                fade.raycastTarget = false;
            }
        }

        void Update()
        {
            if (done) return;
            t += Time.unscaledDeltaTime;

            // 飞抵:`StartPos → 悬停点` 是一条压低的下滑线;悬停期只剩一点上下浮动。
            Vector3 hover = HoverPos();
            if (t > HoverAt) hover += Vector3.up * (Mathf.Sin((t - HoverAt) * 2.2f) * 0.12f);
            Vector3 pos;
            if (t < HoverAt)
                pos = Vector3.Lerp(StartPos(), hover, Smooth(Mathf.Clamp01(t / HoverAt)));
            else if (t < DescendFrom)
                pos = hover;
            else
                pos = Vector3.Lerp(hover, landing, Smooth(Mathf.Clamp01((t - DescendFrom) / (LandAt - DescendFrom))));
            heli.position = pos;

            // 镜头:先停在固定机位看它落下来,落地前后飞到舱门外,`CamAt` 之后停住。
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

        // 模型缺失时的灰盒兜底:机身 + 尾梁 + 主旋翼十字 + 起落架,尺寸与真模型同量级。
        //   ⚠ 机头朝 +X、尾梁在 -X(与 `helicopter.fbx` 的导入朝向一致)。
        void BuildFallback(Vector3 at)
        {
            var body = new Color(0.32f, 0.35f, 0.30f);
            var dark = new Color(0.12f, 0.13f, 0.14f);
            World.Primitive(PrimitiveType.Cube, "Fuselage", at + new Vector3(0.3f, 1.7f, 0f),
                            new Vector3(4.6f, 1.6f, 1.7f), body, heli, null, false);
            World.Primitive(PrimitiveType.Cube, "Cabin", at + new Vector3(1.4f, 2.2f, 0f),
                            new Vector3(2.0f, 1.2f, 1.5f), new Color(0.55f, 0.62f, 0.66f), heli, null, false);
            World.Primitive(PrimitiveType.Cube, "TailBoom", at + new Vector3(-3.2f, 1.6f, 0f),
                            new Vector3(3.0f, 0.28f, 0.28f), body, heli, null, false);
            World.Primitive(PrimitiveType.Cube, "TailFin", at + new Vector3(-4.4f, 2.0f, 0f),
                            new Vector3(0.9f, 1.1f, 0.16f), body, heli, null, false);
            World.Primitive(PrimitiveType.Cube, "RotorX", at + new Vector3(0f, 2.7f, 0f),
                            new Vector3(8.8f, 0.06f, 0.24f), dark, heli, null, false);
            World.Primitive(PrimitiveType.Cube, "RotorZ", at + new Vector3(0f, 2.7f, 0f),
                            new Vector3(0.24f, 0.06f, 8.8f), dark, heli, null, false);
            World.Primitive(PrimitiveType.Cube, "SkidL", at + new Vector3(0.2f, 0.15f, -0.8f),
                            new Vector3(2.6f, 0.12f, 0.12f), dark, heli, null, false);
            World.Primitive(PrimitiveType.Cube, "SkidR", at + new Vector3(0.2f, 0.15f, 0.8f),
                            new Vector3(2.6f, 0.12f, 0.12f), dark, heli, null, false);
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;                 // Destroy 是延迟的 ⇒ 先关掉,这一帧也点不到
                UnityEngine.Object.Destroy(c);
            }
        }
    }
}
