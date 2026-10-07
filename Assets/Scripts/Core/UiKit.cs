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

        // 2026-09-28 用户指定过 华文行楷;**2026-10-06 他自己换掉**:"10.字体换一个辨识度高的(黑体?)…下载并替换可以吗"
        //   ⇒ 现在 = **思源黑体 Noto Sans SC Regular**,SIL OFL 1.1(免费商用、可打包,许可证文本存在
        //     `Assets/Art/FONT-OFL-NotoSansSC.txt`)。
        //   ⚠ 加载方式一起换了:原来那条是"**按资产路径**加载(只有编辑器里有)+ `GameRoot.uiFont`(Inspector 那份)"
        //     两条路;改成 **按 Resources 名字取** —— 与模型/音乐同一约定,打包后同样成立,
        //     而且 `uiFont` 那个字段 **已删**(它会在 Awake 把字体覆盖回旧的那支,是个会说谎的第二真源)。
        public const string ProjectFontResource = "Fonts/NotoSansSC-Regular";
        // 兜底那支行楷仍然留在原地(用户说文件不删):Resources 里那份没导入时先顶一下,总比整屏豆腐好。
        const string LegacyFontPath = "Assets/Scenes/STXINGKA.TTF";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootFont()
        {
            var f = Resources.Load<Font>(ProjectFontResource);
            if (f == null)
            {
#if UNITY_EDITOR
                f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(LegacyFontPath);
#endif
                if (f != null) Debug.LogWarning("[字体] Resources/" + ProjectFontResource + " 没找到 ⇒ 先用旧的 " + f.name + " 顶着。");
            }
            if (f != null) Use(f);
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

        // 带图的一块 UI(Image + Sprite,不吃鼠标):生命值那枚心脏走的就是它(贴图见 `World.HeartSprite`)。
        //   `preserveAspect` ⇒ 矩形给得不方正时图不会拉扁;多出来的边距落在矩形里,视觉上居中。
        public static Image Icon(Transform parent, string name, Sprite sprite, Color color,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            WarnIfInverted(name, anchorMin, anchorMax, offsetMin, offsetMax);
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
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
                // 走 `Strip` 而不是直接 Destroy:编辑模式下 Destroy 不生效(见 `Net` 上面那条注释)。
                Strip(go.GetComponent<Collider>());
            }
            if (label != null) LabelOnTop(go.transform, label);
            return go;
        }

        // ── v0.49:真模型接线的唯一入口 ──────────────────────────────────────
        //   约定 = `Assets/Resources/Models/<key>.fbx`;破损 / 熄灭态放 `<key>_broken.fbx`。
        //   ⚠ 为什么用"按 key 从 Resources 取"而不是给 ItemSO 加一个模型引用字段:加字段要补 170 份 `.asset`
        //     (§7.2 第一条:缺 key 的引用字段读进来是 null),而"哪件东西用哪个模型"本来就是数据 —— 文件名对上就生效,
        //     **没放过模型的东西自动退回灰盒方块**,一条既有资产都不用回改。要换某一件的模型 = 换一个文件。
        static readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>();

        static GameObject LoadModel(string name)
        {
            GameObject g;
            if (_models.TryGetValue(name, out g)) return g;
            g = Resources.Load<GameObject>("Models/" + name);
            _models[name] = g;              // 查不到也要记下来:否则每次摆放都重新走一遍 Resources 查找
            return g;
        }

        public static bool HasModel(string key) { return !string.IsNullOrEmpty(key) && LoadModel(key) != null; }
        // 有没有"专门的破损/熄灭模型"。没有的话调用方会把破损色压在本体上(见 IslandPhase 那条 tint 分支)。
        public static bool HasBrokenModel(string key) { return !string.IsNullOrEmpty(key) && LoadModel(key + "_broken") != null; }

        // v0.53(2026-10-06,**渔网 到手时发现的口子**):这个模型 **有没有作者起的材质**?
        //   有些外部模型整包没有 `Material` 记录(这件 C4D 导出的渔网就是 0 条;`Grass_Large.fbx` 那次是 `Default-Material`)。
        //   ⚠ 为什么这件事必须管:项目的 v0.37 口径是 **"完好 / 破损 由材质说话"**(`World.ItemMat`),
        //   而调用方只在"破损且没有专门破损模型"时才压色 ⇒ 无材质的模型 **完好态会拿 Unity 内置白模**,
        //   那条口径在它身上根本不成立。所以调用方要用这个函数把两种状态一起压。
        public static bool ModelHasMaterial(string key, bool broken)
        {
            var pref = Model(key, broken);
            if (pref == null) return false;
            foreach (var rd in pref.GetComponentsInChildren<Renderer>())
            {
                var m = rd.sharedMaterial;
                if (m != null && m.name != "Default-Material" && m.name != "Default-Particle") return true;
            }
            return false;
        }

        public static GameObject Model(string key, bool broken)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var m = LoadModel(key + (broken ? "_broken" : ""));
            return m != null ? m : LoadModel(key);      // 破损态没有单独模型 ⇒ 交回本体,由调用方决定要不要压色
        }

        // v0.49:**一条通用规则摆不正 46 个手工模型** —— 菜单⑤ 量出来的尺寸就是证据:
        //   `rod` 0.14×2.68×0.65、`key` 0.29×0.10×0.87、`spear` 0.53×9.7×0.33 这类细长件按"最长边归一"
        //   会变成**竖着插在沙上的一根线**。所以这里给少数件写死"先躺平 / 再单独倍率",
        //   不在表里的走默认(最长边归一到格子、不额外转)。
        //   ⚠ 旋转是在**量尺寸之前**做的,所以归一化看到的是躺平之后的包围盒。
        static readonly Dictionary<string, Tuple<Vector3, float>> Placement = new Dictionary<string, Tuple<Vector3, float>>
        {
            // ⚠ v0.57(用户:"**鱼竿的角度设置为如图的145:180:180**" + Inspector 截图):这三个数是他 **拖出来的最终旋转**,
            //   不是"再补一个增量" ⇒ `rod` 登记在下面的 `Absolute` 里,走 **赋值** 那条路(见 `PlaceModel` 的例外说明)。
            //   历史:v0.52 (-24,18,0) → v0.55 Y+180 → v0.56 Z+180 → 现在直接采用他亲手定的最终值。
            { "rod",   Tuple.Create(new Vector3(145f, 180f, 180f), 1.00f) },   // 钓竿:斜靠在海边那一格(落点 = `IslandPhase.RodSeaPos`)
            // v0.56 渔网:绕 Z 转 180°(用户:"渔网旋转180度")。这件 **仍是右乘的增量**(不在 `Absolute` 里)——
            //   因为他给的是"转 180"这个动作,不是 Inspector 上的最终值。
            //   ⚠ 他这次对钓竿写明"绕z轴"、对渔网只写"旋转180度" ⇒ 我按同一个轴(Z)做保持一致;
            //   要的是"原地转半圈"(绕竖轴)就把这里的 Z 挪到 Y:`(0f, 180f, 0f)`,说一声。
            { "net",   Tuple.Create(new Vector3(0f, 0f, 180f), 1.00f) },
            { "spear", Tuple.Create(new Vector3(90f, 0f, 90f), 1.00f) },   // 鱼叉:仍然横躺在沙上
            { "map",   Tuple.Create(new Vector3(90f, 0f, 0f), 1.00f) },    // 藏宝图(卷轴):铺开
            { "scrap", Tuple.Create(new Vector3(90f, 0f, 0f), 1.00f) },    // 残图:同上
            { "key",   Tuple.Create(new Vector3(90f, 0f, 0f), 0.90f) },    // 宝藏钥匙:平放
            { "herb",  Tuple.Create(new Vector3(0f, 0f, 0f), 0.80f) },     // 药草那株草极薄,收一点
            { "flint", Tuple.Create(new Vector3(0f, 0f, 0f), 0.85f) },     // 打火石已换成石头(火柴太细,见 ATTRIBUTION 旁的说明)
        };

        // v0.57:**这几个 key 的 `Placement` 三个数是"最终旋转"(直接赋值),不是"再补一个增量"(右乘)。**
        //   为什么要有这个例外:他给钓竿的是 **Inspector 上拖出来的那组值**(145/180/180),
        //   而 FBX 根节点自带一个 Z-up 修正 —— 若照红线 72 右乘,最终朝向 = 根修正 × 这组数,**复现不了他看到的那个样子**。
        //   ⚠ 红线 72 管的是"我们自己猜的增量"(那种情况赋值确实会抹掉根修正),这里管的是"他亲手定的最终值",两者不冲突。
        //   新增成员前请想清楚:你拿到的是"转多少度"还是"最终是多少度"。
        static readonly HashSet<string> Absolute = new HashSet<string> { "rod" };

        // ⚠ 临时排查开关(v0.49):每件放出来的模型打一行"原生尺寸 / 目标 / 用了多少倍 / 最终尺寸 / 底面y"。
        //   对数比看截图快 —— 比例问题定完就把它改回 false(一行一件,Play 一次几十条)。
        public static bool FitDebug = true;

        // **归一化看身高(Y)的那几件**:人形的原生最长边是"张开的手臂",不是头顶(见 `PlaceModel` 里那条)。
        // 只有这三件走这条路 —— 狐狸/海鸥/骸骨 那几件的原生最长边就是它们的"长度",按最长边收才对(别顺手加)。
        static readonly HashSet<string> ByHeight = new HashSet<string>
        { "mate_pilot", "mate_navigator", "mate_mechanic" };

        // v0.50(用户原话:"1.毯子的缩放改为1:1:1 / 2.钓鱼竿的缩放改为100:100:100,能架在海边吗 / 4.鱼叉改为25:25:25";
        //   我追问这三个数指什么,你选的读法是 **「倍率:100 = 原尺寸」** ⇒ 这几件 **不再归一化到格子**,
        //   直接用模型导入时的原生包围盒 × 倍率:
        //     毯子 1 与 钓竿 100 在这个读法下都是"原尺寸"(毯子 = 3×0.1×2 米的椭圆毯;钓竿 = 2.68 米);
        //     鱼叉 25 = 原尺寸的 25%(它原生 9.7 米,那是根电线杆,这个数把它收到 2.4 米)。
        //   ⚠ **这张表只在沙滩那一层生效**(`PlaceModel` 的 `allowNative`):同一件在机舱里仍然按格子归一 ——
        //     2.68 米的钓竿在 2.5 米宽的过道里会横穿两头,而机舱净高只有 1.9 米,竖起来直接穿天花板。
        static readonly Dictionary<string, float> NativeRatio = new Dictionary<string, float>
        {
            { "blanket", 1f },
            { "rod", 1f },        // 站直 + 微微斜靠(朝向见上面 `Placement`)= 你要的"架在海边"
            { "spear", 0.25f },
        };

        public static float NativeRatioOf(string key)
        {
            float r;
            return !string.IsNullOrEmpty(key) && NativeRatio.TryGetValue(key, out r) ? r : 0f;
        }

        // 把模型摆成"原来那块方块该在的位置与大小":最长边收进 target、底面贴到 pos 的高度、水平居中到 pos。
        //   ⚠ 两处踩过坑的地方,别再改回去(用户实测:"差不多每个东西都要放大 100 倍才看得见,有些卡到地面以下"):
        //     ① 量尺寸**不能读 `renderer.bounds`** —— 刚 `Instantiate` 的那一帧 Unity 可能还没算出世界包围盒,读回来是 0,
        //        于是"缩放没生效 + 底面对齐把东西推到原点下面"。这里自己按 mesh.bounds × 各层矩阵走一遍,不依赖渲染。
        //     ② 缩放是 **乘** 在根节点已有的 localScale 上,不是赋值 —— 这批 FBX 的根节点常常自带 0.01(Blender 按厘米导出),
        //        赋值等于把它抹掉,东西立刻小一百倍。
        public static GameObject PlaceModel(Transform parent, string key, bool broken, Vector3 pos,
                                            Vector3 targetSize, Material tint = null, bool allowNative = false)
        {
            var pref = Model(key, broken);
            if (pref == null) return null;
            var go = UnityEngine.Object.Instantiate(pref);
            go.name = key;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            float extra = 1f;
            Tuple<Vector3, float> pl;
            if (Placement.TryGetValue(key, out pl))
            {
                // v0.57(用户:"**鱼竿的角度设置为如图的145:180:180**",附 Inspector 截图)⇒ `Absolute` 里那几件的三个数
                //   是他 **在 Inspector 里拖出来的最终旋转**,不是"再补一个增量" ⇒ 这里 **赋值**,才能一模一样复现他看到的样子。
                //   ⚠ 这是对红线 72("朝向修正一律右乘,赋值会抹掉 FBX 根节点自带的 Z-up 修正")的 **一处有意例外**:
                //     红线管的是"我们自己猜的增量"(那种情况赋值确实会翻车),而这里是"他亲手定的最终值"。
                //     **别的件照旧右乘**,要新增绝对值就往这个集合里加,并且同样写清楚是谁定的。
                go.transform.rotation = Absolute.Contains(key)
                    ? Quaternion.Euler(pl.Item1)
                    : go.transform.rotation * Quaternion.Euler(pl.Item1);
                extra = pl.Item2;
            }
            Vector3 mn, mx;
            if (WorldAABB(go, out mn, out mx))
            {
                // ⚠ **人形按身高(Y)归一,别的按最长边** —— 这条是被实测日志逼出来的:那三具 `Male_*` 的原生最长边
                //   是 **张开的手臂**(`mate_pilot` 归一后 = `(1.8, 1.677, 0.327)`),于是"按身高 1.8"被做成了
                //   "按翼展 1.8",人反而变矮了。⇒ 在 `ByHeight` 里的 key,目标值取 `targetSize.y`、收敛量取 `size.y`。
                bool byHeight = ByHeight.Contains(key);
                // `NativeRatio` 里那几件(毯子/钓竿/鱼叉)= **原尺寸 × 倍率**,不再收进格子;
                // 其余仍是"最长边(或身高)收进 target"。⚠ 只有沙滩那层允许(`allowNative`),机舱照旧归一。
                float nr = allowNative ? NativeRatioOf(key) : 0f;
                var n0 = mx - mn;
                var native = Mathf.Max(n0.x, Mathf.Max(n0.y, n0.z));
                var target = nr > 0f
                    ? (byHeight ? n0.y : native) * nr
                    : (byHeight ? targetSize.y : Mathf.Max(targetSize.x, Mathf.Max(targetSize.y, targetSize.z))) * extra;
                var total = 1f;
                // **迭代收敛**,不要只量一遍:刚 Instantiate 出来的层级矩阵不一定马上反映新的 localScale,
                // 一遍算出来的倍数可能"看着执行了、实际没生效"(现场就是用户报的"除了罐头全都还很小")。
                // 每一轮都重量一次,收到 2% 以内就停 —— 最多四轮,再多就是模型本身有问题,该去查文件。
                for (int pass = 0; pass < 4; pass++)
                {
                    var size = mx - mn;
                    var m = byHeight ? size.y : Mathf.Max(size.x, size.y, size.z);
                    if (m <= 0.00001f || target <= 0.00001f) break;
                    var err = target / m;
                    if (Mathf.Abs(err - 1f) < 0.02f) break;
                    go.transform.localScale *= err;
                    total *= err;
                    if (!WorldAABB(go, out mn, out mx)) break;
                }
                // 最后只做一次**世界平移**:底面贴到 pos 的高度、水平居中到 pos(平移不受旋转与枢轴影响)。
                // ⚠ 方向别写反:要把"量出来的 min.y"搬到 pos.y,位移是 `pos - 量出来的值`。
                //   写成 `量出来的 - pos` 就是"每件事都被往下推自身高度的一半多" ⇒ 用户报的"全都陷进沙里"。
                if (WorldAABB(go, out mn, out mx))
                {
                    var c = (mn + mx) * 0.5f;
                    go.transform.position += new Vector3(pos.x - c.x, pos.y - mn.y, pos.z - c.z);
                }
                if (FitDebug)
                {
                    Vector3 a, b;
                    var has = WorldAABB(go, out a, out b);
                    var f = b - a;
                    // 原生那一栏 **三个轴都打**:上一版只打最长边,于是"1.8 到底是身高还是翼展"看不出来源,
                    // 只能靠人形那条推。下次再报比例不对,这一行就该能定案。
                    Debug.Log(string.Format("[fit] {0}  原生=({1:0.###}, {2:0.###}, {3:0.###}) 最长={4:0.####}  倍率={5:0.####}  最终=({6:0.###}, {7:0.###}, {8:0.###})  归一={9}  目标={10:0.###}  底面y={11:0.###} 落点y={12:0.###}",
                        key, n0.x, n0.y, n0.z, native, total, f.x, f.y, f.z,
                        (nr > 0f ? "原尺寸×" + nr.ToString("0.##") + (byHeight ? " 按Y" : "") : (byHeight ? "身高Y" : "最长边")),
                        target, a.y, pos.y));
                    if (!has) Debug.LogWarning("[fit] " + key + " 归一化之后量不到了 —— 层级里有问题");
                }
            }
            if (tint != null)
                foreach (var rd in go.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = tint;
            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();   // v0.49:牌子要能被悬浮命中(§11-80)
            return go;
        }

        // 物体在世界空间的顶面 y(牌子高度按它算,不能按方块尺寸猜 —— 模型有高有矮)
        public static float TopOf(GameObject go)
        {
            Vector3 mn, mx;
            return WorldAABB(go, out mn, out mx) ? mx.y : go.transform.position.y;
        }

        // 世界包围盒:把每个网格的本地包围盒 8 个角点乘上它自己的 localToWorldMatrix 再合起来。
        //   蒙皮模型(动物 / 人)走的是 SkinnedMeshRenderer,`MeshFilter` 那条路拿不到网格,两边都要认。
        public static bool WorldAABB(GameObject root, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int found = 0;
            var rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                var skinned = rs[i] as SkinnedMeshRenderer;
                var mesh = skinned != null ? skinned.sharedMesh : null;
                if (mesh == null)
                {
                    var mf = rs[i].GetComponent<MeshFilter>();
                    mesh = mf != null ? mf.sharedMesh : null;
                }
                if (mesh == null) continue;
                var b = mesh.bounds;
                var mtx = rs[i].transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = mtx.MultiplyPoint(new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                found++;
            }
            return found > 0;
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
        // (v0.49 改成 public:运行时新摆的东西(远方那架剪影)也要走这同一个缓存,不要再各建一份材质)
        public static Material Mat(Color c)
        {
            Material m;
            if (_mats.TryGetValue(c, out m)) return m;
            m = new Material(Shader.Find("Standard"));
            m.color = c;
            _mats[c] = m;
            return m;
        }

        // v0.49(用户:"灯光系统在篝火和手电筒再加一下(二者的光)"):运行时加一盏点光。
        //   ⚠ **挂在宿主物体下面** ⇒ `BuildProps` 每轮 Destroy 整批 props 时灯跟着消失,
        //     不需要再开一张"待清理灯光"的名单(那条路迟早会漏)。
        //   shadows 显式关:这盏灯只负责"看着像有火",实时阴影会把场景每一块几何体拖进 shadow pass。
        public static Light PointLight(GameObject host, Vector3 localPos, Color c, float intensity, float range, bool flicker)
        {
            var go = new GameObject("light");
            go.transform.SetParent(host.transform, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            if (flicker) go.AddComponent<GlowLight>().src = l;
            return l;
        }

        // v0.49(用户:"3.触发器写在几个漏电/起火的地方,触碰到之后除了惩罚还有相应的贴图特效")
        //   特效 = 内置 `ParticleSystem` + **运行时程序化生成的一张圆点贴图** ⇒ 零外部素材、零资产文件、零许可账。
        //   `fire` 只改"怎么动":火往上飘(负重力、寿命长、越飞越小),电火花四处迸(正重力、寿命极短)。
        //   ⚠ **没有可用 shader 时返回 null,调用方必须能收**(见 `SparkMat` 上面那段事故)。
        public static ParticleSystem Sparks(GameObject host, Color col, bool fire)
        {
            var mat = SparkMat();
            if (mat == null) return null;

            var ps = host.GetComponent<ParticleSystem>();
            if (ps == null) ps = host.AddComponent<ParticleSystem>();

            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.maxParticles = 220;
            m.startLifetime = fire ? new ParticleSystem.MinMaxCurve(0.55f, 1.05f)
                                   : new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            m.startSpeed = fire ? new ParticleSystem.MinMaxCurve(0.5f, 1.1f)
                                : new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            m.startSize = fire ? new ParticleSystem.MinMaxCurve(0.26f, 0.44f)
                               : new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            m.startColor = col;
            m.gravityModifier = fire ? -0.05f : 1.2f;

            var em = ps.emission;
            em.rateOverTime = fire ? 16f : 7f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.6f, fire ? 0.3f : 0.6f, 0.6f);
            sh.randomDirectionAmount = fire ? 0.2f : 1f;

            var fol = ps.colorOverLifetime;
            fol.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0f, 1f) });
            fol.color = new ParticleSystem.MinMaxGradient(g);

            var sol = ps.sizeOverLifetime;
            sol.enabled = fire;
            sol.size = new ParticleSystem.MinMaxCurve(1f, 0.3f);

            var pr = host.GetComponent<ParticleSystemRenderer>();
            if (pr == null) pr = host.AddComponent<ParticleSystemRenderer>();
            pr.material = mat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        static Texture2D _dot;
        static Material _spark;

        // 一颗"中心实、边缘化开"的白点:粒子的形状全靠这张图,自己算(半径方向二次衰减,硬边会像方块)
        static Texture2D DotTexture()
        {
            if (_dot != null) return _dot;
            const int n = 32;
            _dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
            _dot.name = "spark-dot";
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _dot.Apply();
            _dot.hideFlags = HideFlags.DontSave;      // 只活在运行时:别让谁把它误当资产烘进项目
            return _dot;
        }

        // ⚠ **不能用 `Resources.GetBuiltinResource<Material>("Default-Particle.mat")`** —— 用户实跑抓到:
        //   Unity 2022 报 `Failed to find Default-Particle.mat` / `could not be loaded from the resource file`,
        //   返回 null ⇒ 我那句 `Instantiate(null)` 抛 ArgumentException,而它正在 `AddHazard` 里跑,
        //   **整个拾荒阶段的 `Start()` 就此中断**(后面 Update/LateUpdate 那三条 NRE 全是连带:玩家、相机、HUD 都没建)。
        //   ⇒ 自己按 shader 造一支,并按"越像粒子越优先"往后退;一支都找不到就 **干脆不摆粒子** ——
        //     宁可没有特效,也不能把一整个阶段炸掉(这是运行时唯一允许失败的入口,所以 `Sparks` 会收 null)。
        static Material SparkMat()
        {
            if (_spark != null) return _spark;
            var sh = FirstShader("Particles/Standard Unlit",
                                 "Legacy Shaders/Particles/Alpha Blended Premultiply",
                                 "Sprites/Default", "Standard");
            if (sh == null)
            {
                Debug.LogWarning("[特效] 找不到任何能用的粒子 shader ⇒ 危险区这次不摆粒子(判定与扣时照旧)。");
                return null;
            }
            _spark = new Material(sh);
            _spark.name = "spark";
            _spark.mainTexture = DotTexture();
            _spark.hideFlags = HideFlags.DontSave;
            Debug.Log("[特效] 粒子材质用 shader:" + sh.name);
            return _spark;
        }

        static Shader FirstShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }

        // v0.49(用户:"6.贴图改变在左上角的生命值变化(也就是心脏贴图的变化改变,先找一下'碎心'的资源,没有的话我去找)")
        //   ⇒ 走的是我们定下的 **路 A:程序化画**。一颗心就是一条隐式方程 `(x²+y²-1)³ - x²y³ ≤ 0`,
        //     所以零素材、零许可账;而"碎"是 **每丢一点血多劈一条裂纹** —— 裂纹是状态本身,不是另找一张图。
        //     ⚠ 入口只有 `HeartSprite(lost)` 这一个函数:哪天你下到 Kenney 的 CC0 碎心图(**路 B**),
        //       把它换成"按 lost 选一张 Sprite"就完事,调用方(`IslandPhase.BuildHeart`)一行都不用动。
        //     颜色不在这里 —— 由 `Image.color` 按 hp 插值,所以同一张图能同时当"满血亮红"和"残血暗红"。
        public static Sprite HeartSprite(int lost)
        {
            lost = Mathf.Max(0, lost);
            Sprite s;
            if (_hearts.TryGetValue(lost, out s)) return s;

            // ① 先把心形的 **实际包围盒** 扫出来。不手算解区间 —— 这条方程的上下沿不对称(上面是凹口、下面是尖),
            //   猜一个数就会歪,让程序自己量。
            float minX = 1e9f, maxX = -1e9f, minY = minX, maxY = maxX;
            for (int i = 0; i < 180; i++)
                for (int j = 0; j < 180; j++)
                {
                    float x = i * 0.017f - 1.5f, y = j * 0.017f - 1.5f;
                    if (!HeartInside(x, y)) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (maxX <= minX || maxY <= minY) return null;
            var c = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            // 两个方向共用一个跨度 ⇒ 心形不会被拉扁(68×56 那种矩形里它本来就该留边)
            float span = Mathf.Max(maxX - minX, maxY - minY) * 1.08f;

            const int n = 72;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // 每像素 2×2 取样 = coverage,拿它当 alpha(硬边界在 52 像素的 UI 上会锯齿成台阶)
                    float cov = 0f;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            var p = ToHeart((x + (sx + 0.5f) * 0.5f) / n, (y + (sy + 0.5f) * 0.5f) / n, c, span);
                            if (HeartInside(p.x, p.y)) cov += 0.25f;
                        }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(cov * 255));
                }

            // ② 裂纹。方向按 `k` 定死(同一格血每次画得一模一样,不然每次 Refresh 都会"抖"一下)
            for (int k = 0; k < lost; k++)
            {
                var p = ToHeart(0.5f + (Rand(k) - 0.5f) * 0.44f, 0.84f, c, span);
                float a = -1.57f + (Rand(k + 7) - 0.5f) * 1.1f;      // 大致朝下,左右各歪一点
                for (int step = 0; step < 11; step++)
                {
                    a += (Rand(k * 31 + step) - 0.5f) * 0.62f;         // 一路锯齿着走
                    p.x += Mathf.Cos(a) * 0.12f;
                    p.y += Mathf.Sin(a) * 0.12f;
                    Crack(px, n, ToPixel(p.x, p.y, c, span, n), 1.7f);
                }
            }

            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            t.name = "heart-" + lost;
            t.hideFlags = HideFlags.DontSave;
            s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.DontSave;
            _hearts[lost] = s;
            return s;
        }

        static readonly Dictionary<int, Sprite> _hearts = new Dictionary<int, Sprite>();

        static bool HeartInside(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        // 纹理的 (0..1) → 心形自己的坐标:居中、按 span 铺开(Y 向上)
        static Vector2 ToHeart(float u, float v, Vector2 center, float span)
        {
            return new Vector2(center.x + (u - 0.5f) * span, center.y + (v - 0.5f) * span);
        }

        static Vector2 ToPixel(float x, float y, Vector2 center, float span, int n)
        {
            return new Vector2((x - center.x) / span + 0.5f, (y - center.y) / span + 0.5f) * (n - 1);
        }

        // 裂纹 = 把那一小块 alpha 打成 0(打在形状外面也无所谓:那里本来就没有像素)
        static void Crack(Color32[] px, int n, Vector2 at, float r)
        {
            int cx = Mathf.RoundToInt(at.x), cy = Mathf.RoundToInt(at.y);
            int ri = Mathf.CeilToInt(r);
            for (int y = cy - ri; y <= cy + ri; y++)
                for (int x = cx - ri; x <= cx + ri; x++)
                {
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    float dx = x - at.x, dy = y - at.y;
                    if (dx * dx + dy * dy > r * r) continue;
                    px[y * n + x] = new Color32(255, 255, 255, 0);
                }
        }

        // 只用 `seed` 的确定性伪随机:裂纹要能复现,不能用 Random(每次刷新就会换一副样子)
        static float Rand(int seed)
        {
            float v = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        // ⚠ **删组件这件事分两种模式**:运行时必须用 `Destroy`(帧末才真删,当帧物理还认它,安全),
        //   而 **编辑模式下 `Destroy` 根本不生效** —— Unity 直接报 "Destroy may not be called from edit mode!
        //   Use DestroyImmediate instead."。这条不是纸上问题:自检 `V051()` 在编辑模式里调 `World.Net`,
        //   用 Destroy 的那版当场失败("17 块 collider,16 块挂在子物体上")⇒ 2026-10-06 用户跑自检抓到。
        static void Strip(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        // v0.51(用户:"渔网的模型仍然是个球?")⇒ 库里确实没有网(726 个 FBX 的网格名全搜过;KayKit 十个包
        //   与 Quaternius 索引里也没有渔网,itch / Drive / pixabay 从这台机器连不通)。
        //   先用几何体 **拼一张网** 顶上:两向交叉的细绳 + 一圈浮子 —— 每根绳是一根压扁的方块,
        //   而不是面片(Standard 是 `Cull Back`,单面片从背面看就消失了,这条我踩过)。
        //   ⚠ 它挂在"没有模型才走灰盒"那条兜底路上 ⇒ 哪天 `Assets/Resources/Models/net.fbx` 出现就自动换掉,
        //     不用改一行代码(与红线 70 的按 key 取模型同一套约定)。
        public static GameObject Net(Transform parent, string name, Vector3 center, float size, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var rope = size * 0.42f;
            for (int dir = 0; dir < 2; dir++)
                for (int i = 0; i < 5; i++)
                {
                    var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    r.name = "Rope" + dir + i;
                    r.transform.SetParent(go.transform, false);
                    float off = (i - 2) * size * 0.19f;
                    r.transform.localPosition = new Vector3(dir == 0 ? off : 0f, (i % 2) * 0.02f, dir == 0 ? 0f : off);
                    r.transform.localScale = dir == 0 ? new Vector3(rope * 2.2f, size * 0.035f, size * 0.035f)
                                                     : new Vector3(size * 0.035f, size * 0.035f, rope * 2.2f);
                    r.transform.localRotation = Quaternion.Euler(0f, dir * 40f - 20f, 0f);
                    r.GetComponent<Renderer>().sharedMaterial = m;
                    // ⚠ 每根绳子自带的 collider **必须删**:`IslandProp`(牌子+点击)挂在 **组** 上,
                    //   而 Unity 的 OnMouse* 只发给"命中的那个 collider 所在的物体" ⇒ 留着的话点绳子什么也不发生。
                    Strip(r.GetComponent<Collider>());
                }
            for (int i = 0; i < 6; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Float" + i;
                f.transform.SetParent(go.transform, false);
                float a = i * Mathf.PI * 2f / 6f;
                f.transform.localPosition = new Vector3(Mathf.Cos(a) * size * 0.42f, size * 0.06f, Mathf.Sin(a) * size * 0.42f);
                f.transform.localScale = Vector3.one * (size * 0.11f);
                f.GetComponent<Renderer>().sharedMaterial = m;
                Strip(f.GetComponent<Collider>());
            }
            var c = go.AddComponent<BoxCollider>();
            c.size = new Vector3(size * 0.95f, size * 0.3f, size * 0.95f);
            c.center = new Vector3(0f, size * 0.1f, 0f);
            return go;
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

    // v0.49(用户:"灯光系统在篝火和手电筒再加一下"):火光那种"抖"。
    //   幅度收在 ±20% —— 再大就成了频闪灯,看两秒就烦。
    //   基准亮度取 **挂上来那一刻的 intensity**,所以调用方只给一个数,不必再维护第二份常量。
    public class GlowLight : MonoBehaviour
    {
        public Light src;
        float baseIntensity = -1f;
        void Update()
        {
            if (src == null) return;
            if (baseIntensity < 0f) baseIntensity = src.intensity;
            src.intensity = baseIntensity * (0.8f + 0.4f * Mathf.PerlinNoise(0f, Time.time * 2.2f));
        }
    }

    // v0.50(用户:"5.搜寻飞机能加入远方的动画吗"):那一晚天际线上横穿过去的机影。
    //   自己管自己(与 `GlowLight` 同一类),阶段不需要每帧过问;它 **不读任何玩法状态**,
    //   所以既不影响判定也不会与结算抢时序 —— 纯演出。
    //   ⚠ 走 **localPosition**:整个荒岛那层挂在 `Island`(锚点在原点)下面,写局部坐标 ⇒
    //     以后谁把 Island 挪出原点,这架飞机仍然在岛的同一侧天空(写世界坐标就会分家 —— 那条坑记在 §11-83 ⑤)。
    public class DistantFlyby : MonoBehaviour
    {
        public float fromX = -17f, toX = 17f;
        public float height = 6f;
        public float depth = -12f;      // 贴着海平线那段:太靠近镜头就成了"头顶飞过"
        public float seconds = 14f;
        float t;

        void Update()
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.5f, seconds);
            if (t > 1f) t -= 1f;                        // 循环掠过:那一晚玩家可能盯很久,一趟就没等于没有
            float u = Mathf.Clamp01(t);
            transform.localPosition = new Vector3(
                Mathf.Lerp(fromX, toX, u),
                height + Mathf.Sin(u * Mathf.PI) * 0.8f,   // 一点点拱形:纯直线看着像滑轨
                depth);
        }
    }

    // v0.49(用户:"3.触发器写在几个漏电/起火的地方,触碰到之后除了惩罚还有相应的贴图特效"):危险区判定体。
    //   原来那套是**每帧算一次距离**(`ScavengingPhase.HandleHazards`),现在交给物理:一个 `isTrigger` 的盒子。
    //   `hit` 那一位是必需的 —— 同一次接触可能被连发多次回调,而这里扣掉的是玩家的 60 秒,扣两次就是罚两次。
    //   只认带 `CharacterController` 的那具身体(拾荒层的玩家),别的碰撞体飘进来不算。
    public class HazardTrigger : MonoBehaviour
    {
        public System.Action onHit;
        public ParticleSystem fx;
        public float vanishAfter = 0.4f;      // 碰到之后不立刻消失:留一点时间让那一下迸出的粒子看得见
        bool hit;

        void OnTriggerEnter(Collider other)
        {
            if (hit) return;
            if (other.GetComponent<CharacterController>() == null) return;
            hit = true;
            var c = GetComponent<Collider>();
            if (c != null) c.enabled = false;
            if (fx != null) fx.Emit(30);
            if (onHit != null) onHit();
            Destroy(gameObject, vanishAfter);
        }
    }
}
