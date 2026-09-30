using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §3.2 事件表的数据位。值在 Assets/Data/Events/**.asset 里,由 DemoAssetBaker 一次性建出来;
    // 加一个事件 = 在编辑器里加一条资产并把它的 key 接进 NightResolver 的效果分支(调度代码不动)。
    public partial class Database
    {
        public List<GameEventSO> bagEvents = new List<GameEventSO>();
        public GameEventSO Ship, Ghost, Shadow, TwoLights;   // v0.27:PlaneSecond 已删 —— 飞机线的"第二次"不再是一个专属插播事件
        public GameEventSO Bones;      // v0.24:骸骨 —— 从 涨潮 里独立出来的整局一次事件(铭牌唯一来源)
        public GameEventSO PalmTree;   // v0.28:椰树 —— 常规池新成员,椰子的唯一来源(30% 被砸 -1 生命)
        public GameEventSO Tide, BloodMoon, SearchPlane, ColdNight, Snake, FishSchool, Driftbottle,
                           Gull, SmallShadow, MonkeyNaughty, MonkeyFriendly, Rain, WeaponA, FakeMate;
    }
}
