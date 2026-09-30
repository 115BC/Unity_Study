using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SixtySLike
{
    // demo 的表现层:全部几何体 + 文字标注(用户明确要求先不下模型)。
    // 字体走 OS 动态字体,避免依赖 TMP Essentials 是否已导入。
    public static class Ui
    {
        static Font _font;

        // 2026-09-28:用户指定全项目字体 = Assets/Scenes/STXINGKA.TTF(华文行楷)。
        // 两条路都要走:① 场景加载之前先按路径装好(编辑器里跑),这样机舱标牌/拾取标签这类
        //   在各自 OnEnable/Start 里建出来的文字不会再拿到旧字体;② GameRoot.uiFont(Inspector 里
        //   拖的那份)在 Awake 再覆盖一次,这条在打包后仍然有效(路径加载只在编辑器里有)。
        public const string ProjectFontPath = "Assets/Scenes/STXINGKA.TTF";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootFont()
        {
#if UNITY_EDITOR
            var f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(ProjectFontPath);
            if (f != null) Use(f);
#endif
        }

        public static Font ProjectFont { get { return _font; } }

        public static void Use(Font main)
        {
            if (main == null) return;
            _font = main;
            _digit = main;
            Debug.Log("[字体] 全项目改用 " + main.name + "(缺字形的符号会自动换成 ◆ / ●,见 Ui.Glyph)");
        }

        // 书法体不一定收得住 ♥ ⚡ 这类符号字形,画不出来会是一个空格。
        // 所以 HUD 上的图标都走 Glyph():要的那个字有就用,没有就退到 GB2312 里一定有的 ◆ / ●。
        public static string Glyph(string want, string fallback)
        {
            var f = Font;
            if (f == null || string.IsNullOrEmpty(want)) return fallback;
            // 字形是懒加进图集的:不先 Request 一次,GetCharacterInfo 会对"其实有"的字报 false
            f.RequestCharactersInTexture(want, 48, FontStyle.Normal);
            CharacterInfo ci;
            return f.GetCharacterInfo(want[0], out ci, 48, FontStyle.Normal) && ci.advance > 0 ? want : fallback;
        }

        // ↓ 这两支 OS 字体是"项目字体没加载上时"的后备(以前是全项目的默认方案):
        //   中文走雅黑 UI(比纯雅黑更接近航司 UI 的黑体),纯数字才敢上 DIN 系(Bahnschrift)。
        //   现在正文与倒计时都由 Ui.Use() 换成 STXINGKA,走不到这里。
        static readonly string[] TextFontNames = { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial" };
        static readonly string[] DigitFontNames = { "Bahnschrift SemiBold", "Bahnschrift", "DIN Alternate", "Segoe UI Semibold", "Arial Black", "Arial" };

        public static Font Font { get { if (_font != null) return _font; _font = Pick(TextFontNames, 32, "正文/标牌"); return _font; } }

        static Font _digit;
        public static Font DigitFont { get { if (_digit != null) return _digit; _digit = Pick(DigitFontNames, 64, "倒计时数字"); return _digit; } }

        // GetOSInstalledFontNames 里挑第一个命中的:CreateDynamicFontFromOSFont 静默回退时
        // 你根本不知道自己拿到的是哪支字体,所以挑中了要打一条日志
        static Font Pick(string[] names, int size, string role)
        {
            var installed = Font.GetOSInstalledFontNames();
            foreach (var want in names)
            {
                // 先精确后包含:微软雅黑是 .ttc 字体集合,注册表标签是 "Microsoft YaHei & Microsoft YaHei UI",
                // 枚举出来的写法未必正好等于 "Microsoft YaHei UI",只认精确匹配就会掉到后备字体
                var f = TryFont(installed, want, size, true) ?? TryFont(installed, want, size, false);
                if (f != null) { Debug.Log("[字体] " + role + " → " + want); return f; }
            }
            Debug.Log("[字体] " + role + " → 系统里一支都没命中,回退内置字体");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static Font TryFont(string[] installed, string want, int size, bool exact)
        {
            foreach (var have in installed)
            {
                bool hit = exact
                    ? string.Equals(have, want, System.StringComparison.OrdinalIgnoreCase)
                    : have != null && have.IndexOf(want, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hit) continue;
                var f = Font.CreateDynamicFontFromOSFont(have, size);
                if (f != null) return f;
                f = Font.CreateDynamicFontFromOSFont(want, size);   // 枚举写法拿不到时,按我们写的族名再要一次
                if (f != null) return f;
            }
            return null;
        }

        // ⚠ v0.32:锚点对写反(anchorMin.y > anchorMax.y)会算出一个 **负尺寸的矩形** —— 底板在、按钮在、
        //   文字却整块看不见(「日记页」与「收集页」都中过这个招)。它不抛异常、不进自检,
        //   表现就是"面板是空的",最难查的一类。所以在这里点名一次:判据只用传进来的四个值,不依赖 rect 语义。
        static void WarnIfInverted(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (anchorMin.x > anchorMax.x || anchorMin.y > anchorMax.y)
            {
                Debug.LogWarning("[60slike] UI「" + name + "」锚点对写反了 " + anchorMin + " → " + anchorMax
                                 + " ⇒ 矩形尺寸是负的,这块内容看不见。");
                return;
            }
            // 同一根锚线上 offset 反了,尺寸同样是负的 —— 日记页顶部那道分隔线就是这么丢的
            if (anchorMin.y == anchorMax.y && offsetMax.y - offsetMin.y < 0f)
                Debug.LogWarning("[60slike] UI「" + name + "」offsetMin.y=" + offsetMin.y + " 低于 offsetMax.y=" + offsetMax.y
                                 + " ⇒ 高度是负的,这块内容看不见。");
            if (anchorMin.x == anchorMax.x && offsetMax.x - offsetMin.x < 0f)
                Debug.LogWarning("[60slike] UI「" + name + "」offsetMin.x=" + offsetMin.x + " 大于 offsetMax.x=" + offsetMax.x
                                 + " ⇒ 宽度是负的,这块内容看不见。");
        }

        public static RectTransform Panel(Transform parent, string name, Color color,
                                          Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            WarnIfInverted(name, anchorMin, anchorMax, offsetMin, offsetMax);
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0.01f;
            return rt;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor,
                                 Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            WarnIfInverted(name, anchorMin, anchorMax, offsetMin, offsetMax);
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;      // §12.6:名字等一切外部输入一律 richText off
            return t;
        }

        public static Button Button(Transform parent, string name, string text, int size, Action onClick,
                                    Color bg, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
                                    Color? fg = null)
        {
            var rt = Panel(parent, name, bg, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            b.colors = colors;
            b.onClick.AddListener(() => onClick());

            var t = Label(rt, "text", text, size, TextAnchor.MiddleCenter, fg ?? Color.white,
                          Vector2.zero, Vector2.one, new Vector2(6, 2), new Vector2(-6, -2));
            t.raycastTarget = false;
            b.targetGraphic = img;
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void SetEnabled(Button b, bool on)
        {
            b.interactable = on;
        }
    }

    // 世界空间的几何体 + 文字标注
    public static class World
    {
        // 标注字号按"世界单位"给,不再被父级 localScale 二次放大
        public const float LabelWorldSize = 0.12f;
        // 只留一丝防 z-fighting 的间隙:标注是"印在表面上"的,不是飘着的
        public const float LabelGap = 0.012f;

        // 朝向相机的标注挂在这里:挂在物体身上会被"非等比缩放的父级 + 旋转" shear 掉
        // (隔板 0.12/2.4/1.8、门 1.8/2.2/0.2 都是非等比),所以标签与物体生命周期单独挂钩
        static Transform _labelRoot;
        static Transform LabelRoot()
        {
            if (_labelRoot != null) return _labelRoot;
            _labelRoot = new GameObject("WorldLabels").transform;
            return _labelRoot;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color,
                                           Transform parent = null, string label = null, bool collider = true,
                                           Vector3 euler = default)
        {
            return Primitive(type, name, pos, scale, Mat(color), parent, label, collider, euler);
        }

        // 给材质的那一个重载:几何体不变,只换表面。
        public static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale,
                                           Material mat, Transform parent = null, string label = null,
                                           bool collider = true, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            // 斜切的机身板要靠旋转摆:先 SetParent(false) 再给世界旋转,顺序反了会被父级旋转带偏
            if (euler.x != 0f || euler.y != 0f || euler.z != 0f) go.transform.rotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.Destroy(c);
            }
            if (label != null) LabelOnTop(go.transform, label);
            return go;
        }

        // ⚠ v0.37:物品的 **两种材质** 就收在这一个函数里(用户:"正常与破损用两种材质,后面可以换成两种不同的建模/贴图")。
        //   今天两种只差颜色;以后要差形状或贴图,**只改这个函数**(以及上面那个收 Material 的 Primitive 重载),
        //   沙滩上那些道具的创建代码一行都不用动。
        public static Material ItemMat(ItemSO k, bool broken) { return Mat(ItemColor(k, broken)); }

        static Color ItemColor(ItemSO k, bool broken)
        {
            if (broken) return new Color(0.30f, 0.12f, 0.11f);            // 破损:暗红
            if (k.lore) return new Color(0.34f, 0.28f, 0.46f);            // 彩蛋/暗线件:紫(与收集页同一支)
            if (k is ToolSO) return new Color(0.46f, 0.5f, 0.58f);        // 工具:钢灰
            if (k.consumable) return new Color(0.74f, 0.53f, 0.27f);      // 消耗品:罐头黄
            return new Color(0.56f, 0.58f, 0.62f);
        }

        static readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();

        // 同色共用一份材质:机舱有上百个几何体,每个新建一份材质是白烧 shader 实例
        static Material Mat(Color c)
        {
            Material m;
            if (_mats.TryGetValue(c, out m)) return m;
            m = new Material(Shader.Find("Standard"));
            m.color = c;
            _mats[c] = m;
            return m;
        }

        // 印在物体顶面上的小牌:再高的柱子也不会飘到半空
        public static TextMesh LabelOnTop(Transform owner, string text, Color? color = null)
        {
            var rd = owner.GetComponent<Renderer>();
            var b = rd != null ? rd.bounds : new Bounds(owner.position, Vector3.one);
            // 广告牌是竖直的:锚点居中会把下半截字插进物体顶面被几何体吃掉
            return LabelAt(owner, text, new Vector3(b.center.x, b.max.y + LabelGap, b.center.z),
                           LabelWorldSize, color, TextAnchor.LowerCenter);
        }

        // 贴在表面上的标牌:固定朝向、不做 billboard,字与墙面/门板共面,所以是"印上去"而不是"飘着"。
        // facing = 从表面指向观察者的方向(与 FaceCamera 同一套约定:局部 +Z 是可读面)。
        // ⚠ 荒岛那层不用它 —— 同一个"可读面"约定我在那里连着猜错两次,改用 LabelAt 的 billboard。
        public static TextMesh LabelFlat(Transform owner, string text, Vector3 worldPos, Vector3 facing,
                                         Color? color = null, float size = LabelWorldSize)
        {
            // 朝下的牌(天花板)会让 up 参考与 forward 共线,LookRotation 就得换一条轴
            var up = Mathf.Abs(facing.y) > 0.9f ? Vector3.forward : Vector3.up;
            var tm = MakeLabel(owner, text, worldPos, size, color, TextAnchor.MiddleCenter);
            tm.transform.rotation = Quaternion.LookRotation(facing, up);
            return tm;
        }

        // 朝向相机的版本:小物品/队友/危险物还是用它,贴在球面上的水平字一转视角就读不通了
        public static TextMesh LabelAt(Transform owner, string text, Vector3 worldPos, float size, Color? color = null,
                                       TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var tm = MakeLabel(owner, text, worldPos, size, color, anchor);
            tm.gameObject.AddComponent<FaceCamera>();
            return tm;
        }

        // 标注一律挂在尺度中立的根上:挂在物体身上会被"非等比缩放的父级 + 旋转" shear 掉
        // (隔板 0.12/2.4/1.8、门 1.8/2.2/0.2 都是非等比),所以只借 owner 管生命周期
        static TextMesh MakeLabel(Transform owner, string text, Vector3 worldPos, float size, Color? color,
                                  TextAnchor anchor)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(LabelRoot(), false);
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            // ⚠ font 必须显式给:TextMesh 的 font 默认是空的,以前这里只换了 renderer 的材质,
            // 几何体却按内置字体生成,UV 与图集对不上 → 一屏放大乱码(用户截图里那团白)。
            var f = Ui.Font;
            tm.font = f;
            tm.fontSize = 48;
            tm.text = text;
            tm.characterSize = size;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color ?? Color.white;
            tm.richText = false;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = f.material;
            mr.sharedMaterial.renderQueue = 3000;
            mr.sortingOrder = 5;

            var hold = owner.gameObject.GetComponent<OwnedLabel>();
            if (hold == null) hold = owner.gameObject.AddComponent<OwnedLabel>();
            hold.Add(tm.transform);      // 一个宿主可以挂多条(舱内 5 个区域名都挂在 cabin 上)
            return tm;
        }
    }

    // 物体被拾走/投进箱子/整段销毁时,它头顶的标注跟着消失
    public class OwnedLabel : MonoBehaviour
    {
        readonly List<Transform> labels = new List<Transform>();
        public void Add(Transform t) { if (t != null) labels.Add(t); }
        void OnDestroy()
        {
            for (int i = 0; i < labels.Count; i++)
                if (labels[i] != null) Destroy(labels[i].gameObject);
        }
    }

    // 世界标注正对相机,否则侧过来看就成了一条线
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c == null) return;
            var d = transform.position - c.transform.position;
            if (d.sqrMagnitude < 0.0001f) return;
            // ⚠ 参考轴用 **相机的 up**,不用世界 up:低头看脚边的物品时 d 几乎与世界 up 共线,
            //   LookRotation 的滚转在这个方向上退化 ⇒ 名字会转成"竖着一串"(机舱里对准物品就看得到)。
            //   相机 up 与 forward 永远垂直 ⇒ 没有退化方向;平视时两者几乎重合,荒岛那层观感不变。
            transform.rotation = Quaternion.LookRotation(d, c.transform.up);
        }
    }
}
