using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // §4.5 结局表:T > I > H > A > B > F > C(D/E/G 已于 v0.10 删除)
    public class EndingSO : ScriptableObject
    {
        public EndingId id;
        public string title;
        public List<string> epilogueLines = new List<string>();
        public List<string> epilogueIfTrueClaimed = new List<string>(); // v0.12 §11-23
        public bool usesClaimedEpilogue = true;                          // C 与 F 不读该字段
    }

    public enum EndingId { None, T, A, B, C, F, H, I }
}
