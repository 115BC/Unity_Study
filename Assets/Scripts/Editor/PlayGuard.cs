using UnityEditor;
using UnityEngine;

namespace SixtySLike
{
    // 自动生成删掉之后,在没有 GameRoot 的场景里按 Play 会是一片死静。
    // 这个守卫只做一件事:把"为什么什么都没有"说出来,并立刻退出 Play。
    [InitializeOnLoad]
    public static class PlayGuard
    {
        static PlayGuard() { EditorApplication.playModeStateChanged += OnState; }

        static void OnState(PlayModeStateChange c)
        {
            if (c != PlayModeStateChange.EnteredPlayMode) return;
            if (GameRoot.I != null || Object.FindObjectOfType<GameRoot>() != null) return;
            Debug.LogError("[60slike] 当前场景里没有 GameRoot,所以什么都没有。" +
                           "请按顺序跑菜单 Tools/60slike/① 生成数据资产 → ② 生成 Demo 场景 + 机舱灰盒," +
                           "然后打开 " + DemoAssetBaker.ScenePath + " 再按 Play。");
            EditorApplication.isPlaying = false;
        }
    }
}
