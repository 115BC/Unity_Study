using System;
using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // 荒岛那一层的场景资产接口:天空/大海/沙滩/植被/篝火 是 Assets/Scenes/Demo.unity 里真实的 GameObject,
    // 仓库物品在运行时摆到 propSlots 上。挪动挂点,东西就跟着挪。
    public class IslandStage : MonoBehaviour
    {
        [Header("镜头(进荒岛时主相机搬到这个位姿)")]
        public Transform cameraPose;

        [Header("道具落点(仓库里的每一件按顺序摆上去)")]
        public List<Transform> propSlots = new List<Transform>();
        public Transform mateAnchor;      // 队友
        public Transform gullAnchor;      // 海鸥
        public Transform bonesAnchor;     // 涨潮露出的尸骨
        public Transform seaAnchor;       // 大海:夜里"徒手/下水"那一类动作的落点
        public Transform fireAnchor;      // 篝火

        [Header("印在表面上的标牌")]
        public List<CabinSign> signs = new List<CabinSign>();

        readonly List<GameObject> built = new List<GameObject>();

        void OnEnable() { BuildSigns(); }
        void OnDisable() { for (int i = 0; i < built.Count; i++) if (built[i] != null) Destroy(built[i]); built.Clear(); }

        // v0.49(用户:"除了罐头之外都还是很小,并且不要加文字了(大海,篝火...)")
        //   ⇒ 荒岛那层的**五块地面标牌(天空/大海/沙滩/一些海边植被/篝火)不再印字**。
        //     几何体与 `seaAnchor / fireAnchor` 这些落点一个没动,只是不写字;道具名仍然在(悬浮才亮,§11-80)。
        //     `signs` 这份数据与烘焙器写进场景的那一版都留着 —— 想恢复文字,把这个常量改回 true。
        // ⚠ 是 `static readonly` 而不是 `const`:const false 会让下面那段被编译器判成"永不可达",每次编译刷一条
        //   CS0162 警告 —— 这个开关是要留着的东西,不能靠删代码来消警告。
        public static readonly bool GroundSignsVisible = false;

        void BuildSigns()
        {
            built.Clear();
            if (!GroundSignsVisible) return;
            // 与道具名同一条路:World.LabelAt(billboard)。相机是固定的,所以它看起来就是"印在面上",
            // 但不用去猜 LabelFlat 的左右手序 —— 那条我在荒岛这层连着猜错两次。
            // 锚点仍然取标牌自己算出的表面位置,并且用 LowerCenter 托住,免得下半截字埋进沙里。
            for (int i = 0; i < signs.Count; i++)
            {
                var s = signs[i];
                var h = s.host != null ? s.host : transform;
                var worldPos = h.TransformPoint(s.localPos);
                var tm = World.LabelAt(h, s.text, worldPos, s.size, s.color, TextAnchor.LowerCenter);
                built.Add(tm.gameObject);
            }
        }

        public Transform Slot(int i)
        {
            if (propSlots.Count == 0) return transform;
            return propSlots[i % propSlots.Count];
        }
    }

    // 沙滩上那件东西:点它 = 弹出与它有关的白天行动;悬浮 = 名字(+ 消耗品剩余量 + 精力价)
    public class IslandProp : MonoBehaviour
    {
        public Action onClick;
        public Action onOver;
        public Action onOut;
        // v0.38:悬浮时把"剩余 N / 要花的⚡"**直接补到那块名字牌上**,不再另起一张卡片
        //        (用户:"鼠标放上消耗品显示剩余N,但不需要单独的卡片")。
        public TextMesh label;
        public string baseText, hoverText;

        // v0.46(用户):夜晚"能应对这一晚的道具",悬浮时把那块名字牌 **换个颜色** ——
        //        仍然不另起卡片(v0.37 的口径),颜色是这条信息唯一的载体。
        //        alpha = 0 表示"不换算色",白天所有牌子都走这一条。
        public Color hoverColor = Color.clear;
        Color baseColor; bool baseColorCaptured;

        void OnMouseUpAsButton() { if (onClick != null) onClick(); }
        void OnMouseEnter() { mouseOver = true; ApplyHover(onOver); }
        void OnMouseExit() { mouseOver = false; ApplyHover(onOut); }

        bool mouseOver;

        // v0.55(用户:"**第一人称情况下不要显示物品文字了**")⇒ 自由视角期间牌子 **一律不亮**。
        //   ⚠ 这条必须显式挡,不能想当然"反正没人悬浮":锁住的光标停在屏幕正中,转头时 `OnMouseEnter` 会一件接一件触发
        //   ⇒ 不挡就是"看哪儿弹哪儿一块牌子",正是他要去掉的那种满屏文字。
        //   ⚠ 牌子不亮 **不等于点不到**:`OnMouseUpAsButton`(看着它按左键)与走近按 E 两条照常工作。
        //   注:v0.53 我为"走近的目标点亮牌子"加过一条 `walkOver` + `SetWalkTarget`,这条裁定把它整条撤了
        //   —— 只写不读的东西不留(同"名字输入安全"那批教训:第二份状态早晚会说谎)。
        void ApplyHover(Action then)
        {
            bool on = mouseOver && !IslandWalk.Active;
            if (label != null)
            {
                label.gameObject.SetActive(on);
                if (!string.IsNullOrEmpty(on ? hoverText : baseText)) label.text = on ? hoverText : baseText;
                if (on && hoverColor.a > 0f)
                {
                    if (!baseColorCaptured) { baseColor = label.color; baseColorCaptured = true; }
                    label.color = hoverColor;
                }
                else if (baseColorCaptured) { label.color = baseColor; baseColorCaptured = false; }
            }
            if (then != null) then();
        }
    }
}
