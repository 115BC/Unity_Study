using UnityEngine;

namespace SixtySLike
{
    // v0.84(用户:"上帝视角下的钓鱼竿调大一点,第一人称的钓鱼竿大小保持现状"):
    //   钓竿是一件 **两个视角共用的世界物体**(切 V 不重建道具)⇒ 尺寸只能跟着 `IslandWalk.Active` 走。
    //   ① 第一人称 = 1.0(**现状**,用户明确要求不动);
    //   ② 上帝视角 = 1.6(只放大钓竿,不改 `BeachSize` / 不掉其他道具);
    //   ③ 放大后按 **世界 AABB** 把底面和水平中心放回原处 —— 否则 PlaceModel 摆好的"脚贴沙面"会随缩放漂。
    //   `Capture()` 必须在 `PlaceModel` 之后调用(那时才有最终缩放与包围盒)。
    public class RodViewScale : MonoBehaviour
    {
        public const float GodMul = 1.6f;

        Vector3 baseScale;
        Vector3 baseCenter;
        float baseFootY;
        bool ready;
        bool appliedWalk;

        public void Capture()
        {
            baseScale = transform.localScale;
            appliedWalk = IslandWalk.Active;
            Vector3 mn, mx;
            if (World.WorldAABB(gameObject, out mn, out mx))
            {
                baseCenter = new Vector3((mn.x + mx.x) * 0.5f, 0f, (mn.z + mx.z) * 0.5f);
                baseFootY = mn.y;
                ready = true;
            }
            Apply();
        }

        void Update()
        {
            if (!ready || appliedWalk == IslandWalk.Active) return;
            appliedWalk = IslandWalk.Active;
            Apply();
        }

        void Apply()
        {
            float k = appliedWalk ? 1f : GodMul;
            transform.localScale = baseScale * k;

            Vector3 mn, mx;
            if (!World.WorldAABB(gameObject, out mn, out mx)) return;
            var curCenter = new Vector3((mn.x + mx.x) * 0.5f, 0f, (mn.z + mx.z) * 0.5f);
            var delta = baseCenter - curCenter;
            delta.y += baseFootY - mn.y;
            transform.position += delta;
        }
    }
}
