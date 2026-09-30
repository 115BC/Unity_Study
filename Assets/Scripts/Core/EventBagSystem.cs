using System.Collections.Generic;

namespace SixtySLike
{
    // §3.1 第 3 步:轮空制 Shuffle Bag(v0.8)。抽出来的纯函数,便于自检与复用。
    // 规则:每晚弹 1 个、弹过的移出本轮、全部弹完才重洗;权重只决定同轮顺位;minNight 未到者不入本轮(顺延)。
    public static class EventBagSystem
    {
        public static GameEventSO Draw(RunState s, List<GameEventSO> bagEvents)
        {
            if (s.bag.queue.Count == 0) Refill(s, bagEvents);
            var e = s.bag.queue[0];
            s.bag.queue.RemoveAt(0);
            return e;
        }

        public static void Refill(RunState s, List<GameEventSO> bagEvents)
        {
            var pool = new List<GameEventSO>();
            foreach (var e in bagEvents) if (e.inEventBag && e.minNight <= s.nightCount) pool.Add(e);
            if (pool.Count == 0) pool.AddRange(bagEvents);
            Shuffle(pool, s.rng);
            pool.Sort((a, b) => b.priority.CompareTo(a.priority));   // 顺位加权:高 priority 排前
            s.bag.queue.AddRange(pool);
            s.bag.roundIndex++;
        }

        static void Shuffle(List<GameEventSO> l, System.Random rng)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        // 雨:一个事件占一个轮次位,内部按 weightedVariants 掷子档(3:2:1)
        public static GameEventSO PickVariant(GameEventSO e, System.Random rng)
        {
            int total = 0;
            foreach (var w in e.variantWeights) total += w;
            if (total <= 0) return e.variants[0];
            int r = rng.Next(total);
            for (int i = 0; i < e.variants.Count; i++)
            {
                r -= e.variantWeights[i];
                if (r < 0) return e.variants[i];
            }
            return e.variants[0];
        }
    }
}
