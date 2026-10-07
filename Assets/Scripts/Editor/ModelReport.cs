using UnityEditor;
using UnityEngine;
using SixtySLike;

// 排查用的只读工具:把 Assets/Resources/Models/ 里每个模型的**根节点缩放与原始包围盒**打到控制台。
// 为什么要它:"沙滩上东西太小"有三种根因,修法完全不同 —— ①文件按厘米导出(根节点带 0.01)、
// ②网格 bounds 不对、③运行时那一次测量没读到值。先看数,别再靠猜改。
public static class ModelReport
{
    const string Dir = "Assets/Resources/Models";

    [MenuItem("Tools/60slike/⑤ 打印模型尺寸(排查缩放)", false, 5)]
    public static void Run()
    {
        // ⚠ FBX 在 AssetDatabase 里的类型是 **Model**,不是 Prefab(写 t:Prefab 一条都搜不到);
        //   也不能用 PrefabUtility.LoadPrefabContents —— 它只吃 .prefab,对 FBX 会抛
        //   "ArgumentException: ... is not a prefab file"。所以这里走"临时实例化 → 量 → 立刻销毁"。
        var guids = AssetDatabase.FindAssets("t:Model", new[] { Dir });
        if (guids.Length == 0)
        {
            Debug.LogWarning("[60slike] " + Dir + " 里没有 t:Model 资产(FBX 有没有被 Unity 导入?)");
            return;
        }
        Debug.Log("[60slike] " + Dir + " 共 " + guids.Length + " 个 —— 根 localScale / 原始包围盒(米,最长边)");
        var holder = new GameObject("ModelReportTemp");
        holder.hideFlags = HideFlags.DontSave;
        try
        {
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.Log("  " + path + "  ← 加载不到 GameObject"); continue; }
                var inst = Object.Instantiate(prefab);
                inst.hideFlags = HideFlags.DontSave;
                inst.transform.SetParent(holder.transform, false);
                Vector3 mn, mx;
                bool ok = World.WorldAABB(inst, out mn, out mx);
                var size = ok ? mx - mn : Vector3.zero;
                var max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                var note = !ok ? "  ← 量不到网格(那就不该走归一化)"
                          : max > 3f ? "  ← 原始尺寸远大于道具格 ⇒ 归一化要缩到约 " + (0.75f / max).ToString("0.####")
                          : max < 0.05f ? "  ← 原始尺寸极小(多半按厘米导出)⇒ 归一化要放大约 " + (0.75f / Mathf.Max(0.0001f, max)).ToString("0") + " 倍"
                          : "";
                var ls = inst.transform.localScale;
                Debug.Log(string.Format("  {0,-24} scale=({1:0.####}, {2:0.####}, {3:0.####})  size=({4:0.###}, {5:0.###}, {6:0.###}){7}",
                    prefab.name, ls.x, ls.y, ls.z, size.x, size.y, size.z, note));
                Object.DestroyImmediate(inst);
            }
        }
        finally { Object.DestroyImmediate(holder); }
    }
}
