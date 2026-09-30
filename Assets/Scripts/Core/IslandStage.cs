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

        void BuildSigns()
        {
            built.Clear();
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
        void OnMouseEnter()
        {
            if (label != null && !string.IsNullOrEmpty(hoverText)) label.text = hoverText;
            if (label != null && hoverColor.a > 0f)
            {
                if (!baseColorCaptured) { baseColor = label.color; baseColorCaptured = true; }
                label.color = hoverColor;
            }
            if (onOver != null) onOver();
        }
        void OnMouseExit()
        {
            if (label != null && !string.IsNullOrEmpty(baseText)) label.text = baseText;
            if (label != null && baseColorCaptured) label.color = baseColor;
            if (onOut != null) onOut();
        }
    }
}
