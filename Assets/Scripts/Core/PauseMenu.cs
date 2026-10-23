using UnityEngine;
using UnityEngine.UI;

namespace SixtySLike
{
    // ---- v0.92(用户:"按下esc(不管是在拾荒阶段还是在生存阶段,即开始游戏之后)出现暂停菜单,游戏自动暂停。
    //      暂停菜单自上而下是继续游戏,设置,退出游戏")----
    // 暂停 = 三件事一起做,少一件就会"假暂停":
    // ① `Time.timeScale = 0` —— 冻结一切走 Time.deltaTime 的动画/移动/补间;
    // ② 两个阶段 Update 的入口闸 —— 拾荒倒计时与 IslandWalk 走路都走 **unscaledDeltaTime**,
    //    timeScale 管不到它们,必须在各自的 Update 开头 `if (root.Paused) return;`(ScavengingPhase / IslandWalk 各有一处);
    // ③ 光标放开 —— 第一人称那两层锁着光标,不放开就没法点暂停菜单上的按钮。
    // ⚠ BGM 不停:`Music.Rig` 走 unscaledDeltaTime,暂停期间音乐照常响(大多数游戏也这么干)。
    // ⚠ 暂停菜单开在 `modal` 层:与队友面板/日记同一层,所以"暂停时点不到沙滩上的东西"是免费的
    //    (dim 面板 raycastTarget 挡掉,和那些面板一个机理)。代价:暂停前开着某块面板的话,它会先被清掉。
    public static class PauseMenu
    {
        public static bool Paused;

        public static void Toggle(GameRoot root)
        {
            if (Paused) Resume(root); else Open(root);
        }

        public static void Open(GameRoot root)
        {
            if (Paused || root == null) return;
            Paused = true;
            Time.timeScale = 0f;
            Build(root);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Resume(GameRoot root)
        {
            if (!Paused) return;
            Paused = false;
            Time.timeScale = 1f;
            SettingsPage.IsOpen = false;
            if (root != null) root.ClearModal();
            // 光标交回给阶段自己:第一人称那两层由玩家左键重新锁上(v0.53 起的老口径,不在这里偷偷锁)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 暂停中按 Esc:设置页开着 ⇒ 退一层回暂停菜单;暂停菜单上 ⇒ 直接继续
        public static void Esc(GameRoot root)
        {
            if (SettingsPage.IsOpen) { Build(root); return; }
            Resume(root);
        }

        public static void Build(GameRoot root)
        {
            if (root == null) return;
            SettingsPage.IsOpen = false;
            root.ClearModal();
            var m = root.modal;
            Ui.Panel(m, "dim", new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = Ui.Panel(m, "pauseBox", new Color(0.10f, 0.12f, 0.16f, 1f),
                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                               new Vector2(-220, -230), new Vector2(220, 230));
            Ui.Label(box, "title", "暂停", 30, TextAnchor.MiddleCenter, Color.white,
                     new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -76), new Vector2(0, -30));

            // 自上而下:继续游戏 / 设置 / 退出游戏(用户点名的顺序)
            Ui.Button(box, "resume", "继续游戏", 20, () => Resume(root),
                      new Color(0.24f, 0.42f, 0.30f, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                      new Vector2(-160, -190), new Vector2(160, -140));
            Ui.Button(box, "settings", "设置", 20, () => SettingsPage.Open(root.modal, root, true),
                      new Color(0.22f, 0.26f, 0.32f, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                      new Vector2(-160, -240), new Vector2(160, -190));
            // "退出游戏"在暂停语境 = 退出这一局回标题(自动存档没有、也没有中途存盘点,
            //   直接退桌面太狠)。标题屏那颗"退出游戏"才是 Application.Quit。
            Ui.Button(box, "quit", "退出游戏", 20, () =>
            {
                Paused = false;
                Time.timeScale = 1f;
                SettingsPage.IsOpen = false;
                root.EnterTitle();
            }, new Color(0.46f, 0.20f, 0.17f, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
               new Vector2(-160, -290), new Vector2(160, -240));

            Ui.Label(box, "hint", "Esc = 继续游戏", 13, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.72f),
                     new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 16), new Vector2(0, 44));
        }
    }
}
