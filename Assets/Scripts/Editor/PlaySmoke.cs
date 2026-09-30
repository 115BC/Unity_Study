using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SixtySLike
{
    // 冒烟入口。批处理跑法(⚠ 不要加 -quit,那会在进 Play 模式之前就把编辑器关掉):
    //   Unity.exe -batchmode -nographics -projectPath <proj> -executeMethod SixtySLike.PlaySmoke.Run
    // 也可以从菜单 Tools/60slike/Play 冒烟 手动跑,跑完会自己退出编辑器。
    [InitializeOnLoad]
    public static class PlaySmoke
    {
        static double deadline;

        static PlaySmoke()
        {
            // 进 Play 模式会重载域、把 update 回调清掉,所以每次重载后重新挂一次看门狗
            if (!EditorPrefs.GetBool(SmokeRunner.PrefKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 240.0;
            EditorApplication.update += Guard;
        }

        [MenuItem("Tools/60slike/Play 冒烟(自动跑一局)")]
        public static void Run()
        {
            // 一切都来自场景资产:不显式打开 Demo.unity,冒烟跑的就是"恰好开着的那个场景"
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoAssetBaker.ScenePath) == null)
            {
                Debug.LogError("SMOKE FAILED:" + DemoAssetBaker.ScenePath + " 不存在,先跑菜单 Tools/60slike/② 生成 Demo 场景");
                EditorApplication.Exit(3);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DemoAssetBaker.ScenePath, OpenSceneMode.Single);

            EditorPrefs.SetBool(SmokeRunner.PrefKey, true);
            deadline = EditorApplication.timeSinceStartup + 240.0;
            EditorApplication.update += Guard;
            EditorApplication.isPlaying = true;
        }

        static void Guard()
        {
            // ⚠ 不看 PrefKey:SmokeBehaviour 一启动就把它消费掉了,看门狗只认自己的截止时间
            if (EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Guard;
            Debug.LogError("SMOKE FAILED 超时:240 秒内没跑完");
            EditorPrefs.SetBool(SmokeRunner.PrefKey, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(2);
        }
    }
}
