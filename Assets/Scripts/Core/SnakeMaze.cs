using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // v0.75(用户:"开始按照计划做吧,做完一并提交")：
    //   v0.71 的裁定「毒虫的 swat 走 2D 迷宫」+ v0.72 的六个数落在这里。
    //
    //   ① **A 替换**：迷宫结果 **替换** 原来那次 `Roll(0.6f)`，不保留第二本账。
    //   ② **同被咬**：30 秒到点但没走到出口 = 被追上，同一条 `SnakeBite`。
    //   ③ 玩家能走、蛇不能走的格子 **不算安全区** ⇒ 本实现让两者共用 **同一张可走掩码**，
    //      "某一侧走不进去的缝"这类安全区根本不存在；兜底就是 ②。
    //   ④ 玩家 **1 格/秒**、蛇 **0.7 格/秒**，都是 **连续移动**（按住方向键平滑走，不是按格跳）。
    //   ⑤ 随机生成 + 连通校验；超限退回 **5 张手写图**（随机挑、尽量不与上一张重复）。
    //   ⑥ 蛇起点随机，但 **与玩家起点的曼哈顿距离 ≥ 5 格**；量的是起点格（静态），不追着玩家重算。
    //   ⚠ 迷宫是 `root.modal` 下的全屏弹层，**不新增 RunPhase**：夜里禁第一人称、弹层放开光标
    //     这两条既有规矩自动沿用。
    public static class SnakeMaze
    {
        public const int W = 15;
        public const int H = 11;

        public const float PlayerSpeed = 1f;      // 格/秒
        public const float SnakeSpeed = 0.7f;     // 格/秒
        public const float TimeLimit = 30f;       // 秒
        public const float PlayerRadius = 0.38f;  // 身体半径（格）:1 米走廊里左右各留 12 厘米余量
        public const float CatchDistance = 0.55f;
        public const float ExitDistance = 0.4f;

        // 生成图的起点固定在这一格（奇格，DFS 一定会挖到）；手写图各自带 P。
        public static readonly Vector2Int PlayerStart = new Vector2Int(1, H - 2);

        // 5 张手写兜底图（15×11）。`#` = 墙，`.` = 可走，`P` = 玩家起点，`E` = 出口。
        // ⚠ 这五张都过了连通校验；`V075()` 每次自检会再走一遍（起点→出口、蛇起点→玩家都要可达）。
        public static readonly string[][] FallbackMaps =
        {
            new[]
            {
                "###############",
                "#P............#",
                "#.###.###.###.#",
                "#...#...#...#.#",
                "#.#.###.#.#.#.#",
                "#.#.....#.#...#",
                "#.#.#####.###.#",
                "#...#.........#",
                "#.###.#######.#",
                "#............E#",
                "###############",
            },
            new[]
            {
                "###############",
                "#P............#",
                "#.#####.#####.#",
                "#.....#.#...#.#",
                "#####.#.#.#.#.#",
                "#.....#...#...#",
                "#.#######.###.#",
                "#.......#.....#",
                "#.#####.#.###.#",
                "#...........E.#",
                "###############",
            },
            new[]
            {
                "###############",
                "#P..#.........#",
                "##.##.#######.#",
                "#...#.....#...#",
                "#.#####.#.#.#.#",
                "#.......#...#.#",
                "#.#######.###.#",
                "#.#...........#",
                "#.#.#########.#",
                "#...........E.#",
                "###############",
            },
            new[]
            {
                "###############",
                "#P............#",
                "#.###########.#",
                "#.#.........#.#",
                "#.#.#######.#.#",
                "#.#.#.....#.#.#",
                "#.#.#.###.#.#.#",
                "#...#.#...#...#",
                "###.#.#####.###",
                "#...........E.#",
                "###############",
            },
            new[]
            {
                "###############",
                "#P............#",
                "#.###.#####.#.#",
                "#...#.....#.#.#",
                "###.#.###.#.#.#",
                "#...#...#...#.#",
                "#.#####.#.###.#",
                "#.....#.#.....#",
                "#.###.#.#####.#",
                "#...........E.#",
                "###############",
            },
        };

        static int lastFallback = -1;

        public static SnakeMazeView Open(GameRoot root, Transform parent, int seed, Action<bool> done)
        {
            Vector2Int player, exit, snake;
            var open = Generate(seed, out player, out exit, out snake);
            return SnakeMazeView.Build(root, parent, open, player, exit, snake, done);
        }

        // ---------------- 生成 ----------------
        // 随机 DFS 挖一张 15×11 的迷宫（奇格为房间），再按 12% 开一些墙形成环路 ⇒
        // 玩家有绕路空间，蛇的 A* 也不会被"只有一条走廊"卡成排队。
        // ⚠ 出口取 **BFS 离起点最远的那一格**：出口位置没有单独裁定，这条路保证它一定可达、且不在起手贴脸处。
        public static bool[,] Generate(int seed, out Vector2Int player, out Vector2Int exit, out Vector2Int snake)
        {
            var rng = new System.Random(seed);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var open = Carve(rng);
                player = PlayerStart;
                if (!open[player.x, player.y]) continue;
                exit = ChooseExit(open, player, rng);
                if (Manhattan(player, exit) < 8) continue;

                var starts = new List<Vector2Int>();
                for (int x = 0; x < W; x++)
                    for (int z = 0; z < H; z++)
                    {
                        var c = new Vector2Int(x, z);
                        if (!open[x, z] || c == exit) continue;
                        if (Manhattan(c, player) >= 5) starts.Add(c);
                    }
                if (starts.Count == 0) continue;
                snake = starts[rng.Next(starts.Count)];
                if (!Connected(open, player, exit) || !Connected(open, snake, player)) continue;
                return open;
            }
            return Fallback(rng, out player, out exit, out snake);
        }

        static bool[,] Carve(System.Random rng)
        {
            var open = new bool[W, H];
            var stack = new Stack<Vector2Int>();
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            var order = new Vector2Int[4];
            open[PlayerStart.x, PlayerStart.y] = true;
            stack.Push(PlayerStart);

            while (stack.Count > 0)
            {
                var c = stack.Peek();
                int n = 0;
                for (int i = 0; i < dirs.Length; i++)
                {
                    int nx = c.x + dirs[i].x * 2, nz = c.y + dirs[i].y * 2;
                    if (nx <= 0 || nx >= W - 1 || nz <= 0 || nz >= H - 1 || open[nx, nz]) continue;
                    order[n++] = dirs[i];
                }
                if (n == 0) { stack.Pop(); continue; }
                var d = order[rng.Next(n)];
                open[c.x + d.x, c.y + d.y] = true;
                var nc = new Vector2Int(c.x + d.x * 2, c.y + d.y * 2);
                open[nc.x, nc.y] = true;
                stack.Push(nc);
            }

            for (int x = 1; x < W - 1; x++)
                for (int z = 1; z < H - 1; z++)
                {
                    if (open[x, z]) continue;
                    bool acrossX = open[x - 1, z] && open[x + 1, z];
                    bool acrossZ = open[x, z - 1] && open[x, z + 1];
                    if ((acrossX || acrossZ) && rng.NextDouble() < 0.12) open[x, z] = true;
                }
            return open;
        }

        static bool[,] Fallback(System.Random rng, out Vector2Int player, out Vector2Int exit, out Vector2Int snake)
        {
            int pick = rng.Next(FallbackMaps.Length);
            for (int i = 0; i < FallbackMaps.Length - 1 && pick == lastFallback; i++)
                pick = (pick + 1) % FallbackMaps.Length;
            lastFallback = pick;

            var open = ParseMap(FallbackMaps[pick], out player, out exit);
            var starts = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                {
                    var c = new Vector2Int(x, z);
                    if (!open[x, z] || c == exit) continue;
                    if (Manhattan(c, player) >= 5) starts.Add(c);
                }
            snake = starts.Count > 0 ? starts[rng.Next(starts.Count)] : player;
            return open;
        }

        // ---------------- 纯逻辑（自检直接跑这些）----------------

        public static bool[,] ParseMap(string[] map, out Vector2Int player, out Vector2Int exit)
        {
            player = new Vector2Int(-1, -1);
            exit = player;
            if (map == null || map.Length != H) return null;
            var open = new bool[W, H];
            for (int z = 0; z < H; z++)
            {
                if (map[z] == null || map[z].Length != W) return null;
                for (int x = 0; x < W; x++)
                {
                    char ch = map[z][x];
                    open[x, z] = ch != '#';
                    if (ch == 'P') player = new Vector2Int(x, z);
                    else if (ch == 'E') exit = new Vector2Int(x, z);
                }
            }
            return player.x < 0 || exit.x < 0 ? null : open;
        }

        public static bool Connected(bool[,] open, Vector2Int from, Vector2Int to)
        {
            if (!Inside(open, from) || !Inside(open, to)) return false;
            var seen = new bool[W, H];
            var q = new Queue<Vector2Int>();
            seen[from.x, from.y] = true;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) return true;
                for (int d = 0; d < 4; d++)
                {
                    var n = c + Dir(d);
                    if (!Inside(open, n) || seen[n.x, n.y]) continue;
                    seen[n.x, n.y] = true;
                    q.Enqueue(n);
                }
            }
            return false;
        }

        // A*（四向）。tie-break 写死（f → h → 格子序号）⇒ 同一张图、同一个起点终点永远同一条路，
        // 蛇不会在两格之间来回抖（v0.71 ③ 那条"等待点要稳定"）。
        public static List<Vector2Int> Path(bool[,] open, Vector2Int from, Vector2Int to)
        {
            if (!Inside(open, from) || !Inside(open, to)) return null;
            int start = from.x + from.y * W, goal = to.x + to.y * W;
            var g = new Dictionary<int, float> { { start, 0f } };
            var parent = new Dictionary<int, int>();
            var openList = new List<Node>();
            var closed = new HashSet<int>();
            openList.Add(new Node { idx = start, f = Heur(from, to), h = Heur(from, to) });

            while (openList.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < openList.Count; i++)
                {
                    var a = openList[i];
                    var b = openList[bi];
                    if (a.f < b.f || (a.f == b.f && (a.h < b.h || (a.h == b.h && a.idx < b.idx)))) bi = i;
                }
                var cur = openList[bi];
                openList.RemoveAt(bi);
                if (!closed.Add(cur.idx)) continue;
                if (cur.idx == goal) break;

                var cc = new Vector2Int(cur.idx % W, cur.idx / W);
                for (int d = 0; d < 4; d++)
                {
                    var n = cc + Dir(d);
                    if (!Inside(open, n)) continue;
                    int ni = n.x + n.y * W;
                    if (closed.Contains(ni)) continue;
                    float ng = g[cur.idx] + 1f;
                    float old;
                    if (g.TryGetValue(ni, out old) && ng >= old) continue;
                    g[ni] = ng;
                    parent[ni] = cur.idx;
                    float hh = Heur(n, to);
                    openList.Add(new Node { idx = ni, f = ng + hh, h = hh });
                }
            }

            int at = goal;
            if (at != start && !parent.ContainsKey(at)) return null;
            var cells = new List<Vector2Int>();
            while (true)
            {
                cells.Add(new Vector2Int(at % W, at / W));
                if (at == start) break;
                at = parent[at];
            }
            cells.Reverse();
            return cells;
        }

        public static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        static float Heur(Vector2Int a, Vector2Int b) { return Manhattan(a, b); }

        // 出口取 **BFS 步数 12~18 的格子里随机挑一格**。为什么不取"最远格":随机迷宫的最远格
        //   中位数是 30 格、最长 66 格,而限时是 30 秒、玩家 1 格/秒 ⇒ 一半以上的图 **理想路线都走不完**。
        //   12~18 格 = 12~18 秒的理想路程,给拐弯与失误留一半时间。出口位置本身没有单独裁定(只要求可达),
        //   所以这条是实现细节;`V075()` 会把"30 秒内理论上跑得完"当不变量钉住。
        static Vector2Int ChooseExit(bool[,] open, Vector2Int from, System.Random rng)
        {
            var dist = Distances(open, from);
            var band = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                    if (dist[x, z] >= 12 && dist[x, z] <= 18) band.Add(new Vector2Int(x, z));
            if (band.Count > 0) return band[rng.Next(band.Count)];
            // 兜底(极端小图/手写图里没有那一档):退最远格 —— 至少保证一定可达。
            var best = from; int bestD = -1;
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                {
                    int d = dist[x, z];
                    if (d > bestD || (d == bestD && x + z * W < best.x + best.y * W))
                    { best = new Vector2Int(x, z); bestD = d; }
                }
            return best;
        }

        static int[,] Distances(bool[,] open, Vector2Int from)
        {
            var dist = new int[W, H];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++) dist[x, z] = -1;
            var q = new Queue<Vector2Int>();
            dist[from.x, from.y] = 0;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    var n = c + Dir(d);
                    if (!Inside(open, n) || dist[n.x, n.y] >= 0) continue;
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        static bool Inside(bool[,] open, Vector2Int c)
        {
            return c.x >= 0 && c.x < W && c.y >= 0 && c.y < H && open[c.x, c.y];
        }

        static Vector2Int Dir(int i)
        {
            switch (i)
            {
                case 0: return Vector2Int.right;
                case 1: return Vector2Int.left;
                case 2: return Vector2Int.up;
                default: return Vector2Int.down;
            }
        }

        struct Node { public int idx; public float f, h; }
    }

    // 全屏弹层：一块格子板 + 玩家/蛇/出口三个方块 + 倒计时。
    //   `root.modal` 下 ⇒ 光标自动放开（`GameRoot.ModalOpen`）、世界点击被这块全屏遮罩挡住。
    public class SnakeMazeView : MonoBehaviour
    {
        const float Cell = 40f;

        GameRoot root;
        bool[,] open;
        Vector2Int exitCell;
        Vector2 playerPos, snakePos;
        List<Vector2Int> path;
        int pathIdx;
        Vector2Int lastTarget;
        bool done;
        float timeLeft;
        Action<bool> onDone;
        RectTransform playerMark, snakeMark;
        Text timer;

        public static SnakeMazeView Build(GameRoot root, Transform parent, bool[,] open, Vector2Int player,
                                          Vector2Int exit, Vector2Int snake, Action<bool> done)
        {
            var dim = Ui.Panel(parent, "snakeMaze", new Color(0.012f, 0.015f, 0.024f, 0.985f),
                               Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.GetComponent<Image>().raycastTarget = true;
            var v = dim.gameObject.AddComponent<SnakeMazeView>();
            v.root = root;
            v.open = open;
            v.exitCell = exit;
            v.playerPos = Center(player);
            v.snakePos = Center(snake);
            v.timeLeft = SnakeMaze.TimeLimit;
            v.onDone = done;
            v.BuildUi(dim);
            return v;
        }

        void BuildUi(RectTransform dim)
        {
            Ui.Label(dim, "title", "毒虫 · 跑到出口", 24, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.5f),
                     new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-360, -58), new Vector2(360, -14));
            timer = Ui.Label(dim, "timer", "", 20, TextAnchor.UpperRight, new Color(0.92f, 0.9f, 0.84f),
                             new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-260, -52), new Vector2(-24, -16));

            var board = Ui.Panel(dim, "board", new Color(0.05f, 0.06f, 0.09f, 1f),
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(-SnakeMaze.W * Cell * 0.5f, -SnakeMaze.H * Cell * 0.5f),
                                 new Vector2(SnakeMaze.W * Cell * 0.5f, SnakeMaze.H * Cell * 0.5f));

            for (int x = 0; x < SnakeMaze.W; x++)
                for (int z = 0; z < SnakeMaze.H; z++)
                {
                    if (open[x, z]) continue;
                    var wall = Ui.Panel(board, "w" + x + "_" + z, new Color(0.15f, 0.17f, 0.22f, 1f),
                                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        new Vector2(x * Cell, -(z + 1) * Cell),
                                        new Vector2((x + 1) * Cell, -z * Cell));
                    wall.GetComponent<Image>().raycastTarget = false;
                }

            var exit = Ui.Panel(board, "exit", new Color(1f, 0.80f, 0.28f, 1f),
                                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            MarkCell(exit, exitCell, 0.72f);
            exit.GetComponent<Image>().raycastTarget = false;

            playerMark = Ui.Panel(board, "player", new Color(0.36f, 0.86f, 0.46f, 1f),
                                  new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            MarkCell(playerMark, playerPos, 0.62f);
            playerMark.GetComponent<Image>().raycastTarget = false;

            snakeMark = Ui.Panel(board, "snake", new Color(0.92f, 0.28f, 0.25f, 1f),
                                 new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            MarkCell(snakeMark, snakePos, 0.66f);
            snakeMark.GetComponent<Image>().raycastTarget = false;

            Ui.Label(dim, "hint", "WASD / 方向键移动 · 金色格 = 出口 · 被追上或超时都算被咬",
                     16, TextAnchor.LowerCenter, new Color(0.7f, 0.76f, 0.84f),
                     new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-430, 16), new Vector2(430, 52));
        }

        // 格子矩形：锚在板的左上角，按格号铺开
        static void MarkCell(RectTransform rt, Vector2 cell, float fraction)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Cell * fraction, Cell * fraction);
            rt.anchoredPosition = new Vector2((cell.x + 0.5f) * Cell, -(cell.y + 0.5f) * Cell);
        }

        static void MoveMark(RectTransform rt, Vector2 pos)
        {
            if (rt != null) rt.anchoredPosition = new Vector2((pos.x + 0.5f) * Cell, -(pos.y + 0.5f) * Cell);
        }

        static Vector2 Center(Vector2Int c) { return new Vector2(c.x + 0.5f, c.y + 0.5f); }

        void Update()
        {
            if (done) return;
            if (transform.parent == null) { done = true; return; }

            float real = Time.unscaledDeltaTime;
            timeLeft -= real;
            if (timer != null) timer.text = "倒计时 " + Mathf.CeilToInt(Mathf.Max(0f, timeLeft)) + " 秒";
            if (timeLeft <= 0f) { Finish(false); return; }

            float dt = Mathf.Min(real, 0.05f);      // 卡顿时不让一帧穿过墙
            MovePlayer(dt);
            if (done) return;
            if (Vector2.Distance(playerPos, Center(exitCell)) <= SnakeMaze.ExitDistance) { Finish(true); return; }
            MoveSnake(dt);
            if (done) return;
            if (Vector2.Distance(playerPos, snakePos) <= SnakeMaze.CatchDistance) Finish(false);
        }

        void MovePlayer(float dt)
        {
            float h = Input.GetAxisRaw("Horizontal");
            // ⚠ 板子的第 0 行画在最上面(z 向下增大)⇒ 按 W(垂直 +1)要 **减小 z**,否则上下颠倒。
            float v = -Input.GetAxisRaw("Vertical");
            var d = new Vector2(h, v);
            if (d.sqrMagnitude > 1f) d.Normalize();
            if (d.sqrMagnitude < 0.0001f) return;
            float step = SnakeMaze.PlayerSpeed * dt;
            TryMove(d.x * step, 0f);
            TryMove(0f, d.y * step);
            MoveMark(playerMark, playerPos);
        }

        void TryMove(float dx, float dz)
        {
            var np = new Vector2(playerPos.x + dx, playerPos.y + dz);
            if (!Blocked(np.x, np.y)) playerPos = np;
        }

        // 圆 vs 墙格：只查中心周围 3×3 的格子，算圆心到格子的最近点距离
        bool Blocked(float x, float z)
        {
            int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
            for (int ix = cx - 1; ix <= cx + 1; ix++)
                for (int iz = cz - 1; iz <= cz + 1; iz++)
                {
                    if (ix < 0 || iz < 0 || ix >= SnakeMaze.W || iz >= SnakeMaze.H) return true;
                    if (open[ix, iz]) continue;
                    float px = Mathf.Clamp(x, ix, ix + 1f);
                    float pz = Mathf.Clamp(z, iz, iz + 1f);
                    float ddx = x - px, ddz = z - pz;
                    if (ddx * ddx + ddz * ddz < SnakeMaze.PlayerRadius * SnakeMaze.PlayerRadius) return true;
                }
            return false;
        }

        void MoveSnake(float dt)
        {
            // ⚠ 用 Floor 而不是 Round:position 是"格单位"的连续坐标(格中心在 N+0.5),
            //   `RoundToInt(0.5)` 在 C# 里走 **银行家舍入** 会回到 0 ⇒ 目标格会偏一格。
            var target = new Vector2Int(Mathf.FloorToInt(playerPos.x), Mathf.FloorToInt(playerPos.y));
            if (path == null || path.Count == 0 || target != lastTarget)
            {
                lastTarget = target;
                var here = new Vector2Int(Mathf.FloorToInt(snakePos.x), Mathf.FloorToInt(snakePos.y));
                path = SnakeMaze.Path(open, here, target);
                pathIdx = 0;
            }
            if (path == null || path.Count == 0) return;

            if (pathIdx >= path.Count - 1)
                snakePos = Vector2.MoveTowards(snakePos, playerPos, SnakeMaze.SnakeSpeed * dt);
            else
            {
                var wp = Center(path[pathIdx]);
                snakePos = Vector2.MoveTowards(snakePos, wp, SnakeMaze.SnakeSpeed * dt);
                if (Vector2.Distance(snakePos, wp) < 0.001f) pathIdx++;
            }
            MoveMark(snakeMark, snakePos);
        }

        void Finish(bool escaped)
        {
            if (done) return;
            done = true;
            var cb = onDone;
            onDone = null;
            if (root != null) root.ClearModal();
            if (cb != null) cb(escaped);
            Destroy(gameObject);
        }
    }
}
