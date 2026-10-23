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
    //   ④ 玩家 **3 格/秒**、蛇 **2.4 格/秒**（= 玩家的 0.8 倍），都是 **连续移动**（按住方向键平滑走，
    //      不是按格跳）。v0.86 用户把两个数从 1 / 0.7 一起调快；限时仍 30 秒。
    //   ⑤ 随机生成 + 连通校验；超限退回 **5 张手写图**（随机挑、尽量不与上一张重复）。
    //   ⑥ 蛇起点随机，但 **与玩家起点的曼哈顿距离 ≥ 5 格**；量的是起点格（静态），不追着玩家重算。
    //   ⚠ 迷宫是 `root.modal` 下的全屏弹层，**不新增 RunPhase**：夜里禁第一人称、弹层放开光标
    //     这两条既有规矩自动沿用。
    public static class SnakeMaze
    {
        public const int W = 15;
        public const int H = 11;

        // v0.86(用户:"迷宫的移动速度加快,暂定玩家3格/s,蛇是玩家的0.8倍"):
        //   两个数一起改;蛇的倍率只写在这里,别再抄一个 2.4 到别处。
        public const float PlayerSpeed = 3f;               // 格/秒
        public const float SnakeSpeed = PlayerSpeed * 0.8f; // 格/秒 = 2.4
        public const float TimeLimit = 30f;                 // 秒(本轮没改)
        public const float PlayerRadius = 0.38f;  // 身体半径（格）:1 米走廊里左右各留 12 厘米余量
        public const float MonsterRadius = 0.33f; // v0.91:蛇/影怪贴着色边界走时用的半径
        public const float CatchDistance = 0.55f;
        // v0.91(用户:"仅怪物能走的用绿色,仅玩家能走的用橙色"):橙色格是 **断视线/抄近路**,不是安全屋 ——
        //   怪物进不去,但它可以贴着色边界扑;玩家站在橙色格里时判定距离放宽到 0.95 格,
        //   否则"站在橙色格里不动"就是无敌点(怪物半径 0.33 + 玩家半径 0.38 = 0.71 > 0.55,永远抓不到)。
        public const float CatchDistanceInSpecial = 0.95f;
        public const float ExitDistance = 0.4f;

        // 特殊格:0 = 普通,1 = 仅怪物能走(绿),2 = 仅玩家能走(橙)。
        public const byte SideMonster = 1;
        public const byte SidePlayer = 2;

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

        public static SnakeMazeView Open(GameRoot root, Transform parent, int seed, Action<bool> done,
                                        string title = null, string hint = null)
        {
            Vector2Int player, exit, snake;
            byte[,] special;
            var open = Generate(seed, out player, out exit, out snake, out special);
            return SnakeMazeView.Build(root, parent, open, special, player, exit, snake, done, title, hint);
        }

        // ---------------- 生成 ----------------
        // 随机 DFS 挖一张 15×11 的迷宫（奇格为房间），再开一些墙形成环路 ⇒
        // 玩家有绕路空间，蛇的 A* 也不会被"只有一条走廊"卡成排队。
        // v0.83(用户:"迷宫的逻辑还是不对,不应该只有唯一一条路能到出口"):
        //   ① 开墙比例 12% → 22%;② 出口只在 **最短路径至少有两条** 的格子里挑(v0.82 之前只保证可达)。
        //   ⚠ 出口仍取 BFS 步数 12~18 格那一档 —— 这一档是 v0.75 按 1 格/秒定的;v0.86 玩家提速到
        //     3 格/秒后它变得更宽松(12~18 步 ≈ 4~6 秒),但出口位置不是本轮要改的东西,保持不动。
        public static bool[,] Generate(int seed, out Vector2Int player, out Vector2Int exit, out Vector2Int snake)
        {
            byte[,] special;
            return Generate(seed, out player, out exit, out snake, out special);
        }

        public static bool[,] Generate(int seed, out Vector2Int player, out Vector2Int exit, out Vector2Int snake,
                                       out byte[,] special)
        {
            var rng = new System.Random(seed);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var open = Carve(rng);
                player = PlayerStart;
                if (!open[player.x, player.y]) continue;
                exit = ChooseExit(open, player, rng);
                if (exit.x < 0 || Manhattan(player, exit) < 8) continue;

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
                special = Specialize(open, player, exit, snake, rng);
                return open;
            }
            return Fallback(rng, out player, out exit, out snake, out special);
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

            Braid(open, rng, 0.22f);
            return open;
        }

        // 把"两边都是路的墙"按概率打开 ⇒ 出现环线。手写兜底图也走这一支。
        static void Braid(bool[,] open, System.Random rng, float chance)
        {
            for (int x = 1; x < W - 1; x++)
                for (int z = 1; z < H - 1; z++)
                {
                    if (open[x, z]) continue;
                    bool acrossX = open[x - 1, z] && open[x + 1, z];
                    bool acrossZ = open[x, z - 1] && open[x, z + 1];
                    if ((acrossX || acrossZ) && rng.NextDouble() < chance) open[x, z] = true;
                }
        }

        // 兜底图如果仍只有一条最短路径,继续开墙,直到至少两条(或没有可开的墙)。
        static void EnsureLoop(bool[,] open, Vector2Int from, Vector2Int to, System.Random rng)
        {
            for (int tries = 0; tries < 40 && ShortestPathCount(open, from, to) < 2; tries++)
            {
                var candidates = new List<Vector2Int>();
                for (int x = 1; x < W - 1; x++)
                    for (int z = 1; z < H - 1; z++)
                    {
                        if (open[x, z]) continue;
                        int n = 0;
                        if (open[x - 1, z]) n++;
                        if (open[x + 1, z]) n++;
                        if (open[x, z - 1]) n++;
                        if (open[x, z + 1]) n++;
                        if (n >= 2) candidates.Add(new Vector2Int(x, z));
                    }
                if (candidates.Count == 0) return;
                var pick = candidates[rng.Next(candidates.Count)];
                open[pick.x, pick.y] = true;
            }
        }

        static bool[,] Fallback(System.Random rng, out Vector2Int player, out Vector2Int exit, out Vector2Int snake,
                                out byte[,] special)
        {
            int pick = rng.Next(FallbackMaps.Length);
            for (int i = 0; i < FallbackMaps.Length - 1 && pick == lastFallback; i++)
                pick = (pick + 1) % FallbackMaps.Length;
            lastFallback = pick;

            var open = ParseMap(FallbackMaps[pick], out player, out exit);
            Braid(open, rng, 0.35f);
            EnsureLoop(open, player, exit, rng);
            var starts = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                {
                    var c = new Vector2Int(x, z);
                    if (!open[x, z] || c == exit) continue;
                    if (Manhattan(c, player) >= 5) starts.Add(c);
                }
            snake = starts.Count > 0 ? starts[rng.Next(starts.Count)] : player;
            special = Specialize(open, player, exit, snake, rng);
            return open;
        }

        // v0.91(用户:"仅怪物能走的用绿色,仅玩家能走的用橙色"):
        //   **绿色(SideMonster)= 怪物专用捷径**,玩家走不进去;**橙色(SidePlayer)= 玩家专用捷径**,
        //   怪物走不进去。两边各落 2 簇、每簇 2~4 格;每落一簇就验一次两张掩码
        //   (玩家→出口、怪物→玩家),断任何一条就把这簇撤回 —— 特殊地形只加"选择",不改"能不能赢"。
        //   起点/出口/蛇起点永不落特殊格(玩家起点变绿 = 开局站墙里;蛇起点变橙 = 它自己出不来)。
        static byte[,] Specialize(bool[,] open, Vector2Int player, Vector2Int exit, Vector2Int snake, System.Random rng)
        {
            var special = new byte[W, H];
            var reserved = new HashSet<int> { Idx(player), Idx(exit), Idx(snake) };
            for (int round = 0; round < 2; round++)
            {
                AddCluster(open, special, SideMonster, player, exit, snake, rng, reserved);
                AddCluster(open, special, SidePlayer, player, exit, snake, rng, reserved);
            }
            return special;
        }

        static void AddCluster(bool[,] open, byte[,] special, byte side, Vector2Int player, Vector2Int exit,
                               Vector2Int snake, System.Random rng, HashSet<int> reserved)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var cells = GrowCluster(open, special, rng, reserved);
                if (cells.Count < 2) continue;
                foreach (var c in cells) special[c.x, c.y] = side;
                if (MaskPathsOk(open, special, player, exit, snake)) return;
                foreach (var c in cells) special[c.x, c.y] = 0;
            }
        }

        static List<Vector2Int> GrowCluster(bool[,] open, byte[,] special, System.Random rng, HashSet<int> reserved)
        {
            var seeds = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                {
                    var c = new Vector2Int(x, z);
                    if (!open[x, z] || special[x, z] != 0 || reserved.Contains(Idx(c))) continue;
                    seeds.Add(c);
                }
            var cells = new List<Vector2Int>();
            if (seeds.Count == 0) return cells;
            cells.Add(seeds[rng.Next(seeds.Count)]);

            int want = 2 + rng.Next(3);        // 2~4 格
            for (int step = 0; step < 24 && cells.Count < want; step++)
            {
                var from = cells[rng.Next(cells.Count)];
                bool added = false;
                for (int i = 0; i < 4; i++)
                {
                    var n = from + Dir(rng.Next(4));
                    if (!Inside(open, n) || special[n.x, n.y] != 0 || reserved.Contains(Idx(n)) || cells.Contains(n))
                        continue;
                    cells.Add(n);
                    added = true;
                    break;
                }
                if (!added) break;
            }
            return cells;
        }

        static bool MaskPathsOk(bool[,] open, byte[,] special, Vector2Int player, Vector2Int exit, Vector2Int snake)
        {
            var playerWalk = WalkMask(open, special, SidePlayer);
            var monsterWalk = WalkMask(open, special, SideMonster);
            var path = Path(playerWalk, player, exit);
            // 绿色格会把玩家的近路堵掉 ⇒ 路线可能被挤过 24 格(v0.83 那条"30 秒内走得到"会失真)。
            //   这条和"走得到"一起验:不合格的这一簇直接撤回。
            if (path == null || path.Count - 1 > 24) return false;
            return Path(monsterWalk, snake, player) != null;
        }

        // 某一侧真正能走的格子:普通格两边都能走;绿色只有怪物、橙色只有玩家。
        public static bool[,] WalkMask(bool[,] open, byte[,] special, int side)
        {
            var mask = new bool[W, H];
            if (open == null) return mask;
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                    mask[x, z] = open[x, z] && (special == null || special[x, z] == 0 || special[x, z] == side);
            return mask;
        }

        static int Idx(Vector2Int c) { return c.x + c.y * W; }

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
        //   中位数是 30 格、最长 66 格,而 v0.75 定这一档时的账是"限时 30 秒、玩家 1 格/秒" ⇒
        //   一半以上的图 **理想路线都走不完**。12~18 格 = v0.86 提速后 4~6 秒的理想路程,余量很大。
        //   出口位置本身没有单独裁定(只要求可达),所以这条是实现细节;`V075()` 会把
        //   "理想路线 ≤24 步"当生成器不变量钉住(它在任何速度下都不该被拉长)。
        // v0.83(用户):这一档里再要求 **最短路径 ≥ 2 条** —— 否则玩家沿唯一那条走廊跑,没有绕路空间。
        //   全都不满足就退"有两条、且 24 步内走得到"的格子;再没有就 -1,让 `Generate` 重挖一张。
        //   ⚠ 不许退回"最远格":那张图一定只有一条路,还会把 12~18 秒的路线拉成 30 秒走不完。
        static Vector2Int ChooseExit(bool[,] open, Vector2Int from, System.Random rng)
        {
            var dist = Distances(open, from);
            var band = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                    if (dist[x, z] >= 12 && dist[x, z] <= 18 &&
                        ShortestPathCount(open, from, new Vector2Int(x, z)) >= 2)
                        band.Add(new Vector2Int(x, z));
            if (band.Count > 0) return band[rng.Next(band.Count)];

            // 兜底:放宽到 8~24 步,但仍要 ≥2 条最短路径。
            var wider = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                    if (dist[x, z] >= 8 && dist[x, z] <= 24 &&
                        ShortestPathCount(open, from, new Vector2Int(x, z)) >= 2)
                        wider.Add(new Vector2Int(x, z));
            if (wider.Count > 0) return wider[rng.Next(wider.Count)];
            return new Vector2Int(-1, -1);
        }

        // `from → to` 的最短路径有几条(数到 2 就够,直接截断)。0 = 不连通。
        //   用在出口选择与自检上;它只读可走掩码,与蛇的 A* 不共用状态。
        public static int ShortestPathCount(bool[,] open, Vector2Int from, Vector2Int to)
        {
            if (!Inside(open, from) || !Inside(open, to)) return 0;
            var dist = Distances(open, from);
            if (dist[to.x, to.y] < 0) return 0;
            var ways = new int[W, H];
            ways[from.x, from.y] = 1;
            var seen = new bool[W, H];
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (seen[c.x, c.y]) continue;      // 同一格可能被多个前驱入队:只在轮到时展开一次
                seen[c.x, c.y] = true;
                int dc = dist[c.x, c.y];
                if (c == to) continue;
                for (int d = 0; d < 4; d++)
                {
                    var n = c + Dir(d);
                    if (!Inside(open, n) || dist[n.x, n.y] != dc + 1) continue;
                    ways[n.x, n.y] = Mathf.Min(2, ways[n.x, n.y] + ways[c.x, c.y]);
                    q.Enqueue(n);
                }
            }
            return ways[to.x, to.y];
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
        public const float Cell = 40f;

        // v0.84:同一张迷宫现在服务两场事件 ⇒ 标题/提示由调用方给,默认仍是毒虫那一套。
        public const string DefaultTitle = "毒虫 · 跑到出口";
        public const string DefaultHint = "WASD / 方向键移动 · 深色格 = 出口 · 绿色 = 只有怪物能走 · 橙色 = 只有你能走";
        public const string ShadowTitle = "影怪 · 逃出黑暗";
        public const string ShadowHint = "WASD / 方向键移动 · 深色格 = 出口 · 绿色 = 只有怪物能走 · 橙色 = 只有你能走";

        // v0.76(用户:"墙和路面的颜色分隔再大一点(墙黑色,路白色,特殊路径使用黑色的邻近色)"):
        //   三种颜色只在这里各写一次 —— 板面 = 路(白)、墙(黑)、出口那一格(近黑的一档)。
        //   ⚠ 出口不能也用纯黑:它与墙同色时就只剩"白色缺口"能认路,那个缺口恰好就是出口本身。
        public static readonly Color PathColor = Color.white;
        public static readonly Color WallColor = Color.black;
        public static readonly Color ExitColor = new Color(0.13f, 0.12f, 0.10f);
        // v0.91:用户点名的两种特殊地形 —— 绿 = 仅怪物、橙 = 仅玩家。
        public static readonly Color MonsterOnlyColor = new Color(0.20f, 0.62f, 0.30f);
        public static readonly Color PlayerOnlyColor = new Color(0.95f, 0.55f, 0.12f);

        GameRoot root;
        bool[,] open;
        byte[,] special;
        bool[,] playerWalk, monsterWalk;
        Vector2Int exitCell;
        Vector2 playerPos, snakePos, playerVel;
        List<Vector2Int> path;
        int pathIdx;
        Vector2Int lastAim;
        float repathAt;
        bool done;
        float timeLeft;
        Action<bool> onDone;
        RectTransform playerMark, snakeMark;
        Text timer;

        // v0.91:怪物不再"跟着玩家的当前格一格一格挪":
        //   ① 按玩家速度往前看 0.45 秒截击(预测格对怪物可走才用,否则退回当前格);
        //   ② 每 0.2 秒重算一次,目标格一变也重算;
        //   ③ 最后一段有直线视线就直接扑,不再绕格子中心;
        //   ④ 玩家站在橙色格时,目标改成最近的可站格,靠碰撞贴在橙色边界上。
        const float AiLookAhead = 0.45f;
        const float AiRepathSeconds = 0.2f;

        public static SnakeMazeView Build(GameRoot root, Transform parent, bool[,] open, byte[,] special,
                                          Vector2Int player, Vector2Int exit, Vector2Int snake, Action<bool> done,
                                          string title = null, string hint = null)
        {
            var dim = Ui.Panel(parent, "snakeMaze", new Color(0.012f, 0.015f, 0.024f, 0.985f),
                               Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.GetComponent<Image>().raycastTarget = true;
            var v = dim.gameObject.AddComponent<SnakeMazeView>();
            v.root = root;
            v.open = open;
            v.special = special;
            v.playerWalk = SnakeMaze.WalkMask(open, special, SnakeMaze.SidePlayer);
            v.monsterWalk = SnakeMaze.WalkMask(open, special, SnakeMaze.SideMonster);
            v.exitCell = exit;
            v.playerPos = CellCenter(player);
            v.snakePos = CellCenter(snake);
            v.timeLeft = SnakeMaze.TimeLimit;
            v.onDone = done;
            v.BuildUi(dim, title ?? DefaultTitle, hint ?? DefaultHint);
            return v;
        }

        void BuildUi(RectTransform dim, string title, string hint)
        {
            Ui.Label(dim, "title", title, 24, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.5f),
                     new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-360, -58), new Vector2(360, -14));
            timer = Ui.Label(dim, "timer", "", 20, TextAnchor.UpperRight, new Color(0.92f, 0.9f, 0.84f),
                             new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-260, -52), new Vector2(-24, -16));

            var board = Ui.Panel(dim, "board", PathColor,
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(-SnakeMaze.W * Cell * 0.5f, -SnakeMaze.H * Cell * 0.5f),
                                 new Vector2(SnakeMaze.W * Cell * 0.5f, SnakeMaze.H * Cell * 0.5f));

            for (int x = 0; x < SnakeMaze.W; x++)
                for (int z = 0; z < SnakeMaze.H; z++)
                {
                    if (open[x, z]) continue;
                    var wall = Ui.Panel(board, "w" + x + "_" + z, WallColor,
                                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        new Vector2(x * Cell, -(z + 1) * Cell),
                                        new Vector2((x + 1) * Cell, -z * Cell));
                    wall.GetComponent<Image>().raycastTarget = false;
                }

            // v0.91:特殊地形先画在板面上(墙之后、出口/角色之前)—— 绿盖着白路,但不会盖住墙。
            if (special != null)
                for (int x = 0; x < SnakeMaze.W; x++)
                    for (int z = 0; z < SnakeMaze.H; z++)
                    {
                        if (!open[x, z] || special[x, z] == 0) continue;
                        var tile = Ui.Panel(board, "s" + x + "_" + z,
                                            special[x, z] == SnakeMaze.SideMonster ? MonsterOnlyColor : PlayerOnlyColor,
                                            new Vector2(0f, 1f), new Vector2(0f, 1f),
                                            new Vector2(x * Cell, -(z + 1) * Cell),
                                            new Vector2((x + 1) * Cell, -z * Cell));
                        tile.GetComponent<Image>().raycastTarget = false;
                    }

            var exit = Ui.Panel(board, "exit", ExitColor,
                                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            PlaceMark(exit, exitCell, 0.72f);
            exit.GetComponent<Image>().raycastTarget = false;

            playerMark = Ui.Panel(board, "player", new Color(0.36f, 0.86f, 0.46f, 1f),
                                  new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            SizeMark(playerMark, 0.62f);
            MoveMark(playerMark, playerPos);
            playerMark.GetComponent<Image>().raycastTarget = false;

            snakeMark = Ui.Panel(board, "snake", new Color(0.92f, 0.28f, 0.25f, 1f),
                                 new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            SizeMark(snakeMark, 0.66f);
            MoveMark(snakeMark, snakePos);
            snakeMark.GetComponent<Image>().raycastTarget = false;

            Ui.Label(dim, "hint", hint,
                     16, TextAnchor.LowerCenter, new Color(0.7f, 0.76f, 0.84f),
                     new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-430, 16), new Vector2(430, 52));
        }

        // ---------------- 格子坐标 → 板上的点 ----------------
        // 连续格坐标(格中心 = N+0.5)⇒ anchoredPosition。**这是唯一的一条映射**。
        //   ⚠ v0.76 修的 bug 就在这里:以前 `MarkCell` 给 **格号** 加了 0.5、`MoveMark` 又给
        //     **已经含 0.5 的连续坐标** 再加一次 ⇒ 玩家/蛇的方块始终画在实际碰撞点的 **右下方半格**,
        //     看起来就是"方块压着墙走、到出口时方块还没碰到格子"——用户报的「碰撞有问题」。
        public static Vector2 MarkAnchor(Vector2 continuous)
        {
            return new Vector2(continuous.x * Cell, -continuous.y * Cell);
        }

        public static Vector2 CellCenter(Vector2Int c) { return new Vector2(c.x + 0.5f, c.y + 0.5f); }

        static void SizeMark(RectTransform rt, float fraction)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Cell * fraction, Cell * fraction);
        }

        // 只给"按格号"的那一格用(出口);玩家/蛇走连续坐标,尺寸与位置分开设。
        static void PlaceMark(RectTransform rt, Vector2Int cell, float fraction)
        {
            SizeMark(rt, fraction);
            rt.anchoredPosition = MarkAnchor(CellCenter(cell));
        }

        static void MoveMark(RectTransform rt, Vector2 pos)
        {
            if (rt != null) rt.anchoredPosition = MarkAnchor(pos);
        }

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
            if (Vector2.Distance(playerPos, CellCenter(exitCell)) <= SnakeMaze.ExitDistance) { Finish(true); return; }
            MoveSnake(dt);
            if (done) return;
            // v0.91:站在橙色格(只有玩家能走)里时判定放宽 —— 怪物贴着色边界能扑到,橙色格不是无敌点。
            float catchDist = InPlayerOnlyCell(playerPos)
                ? SnakeMaze.CatchDistanceInSpecial : SnakeMaze.CatchDistance;
            if (Vector2.Distance(playerPos, snakePos) <= catchDist) Finish(false);
        }

        void MovePlayer(float dt)
        {
            float h = Input.GetAxisRaw("Horizontal");
            // ⚠ 板子的第 0 行画在最上面(z 向下增大)⇒ 按 W(垂直 +1)要 **减小 z**,否则上下颠倒。
            float v = -Input.GetAxisRaw("Vertical");
            var d = new Vector2(h, v);
            if (d.sqrMagnitude > 1f) d.Normalize();

            var before = playerPos;
            if (d.sqrMagnitude >= 0.0001f)
            {
                float step = SnakeMaze.PlayerSpeed * dt;
                TryMove(d.x * step, 0f);
                TryMove(0f, d.y * step);
                MoveMark(playerMark, playerPos);
            }
            // 实际位移/时间 = 速度;怪物靠它预测玩家下一步去哪(撞墙时速度自然掉下来)。
            var actual = (playerPos - before) / Mathf.Max(dt, 0.0001f);
            playerVel = Vector2.Lerp(playerVel, actual, 1f - Mathf.Exp(-14f * dt));
        }

        void TryMove(float dx, float dz)
        {
            var np = new Vector2(playerPos.x + dx, playerPos.y + dz);
            if (!Blocked(playerWalk, np.x, np.y, SnakeMaze.PlayerRadius)) playerPos = np;
        }

        // 圆 vs 不可走格：只查中心周围 3×3 的格子，算圆心到格子的最近点距离。
        // v0.91:掩码由调用方给 —— 玩家传 `playerWalk`(橙色能走/绿色不能),怪物传 `monsterWalk`(相反)。
        bool Blocked(bool[,] mask, float x, float z, float radius)
        {
            int cx = Mathf.FloorToInt(x), cz = Mathf.FloorToInt(z);
            for (int ix = cx - 1; ix <= cx + 1; ix++)
                for (int iz = cz - 1; iz <= cz + 1; iz++)
                {
                    if (ix < 0 || iz < 0 || ix >= SnakeMaze.W || iz >= SnakeMaze.H) return true;
                    if (mask[ix, iz]) continue;
                    float px = Mathf.Clamp(x, ix, ix + 1f);
                    float pz = Mathf.Clamp(z, iz, iz + 1f);
                    float ddx = x - px, ddz = z - pz;
                    if (ddx * ddx + ddz * ddz < radius * radius) return true;
                }
            return false;
        }

        void MoveSnake(float dt)
        {
            var playerCell = CellOf(playerPos);
            bool playerReachable = Walkable(monsterWalk, playerCell);

            // ① 截击:追玩家 0.45 秒后的位置(只在那个格子对怪物可走、且没跑出 3 格时用)。
            var predicted = CellOf(playerPos + playerVel * AiLookAhead);
            Vector2Int aim;
            if (playerReachable && predicted != playerCell && Walkable(monsterWalk, predicted) &&
                SnakeMaze.Manhattan(predicted, playerCell) <= 3)
                aim = predicted;
            else if (playerReachable)
                aim = playerCell;
            else
                aim = NearestMonsterCell(playerCell, 4, snakePos);   // 玩家在橙色格:贴到最近的可站格

            // ② 定期重算 + 目标一变就重算(不再只在"玩家换格"时算,跑动中的玩家会被提前堵住)。
            bool repath = path == null || path.Count == 0 || aim != lastAim ||
                          Time.unscaledTime >= repathAt;
            if (repath)
            {
                var here = CellOf(snakePos);
                path = SnakeMaze.Path(monsterWalk, here, aim);
                if ((path == null || path.Count == 0) && aim != playerCell && playerReachable)
                    path = SnakeMaze.Path(monsterWalk, here, playerCell);
                pathIdx = 0;
                lastAim = aim;
                repathAt = Time.unscaledTime + AiRepathSeconds;
                if (path != null && path.Count > 1 && path[0] == here) pathIdx = 1;
            }
            if (path == null || path.Count == 0) return;

            while (pathIdx < path.Count - 1 &&
                   Vector2.Distance(snakePos, CellCenter(path[pathIdx])) < 0.12f) pathIdx++;

            Vector2 goal;
            if (pathIdx < path.Count - 1)
                goal = CellCenter(path[pathIdx]);
            else
            {
                // ③ 最后一段:有直线视线就直接扑;没有就先走到最后一格中心再贴上去。
                bool nearAim = Vector2.Distance(snakePos, CellCenter(path[path.Count - 1])) < 0.6f;
                bool los = playerReachable && LineClear(snakePos, playerPos);
                goal = nearAim || los ? playerPos : CellCenter(path[path.Count - 1]);
            }

            Vector2 delta = goal - snakePos;
            float len = delta.magnitude;
            if (len > 0.0001f)
            {
                delta /= len;
                float travel = Mathf.Min(SnakeMaze.SnakeSpeed * dt, len);
                // 分轴试:贴墙时还能沿另一条轴滑过去,不会在墙角卡死。
                TryMoveMonster(delta.x * travel, 0f);
                TryMoveMonster(0f, delta.y * travel);
            }
            MoveMark(snakeMark, snakePos);
        }

        void TryMoveMonster(float dx, float dz)
        {
            var np = new Vector2(snakePos.x + dx, snakePos.y + dz);
            if (!Blocked(monsterWalk, np.x, np.y, SnakeMaze.MonsterRadius)) snakePos = np;
        }

        bool LineClear(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.CeilToInt(len / 0.08f);
            for (int i = 1; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                if (Blocked(monsterWalk, p.x, p.y, SnakeMaze.MonsterRadius)) return false;
            }
            return true;
        }

        // 从玩家格往外的第 1 圈开始,找离怪物最近的一格(玩家在橙色格时,怪物的落点)。
        Vector2Int NearestMonsterCell(Vector2Int around, int maxRing, Vector2 from)
        {
            for (int ring = 1; ring <= maxRing; ring++)
            {
                var best = new Vector2Int(-1, -1);
                float bestD = float.MaxValue;
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Mathf.Abs(dx) + Mathf.Abs(dz) != ring) continue;
                        var c = new Vector2Int(around.x + dx, around.y + dz);
                        if (!Walkable(monsterWalk, c)) continue;
                        float d = Vector2.Distance(from, CellCenter(c));
                        if (d < bestD) { bestD = d; best = c; }
                    }
                if (best.x >= 0) return best;
            }
            return new Vector2Int(-1, -1);
        }

        bool InPlayerOnlyCell(Vector2 p)
        {
            if (special == null) return false;
            var c = CellOf(p);
            if (c.x < 0 || c.x >= SnakeMaze.W || c.y < 0 || c.y >= SnakeMaze.H) return false;
            return special[c.x, c.y] == SnakeMaze.SidePlayer;
        }

        static Vector2Int CellOf(Vector2 p)
        {
            // ⚠ Floor 而不是 Round:连续坐标的格中心是 N+0.5;Round 的银行家舍入会把 0.5 判回 0。
            return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        static bool Walkable(bool[,] mask, Vector2Int c)
        {
            return c.x >= 0 && c.x < SnakeMaze.W && c.y >= 0 && c.y < SnakeMaze.H && mask[c.x, c.y];
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
