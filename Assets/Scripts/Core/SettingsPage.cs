using System;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // ---- v0.92 设置页:画面 + 音效(用户:"设置页面可以设置画面/音效;画面:全屏/窗口/窗口(无边框),帧率上限,
    //      分辨率(以16:9为主);音效:主音量(所有声音),BGM音量(拾荒和生存阶段的BGM)")----
    // 四条口径:
    // ① 全部走"循环档"按钮,不摆滑杆 —— 与 v0.61 音量四档同一条理由(滑杆要 fill/handle 那套图形,
    //    半吊子滑杆比循环按钮更容易做坏)。点一下换一档、立刻套用、按钮自己刷字。
    // ② 分辨率只列 16:9(用户原话"以16:9为主"),5 档;帧率 30/60/120/144/不限。
    // ③ 主音量 = `AudioListener.volume`(所有声音的唯一总闸,这个项目没有独立音效通道,
    //    所以它此刻实际管的就是 BGM;将来加音效会自动进这条通道);BGM 音量 = `Music.Volume`(只乘 BGM 两路)。
    // ④ 画面/音效 = 两个子页,由设置框顶部的 tab 栏(画面|声音)切换,同一时刻只显示一张
    //    (用户:"画面和声音做成子页面,由设置页面的菜单栏切换")。
    // ⚠ 本类被两处复用:标题屏的设置页(右栏)与暂停菜单里的设置页。行边界由调用方传,不在这里写死坐标。
    public static class SettingsPage
    {
        // 设置页开着 ⇒ 暂停菜单的 Esc 知道该"回暂停菜单"而不是直接继续游戏
        //   ⚠ 叫 IsOpen 而不叫 Open:`Open(...)` 是本类"打开设置页"的那个方法,同名字段 CS0102。
        public static bool IsOpen;

        // 启动时套用一次(GameRoot.Awake):画面三项 + 主音量。BGM 音量由 Music 自己懒读 SaveData。
        public static void ApplySaved()
        {
            ApplyVideo();
            AudioListener.volume = SaveData.masterVolume;
        }

        public static void ApplyVideo()
        {
            var r = ResList[SaveData.resIndex];
            Screen.SetResolution(r.x, r.y, ModeOf(SaveData.screenMode));
            Application.targetFrameRate = FpsList[SaveData.fpsIndex];
        }

        // 分辨率:全 16:9(1280×720 / 1600×900 / 1920×1080 / 2560×1440 / 3840×2160)
        public static readonly Vector2Int[] ResList =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        };
        // 帧率上限:-1 = 不限(Application.targetFrameRate 的口径)
        public static readonly int[] FpsList = { 30, 60, 120, 144, -1 };
        public static readonly string[] ScreenModeNames = { "全屏", "窗口", "窗口(无边框)" };

        // 音量四档(与 v0.61 同一张表;标题屏那页也读这两张,不再各存一份)
        public static readonly float[] VolSteps = { 0f, 0.35f, 0.7f, 1f };
        public static readonly string[] VolNames = { "关", "低", "中", "高" };

        // tab 栏(画面/声音 子页切换)的两种底色:没选中 = 与选项行同款的淡白,选中 = 提亮的蓝灰
        static readonly Color TabOff = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color TabOn = new Color(0.32f, 0.40f, 0.52f, 1f);

        public static int VolIndex(float v)
        {
            int best = 0; float bd = 9f;
            for (int i = 0; i < VolSteps.Length; i++)
            {
                float d = Mathf.Abs(VolSteps[i] - v);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static FullScreenMode ModeOf(int i)
        {
            if (i == 1) return FullScreenMode.Windowed;          // 窗口
            if (i == 2) return FullScreenMode.FullScreenWindow;  // 窗口(无边框)
            return FullScreenMode.ExclusiveFullScreen;           // 全屏
        }

        static string ScreenModeText() { return ScreenModeNames[SaveData.screenMode]; }
        static string ResText() { var r = ResList[SaveData.resIndex]; return r.x + "×" + r.y; }
        static string FpsText() { int f = FpsList[SaveData.fpsIndex]; return f < 0 ? "不限" : f.ToString(); }
        static string MasterVolText() { return VolNames[VolIndex(SaveData.masterVolume)]; }
        static string BgmVolText() { return VolNames[VolIndex(Music.Volume)]; }

        // ---- 暂停菜单里的设置页(整行宽) ----
        public static void Open(RectTransform modal, GameRoot root, bool fromPause)
        {
            root.ClearModal();
            IsOpen = true;
            var m = modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "setBox", new Color(0.12f, 0.14f, 0.18f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-360, -390), new Vector2(360, 250));
            Ui.Label(box, "title", "设置", 24, TextAnchor.UpperLeft, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -50), new Vector2(-20, -18));
            // v0.92 补:画面/声音 改子页 + tab 栏切换。tab 栏上边从 -62 起(标题底 -50,留 12 间隙);
            //   子页底 = -298,提示行 -298..-324,底部"返回"按钮在 16..52 —— 全部在框内。
            float y = AppendVideoAudio(box, root, 20f, -20f, -62f);
            Ui.Label(box, "hint", "主音量管所有声音;BGM 只管拾荒/生存阶段的音乐。",
                     12, TextAnchor.MiddleLeft, new Color(0.6f, 0.65f, 0.72f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, y - 26), new Vector2(-20, y));
            Ui.Button(box, "close", fromPause ? "返回暂停菜单" : "返回", 16, () =>
            {
                IsOpen = false;
                if (fromPause) PauseMenu.Build(root);
                else root.ClearModal();
            }, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
               new Vector2(-90, 16), new Vector2(90, 52));
        }

        // ---- 两处共用:画面 / 声音 两个子页,由顶部菜单栏(tab)切换 ----
        // 用户原话:"画面和声音做成子页面,由设置页面的菜单栏切换"。
        // 结构:一行两颗 tab(画面 | 声音,从中点平分宽度)→ 下面两张透明子页,同一时刻只显示一张
        //   (SetActive 开关;行都摆在各子页里,互不重叠 —— 老版"两组五行堆一条"的排版就此退役)。
        // x0/x1 = tab 栏/子页的左右 offset;top = tab 栏上边。返回子页底边的 y,调用方接着摆提示/按钮。
        // ⚠ 子页内部的行 x0/x1 传 0:行的锚点是 (0,1)-(1,1),0/0 = 撑满子页宽(宽度由锚点定,不是 offset)。
        // ⚠ tab 选中态 = 直接刷按钮 Image 的底色:`Ui.Button` 的 ColorTint normalColor 是白色,
        //   底色即 image.color,高亮只是乘 1.15,两套不冲突(比再包一层"选中框"省事)。
        public static float AppendVideoAudio(RectTransform box, GameRoot root, float x0, float x1, float top)
        {
            UnityEngine.UI.Button tabV = null, tabA = null;
            RectTransform pageV = null, pageA = null;
            // 先声明后赋值:闭包在 **点击那一刻** 才跑,届时四个引用都拿到了(同 Row 那条 CS0841 的教训)
            Action<int> show = null;
            show = which =>
            {
                if (pageV != null) pageV.gameObject.SetActive(which == 0);
                if (pageA != null) pageA.gameObject.SetActive(which == 1);
                if (tabV != null) tabV.GetComponent<Image>().color = which == 0 ? TabOn : TabOff;
                if (tabA != null) tabA.GetComponent<Image>().color = which == 1 ? TabOn : TabOff;
            };

            // tab 栏:先铺一条与栏同宽的透明底板,两颗 tab 摆进底板里,offset 从 0 起算。
            // ⚠ v0.92 事故档(用户截图:"菜单栏的渲染都出问题了"):tab 直接摆 box 里那次,中点写的
            //   是 (x0+x1)/2 —— x0 是"距父左边的 offset"、x1 是"距父右边的 offset",两个不同
            //   基准的数直接平均 ⇒ 暂停页 mid=0、标题页 mid=175,两颗 tab 各宽出 4 倍多,全宽
            //   重叠成一整条、文字糊成"声音面",标题页那颗还探出框外。
            //   挪进底板后:板的宽由 Unity 从锚点+offset 自己算,两颗 tab 一个锚左、一个锚右,
            //   half 对半劈 —— 不碰任何基准换算,这类错无从犯起。
            //   (顺带留档:WarnIfInverted 对"拉伸锚点 + offset 写错"是盲区 —— 它只在
            //    anchorMin==anchorMax 时才查 offset,所以这次一声警告都不响。)
            var bar = Ui.Panel(box, "tabBar", new Color(0, 0, 0, 0), new Vector2(0, 1), new Vector2(1, 1),
                               new Vector2(x0, top - 36), new Vector2(x1, top));
            bar.GetComponent<Image>().raycastTarget = false;
            float half = (bar.rect.width - 8f) * 0.5f;    // 两颗等宽,中间留 8 缝
            tabV = Ui.Button(bar, "tabVideo", "画面", 16, () => show(0), TabOff,
                             new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -36), new Vector2(half, 0));
            tabA = Ui.Button(bar, "tabAudio", "声音", 16, () => show(1), TabOff,
                             new Vector2(1, 1), new Vector2(1, 1), new Vector2(-half, -36), new Vector2(0, 0));

            // 子页高 192:装得下 小标题(22)+ 3 行(每行 44)= 154,余量给呼吸
            float pageBottom = top - 236;
            pageV = Ui.Panel(box, "pageVideo", new Color(0, 0, 0, 0), new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(x0, pageBottom), new Vector2(x1, top - 44));
            pageV.GetComponent<Image>().raycastTarget = false;   // 透明底板别抢点击
            pageA = Ui.Panel(box, "pageAudio", new Color(0, 0, 0, 0), new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(x0, pageBottom), new Vector2(x1, top - 44));
            pageA.GetComponent<Image>().raycastTarget = false;

            PageHead(pageV, "画面");
            Row(pageV, "screenMode", "显示模式", ScreenModeText,
                () => { SaveData.SetScreenMode((SaveData.screenMode + 1) % ScreenModeNames.Length); ApplyVideo(); },
                0f, 0f, -30f);
            Row(pageV, "res", "分辨率", ResText,
                () => { SaveData.SetResIndex((SaveData.resIndex + 1) % ResList.Length); ApplyVideo(); },
                0f, 0f, -74f);
            Row(pageV, "fps", "帧率上限", FpsText,
                () => { SaveData.SetFpsIndex((SaveData.fpsIndex + 1) % FpsList.Length); ApplyVideo(); },
                0f, 0f, -118f);

            PageHead(pageA, "音效");
            Row(pageA, "masterVol", "主音量(所有声音)", MasterVolText,
                () => { SaveData.SetMasterVolume(VolSteps[(VolIndex(SaveData.masterVolume) + 1) % VolSteps.Length]);
                        AudioListener.volume = SaveData.masterVolume; },
                0f, 0f, -30f);
            Row(pageA, "bgmVol", "BGM音量", BgmVolText,
                () => { Music.SetVolume(VolSteps[(VolIndex(Music.Volume) + 1) % VolSteps.Length]); },
                0f, 0f, -74f);

            show(0);
            return pageBottom;
        }

        // 子页里的小标题(与老版「— 画面 —」同一句式,只是挪进子页)
        static void PageHead(RectTransform page, string text)
        {
            Ui.Label(page, "head", "— " + text + " —", 15, TextAnchor.MiddleLeft, new Color(0.6f, 0.65f, 0.72f),
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -26), new Vector2(0, -4));
        }

        // 一行"前缀:当前值(切换)"按钮。点一下 = 换一档 + 套用 + 刷自己的字(闭包抓 btn,不需要外部刷新句柄)。
        static float Row(RectTransform box, string name, string prefix, Func<string> value, Action cycle,
                         float x0, float x1, float top)
        {
            // ⚠ 先声明、后赋值:闭包在 **点击那一刻** 才跑,那时 btn 早就拿到了;写在初始器里
            //   就是 CS0841(本地变量在声明之前无法使用)。
            UnityEngine.UI.Button btn = null;
            btn = Ui.Button(box, name, "", 16, () =>
            {
                cycle();
                Ui.SetButtonText(btn, prefix + ":" + value() + "(切换)");
            }, new Color(1, 1, 1, 0.10f), new Vector2(0, 1), new Vector2(1, 1),
               new Vector2(x0, top - 36), new Vector2(x1, top));
            Ui.SetButtonText(btn, prefix + ":" + value() + "(切换)");
            return top - 44;
        }
    }
}
