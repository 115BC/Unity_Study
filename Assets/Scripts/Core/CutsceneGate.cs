namespace SixtySLike
{
    // ---- v0.92:过场总闸 ----
    // 谁要问"现在有没有过场在演":GameRoot.Update 的 Esc 暂停(过场正演着弹暂停,会和
    //   Timeline / 镜头脚本打架);以后任何"过场期间别接输入"的地方都走这一条,不再各写一份名单。
    // ⚠ 这份"六个一起认"的名单 **只能放在这里**:自检把 GameRoot.cs 里每个结局过场标志的
    //   出现次数钉死为 2(ApplyIslandView / KeepIslandView 那两处镜头让位),所以 GameRoot
    //   只调 `CutsceneGate.Any()`;ApplyIslandView / KeepIslandView 也不并过来 —— 坠机过场
    //   发生在拾荒阶段、与荒岛镜头无关,把 PlaneCrash 加进那两处反而会让计数变成 3。
    public static class CutsceneGate
    {
        public static bool Any()
        {
            return ShipEndingCutscene.Active || HelicopterEndingCutscene.Active || GullEndingCutscene.Active ||
                   BonesEndingCutscene.Active || ShadowScareCutscene.Active || PlaneCrashCutscene.Active;
        }
    }
}
