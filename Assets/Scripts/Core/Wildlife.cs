using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // v0.75(用户 v0.71 ⑧⑪):「海鸥绕岛」+「海鸥要扇翅膀」。
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

    // 天上绕岛飞的那几只:纯装饰、无 collider、不进任何判定。
    //   绕的是 `IslandTerrain.Center`(岛心的唯一出处);场景里 Island 层锚在原点(v0.57),
    //   所以这里写世界坐标与挂在它下面等价。
    public class GullCircle : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 26f;
        public float height = 9f;
        public float speed = 0.05f;
        public float phase;

        void Update()
        {
            float a = phase + Time.time * speed;
            transform.position = center + new Vector3(Mathf.Cos(a) * radius,
                                                      height + Mathf.Sin(a * 2.3f) * 0.8f,
                                                      Mathf.Sin(a) * radius);
            var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            if (tangent.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }

    public class Wildlife : MonoBehaviour
    {
        readonly List<GameObject> birds = new List<GameObject>();

        public static Wildlife Attach(GameRoot root, GameObject host)
        {
            var w = host.GetComponent<Wildlife>();
            if (w == null) w = host.AddComponent<Wildlife>();
            w.Build(root);
            return w;
        }

        void Build(GameRoot root)
        {
            if (root == null || root.islandStage == null) return;
            var parent = root.islandStage.transform;
            for (int i = 0; i < 4; i++)
            {
                float r = 24f + i * 3.5f;
                float h = 8.5f + (i % 3) * 1.6f;
                float phase = i * Mathf.PI * 0.5f;
                var pos = new Vector3(IslandTerrain.Center.x + Mathf.Cos(phase) * r, h,
                                      IslandTerrain.Center.y + Mathf.Sin(phase) * r);
                var go = World.PlaceModel(parent, "gull", false, pos, Vector3.one * 1.5f);
                if (go == null)
                    go = World.Primitive(PrimitiveType.Sphere, "SkyGull" + i, pos, new Vector3(0.9f, 0.35f, 0.45f),
                                         new Color(0.95f, 0.96f, 0.98f), parent, null, false);
                else
                {
                    var col = go.GetComponent<Collider>();
                    if (col != null) Destroy(col);      // 天上那只不可点、不抢任何落点
                }
                go.name = "SkyGull" + i;
                var c = go.AddComponent<GullCircle>();
                c.center = new Vector3(IslandTerrain.Center.x, 0f, IslandTerrain.Center.y);
                c.radius = r;
                c.height = h;
                c.speed = 0.045f + i * 0.006f;
                c.phase = phase;
                if (go.GetComponent<GullFlap>() == null) go.AddComponent<GullFlap>();
                birds.Add(go);
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < birds.Count; i++) if (birds[i] != null) Destroy(birds[i]);
            birds.Clear();
        }
    }
}
