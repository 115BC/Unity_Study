using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // v0.90(用户:"死在岛上就看到岛上的骸骨即可(摄像头推近骸骨然后结束)")
    //   ⇒ 结局 C 不再"当场换屏":先让相机从荒岛固定机位推到 `IslandStage.bonesAnchor` 那一具骸骨,
    //     停一拍、压黑,再进结局屏。
    //   ⚠ 与海鸥/邮轮/直升机同一条口径:结算 `EndRun(C)` 只在过场回调里发生一次 ——
    //     `EndingPhase` 是纯 UI,一进场 Island 层和相机全收走,那时就没东西可看了。
    //   ⚠ 相机在过场期间不归 `CameraPose` 管:`Active` 这个静态闸让 `ApplyIslandView` / `KeepIslandView`
    //     两处让位,否则每帧都会被拽回固定机位(§11-84⑤)。
    public class BonesEndingCutscene : MonoBehaviour
    {
        // 时间轴(秒,unscaled):推近 0.9 → 3.8,停住,4.9 开始压黑,6.1 结束。
        public const float CamFrom = 0.9f;
        public const float CamAt = 3.8f;
        public const float FadeFrom = 4.9f;
        public const float Duration = 6.1f;

        public const float CamDistance = 2.7f;    // 镜头离骸骨多远(米)
        public const float CamHeight = 1.35f;     // 镜头离地多高
        public const float LookHeight = 0.22f;    // 看向骸骨的哪个高度(贴地那具,不要看脚底)

        public static bool Active;

        public System.Action onDone;

        Camera cam;
        Vector3 camStart, camEnd, camLook;
        Quaternion camStartRot, camEndRot;
        Image fade;
        float t;
        bool done;

        // 骸骨落点:优先用场景里的 `bonesAnchor`(在 Scene 视图里挪它,这一镜跟着走);
        // 没有场景层就退回岛心。y 一律取地表高度,免得模型半截埋进沙里。
        public static Vector3 SpotFrom(IslandStage st)
        {
            Vector3 p = st != null && st.bonesAnchor != null
                ? st.bonesAnchor.position
                : new Vector3(IslandTerrain.Center.x, 0f, IslandTerrain.Center.y);
            p.y = IslandTerrain.GroundY(p.x, p.z);
            return p;
        }

        // 镜头终点与视线:从 **岛心一侧** 往骸骨看(镜头落在陆地上,背景是沙滩与海),
        // 退开 `CamDistance`、抬高 `CamHeight`。纯函数,自检可以脱离场景算。
        public static Vector3 CameraGoal(Vector3 spot)
        {
            var c = IslandTerrain.Center;
            var outward = new Vector3(spot.x - c.x, 0f, spot.z - c.y);
            if (outward.sqrMagnitude < 0.0001f) outward = Vector3.forward;
            outward.Normalize();
            return spot - outward * CamDistance + Vector3.up * CamHeight;
        }

        public static Vector3 CameraLookAt(Vector3 spot)
        {
            return spot + Vector3.up * LookHeight;
        }

        public static BonesEndingCutscene Play(GameRoot root, Transform parent, Vector3 spot, System.Action onDone)
        {
            var go = new GameObject("BonesEndingCutscene");
            go.transform.SetParent(parent, false);      // 挂在阶段身上 ⇒ EndRun 拆阶段时一起收掉
            var c = go.AddComponent<BonesEndingCutscene>();
            c.Build(root, spot, onDone);
            return c;
        }

        void Build(GameRoot root, Vector3 spot, System.Action onDone)
        {
            Active = true;
            this.onDone = onDone;

            cam = root != null ? root.cam : null;
            if (cam != null)
            {
                // 从固定机位起手:先记下 CameraPose 那一帧的世界位姿,再把相机搬到 root 底下独立驱动。
                camStart = cam.transform.position;
                camStartRot = cam.transform.rotation;
                cam.transform.SetParent(root.transform, true);
            }

            // 一具完整的人类骸骨(与海鸥结局同一份模型)。缺模型时退回几根灰盒骨头,过场照演。
            var skel = World.PlaceModel(transform, "skeleton", false, spot, Vector3.one * 1.8f);
            if (skel != null)
            {
                foreach (var col in skel.GetComponentsInChildren<Collider>(true)) Destroy(col);
            }
            else
            {
                var bone = new Color(0.86f, 0.84f, 0.76f);
                World.Primitive(PrimitiveType.Capsule, "bone", spot + new Vector3(0f, 0.12f, 0f),
                                new Vector3(0.22f, 0.8f, 0.22f), bone, transform, null, false, new Vector3(0f, 0f, 90f));
                for (int i = 0; i < 4; i++)
                    World.Primitive(PrimitiveType.Capsule, "rib" + i, spot + new Vector3(0.05f, 0.16f, 0f),
                                    new Vector3(0.08f, 0.55f - i * 0.07f, 0.08f), bone, transform, null, false,
                                    new Vector3(90f, 0f, 90f));
            }

            // 夜里死亡也看得见:给骸骨补一盏小暖光(纯演出,不改任何数值)。
            var lit = new GameObject("BonesLight");
            lit.transform.SetParent(transform, false);
            lit.transform.position = spot;
            World.PointLight(lit, new Vector3(0f, 0.9f, 0f),
                             new Color(0.92f, 0.86f, 0.72f), 1.6f, 6f, false);

            camEnd = CameraGoal(spot);
            camLook = CameraLookAt(spot);
            if (cam != null) camEndRot = Quaternion.LookRotation(camLook - camEnd, Vector3.up);

            if (root != null && root.hud != null)
            {
                fade = Ui.Panel(root.hud, "bonesfade", new Color(0f, 0f, 0f, 0f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                fade.raycastTarget = false;
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (cam != null)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CamFrom, CamAt, t));
                cam.transform.position = Vector3.Lerp(camStart, camEnd, k);
                cam.transform.rotation = Quaternion.Slerp(camStartRot, camEndRot, k);
            }
            if (fade != null)
            {
                float a = Mathf.InverseLerp(FadeFrom, Duration, t);
                fade.color = new Color(0f, 0f, 0f, a);
            }
            if (!done && t >= Duration)
            {
                done = true;
                if (onDone != null) onDone();
            }
        }

        void OnDestroy() { Active = false; }
    }
}
