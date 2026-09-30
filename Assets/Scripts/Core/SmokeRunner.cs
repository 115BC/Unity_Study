#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SixtySLike
{
    // 批处理冒烟:PlaySmoke.Run() 置开关后进 Play 模式,这个钩子读到开关就自动把
    // 标题 → 拾荒 → 荒岛 → 长跑到结局 走一遍,把断言与异常计数写进日志后退出。
    // 整个文件包在 UNITY_EDITOR 里,不会进任何构建。
    public static class SmokeRunner
    {
        public const string PrefKey = "60slike.smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            if (!EditorPrefs.GetBool(PrefKey, false)) return;
            var go = new GameObject("SmokeRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SmokeBehaviour>();
        }
    }

    public class SmokeBehaviour : MonoBehaviour
    {
        int errors;
        readonly StringBuilder notes = new StringBuilder();

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        void OnLog(string cond, string stack, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception) return;
            errors++;
            int nl = cond.IndexOf('\n');
            notes.Append("  x 异常/错误:").Append(nl < 0 ? cond : cond.Substring(0, nl)).Append("\n");
        }

        IEnumerator Start()
        {
            // 消费掉开关:哪怕这一趟崩了,也不会劫持用户下一次手动 Play
            EditorPrefs.SetBool(SmokeRunner.PrefKey, false);
            yield return null; yield return null;

            Step(GameRoot.I != null, "GameRoot 来自场景资产(Assets/Scenes/Demo.unity)");
            if (GameRoot.I == null) { Done(); yield break; }
            var R = GameRoot.I;

            R.EnterScavenging("冒烟测试");
            yield return null; yield return null;
            Step(R.state.phase == RunPhase.Scavenging, "进入拾荒阶段");
            Step(R.state.playerName == "冒烟测试", "名字过了 §12.6 的过滤器,回显 = " + R.state.playerName);
            CheckLabels();

            R.EnterIsland();
            yield return null; yield return null;
            Step(R.island != null, "进入荒岛阶段");
            Step(R.state.day == 1, "落在第 1 天,实际 = " + R.state.day);

            // 夜晚:v0.14 起不弹面板,所以这里验的是"三步次序能掷出一个结果、并且不越权触发结局"
            NightResolver.Begin(R);
            var ev = NightResolver.Current;
            Step(NightResolver.Calm || ev != null,
                 NightResolver.Calm ? "今夜无事发生(20% 前置掷命中)" : "夜晚弹出了事件:" + ev.displayName);
            string sum = NightResolver.Sleep();          // 尝试睡去 = 这一晚的"什么都不做"
            Step(sum != null && sum.Length > 0, "尝试睡去给出了结算:\"" + sum.Replace("\n", " ") + "\"");
            NightResolver.End();
            yield return null; yield return null;
            Step(R.state.phase == RunPhase.Island, "夜晚结算没有直接触发结局");

            // 长跑:什么白天行动都不做,一路推进早上结算,直到出结局(饿死 / 第 50 天 B)
            int guard = 0;
            while (R.state.phase != RunPhase.Ended && guard++ < 90)
            {
                if (R.island == null) { Step(false, "长跑第 " + guard + " 天:island 引用丢了"); break; }
                R.island.MorningTick(false);
                yield return null;
            }
            Step(R.state.phase == RunPhase.Ended,
                 "不做任何行动长跑到结局:第 " + R.state.day + " 天 → 结局 " + R.state.ending);
            Done();
        }

        void Step(bool ok, string what)
        {
            notes.Append(ok ? "  ok  " : "  FAIL ").Append(what).Append("\n");
            if (!ok) errors++;
        }

        // 用户要求"文字不要飘在空中,要贴在物品/物体表面上"。这条断言把这句话变成可判定的几何:
        // 每条世界标注到最近实体包围盒的距离必须 < 0.06(= 贴在面上),且中心不能落在任何包围盒里(= 没埋进几何体)。
        void CheckLabels()
        {
            var root = GameObject.Find("WorldLabels");
            if (root == null) { Step(false, "找不到 WorldLabels 标注根"); return; }
            var tms = root.GetComponentsInChildren<TextMesh>();
            var rs = FindObjectsOfType<Renderer>();
            int floating = 0, buried = 0;
            foreach (var tm in tms)
            {
                var p = tm.transform.position;
                float best = float.MaxValue;
                foreach (var r in rs)
                {
                    if (r.GetComponent<TextMesh>() != null) continue;   // 标注自己的文字包围盒不算实体
                    if (Inside(r, p)) { buried++; if (buried <= 4) notes.Append("      埋进 ").Append(tm.text).Append(" @ ").Append(p.ToString("F2")).Append(" in ").Append(r.name).Append("\n"); }
                    float d = Vector3.Distance(p, r.bounds.ClosestPoint(p));
                    if (d < best) best = d;
                }
                if (best > 0.06f) floating++;
            }
            Step(tms.Length > 0 && floating == 0,
                 "标注全部贴在表面上:共 " + tms.Length + " 条,飘空 " + floating + " 条");
            Step(buried == 0, "没有标注埋进几何体:命中 " + buried + " 处");
        }

        // 埋没判定走局部空间:斜置 64° 的肩板旋转后世界 AABB 会胀成一整块,
        // 用 bounds.Contains 会把"贴在门板上"的 EXIT 牌误报成埋进墙体。
        // 本项目的实体全是 Unity 原语,局部坐标 ±0.5 就是它本身。
        static bool Inside(Renderer r, Vector3 p)
        {
            var l = r.transform.InverseTransformPoint(p);
            return Mathf.Abs(l.x) <= 0.5f && Mathf.Abs(l.y) <= 0.5f && Mathf.Abs(l.z) <= 0.5f;
        }

        void Done()
        {
            Debug.Log("[SMOKE] 冒烟结果\n" + notes + (errors == 0 ? "SMOKE PASS" : "SMOKE FAILED " + errors));
            EditorPrefs.SetBool(SmokeRunner.PrefKey, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
    }
}
#endif
