using UnityEngine;

namespace SixtySLike
{
    // v0.90(用户:"队友能有一些自己的动作吗?比如有自己的做点小动作的动画(弯腰/挠头/走两步...)")
    //   白天站在沙滩上的队友会隔几秒做一个随机小动作。**纯程序化**:直接摆骨节点,
    //   不烘 Animator、不引新资产、不需要菜单①。
    //   ⚠ 为什么不是播 FBX 里的动画:那批 `Man_Idle/Walk/Clap/Sitting` 是 Generic 的 Mecanim clip,
    //     要播就得在编辑器里烘 controller 进 `Resources/Anim`(海鸥那条路)。这轮先用骨节点做出
    //     "换重心 / 挠头 / 弯腰 / 走两步" 四种可见的小动作;要换成原动画,再说一声,那一轮需要跑菜单①。
    //   ⚠ 只在白天加(夜里队友躺下,`LayDown` 会转根节点;这里再动根就会打架)。
    //   ⚠ 所有角度都取小值:这是"站着没事做的小动作",不是另一套动画 —— 摆错轴也只是轻微晃动,
    //     不会把人扭成麻花。骨节点按名字找(Quaternius 人形: Hips / Neck / UpperArm.L ...)。
    public class MateFidget : MonoBehaviour
    {
        public const float IdleMin = 2.6f;
        public const float IdleMax = 6.0f;
        public const float PaceDistance = 0.7f;

        Transform hips, neck, head, armL, armR, foreL, foreR, thighL, thighR, shinL, shinR;
        Quaternion hips0, neck0, head0, armL0, armR0, foreL0, foreR0, thighL0, thighR0, shinL0, shinR0;
        Vector3 basePos;
        Quaternion baseRot;
        float wait, t, dur;
        int act;                  // 0 待机,1 换重心,2 挠头,3 弯腰,4 走两步
        float seed;

        void Awake()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
            seed = Random.Range(0f, 20f);
            hips = Find("Hips");
            neck = Find("Neck");
            head = Find("Head");
            armL = Find("UpperArm.L");
            armR = Find("UpperArm.R");
            foreL = Find("LowerArm.L");
            foreR = Find("LowerArm.R");
            thighL = Find("UpperLeg.L");
            thighR = Find("UpperLeg.R");
            shinL = Find("LowerLeg.L");
            shinR = Find("LowerLeg.R");

            hips0 = Rest(hips);
            neck0 = Rest(neck);
            head0 = Rest(head);
            armL0 = Rest(armL); armR0 = Rest(armR);
            foreL0 = Rest(foreL); foreR0 = Rest(foreR);
            thighL0 = Rest(thighL); thighR0 = Rest(thighR);
            shinL0 = Rest(shinL); shinR0 = Rest(shinR);

            if (hips == null && head == null && armR == null) enabled = false;   // 灰盒胶囊兜底:没有骨头
            wait = Random.Range(IdleMin, IdleMax);
        }

        Transform Find(string n)
        {
            foreach (var tr in GetComponentsInChildren<Transform>(true))
                if (tr.name == n) return tr;
            return null;
        }

        static Quaternion Rest(Transform tr) { return tr != null ? tr.localRotation : Quaternion.identity; }

        void Update()
        {
            float dt = Time.deltaTime;
            float tt = Time.time + seed;

            // 呼吸/重心永远在:幅度只有一两度,站着不动时也活着。
            float breath = Mathf.Sin(tt * 1.5f) * 1.2f;
            Vector3 oNeck = new Vector3(breath * 0.5f, 0f, 0f);
            Vector3 oHead = new Vector3(-breath * 0.4f, Mathf.Sin(tt * 0.63f) * 2.2f, 0f);
            Vector3 oHips = new Vector3(0f, 0f, Mathf.Sin(tt * 0.82f) * 0.8f);
            Vector3 oArmL = new Vector3(0f, 0f, Mathf.Sin(tt * 1.1f) * 1.4f);
            Vector3 oArmR = new Vector3(0f, 0f, -Mathf.Sin(tt * 1.1f) * 1.4f);
            Vector3 oForeL = Vector3.zero, oForeR = Vector3.zero;
            Vector3 oThighL = Vector3.zero, oThighR = Vector3.zero, oShinL = Vector3.zero, oShinR = Vector3.zero;
            float yaw = 0f, move = 0f;

            if (act == 0)
            {
                wait -= dt;
                if (wait <= 0f)
                {
                    act = Random.Range(1, 5);
                    t = 0f;
                    dur = act == 4 ? 3.4f : act == 3 ? 2.2f : act == 2 ? 2.0f : 1.8f;
                }
            }
            else
            {
                t += dt;
                float u = Mathf.Clamp01(t / dur);
                float w = Mathf.Sin(Mathf.PI * u);      // 起落都缓:两头 0、中间 1

                if (act == 1)                            // 换重心:胯部侧倾 + 转头
                {
                    oHips += new Vector3(0f, 0f, 4.5f * w);
                    oHead += new Vector3(0f, 15f * w, 0f);
                    oArmL += new Vector3(0f, 0f, 5f * w);
                    oArmR += new Vector3(0f, 0f, -5f * w);
                }
                else if (act == 2)                       // 挠头:抬右臂、屈肘、头偏过去
                {
                    oArmR += new Vector3(-22f * w, 0f, -68f * w);
                    oForeR += new Vector3(-78f * w, 0f, 0f);
                    oHead += new Vector3(-6f * w, -12f * w, 7f * w);
                    oNeck += new Vector3(0f, 0f, 5f * w);
                }
                else if (act == 3)                       // 弯腰:胯前倾、腿反向补一点、低头
                {
                    oHips += new Vector3(18f * w, 0f, 0f);
                    oThighL += new Vector3(-18f * w, 0f, 0f);
                    oThighR += new Vector3(-18f * w, 0f, 0f);
                    oShinL += new Vector3(8f * w, 0f, 0f);
                    oShinR += new Vector3(8f * w, 0f, 0f);
                    oNeck += new Vector3(10f * w, 0f, 0f);
                }
                else                                     // 走两步:原地朝 +x 迈出再退回
                {
                    float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.12f))
                                 * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.88f) / 0.12f)));
                    yaw = 90f * turn;
                    move = PaceDistance * Mathf.Sin(Mathf.PI * u) * turn;
                    float step = Mathf.Sin(u * Mathf.PI * 4f) * 22f * w;
                    oThighL += new Vector3(step, 0f, 0f);
                    oThighR += new Vector3(-step, 0f, 0f);
                    oShinL += new Vector3(-Mathf.Max(0f, step) * 0.8f, 0f, 0f);
                    oShinR += new Vector3(Mathf.Max(0f, -step) * 0.8f, 0f, 0f);
                    oArmL += new Vector3(-step * 0.6f, 0f, 0f);
                    oArmR += new Vector3(step * 0.6f, 0f, 0f);
                }

                if (t >= dur)
                {
                    act = 0;
                    t = 0f;
                    wait = Random.Range(IdleMin, IdleMax);
                }
            }

            Pose(hips, hips0, oHips);
            Pose(neck, neck0, oNeck);
            Pose(head, head0, oHead);
            Pose(armL, armL0, oArmL);
            Pose(armR, armR0, oArmR);
            Pose(foreL, foreL0, oForeL);
            Pose(foreR, foreR0, oForeR);
            Pose(thighL, thighL0, oThighL);
            Pose(thighR, thighR0, oThighR);
            Pose(shinL, shinL0, oShinL);
            Pose(shinR, shinR0, oShinR);

            // 根节点只在"走两步"时动;其余时刻钉回原位,免得残留位移。
            transform.position = basePos + Vector3.right * move;
            transform.rotation = baseRot * Quaternion.Euler(0f, yaw, 0f);
        }

        static void Pose(Transform bone, Quaternion rest, Vector3 euler)
        {
            if (bone != null) bone.localRotation = rest * Quaternion.Euler(euler);
        }
    }
}
