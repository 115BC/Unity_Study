using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SixtySLike
{
    // v0.75(用户 v0.71 ⑧⑨):「第一人称才有天空盒,否则就远处有云即可」+
    //   「装天空盒时把 ambient 钉成固定色,天空盒只当背景,不参与环境光」。
    //
    //   三条口径:
    //   ① **环境光的唯一开关在这里**:荒岛一进来就把场景原来那份(Skybox 模式)抄成一个固定色、切到 Flat,
    //      离开荒岛时原样交回。`IslandPhase.SetNightLook` 只在这两个固定色之间切(白天 / 夜里的冷蓝),
    //      **不再碰 Skybox 模式** —— 否则夜里改一次、天亮改回去,两次都要赌场景那份还在。
    //   ② **清屏方式按视角切**:固定镜头 = SolidColor(白天纯天蓝 / 夜里深蓝,由 `SetNightLook` 给);
    //      自由视角(白天)= Skybox。夜里本来就不许进第一人称(v0.56),这里再读一次 `IslandWalk.Active` 即可。
    //   ③ 天空盒与云都是 **纯演出**:不参与任何判定、不给任何数值。
    public class IslandSky : MonoBehaviour
    {
        // 抄不到场景那份时的兜底(与"现在的白天观感"接近的一支);正常路径会用 ambientProbe 现场采样。
        static readonly Color FallbackDayAmbient = new Color(0.52f, 0.56f, 0.62f);

        GameRoot root;
        AmbientMode _origMode;
        Color _origColor;
        Material _origSky;
        CameraClearFlags _origClear;
        Material _sky;
        bool _done;
        readonly List<GameObject> _clouds = new List<GameObject>();

        public Color DayAmbient { get; private set; }

        public static IslandSky Attach(GameRoot root, GameObject host)
        {
            var s = host.GetComponent<IslandSky>();
            if (s == null) s = host.AddComponent<IslandSky>();
            s.root = root;
            s.Init();          // ⚠ 不能放在 Awake:AddComponent 那一刻 root 还没赋值
            return s;
        }

        void Init()
        {
            if (_done) return;
            _done = true;
            DayAmbient = SampleAmbient();

            // 先把"进荒岛之前"那份原样存下来 —— 退出/换局时它必须回得去(与 v0.49 的夜环境光同一条教训:
            //   RenderSettings 是全局的,不交还就会污染下一局的机舱)。
            _origMode = RenderSettings.ambientMode;
            _origColor = RenderSettings.ambientLight;
            _origSky = RenderSettings.skybox;
            if (root != null && root.cam != null) _origClear = root.cam.clearFlags;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = DayAmbient;

            _sky = SkyMaterial();
            if (_sky != null) RenderSettings.skybox = _sky;
            BuildClouds();
            ApplyClear(false);
        }

        void Update()
        {
            if (root == null || root.cam == null) return;
            ApplyClear(IslandWalk.Active);
        }

        void ApplyClear(bool firstPerson)
        {
            if (root == null || root.cam == null) return;
            var want = firstPerson ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            if (root.cam.clearFlags != want) root.cam.clearFlags = want;
        }

        // 「抄当前观感」:场景是 Skybox 环境光模式,`ambientProbe` 就是那套天空的球谐。
        //   六个方向采一遍取平均 = 一个能代表"现在的环境亮度"的固定色;采样为黑(没算过 / 没天空)才用兜底。
        static Color SampleAmbient()
        {
            var dirs = new[]
            {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
            };
            var cols = new Color[dirs.Length];
            RenderSettings.ambientProbe.Evaluate(dirs, cols);
            var sum = Color.black;
            for (int i = 0; i < cols.Length; i++) sum += cols[i];
            sum /= cols.Length;
            if (sum.r + sum.g + sum.b < 0.05f) return FallbackDayAmbient;
            return new Color(Mathf.Clamp01(sum.r), Mathf.Clamp01(sum.g), Mathf.Clamp01(sum.b));
        }

        static Material SkyMaterial()
        {
            var sh = Shader.Find("Skybox/Procedural");
            if (sh == null)
            {
                Debug.LogWarning("[天空] 找不到 Skybox/Procedural ⇒ 第一人称的天空沿用场景里那份,云照常摆。");
                return null;
            }
            var m = new Material(sh);
            m.name = "IslandSky";
            // 太阳碟关掉:固定镜头里那盏太阳已经被夜灯压暗,天空盒再画一个太阳就会与光照对不上。
            m.SetFloat("_SunDisk", 0f);
            m.SetFloat("_SunSize", 0.04f);
            m.SetFloat("_AtmosphereThickness", 0.85f);
            m.SetColor("_SkyTint", new Color(0.52f, 0.68f, 0.92f));
            m.SetColor("_GroundColor", new Color(0.36f, 0.43f, 0.50f));
            m.SetFloat("_Exposure", 1.15f);
            m.hideFlags = HideFlags.DontSave;
            return m;
        }

        // 固定镜头那一侧的天空 = 远处几团云。绕 **岛心** 摆一圈,不写死某一边
        //   (与 v0.57 那条"海边不写死 z"同一个意图;岛心只有一个出处 `IslandTerrain.Center`)。
        void BuildClouds()
        {
            if (root == null || root.islandStage == null) return;
            var parent = root.islandStage.transform;
            var mat = World.Mat(new Color(0.95f, 0.96f, 0.98f));
            var c = new Vector3(IslandTerrain.Center.x, 0f, IslandTerrain.Center.y);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + 0.3f;
                float r = 48f + (i % 3) * 6f;
                var pos = c + new Vector3(Mathf.Cos(a) * r, 8.5f + (i % 2) * 2.2f, Mathf.Sin(a) * r);
                var g = World.Primitive(PrimitiveType.Sphere, "Cloud" + i, pos,
                                        new Vector3(10f, 1.7f, 5.2f), mat, parent, null, false,
                                        new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
                _clouds.Add(g);
            }
        }

        // 雨:挂在荒岛层下、由 `BuildProps` 那一批一起清掉(它是"那一晚"的演出,不是常驻天气)。
        //   粒子沿物体局部 +Z 走,所以整支发射器绕 X 转 90° ⇒ +Z 朝下。
        public static GameObject RainFx(Transform parent, Transform cameraPose)
        {
            var go = new GameObject("RainFx");
            go.transform.SetParent(parent, false);
            go.transform.position = cameraPose != null
                ? cameraPose.position + cameraPose.forward * 6f + Vector3.up * 1.5f
                : new Vector3(IslandTerrain.Center.x, 6f, IslandTerrain.Center.y);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.maxParticles = 900;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(9f, 14f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.045f);
            m.startColor = new Color(0.72f, 0.82f, 0.95f, 0.55f);
            m.gravityModifier = 1.6f;

            var em = ps.emission;
            em.rateOverTime = 420f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(22f, 1f, 16f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var pr = go.GetComponent<ParticleSystemRenderer>();
            var mat = World.ParticleMat();
            if (mat == null) pr.enabled = false;
            else pr.material = mat;
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.lengthScale = 3.5f;
            pr.velocityScale = 0.08f;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return go;
        }

        void OnDestroy()
        {
            // 只交还 **自己动过** 的东西;`_done` 没走完 Init 就退出时一份都不该动。
            if (!_done) return;
            RenderSettings.ambientMode = _origMode;
            RenderSettings.ambientLight = _origColor;
            RenderSettings.skybox = _origSky;
            if (root != null && root.cam != null) root.cam.clearFlags = _origClear;
            for (int i = 0; i < _clouds.Count; i++) if (_clouds[i] != null) Destroy(_clouds[i]);
            _clouds.Clear();
            if (_sky != null) Destroy(_sky);
        }
    }
}
