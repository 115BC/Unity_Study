using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    /// v0.58(用户:"**如果能有海浪更好**" → 定案「**演出:浪线一条会动的泡沫带**」)
    /// 浪线上那三条会推进 / 消退的白泡沫带。
    ///
    /// ⚠ **它不是机制**:不参与 涨潮 的判定、不挡走路、不进任何结算 —— 它只是让那条几何接缝不再是死的。
    ///   (涨潮 从 v0.x 起就是夜晚事件的数值,与这条带子之间 **没有任何联系**;哪天要让海浪参与那条线,
    ///    那是新机制,要单独裁定,而且"画着大浪而那一晚没涨"这种两本账必须一起解决。)
    ///
    /// 三条口径:
    /// ① **半径只有一个出处**:`InnerR / OuterR` 全部从 `IslandTerrain.WaterR` 推 —— 这条带子的位置就是浪线,
    ///    岛改大小它自己跟着走(与 v0.57 那条"海边那几件不许写死 z"同一个道理)。
    /// ② **不每帧重算顶点**:环的中心就是岛心 ⇒ 均匀缩放 x/z 就是"半径呼吸",所以 `Update` 只写 `localScale`
    ///    与材质 alpha(顶点 y 是烘焙时按 `SurfaceY` 采好的,±0.22 米的呼吸带来的高度误差 <5 厘米,看不见)。
    /// ③ **不受光的白沫会被夜里照成一片刺眼的亮**,所以走透明 Standard(受光)而不是 `Unlit/Color`:
    ///    夜里环境光压到 0.13 那一档时它自己就暗下去了,不需要再开一条"夜晚开关"。
    public class Shoreline : MonoBehaviour
    {
        public static float InnerR { get { return IslandTerrain.WaterR - 0.95f; } }
        public static float OuterR { get { return IslandTerrain.WaterR + 0.45f; } }

        Material[] mats;
        Transform[] bands;
        float t;

        /// 由 `IslandPhase` 创建(挂在场景的 Island 层下,阶段销毁时由它一并收掉)。
        public static Shoreline Build(Transform parent)
        {
            var go = new GameObject("Shoreline");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(IslandTerrain.Center.x, 0f, IslandTerrain.Center.y);
            var s = go.AddComponent<Shoreline>();
            s.BuildBands();
            return s;
        }

        void BuildBands()
        {
            var mesh = Band(InnerR, OuterR, 96);
            bands = new Transform[3];
            mats = new Material[3];
            for (int i = 0; i < bands.Length; i++)
            {
                var b = new GameObject("Band" + i);
                b.transform.SetParent(transform, false);
                b.AddComponent<MeshFilter>().sharedMesh = mesh;      // 三条共用一张网,靠缩放错开
                b.AddComponent<MeshRenderer>().sharedMaterial = mats[i] = Foam();
                bands[i] = b.transform;
            }
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < bands.Length; i++)
            {
                float ph = t * 0.85f + i * 2.1f;
                // 三条各占一档基础半径(1 - 0.012*2 ≈ 0.5 米间距),再各自呼吸 ±0.22 米
                float k = 1f - i * 0.026f + Mathf.Sin(ph) * 0.011f;
                bands[i].localScale = new Vector3(k, 1f, k);
                var c = mats[i].color;
                c.a = 0.13f + 0.19f * (0.5f + 0.5f * Mathf.Sin(ph + 1.1f));   // 沫起来时最亮
                mats[i].color = c;
            }
        }

        // 透明那一档要自己配齐:Standard 的 `_Mode` 数值只是给 Inspector 看的,
        // 真正让它混起来的是 blend 因子 + `_ALPHABLEND_ON` + renderQueue(少一条就是"白沫看不见"或"白沫挡住一切")。
        static Material Foam()
        {
            var m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.color = new Color(0.94f, 0.98f, 1f, 0.22f);
            return m;
        }

        static Mesh Band(float inner, float outer, int seg)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                var c0 = P(inner, a0); var c1 = P(outer, a0); var c2 = P(outer, a1); var c3 = P(inner, a1);
                int n = v.Count;
                // 绕序与烘焙器那个 `Quad` 同一条(极坐标 (r,a) → (x,z) 保向),反了这条带子就是看不见
                v.Add(c0); v.Add(c3); v.Add(c1);
                v.Add(c3); v.Add(c2); v.Add(c1);
                tri.Add(n); tri.Add(n + 1); tri.Add(n + 2);
                tri.Add(n + 3); tri.Add(n + 4); tri.Add(n + 5);
            }
            var m = new Mesh { name = "ShoreFoam" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            float sum = 0f;
            foreach (var nn in m.normals) sum += nn.y;
            if (sum < 0f) Debug.LogError("[海浪] ShoreFoam 的绕序反了(法向平均朝下)⇒ 泡沫带会整个看不见。");
            return m;
        }

        // 局部坐标(物体已经摆在岛心):y 采地面高度再抬 1.2 厘米 ⇒ 贴沙不扎穿
        static Vector3 P(float r, float a)
        {
            float wx = IslandTerrain.Center.x + Mathf.Cos(a) * r;
            float wz = IslandTerrain.Center.y + Mathf.Sin(a) * r;
            return new Vector3(wx - IslandTerrain.Center.x, IslandTerrain.SurfaceY(wx, wz) + 0.012f, wz - IslandTerrain.Center.y);
        }
    }
}
