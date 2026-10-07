using UnityEngine;

namespace SixtySLike
{
    // v0.49(用户:"5.重新建立一些地形,玩家所处的荒岛有个小山包,有草地/稀树林")
    //   **没有用 Unity 的 Terrain 组件**:那要一份 TerrainData(高度图 + 每层 alphamap + 草皮贴图),
    //   全是要写进场景 YAML 的大资产,而这座山包在固定镜头里就是"岛的内侧缓缓抬起来的一块沙与草"。
    //   ⇒ 一个 **解析式高度场** + 烘焙时生成的网格:形状只有两个高斯包,改数就改这里,跑菜单④重烘。
    //   ⚠ 这条路上还顺手解决了 §11-83 ⑤ 的前置之一:"24 个道具落点全在 y=0" —— 落点要不要按地形采样,
    //     现在有一个免费的 `Height(x, z)` 可问(将来第一人称那条路 §11-84 更要它)。
    //     **本轮一个落点都没动**:山包整体落在道具区之外(见下面两个包的中心与 σ)。
    public static class IslandTerrain
    {
        // 屏幕左 = 世界 +x(相机 yaw=180)。
        // v0.59(用户:"**地形起伏不用太大,稍微带点即可**"):v0.49 那两个包(2.4 米主包 + 1.7 米那道脊)
        //   换成 **一个 1.1 米的缓包**。顺带把 v0.57 记的那笔旧账结掉了:旧山脊在网格边界 x=21 那一刀
        //   还剩 1.3 米高 ⇒ 自由视角能看见断面;现在包心离网格四边都 ≥3σ(边界处高度 <1 毫米),**断面没有了**。
        //   ⚠ 包心 (9,-2) 不是随手放的:它离 **每个落点** 都够远,`DemoChecks.V049T()` 那条"落点高度 <0.15 米"
        //     才继续成立(最紧的是 尸骨 (3.2, 0.4),高 0.12 米)—— 挪包之前先跑自检看这条。
        static readonly float[] Main = { 9f, -2f, 3.0f, 1.1f };      // x, z, σ, 高

        public static float Height(float x, float z)
        {
            return Bump(x, z, Main);
        }

        static float Bump(float x, float z, float[] b)
        {
            float dx = x - b[0], dz = z - b[1];
            return b[3] * Mathf.Exp(-(dx * dx + dz * dz) / (2f * b[2] * b[2]));
        }

        // 沙滩顶面(所有道具落点 y=0 就是踩在这一面上)
        public const float SandTopY = 0.02f;

        // ── v0.57(用户:"**荒岛建模为一个岛,周围全是海(固定资产)**" + 定案「**大圆岛:现有东西全不动**」)──
        //   **岛形只有一个出处**:烘焙器拿它生成网格、自由视角拿它当"不能下水"的墙、自检拿它算可达 ——
        //   三处各写一份半径,就一定会出现"能走出岛"或"海边那块点不到"。
        //   数字是照着 **现有内容的包围盒** 定的(不能挪东西是用户的裁定):
        //     道具区 x ±5.5 / z 3.4~8.2、山包高斯尾巴伸到 x≈23、椰树到 x 11.6、海鸥挂点 x -8.5
        //   ⇒ 中心 (5, 2) 让最远的山尾(x≈23)离中心约 18.5 米,干岛面半径取 20 就全包住了。
        public static readonly Vector2 Center = new Vector2(5f, 2f);
        public const float FlatR = 20f;          // 干的岛面(顶面 = SandTopY)
        public const float BeachR = 23.5f;       // 再往外是入水的沙滩,一路降到 BeachDeepY
        public const float BeachDeepY = -0.6f;
        public const float SeaY = -0.02f;        // 海面顶面:比沙顶 **低 4 厘米** ⇒ 浪线落在沙滩上,而不是把岛泡在水里
        public const float WalkR = 19.6f;        // 自由视角能走到的半径(他裁的"沙滩+山坡可以,不能下水")

        public static float Radius(float x, float z)
        {
            float dx = x - Center.x, dz = z - Center.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// **浪线在第几米**(岛面降到与海面齐平的那一圈)。
        /// 烘焙 大海 那块可点板、摆 钓竿、以及"走到沙滩尽头"都问这一条,而不是各自写一个 z= ——
        /// 岛一旦改大小,这几处必须一起跟着走(v0.52 那版是从 `seaAnchor` 反推的,岛一变形就全错位)。
        public static readonly float WaterR =
            FlatR + (BeachR - FlatR) * (SandTopY - SeaY) / (SandTopY - BeachDeepY);

        /// 岛面本身(不含山包):岛心是平的干沙,出了 FlatR 沿沙滩斜着入水。
        public static float SurfaceY(float x, float z)
        {
            float r = Radius(x, z);
            if (r <= FlatR) return SandTopY;
            float t = Mathf.Clamp01((r - FlatR) / (BeachR - FlatR));
            return Mathf.Lerp(SandTopY, BeachDeepY, t);
        }

        /// 站在这块地面上脚底该在哪:**只有"走路的人"采这个**(道具落点仍然一律 y=0)。
        /// v0.53(§11-84 第一人称共存)那条规则照旧:**只有走路的人按这条采样**——
        ///   山包不许吞任何一块可点面(§11-83 ⑤),所以 `DemoChecks.V049T()` 里"落点高度 <0.15 米"
        ///   那条断言 **继续生效**,不要因为这里能采样就顺手去搬落点。
        public static float GroundY(float x, float z) { return SurfaceY(x, z) + Height(x, z); }

        /// 在岛形之内吗(自由视角的墙 / 自检的可达判据都问这一条)
        public static bool OnLand(float x, float z) { return Radius(x, z) <= WalkR; }

        // ── v0.58(用户:"**之前的岛屿改为外围一圈是沙滩,中间为草地(有椰树,灌木等植物),物品放在沙滩上**")──
        //   分界从 **按海拔**(v0.49 那条 0.55 米线)**换成按位置**;v0.49 那个常量没有读者了,已删 ——
        //   留着一个没人读的数,比留一句注释更容易骗到下一次改代码的人。
        // v0.59(用户:"**自检没问题,但是我要的效果没出现,岛屿外围是沙滩,中间是草地加植被**" +
        //   定案「**保持现状,图在自由视角里看**」)⇒ v0.58 那个"偏心到山包上"的草地圆 **作废**,改成真·同心:
        //   **草地 = 岛心往外到 `GrassOuterR` 的一整片,但挖掉营地那一块沙地**(`CampClear` 那个圆)。
        //   为什么 v0.58 不敢同心:营地在岛心南侧,同心圆会把营地吞进草地。挖一个洞就两全了 ——
        //   走起来是"外围一圈沙 → 中间一整片绿地(椰树/灌木/草丛/缓包都在里头)→ 营地那一块是沙地空地",
        //   固定镜头里绿地从画面约 58% 高开始(营地沙地在它前面),所以 **镜头与营地一个都不用动**。
        //   ⚠ `CampClearR = 8.4` 是照 **营地包围盒 + 队友挂点** 量的(24 格四角离 (0,5.8) 是 6.0 米,
        //     队友挂点 (-6.8,1.6) 是 8.0 米)⇒ 再小就会把队友或某一格吞进草地,`V058()` ③ 会红。
        //   ⚠ "物品放在沙滩上"这条由 **挖洞** 保证,不是由偏心保证:`DemoChecks.V058()` ③ 钉住每个落点 `!OnGrass`。
        public const float GrassOuterR = 16.5f;      // 草地外沿:再往外 3.5 米沙带 = "外围一圈沙滩"
        public static readonly Vector2 CampClear = new Vector2(0f, 5.8f);
        public const float CampClearR = 8.4f;

        /// 这块地面是草地吗?外沿带两条起伏(不是死板圆弧),营地那一块挖成沙地空地。
        // 草地外沿半径(随角度摆动)。**`OnGrass` 和烘焙的草地贴片(`DemoAssetBaker.GrassPatch`)走同一条曲线** ——
        // v0.60:写成两份数必 drift(贴片边界和玩法边界各说各话 = 脚踩在沙上却站在绿里),提成方法两边调。
        public static float GrassBoundaryR(float a)
        {
            return GrassOuterR + Mathf.Sin(a * 3f) * 0.9f + Mathf.Sin(a * 7f + 1.3f) * 0.45f;
        }

        public static bool OnGrass(float x, float z)
        {
            float a = Mathf.Atan2(z - Center.y, x - Center.x);
            if (Radius(x, z) > GrassBoundaryR(a)) return false;
            float dx = x - CampClear.x, dz = z - CampClear.y;
            return Mathf.Sqrt(dx * dx + dz * dz) > CampClearR;
        }

        // 坡太陡就不摆东西:树/石头这类"贴着地"的件落在陡坡上会看起来插在空气里。
        public static bool Placeable(float x, float z)
        {
            const float e = 0.25f;
            return Mathf.Max(Mathf.Abs((Height(x + e, z) - Height(x - e, z)) / (2f * e)),
                             Mathf.Abs((Height(x, z + e) - Height(x, z - e)) / (2f * e))) < 0.62f;
        }
    }
}
