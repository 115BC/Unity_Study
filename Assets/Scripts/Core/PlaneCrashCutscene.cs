using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // v0.91(用户:"飞机坠毁也做一个动画吧,如果没来得及跳出舱门则播放坠毁动画")
    //   倒计时归零且人不在舱门区 ⇒ 不再当场 `EndRun(C)`:先把镜头从舱内震到失控、压黑,再进结局屏。
    //   ⚠ 只有 `ScavengingPhase` 在 `crashing` 期间停手,这一镜才拿得到镜头 —— 它与
    //     `ApplyTurbulence` / `HandleLook` 争的是同一只相机,所以那条早退是必要条件。
    //   ⚠ `EndRun(C)` 只在回调里发生一次(与海鸥/邮轮/骸骨同一条口径):提前结算会把场景与镜头一起收走。
    public class PlaneCrashCutscene : MonoBehaviour
    {
        // 时间轴(unscaled 秒):0.0 开始抖 → 0.6 失控 → 2.4 压黑 → 3.4 回调。
        public const float Duration = 3.4f;
        public const float ShakeFrom = 0.15f;
        public const float OutOfControlAt = 0.6f;
        public const float FadeFrom = 2.4f;

        public static bool Active;

        public System.Action onDone;

        Camera cam;
        Vector3 camStart;
        Quaternion rotStart;
        Image fade;
        float t;
        bool done;

        // v0.91 补:与海鸥/邮轮/直升机/骸骨同一条口径 —— 挂在阶段身上,`EndRun` 拆阶段时一起收掉。
        //   以前挂在 `root` 上 ⇒ 播完还活到结局屏,每帧继续写相机。
        public static PlaneCrashCutscene Play(GameRoot root, Transform parent, System.Action onDone)
        {
            var go = new GameObject("PlaneCrashCutscene");
            go.transform.SetParent(parent != null ? parent : (root != null ? root.transform : null), false);
            var c = go.AddComponent<PlaneCrashCutscene>();
            c.Build(root, onDone);
            return c;
        }

        void Build(GameRoot root, System.Action onDone)
        {
            Active = true;
            this.onDone = onDone;

            cam = root != null ? root.cam : null;
            if (cam != null)
            {
                camStart = cam.transform.position;
                rotStart = cam.transform.rotation;
                cam.transform.SetParent(root.transform, true);
            }

            // 舱内的火与电火花:镜头脚边一摊火、头顶一片迸溅。纯演出,不参与任何判定。
            if (cam != null)
            {
                var fire = new GameObject("crashFire");
                fire.transform.SetParent(transform, false);
                fire.transform.position = camStart + rotStart * new Vector3(0.4f, -1.3f, 2.4f);
                World.Sparks(fire, new Color(1f, 0.42f, 0.12f), true);
                World.PointLight(fire, Vector3.up * 0.5f, new Color(1f, 0.45f, 0.15f), 1.8f, 7f, true);

                var arc = new GameObject("crashArc");
                arc.transform.SetParent(transform, false);
                arc.transform.position = camStart + rotStart * new Vector3(-0.5f, 0.9f, 2.0f);
                World.Sparks(arc, new Color(1f, 0.95f, 0.35f), false);
            }

            if (root != null && root.hud != null)
            {
                fade = Ui.Panel(root.hud, "crashfade", new Color(0f, 0f, 0f, 0f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                fade.raycastTarget = false;
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (cam != null)
            {
                float shake = Mathf.InverseLerp(ShakeFrom, OutOfControlAt, t);
                float wild = Mathf.InverseLerp(OutOfControlAt, FadeFrom, t);
                float amp = 0.035f * shake + 0.16f * wild;
                float t2 = Time.unscaledTime * (6f + 10f * wild);
                var jitter = new Vector3(
                    (Mathf.PerlinNoise(t2, 0.3f) - 0.5f) * 2f * amp,
                    (Mathf.PerlinNoise(0.7f, t2) - 0.5f) * 2f * amp,
                    0f);
                // 越来越向一侧压过去:像机头已经抬不起来。相机位置跟着抖,但整体不移动(舱内视角)。
                float roll = (Mathf.PerlinNoise(t2 * 0.7f, 1.7f) - 0.5f) * (10f + 55f * wild);
                float pitch = -22f * wild + (Mathf.PerlinNoise(t2 * 0.8f, 4.1f) - 0.5f) * 12f * wild;
                cam.transform.position = camStart + cam.transform.right * jitter.x + cam.transform.up * jitter.y;
                cam.transform.rotation = rotStart * Quaternion.Euler(pitch, 0f, roll);
            }
            if (fade != null)
            {
                float a = Mathf.InverseLerp(FadeFrom, Duration - 0.05f, t);
                fade.color = new Color(0f, 0f, 0f, a);
            }
            if (!done && t >= Duration)
            {
                done = true;
                if (onDone != null) onDone();
                // v0.91 补:回调里已经 EndRun ⇒ 这一帧起就没人看它了;自毁保证它不会在结局屏上继续写相机。
                Destroy(gameObject);
            }
        }

        void OnDestroy() { Active = false; }
    }
}
