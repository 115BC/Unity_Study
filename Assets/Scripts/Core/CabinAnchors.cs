using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // 机舱灰盒不再是 Play 时代码生成的:它是 Assets/Scenes/Demo.unity 里真实的 GameObject,
    // 颜色是 Assets/Materials/Greybox/*.mat。这个组件就是玩法与那份场景资产的接口 ——
    // 你在 Scene 视图里把舱门/储物箱/出生点/椅面挪到哪,拾荒玩法就跟到哪。
    public class CabinAnchors : MonoBehaviour
    {
        [Header("玩法要读的位置(缺了拾荒阶段起不来)")]
        public Transform doorAnchor;       // 出舱判定的圆心(舱门内侧那个地面点)
        public Transform playerSpawn;      // 开局站立点(脚下)
        [Tooltip("椅面 Renderer:小件物品的落点吸附到最近的一块椅面,并沿用它的面高度")]
        public List<Renderer> seatPans = new List<Renderer>();

        [Header("印在表面上的标牌(文字/挂点/朝向都能在 Inspector 里改)")]
        public List<CabinSign> signs = new List<CabinSign>();

        readonly List<GameObject> built = new List<GameObject>();

        // 机舱只在拾荒阶段被打开( GameRoot 按阶段 SetActive ),标牌因此跟着一起生灭
        void OnEnable() { BuildSigns(); }
        void OnDisable() { for (int i = 0; i < built.Count; i++) if (built[i] != null) Destroy(built[i]); built.Clear(); }

        void BuildSigns()
        {
            built.Clear();
            for (int i = 0; i < signs.Count; i++)
            {
                var s = signs[i];
                var h = s.host != null ? s.host : transform;
                var tm = World.LabelFlat(h, s.text, h.TransformPoint(s.localPos),
                                         h.TransformDirection(s.facingLocal), s.color, s.size);
                built.Add(tm.gameObject);
            }
        }
    }

    [System.Serializable]
    public class CabinSign
    {
        public string text;
        public Transform host;                            // 相对哪个物体摆:挪动宿主,标牌跟着走
        public Vector3 localPos;                          // host 局部空间里的位置
        public Vector3 facingLocal = Vector3.forward;     // 可读面朝向(局部空间):墙牌朝 +z,天花板牌朝 -y
        public float size = World.LabelWorldSize;
        public Color color = Color.white;
    }
}
