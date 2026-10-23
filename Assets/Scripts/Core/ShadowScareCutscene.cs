using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // v0.91(用户:"如果被影怪打了(包括躲被子被抓住和不做出任何应对)则有一个影怪的跳杀(jumpscare动画)")
    //   ⇒ 结算已经落完以后补一镜:把场上那只影怪/小影怪从镜头前方扑过来、贴脸、压黑,再进天亮/结局。
    //   ⚠ 与海鸥/邮轮/骸骨同一条口径:`Active` 是给 `GameRoot.ApplyIslandView` / `KeepIslandView` 的让位闸,
    //     否则 `IslandPhase.LateUpdate` 每帧都会把镜头拽回固定机位,扑到一半就弹回去(§11-84⑤)。
    //   ⚠ 演出自己造一只模型,不移动场上那只 —— 场上那只只负责"夜里站在营地外"那一层;
    //     把它挪到镜头前再还原,会与 `BuildProps` 的重建打架。
    public class ShadowScareCutscene : MonoBehaviour
    {
        // 时间轴(unscaled 秒):0.00~0.26 贴身预备 → 0.26~0.62 扑上来 → 0.62~1.00 贴脸抖动
        //   → 0.98~1.34 压黑 → 1.40 回调。
        public const float LungeFrom = 0.26f;
        public const float LungeAt = 0.62f;
        public const float FadeFrom = 0.98f;
        public const float Duration = 1.4f;

        public const float StartDistance = 3.0f;   // 起手离镜头多远(米)
        public const float EndDistance = 0.55f;    // 贴脸时离镜头多近(米)

        public static bool Active;

        public System.Action onDone;

        Camera cam;
        Transform monster;
        Transform monsterRoot;
        Vector3 startPos, endPos, baseScale;
        Quaternion startRot;
        Image fade;
        float t;
        bool hitApplied, done;

        // v0.91(用户:"使用手电筒则让影怪直接消失(两个影怪都有效)"):同一份文件里放"驱散"那一镜,
        //   它不需要让位闸(镜头不动),但同样由阶段持有生命周期。
        public static ShadowScareCutscene Play(GameRoot root, Transform parent, string modelKey, float height,
                                                GameObject worldMonster, System.Action onDone)
        {
            if (worldMonster != null) worldMonster.SetActive(false);
            var go = new GameObject("ShadowScareCutscene");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<ShadowScareCutscene>();
            c.Build(root, modelKey, height, onDone);
            return c;
        }

        void Build(GameRoot root, string modelKey, float height, System.Action onDone)
        {
            Active = true;
            this.onDone = onDone;

            cam = root != null ? root.cam : null;
            if (cam != null)
            {
                // 先把镜头从 `cameraPose` 收过来独立驱动,过场结束后 `Refresh -> ApplyIslandView` 会挂回去。
                cam.transform.SetParent(root.transform, true);
                var fwd = cam.transform.forward;
                var up = cam.transform.up;
                startPos = cam.transform.position + fwd * StartDistance - up * 0.15f;
                endPos = cam.transform.position + fwd * EndDistance;
                startRot = cam.transform.rotation;
            }

            var pref = World.PlaceModel(transform, modelKey, false, startPos, Vector3.one * height, null, false);
            if (pref != null)
            {
                foreach (var col in pref.GetComponentsInChildren<Collider>(true)) Destroy(col);
                monsterRoot = pref.transform;
            }
            else
            {
                // 缺模型:一团深色胶囊,扑过来的位移与压黑照演,不会变成"点了没反应"。
                var fallback = World.Primitive(PrimitiveType.Capsule, "shadowScare",
                                               startPos + Vector3.up * height * 0.5f,
                                               new Vector3(height * 0.36f, height * 0.5f, height * 0.36f),
                                               new Color(0.04f, 0.05f, 0.08f), transform, null, false);
                monsterRoot = fallback.transform;
            }
            monster = monsterRoot;
            baseScale = monster.localScale;
            FaceCamera();

            // 一点冷光,夜里也看得清轮廓。挂在模型下 ⇒ 过场结束一起收掉。
            if (monsterRoot != null)
                World.PointLight(monsterRoot.gameObject, new Vector3(0f, height * 0.55f, 0f),
                                 new Color(0.55f, 0.62f, 0.95f), 1.7f, 5.5f, false);

            if (root != null && root.hud != null)
            {
                fade = Ui.Panel(root.hud, "shadowscarefade", new Color(0f, 0f, 0f, 0f),
                                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                fade.raycastTarget = false;
            }
        }

        // 模型根节点的 +Z 对准镜头(水平的,不往天上仰):Quaternius 这一包是 Y-up / Z-forward,
        //   根节点只承担朝向,FBX 子节点自带的轴修正原样保留。
        void FaceCamera()
        {
            if (monster == null || cam == null) return;
            var dir = cam.transform.position - monster.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            monster.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LungeFrom, LungeAt, t));
            float rush = k * k * k;          // 前三秒慢、最后一下扑到脸上
            if (monster != null)
            {
                monster.position = Vector3.Lerp(startPos, endPos, rush);
                monster.localScale = baseScale * Mathf.Lerp(1f, 1.55f, rush);
            }
            if (cam != null)
            {
                // 贴脸之后镜头跟着抖;扑的那一下本身不抖,免得"看不清是什么打过来"。
                float shake = t < LungeAt ? 0f : Mathf.Clamp01((t - LungeAt) * 5f);
                float amp = 2.2f * shake * (1f - Mathf.InverseLerp(FadeFrom, Duration, t));
                var jitter = new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp), 0f);
                cam.transform.rotation = startRot * Quaternion.Euler(jitter);
            }
            if (fade != null)
            {
                float a = Mathf.InverseLerp(FadeFrom, Duration - 0.06f, t);
                fade.color = new Color(0f, 0f, 0f, a);
            }
            if (!hitApplied && t >= LungeAt)
            {
                hitApplied = true;
                if (monster != null) monster.position = endPos;
            }
            if (!done && t >= Duration)
            {
                done = true;
                if (onDone != null) onDone();
            }
        }

        void OnDestroy() { Active = false; }
    }

    // 手电筒驱散:模型缩掉 + 一圈冷色火花 + 白闪,0.5 秒。镜头不动,所以不进 `Active` 闸。
    public class ShadowVanishFx : MonoBehaviour
    {
        public const float Duration = 0.5f;

        public System.Action onDone;

        GameObject monster;
        Vector3 scale0;
        Image flash;
        float t;

        public static ShadowVanishFx Play(GameRoot root, GameObject monster, System.Action onDone)
        {
            var go = new GameObject("ShadowVanishFx");
            go.transform.SetParent(root != null ? root.transform : null, false);
            var c = go.AddComponent<ShadowVanishFx>();
            c.monster = monster;
            if (monster != null) c.scale0 = monster.transform.localScale;
            c.onDone = onDone;
            c.Build(root);
            return c;
        }

        void Build(GameRoot root)
        {
            var host = new GameObject("vanishSparks");
            host.transform.SetParent(transform, false);
            host.transform.position = monster != null ? monster.transform.position : transform.position;
            World.Sparks(host, new Color(0.72f, 0.85f, 1f), false);

            if (root != null && root.hud != null)
            {
                flash = Ui.Panel(root.hud, "shadowvanishflash", new Color(0.86f, 0.92f, 1f, 0.78f),
                                 Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
                flash.raycastTarget = false;
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.InverseLerp(0f, Duration, t);
            if (monster != null)
            {
                monster.transform.localScale = scale0 * (1f - k);
                monster.transform.Rotate(Vector3.up, 540f * Time.unscaledDeltaTime, Space.World);
                if (k >= 0.65f) monster.SetActive(false);
            }
            if (flash != null)
                flash.color = new Color(0.86f, 0.92f, 1f, Mathf.Lerp(0.78f, 0f, k));
            if (t >= Duration)
            {
                if (onDone != null) onDone();
                Destroy(gameObject);
            }
        }
    }
}
