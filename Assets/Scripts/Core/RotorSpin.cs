using UnityEngine;

namespace SixtySLike
{
    // v0.83(用户:"螺旋桨能旋转吗"):远处那一架与结局过场里的直升机共用这一支。
    //   FBX 实测:主旋翼节点 `Propeller_2` 的局部 Y 是盘面法线(父节点只有绕 Y 的朝向),
    //   尾桨 `Propeller` 的盘面在局部 X-Y ⇒ 绕局部 Z 转。两处都用 `Space.Self`,
    //   不再猜世界轴 —— 模型被 `PlaceModel` 归一化/转向之后仍然对。
    public class RotorSpin : MonoBehaviour
    {
        public float mainDegPerSecond = 900f;
        public float tailDegPerSecond = 1400f;

        Transform main, tail;

        void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (t.name == "Propeller_2") { main = t; continue; }
                if (t.name == "Propeller") { tail = t; }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (main != null) main.Rotate(0f, mainDegPerSecond * dt, 0f, Space.Self);
            if (tail != null) tail.Rotate(0f, 0f, tailDegPerSecond * dt, Space.Self);
        }
    }
}
