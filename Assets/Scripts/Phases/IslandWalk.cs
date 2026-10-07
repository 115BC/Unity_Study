using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    /// v0.53(§11-84,用户当年定案「**共存,使用v切换**」,这轮原话「**再实现v键切换视角**」)
    /// 荒岛那一层的 **自由行走视角**,与固定镜头共存:`V` 一键来回,固定镜头仍是默认。
    ///
    /// 三条口径写死在这里(全部是用户这一轮的裁定,不是我替他选的):
    /// ① **手 = 机舱那一层同一套数** —— 全部读 `ScavengingPhase.LookSensitivity / LookPitchClamp / WalkSpeed /
    ///    Reach / EyeHeight`,这个文件里 **不出现任何一个新数字**(他的原话:"直接复用机舱那一套")。
    /// ② **能走到哪 = 沙滩 + 山坡,不能下水**(他的原话)。下坡靠 `IslandTerrain.GroundY` 采样,
    ///    水线那一头是一面墙;不新增"涉水/游泳"这类他没点过的机制。
    /// ③ **屏幕上的入口**:两轮裁定叠在这里,按字面记 ——
    ///    · v0.53 他先说「**全保留,E 只是多一个入口**」⇒ 固定镜头那一层的按钮/悬浮/点击 **一个像素没动**;
    ///    · v0.54 他再说「**生存视角切换后第一人称游玩,用e与物品交互,tab键可以用鼠标选择结束一天/探索荒岛**」
    ///      ⇒ **自由视角这一路**白天的那几个入口 **不常驻屏幕**,登记进 `IslandPhase.dayEntries`,由 **Tab 面板**用鼠标选;
    ///      夜晚那一屏 v0.54 时他说「**不动**」;v0.56 进一步定案「**夜晚直接切到上帝视角,禁止第一人称**」
    ///      ⇒ **这一路只在白天存在**:入夜自动退回固定镜头,夜里按 V 不进(见 `forbidFreeLook`)。
    ///    ⚠ 同一份 `dayEntries` 喂两种摆法 ⇒ 不会出现"屏上有 5 个入口、Tab 里 4 个"那种谎(名单类问题本项目记过几次)。
    ///
    /// ⚠ v0.55 他把这两条都改了(原话:"**第一人称情况下鼠标转动移动视野,不要拖动转动视野**"、
    ///    "**第一人称情况下不要显示物品文字了**"):
    /// - **光标锁住**,鼠标一动就转头(与机舱那层同一手感)。
    ///   ⚠ v0.56 更正:放开光标的判据 **不是"哪几个面板"的名单**,而是 **有没有任何弹层**(`GameRoot.ModalOpen`)——
    ///   上一版我只认了"Tab 面板 / 夜晚那屏 / 回固定镜头"三个,结果他报「**队友页面的鼠标在第一人称模式下消失了**」:
    ///   队友面板、制造面板、日记、≥2⚡ 确认窗 **全都开在 `modal` 底下**,一条判据一起覆盖(名单式判据必漏,这条项目撞过几次)。
    ///   ⚠ 一个顺带的好消息:锁住的光标停在屏幕正中 ⇒ **"看着某件东西按左键"仍然点得着**,
    ///   所以上一轮那笔"`E` 走不到 大海 / 海鸥"的账 **不用挪锚点也不用加涉水就解决了**(看着它们点,或走近按 E)。
    /// - **自由视角期间物品牌子一律不亮**(`IslandProp.ApplyHover` 里按 `IslandWalk.Active` 挡掉)——
    ///   不挡的话转头时每看向一件东西就弹一块字,正是他要去掉的满屏文字。
    ///   ⇒ 代价:**E 会命中哪一件,屏幕上没有任何提示**。要留个非文字的提示(准星/脚下小圈)你说,我没自己加。
    public class IslandWalk : MonoBehaviour
    {
        public GameRoot root;

        // v0.54(用户:"**生存视角切换后第一人称游玩,用e与物品交互,tab键可以用鼠标选择结束一天/探索荒岛**")
        //   ⇒ 自由视角下 **白天的那几个入口不再常驻屏幕**,改成 Tab 唤出一个面板用鼠标选。
        //   面板 **由 `IslandPhase` 建**(只有它知道今天哪些行动存在),这个组件只负责"按键报出去"和"菜单开着就别动"。
        //   ⚠ 三个钩子都是可空的:固定镜头那一层(和夜晚那一屏,他明确说"不动")一个都不接。
        public System.Action onTab;             // Tab:开/关白天行动面板
        public System.Action onEsc;             // Esc:空关(不做事)
        public System.Func<bool> menuOpen;      // 面板开着 ⇒ 这一帧不当自己在走路(别移动、别按 E 误触)

        /// 这一帧是不是自由视角。`GameRoot.KeepIslandView()` 靠它让位 —— 那条每帧把相机 local 归零的约束
        /// 是 §11-84⑤ 点名的"必须让位"的东西,不然自由视角里相机被每一帧拽回 CameraPose,人根本走不动。
        public static bool Active;

        IslandStage st;
        Vector3 pos;                        // 脚的位置(y 已按高度场采好)
        float yaw, pitch;

        // v0.57(用户:"**记录摄像头状态,如果前一天白天为第一人称模式,第二天也为第一人称模式**")
        //   ⇒ **意图(`wanted`)与现状(`Active`)分开存**:`wanted` 只有他按 V 才翻转;夜里被强制退回上帝视角只改 `Active`。
        //     所以"夜里不能用第一人称"与"天亮记得回到第一人称"这两条不再互相打架。
        //     它 **不进存档、也不进 PlayerPrefs**:`IslandWalk` 挂在阶段物体上,一局里活到天亮,正是"隔天记得"要的范围;
        //     开新局回到默认的固定镜头(v0.14 那条默认不变)。
        bool wanted;

        // v0.56(用户:"**夜晚直接切到上帝视角,禁止第一人称**")⇒ 入夜自动退回固定镜头,而且夜里按 V 不进自由视角。
        //   判据由 `IslandPhase` 给(`() => night`)—— 这一层不自己猜"现在是不是夜里"。
        public System.Func<bool> forbidFreeLook;

        // v0.61(用户:"**在生存阶段的第一人称模式加入shift奔跑**"):奔跑倍率。
        //   ⚠ 这是 **这一层独有的新数**,不来自机舱那五个共用常量(v0.53 那条"文件里不出现新数字"说的是
        //     **手感那五个**要共用,不是禁止加新轴);跑步只存在于自由视角,机舱没有跑这条路。
        public const float SprintMul = 1.8f;

        // v0.56(用户:"**队友页面的鼠标在第一人称模式下消失了**")⇒ 光标放开不再看"是不是 Tab 面板",
        //   而是看 **有没有任何弹层开着**(`GameRoot.ModalOpen`):队友面板、制造面板、日记、≥2⚡ 确认窗、Tab 行动面板
        //   全都开在 `modal` 底下 ⇒ 一条判据覆盖全部。上一版我只认了 Tab 那一个,所以队友面板一开光标就"没了"
        //   —— **名单式判据必漏,这是本项目第 N 次撞到同一面墙**(彩蛋名单、字体加载路、行动条目表)。
        bool CursorFree() { return root != null && root.ModalOpen; }

        void ApplyCursor()
        {
            bool free = !Active || CursorFree();
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        readonly List<IslandProp> cache = new List<IslandProp>();
        IslandProp target;
        bool stale = true;

        // 可行走区。v0.57:**边界不再是"一块板的四条边",而是岛形本身**
        //   (用户:"荒岛建模为一个岛,周围全是海" —— 岛一成形,原来那套 x±19.4 / z[seaAnchor+3, 12] 的盒子
        //    夹出来的是一个方方正正的沙滩,角上会伸进海里,而正北方向又被凭空挡死)。
        //   ⇒ 判据只有一条:`IslandTerrain.WalkR` 那个圆(它就在浪线 `WaterR` 之内 0.6 米)——
        //     他能走到沙滩尽头,但 **永远踩不到水里**(v0.53 那句"沙滩+山坡可以,不能下水"照旧成立)。
        //   ⚠ 半径不在这里写第二份:烘焙、走路、自检三处问的都是 `IslandTerrain`(见那个文件的顶部)。

        public static Vector3 ClampToBeach(Vector3 p)
        {
            float dx = p.x - IslandTerrain.Center.x, dz = p.z - IslandTerrain.Center.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            if (r > IslandTerrain.WalkR)
            {
                float k = IslandTerrain.WalkR / (r < 0.0001f ? 0.0001f : r);
                p.x = IslandTerrain.Center.x + dx * k;
                p.z = IslandTerrain.Center.y + dz * k;
            }
            return p;
        }

        // 与 `ClampToBeach` **同一个判据**(夹完不变 = 在区内)。自检走这一条,不另写一份圆。
        public static bool Walkable(Vector3 p) { return IslandTerrain.OnLand(p.x, p.z); }

        // 出生点:道具最后一排(z 8.2)与镜头(z 13.5)之间的空沙地,而且 **离每个落点都 > Reach**
        // ⇒ 一切换视角不会出现"一按 E 就把某件东西用了"的误触(`DemoChecks.V053()` 钉这一条)。
        public static readonly Vector3 Spawn = new Vector3(0f, 0f, 10.6f);

        void Update()
        {
            var S = root != null ? root.state : null;
            if (S == null || S.phase != RunPhase.Island)
            {
                // 阶段要走了(结局屏/回主菜单):安静收掉自由视角。`GameRoot.DestroyPhase` 会先把相机从这一层搬出去,
                // 所以这里 **不去调 ApplyIslandView** —— 那是"进荒岛"才该做的事,在别的阶段调用会把镜头搬回岛上。
                if (Active) Off(true);
                return;
            }
            // v0.56(用户:"**夜晚直接切到上帝视角,禁止第一人称**")+ v0.57(用户:"**记录摄像头状态,如果前一天白天为第一人称模式,第二天也为第一人称模式**")
            //   ⇒ **"玩家要哪一路"(`wanted`)与"此刻是哪一路"(`Active`)是两件事,必须分开存**:
            //     夜里把 `Active` 关掉(强制上帝视角),但 `wanted` 保留 ⇒ 天亮自动回到他昨天那一路。
            //     只有 **他自己按 V** 才改 `wanted`。以前只有一个 `Active`,所以要么夜里能进第一人称、要么天亮回不去 —— 没有第三条路。
            bool banned = forbidFreeLook != null && forbidFreeLook();
            if (banned && Active) { Off(false); return; }            // 入夜:退回上帝视角,`wanted` 不动
            if (Input.GetKeyDown(KeyCode.V))
            {
                if (banned)
                {
                    S.Log("夜晚是上帝视角:第一人称只在白天用(V 在夜里不生效)—— 天亮会自动回到你昨天那一路。");
                    return;
                }
                wanted = !wanted;                                     // 意图只在这里变
                if (wanted) On(true); else Off(false);
                return;
            }
            if (!banned && wanted && !Active) { On(false); return; }  // 天亮/回到白天:恢复他记住的那一路
            if (!Active) return;
            ApplyCursor();        // v0.56:每帧对齐 —— 弹层可能是"按 E 打开的队友面板",不能只在按键那一刻改
            // v0.54:Tab 开关白天行动面板;开着面板时 **不走路、不按 E**(菜单那一瞬间不该顺手做掉一件东西)。
            //   Esc 只在"面板正开着"时才当作空关 —— 没开的时候不动它,免得以后别处要用 Esc 我先把这条路占了。
            if (Input.GetKeyDown(KeyCode.Tab) && onTab != null) { onTab(); ApplyCursor(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) && onEsc != null) { onEsc(); ApplyCursor(); return; }
            if (CursorFree()) return;           // 任何弹层开着 ⇒ 不走路、不按 E(与"放开光标"同一个条件,不分两处判据)
            Look();
            Move();
            PickTarget();
            if (target != null && Input.GetKeyDown(KeyCode.E) && target.onClick != null) target.onClick();
        }

        void On(bool announce)
        {
            st = root.islandStage;
            if (st == null || root.cam == null)
            {
                // ⚠ 这里必须把 `wanted` 一起清掉:否则"没有荒岛层"会让下面那句 `wanted && !Active → On()`
                //   每帧重试、每帧刷一条日志(日志被刷满 = 玩家看不到真正有用的行)。
                wanted = false;
                // v0.61 文案收口:玩家看见的那句只说"怎么了",修法(跑哪个菜单)进 Console 那条 dev 通道
                root.state.Log("自由视角开不了:场景里缺荒岛那一层。");
                Debug.LogError("[60slike] 场景里没有荒岛层 ⇒ 自由视角开不了。跑菜单 Tools/60slike/② 或 ④ 补建那一层。");
                return;
            }
            // 从固定镜头 **当前姿态** 接手:否则一按 V 镜头就从"俯角 26、yaw 180"跳到水平正对 -z,晕一次。
            var e = root.cam.transform.rotation.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;         // Unity 的 X 为正 = 往下俯视(与烘焙器那条一致)

            pos = ClampToBeach(Spawn);
            pos.y = IslandTerrain.GroundY(pos.x, pos.z);
            // ⚠ v0.55 **不再造一个"眼睛"物体把相机挂进去** —— 那是上一轮那个 bug 的根:
            //   `Off()` 里 `Destroy(eye.gameObject)` 时相机还是它的子物体,Unity 连子物体一起销毁 ⇒
            //   回固定镜头后 Game 视图整屏 "No cameras rendering"(用户截图抓到的就是这条,日志还打着"挂到 CameraPose")。
            //   现在相机 **只被搬到世界根 + 每帧直接写位姿**,退出时由 `ApplyIslandView` 挂回 CameraPose,没有任何东西要销毁。
            root.cam.transform.SetParent(null, false);
            root.cam.transform.position = pos + Vector3.up * ScavengingPhase.EyeHeight;
            root.cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            Active = true;
            stale = true;
            ApplyCursor();
            if (announce)
                root.state.Log("自由视角(V 回固定镜头):WASD 走 · Shift 跑 · 移动鼠标 = 转视野 · 走近按 E 做那一下 · " +
                               "Tab = 白天的行动(会放开光标给你点,Esc/再按 Tab 收起并重新锁上)。");
            else
                root.state.Log("天亮:自动回到你昨天那一路(第一人称)。按 V 可切回上帝视角。");
        }

        void Off(bool silent)
        {
            // v0.54:回固定镜头之前先空关 Tab 面板 —— 不然面板留在屏上,而这一层的按钮又回来了,两条入口叠一屏。
            if (Active && menuOpen != null && menuOpen() && onTab != null) onTab();
            Active = false;
            ClearTarget();
            st = null;
            ApplyCursor();                      // 回固定镜头:光标交还给 UI
            if (silent) return;
            // 回固定镜头:唯一入口还是 GameRoot 那一条(挂回 CameraPose + local 归零),不在这里重复实现
            root.ApplyIslandView("退出自由视角(V)");
        }

        void Look()
        {
            // v0.55(用户:"**鼠标转动移动视野,不要拖动转动视野**")⇒ 光标锁住,鼠标位移直接就是转头。
            //   副作用是好事:**锁住的光标停在屏幕正中**,所以"看着某件东西按左键"仍然能点它 ——
            //   上一轮我说"大海/海鸥 只能鼠标点而自由视角点不到",这条现在自动解决了(看着它们点,或走近按 E)。
            yaw += Input.GetAxis("Mouse X") * ScavengingPhase.LookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * ScavengingPhase.LookSensitivity,
                                -ScavengingPhase.LookPitchClamp, ScavengingPhase.LookPitchClamp);
            root.cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        void Move()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var t = root.cam.transform;
            var f = t.forward; f.y = 0f;
            var r = t.right; r.y = 0f;
            if (f.sqrMagnitude > 0.0001f) f.Normalize();
            if (r.sqrMagnitude > 0.0001f) r.Normalize();
            var dir = f * v + r * h;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            // v0.61(用户:"**如果可以的话,在生存阶段的第一人称模式加入shift奔跑**"):Shift = 跑。
            //   ⚠ **纯手感,不耗任何数值** —— 体力是"行动价"那本账(潜水 3 / 钓鱼 1 / 开椰子 1…),
            //     拿它收跑步的税 = 新机制,他没点,我不替他加;要收再说,那是单独一条裁定。
            //   边界照旧由 `ClampToBeach` 夹:跑得更快 ≠ 跑得更远,水线那面墙一步不让。
            float speed = ScavengingPhase.WalkSpeed * (Input.GetKey(KeyCode.LeftShift) ? SprintMul : 1f);
            pos += dir * (speed * Time.unscaledDeltaTime);

            pos = ClampToBeach(pos);
            // ⚠ 没有 CharacterController、也没有重力:地面是 **解析高度场**,采一下就得到 y(§11-83⑤ 那条
            //   "落点还是 y=0"的账就是这样结掉的 —— 采样只开给走路的人,道具一个不搬)。
            //   代价写清楚:所以 **没有"跳"、没有"坡上滑落"**,那些都是要新增机制才能有的东西,他没点。
            pos.y = IslandTerrain.GroundY(pos.x, pos.z);
            t.position = pos + Vector3.up * ScavengingPhase.EyeHeight;
        }

        // 最近的可点目标(水平距离,与机舱那层 `Nearest` 同一口径:架子上的东西和地上的该有一样的半径)
        void PickTarget()
        {
            if (stale) Rescan();
            IslandProp best = null;
            float bd = ScavengingPhase.Reach * ScavengingPhase.Reach;
            for (int i = 0; i < cache.Count; i++)
            {
                var p = cache[i];
                if (p == null || p.onClick == null) continue;
                float dx = p.transform.position.x - pos.x, dz = p.transform.position.z - pos.z;
                float d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = p; }
            }
            if (ReferenceEquals(best, target)) return;
            target = best;          // v0.55:**只当 E 的目标用**,不再点亮任何牌子(用户:"第一人称情况下不要显示物品文字了")
        }

        void ClearTarget() { target = null; }

        void Rescan()
        {
            stale = false;
            cache.Clear();
            cache.AddRange(FindObjectsOfType<IslandProp>());   // 道具/篝火/大海/队友/尸骨/那只狐狸 都是 IslandProp
            if (target != null && !cache.Contains(target)) target = null;
        }

        // 沙滩上的东西每轮重建(`BuildProps` 先 Destroy 整批再摆)⇒ 缓存必须作废,
        // 否则自由视角会攥着一堆已销毁的 IslandProp,而 Unity 的"假 null"让 `!= null` 那种判法骗得过。
        public void MarkStale() { stale = true; ClearTarget(); }

        void OnDestroy() { Active = false; }               // 阶段被回收时绝不让静态标志留在 true
    }
}
