using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // 手电筒:v0.12 不损坏、改电量(§11-27)
    public class FlashlightSO : ToolSO
    {
        // 电量是"每一局"的状态,所以它不在这里 —— 在 RunState.flashlightCharge(v0.44 起是 0~2 的格数)
        // (SO 现在是 Assets/Data 里的盘上资产,挂运行态会把上一局的电量写进下一局)
        // ⚠ v0.39:原来这里有个 chargeStaminaCost = 3,但 **玩法代码从来没读过它**(充电的 3 点精力是
        //   DoCharge 里写死的),而 v0.39 起充电是 0 精力 ⇒ 这个会撒谎的死字段已删。
        //   充电价现在唯一的真源是 ActivitySO "charge" 的 cost(= 0)。
    }
}
