using System.Collections.Generic;
using UnityEngine;

namespace SixtySLike
{
    // v0.49(用户:"9.背景音乐推荐一下,拾荒阶段的音乐要越来越急促,最好只有1min左右;生存阶段的音乐要平静旷远,
    //   有海边的感觉;夜晚根据事件的不同选择合适的bgm")
    //   ⇒ 三段各有曲库,入口只有 `Scavenge / Day / Night` 三个方法(加 `Stop`)。
    //   ⚠ 曲库走 `Resources.Load<AudioClip>("Audio/<文件名>")`,与模型那一条是同一个约定
    //     (`Resources/Models/<key>.fbx`):**放一个 mp3 进去就生效,不碰任何 .asset、不碰场景。**
    //     素材 = incompetech 六首,**许可 CC BY 4.0 ⇒ 必须署名**;署名文本在 `Assets/Audio/CREDITS.txt`
    //     与 `Assets/Art/ATTRIBUTION.md`,而 §11-83 ⑨ 记着那笔 **还没建的 credits 页** 的账。
    //   "越来越急促"用的是原版那一招:整曲不动,随倒计时把 `pitch` 从 1.0 抬到 1.25。
    public static class Music
    {
        // 拾荒:两首都"赶时间",按局随机一首(67 / 66 秒,正好贴着那 60 秒)
        static readonly string[] ScavengeClips = { "Feral_Chase", "Division" };
        // 生存:平静旷远、有海边的感觉;按天轮换,免得七天都是同一首
        static readonly string[] Calm = { "Gymnopedie_No_3", "Enchanted_Valley" };
        // 夜晚:两支。超自然 / 死亡意象那一组走 Ghost_Dance(Dark, Epic),其余(天气、动物、涨潮、搜寻飞机…)
        //   走 MeasuredPaces —— 它那张卡是 Unnerving / Eerie,一步一步压过来的那种。
        // ⚠ public 只为了让自检能拿它对着 **全部 GameEventSO 资产** 核一遍 key:
        //   key 写错不会报错,只会"那一晚永远放默认那首"—— 这种降级没人会注意到,所以必须被钉住。
        public static readonly HashSet<string> Spooky = new HashSet<string>
        {
            "shadow", "smallshadow", "bones", "ghostship", "ship", "bloodmoon", "fakemate", "twolights"
        };
        const string NightTense = "MeasuredPaces";
        const string NightSpooky = "Ghost_Dance";

        // ⚠ 只在 **运行态** 做事:自检与烘焙器都在编辑模式里跑,而那里建 GameObject / 放 AudioSource 是非法的。
        static bool Usable { get { return Application.isPlaying; } }

        public static void Scavenge()
        {
            if (!Usable) return;
            var r = Ensure();
            r.tension = 0f;
            r.Switch(ScavengeClips[UnityEngine.Random.Range(0, ScavengeClips.Length)]);
        }

        // t = 已经过掉的时间占比(0 → 1)。`Music.Rig.Update` 里再做平滑,所以这里直接给就行。
        public static void Tension(float t)
        {
            if (rig != null) rig.tension = Mathf.Clamp01(t);
        }

        public static void Day(int day)
        {
            if (!Usable) return;
            var r = Ensure();
            r.tension = 0f;
            r.Switch(Calm[Mathf.Abs(day) % Calm.Length]);
        }

        public static void Night(string eventKey)
        {
            if (!Usable) return;
            var r = Ensure();
            r.tension = 0f;
            r.Switch(eventKey != null && Spooky.Contains(eventKey) ? NightSpooky : NightTense);
        }

        public static void Stop()
        {
            if (rig != null) { rig.current = null; rig.Fade(); }
        }

        // v0.61(用户:"设置里还有…音量"):BGM 总音量,0~1,存 `SaveData.musicVolume`。
        //   设置页是四档循环(关/低/中/高),不摆滑杆 —— 滑杆要 fill/handle 那套图形,半吊子滑杆比四档按钮更容易做坏。
        //   ⚠ 只作用于 BGM:这个项目 **没有独立的音效通道**(脚步/海浪那些本来就没有音频源),
        //     要"音效音量"得先给音效开一条通道,那是另一轮的活,不在这条里偷做。
        static float _volume = -1f;
        public static float Volume
        {
            get { if (_volume < 0f) _volume = SaveData.musicVolume; return _volume; }
        }
        public static void SetVolume(float v)
        {
            _volume = Mathf.Clamp01(v);
            SaveData.SetMusicVolume(_volume);
        }

        static Rig rig;

        static Rig Ensure()
        {
            if (rig != null) return rig;
            var go = new GameObject("Music");
            rig = go.AddComponent<Rig>();
            go.AddComponent<AudioSource>();
            go.AddComponent<AudioSource>();
            rig.Init();
            // ⚠ **不挂 DontDestroyOnLoad**:这个项目从头到尾不换场景(阶段是同一个场景里的 GameObject),
            //   而一旦把它钉住,编辑器里停止 Play 之后它会变成残留对象,下次进 Play 又新建一个 ⇒ 音乐双份。
            return rig;
        }

        class Rig : MonoBehaviour
        {
            public AudioSource a, b;
            public string current;
            public float tension;
            float ta, tb;
            AudioSource live;

            // 整个类只有这一个"多响"的数
            const float Level = 0.45f;
            const float FadeRate = 0.55f;       // 交叉淡入:约 0.8 秒换完

            public void Init()
            {
                a = GetComponents<AudioSource>()[0];
                b = GetComponents<AudioSource>()[1];
                a.loop = true; b.loop = true;
                a.playOnAwake = false; b.playOnAwake = false;
                a.spatialBlend = 0f; b.spatialBlend = 0f;     // BGM 不进 3D:第一人称走来走去不该忽大忽小
                a.volume = 0f; b.volume = 0f;
            }

            public void Switch(string name)
            {
                if (string.IsNullOrEmpty(name) || name == current) return;
                var clip = Clip(name);
                if (clip == null) return;
                current = name;
                // 响着的那路不动,闲着的起新曲,两路的目标音量对调 ⇒ Update 里淡入淡出
                var idle = ta >= tb ? b : a;
                live = idle;
                idle.clip = clip;
                idle.time = 0f;
                idle.volume = 0f;
                idle.Play();
                if (idle == a) { ta = Level; tb = 0f; } else { tb = Level; ta = 0f; }
            }

            public void Fade() { ta = 0f; tb = 0f; }

            void Update()
            {
                // v0.61(设置页的音量):淡入淡出的 **目标值** 乘总音量 ⇒ 改音量时两路会自己滑过去,
                //   不需要额外的"改音量"逻辑;关到 0 就是静音,而淡出中的旧曲照旧停(判据看未乘的 ta/tb)。
                a.volume = Mathf.MoveTowards(a.volume, ta * Volume, Time.unscaledDeltaTime * FadeRate);
                b.volume = Mathf.MoveTowards(b.volume, tb * Volume, Time.unscaledDeltaTime * FadeRate);
                // "越来越急促":只有当前那一排在变调,淡出中的旧曲不参与(不然会听到两首同时升调)
                var now = ta >= tb ? a : b;
                if (now == live && now.isPlaying) now.pitch = Mathf.Lerp(1f, 1.25f, tension);
                if (ta <= 0.001f && a.isPlaying && a.clip != null && a.clip.name != current) a.Stop();
                if (tb <= 0.001f && b.isPlaying && b.clip != null && b.clip.name != current) b.Stop();
            }
        }

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        static AudioClip Clip(string name)
        {
            AudioClip c;
            if (_clips.TryGetValue(name, out c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) Debug.LogWarning("[音乐] Resources/Audio/" + name + " 不在 —— 放一个 mp3 进去就生效(文件名不带扩展名)。");
            _clips[name] = c;
            return c;
        }
    }
}
