# 美术资产出处与许可

本项目所有 3D 模型、音乐、字体都来自公开免费素材。取证方式:**逐个 zip 读包内 `License.txt` 原文**,读不到的记在「无许可文件」一节,并写明推理依据。
本文件是法律口径的唯一出处 —— 代码注释、开发文档里不要再各自写一遍。

## 结论速览

| 来源 | 许可 | 证据强度 |
|---|---|---|
| KayKit(Kay Lousberg)| **CC0 1.0** | 逐包 `LICENSE.txt` 原文,10 个仓库/包全部命中 |
| Quaternius(Justin, @Quaternius) | **CC0 1.0**(我们手上这批) | 17 个 zip 里 13 个带 CC0 原文;另 4 个见下 |
| acornbringer / Easy Animated Enemy | CC0(**用户口头确认**) | 本机连不上 itch.io,包内也无许可文件 —— 见「待补证」 |
| Incompetech(Kevin MacLeod)音乐 6 首 | **CC BY 4.0 ⇒ 必须署名** | 站方许可页原文;游戏内文本在 **`Assets/Resources/Text/Credits.txt`**(唯一一份,致谢页读它) |
| Noto Sans SC 字体(思源黑体) | **SIL OFL 1.1 ⇒ 必须随字体带许可文本** | 许可证全文已存 `Assets/Art/FONT-OFL-NotoSansSC.txt`,见「字体」一节 |
| **用户提供:渔网(2026-10-06 下载,2026-10-07 给出来源页)** | **CC BY 4.0 ⇒ 必须署名** | Sketchfab 公开 API 逐字核到 `"CC Attribution"` / `creativecommons.org/licenses/by/4.0/`,作者 **cozee4sure** —— 见「渔网」一节 |

⚠ **三档义务不一样,别混着记:CC0 = 什么都不用做;OFL = 带上许可证文本(不要求界面署名);CC BY = 必须署名。**

**已清点总量:22 个 zip / 783 个 FBX(未解压)+ KayKit 已解出 539 个 = 1322 个模型。**

## ⚠ Quaternius 的许可在 2026-08-28 换过了 —— 这条必须记住

- **我们下的这批包**:包内 `License.txt` 全部写着 `CC0 1.0 Universal (CC0 1.0) Public Domain Dedication`。
- **他现在的官网**(`https://quaternius.com/license.html`,页面自述 "Last updated: 8/28/2026")已经改成自定义的 **QAL v1.0(Quaternius Asset License)**:可 free of charge 用于 personal/educational/**commercial**,`with no credit required`;**但不得把素材本身转售或再分发**("you just can't resell or redistribute the assets themselves as assets")。
- CC0 是**不可撤销的公有领域 dedication**,所以下载时带 CC0 文本的这批,永久可按 CC0 使用。
- **以后再从 Quaternius 下任何东西,先读包内 `License.txt`**:如果写的是 QAL 而不是 CC0,那就多一条限制 —— 本仓库若公开分发,只能作为"游戏内部素材",不能打包成素材包外发。

## 一条对仓库的实际约束(与许可无关,但同一件事)

`Assets/Art/` 里现在躺着 567MB+ 的 zip。素材许可允许我们留着用,但**把 zip 原样提交进 git 会同时踩 QAL 的"不得再分发"和让仓库体积爆炸**。所以:只提 `.fbx`(+贴图)进 `Assets/`,zip 挪到 `_packs/` 并 `.gitignore` 掉。

## KayKit — Kay Lousberg(www.kaylousberg.com)

许可:**CC0 1.0 Universal**(`License: (Creative Commons Zero, CC0) http://creativecommons.org/publicdomain/zero/1.0/`)。取用 2026-10-01,`https://raw.githubusercontent.com/KayKit-Game-Assets/<仓库>/HEAD/...`

| 目录 / zip | 源仓库 |
|---|---|
| `Art/characters/` | KayKit-Character-Pack-Adventures-1.0 |
| `Art/dungeon/` | KayKit-Dungeon-Remastered-1.0 |
| `Art/nature/` | KayKit-Medieval-Hexagon-Pack-1.0 |
| `Art/halloween/` | KayKit-Halloween-Bits-1.0 |
| `Art/restaurant/` | KayKit-Restaurant-Bits-1.0 |
| `Art/furniture/` | KayKit-Furniture-Bits-1.0 |
| `Art/prototype/` | KayKit-Prototype-Bits-1.0 |
| `Art/city/` | KayKit-City-Builder-Bits-1.0 |
| `KayKit_Mixed_Bag_1_FREE.zip`(82 模型:**雨伞×4**、`chainlink`/`chain_anchor`、`mining_helmet`、`comicbooks`、`instantcamera`、`circus_tent`、`idol`) | itch.io,包内 `LICENSE.txt` 同为 CC0 |

## Quaternius — Justin(@Quaternius,patreon.com/quaternius)

取用 2026-10-01,链接都是各包页上的 "Just give me the Download" Google Drive 直链。

### 带 CC0 原文的(13 个包)

| zip 尾号 | 包 | FBX |
|---|---|---|
| 0930T173511 | Survival | 53 |
| 0930T173540 | Ultimate Stylized Nature(**提了 4 张贴图**,见下)| 63 |
| 0930T173657 | Ultimate Food | 103 |
| 0930T173710 | Ultimate Crops | 102 |
| 0930T175124 | Ultimate Animated Animals | 12 |
| 0930T175219 | Ships | 6 |
| 0930T175252 | Ultimate Monsters | 40 |
| 1001T012552 | Modular Weapons(**Spear**) | 24 |
| 1001T012654 | Animated Women | 8 |
| 1001T012726 | Animated Men | 8 |
| 1001T020950 | Animated Fish | 7 |
| 1001T020953 | Farm Animals | 7 |
| 1001T021023 | **Ultimate RPG**(**`Key1..Key4`+`Padlock`**、`Potion1-11 空/满`、`Scroll`、`Skull/Skull2/Bone`、`Chest_Open/Ingots`、`Ring1-7`、`Bag`) | 106 |

**`Ultimate Stylized Nature` 里额外提了 4 张贴图**(CC0 覆盖整个包,提贴图不改变许可结论):
`Assets/Art/quaternius/stylized_nature/Textures/` 下 `PalmTree_Trunk.png`、`PalmTree_Leaves.png`、`Bush_Leaves.png`、`Grass.png`,合计约 1.4MB。
原因:这批 FBX **不内嵌贴图**,而它的**材质名写的就是贴图名** ⇒ 白模的修法是把 PNG 从包里提出来按名配上(过程与判断依据见开发文档 v0.49 变更记录【⑯】)。原始 449MB 的包照旧只在 `_packs/`,不进 git。

### 包内漏放 License.txt 的(6 个)

| zip 尾号 | 包 | FBX | 推理依据 |
|---|---|---|---|
| 0930T173706 | Simple Nature | 13 | 同作者、同 Drive 分发方式,其余 13 包一致 CC0 |
| 0930T173715 | Animated Guns | 6 | 同上 |
| 0930T173728 | Animated Zombie | 2 | 同上 |
| 0930T175411 | Pirate Kit | 71 | 同上 |
| 1001T012631 | RPG Essentials | 13 | 同上;Drive ID 逐字等于 `quaternius.com/packs/rpg.html` 页面里的那条 |
| 1001T021032 | Junk Food | 16 | 同上;Drive ID 来自 `quaternius.com/packs/junkfood.html` |

## 待补证(用户已确认 CC0,我这边复核不了)

| 包 | 内容 | 缺什么 |
|---|---|---|
| `Simplistic Low Poly Nature.zip`(25) | 鸟×2、蝴蝶、鱼、松鼠、龟、荷叶、蘑菇、树枝、羽毛、草、苔石 | 出处 = `https://acornbringer.itch.io/assets-simplistic-low-poly-nature`(用户给出)。**itch.io 从这台机器 TCP 超时(试过三轮:curl / WebFetch / 后台),页面读不出**,包内也没有 LICENSE 文件。**用户确认按 CC0 使用。** |
| `Easy Animated Enemy Pack - Jan 2019.zip`(6) | **`Snake` `Snake_angry`**、`Rat`、`Wasp`、`Frog`、`Spider` | 无出处页、无许可文件。**用户确认按 CC0 使用。** |

要补证时:打开页面看右下角 **More info → `License:`** 那一行,抄回来即可。

⚠ **2026-10-07 决定状态(用户:"全传")**:素材源库整批进版本库 ⇒ **这两个包也会随公开仓库被再分发**,依据仍然是"用户口头确认 CC0,本台账的编者未能复核"。⚠ 自查同时给过一个零代价的替代方案(现在没有采用):**这两个包里的东西一件都没被用** —— `Assets/Art/itch` 共 34 个文件 / 7MB,按 GUID 反查场景与材质 **引用 0 个**,`Assets/Resources/Models/` 里也没有任何一件来自它们(海鸥用的是 Quaternius 的 `Birb`,狐狸用的是 Quaternius 的 `Fox`;`Snake` 当年只是备着,从没接线)。要改主意的话,把 `/_packs/` 那条 gitignore 的思路照搬一行 `Assets/Art/itch/` 就行,玩法与画面零变化。

## 渔网 —— **许可已确认:CC BY 4.0,须署名**(2026-10-07 补上来源)

**这一节原来写的是"连口头许可都没有",现在改口径:用户 2026-10-07 给出来源页
`https://sketchfab.com/3d-models/fishing-net-cbb4bfc4e5654500a70dfe72ffc0ce0a#download`。**

- **取证方式**:页面本身被 Cloudflare 挡(WebFetch 只拿到空壳),但 **Sketchfab 公开 API 可达** —— 按 UID 取回机器可读字段,逐字为
  `name = "fishing net"`、`user.displayName = "cozee4sure"`、`license.name = "CC Attribution"`、
  `license.url = http://creativecommons.org/licenses/by/4.0/`、
  `license.dependenciesRequired = "Author must be credited. Commercial use is allowed."`、`isDownloadable = true`。
- ⇒ **可以带进公开发布 / 上架 / 分发用的构建**(CC BY 允许商用),**条件是署名**。署名文本在
  **`Assets/Resources/Text/Credits.txt`** 的「模型 · 渔网」那一节(游戏主菜单「致谢」按钮读的就是那一份,别处不再抄)。

- **落地文件**:`Assets/Art/user_provided/fishing_net.fbx`(存底)+ `Assets/Resources/Models/net.fbx`(运行时按 key 取的那份,两份同一字节)。
- **怎么进来的**:用户从 `D:\Edge_Download\fishing-net.zip` 提供(2026-10-06)。**整包只有一层套一层的压缩包,里面只有模型**:
  - 外层 1 条:`source/fishing net.zip`(2,865,726 B,时间 2019-01-12 18:41)
  - 内层 1 条:`模型/fishing net.fbx`(2,865,584 B,时间 2019-01-13 02:31)
  - ⇒ **没有 `License.txt`、没有 README、没有 `readme` 之类,连一个 txt 都没有**。
- **我从文件本体读到的**(这些不是推测,是字节里在的):`Kaydara FBX Binary`;导出器写的是 **`MAXON CINEMA 4D Studio (RC - R19) 19.024`** + **`FBX SDK/FBX Plugins version 2017.1`**;**一个 `FbxMesh`** 的单网格;**`Material` / `Texture` / `Video` / 嵌入贴图记录全部为 0** ⇒ **它本来就没有材质与贴图**(所以进游戏是"白模",颜色由我们的 `World.ItemMat` 那一档工具灰给,这不是导入坏了)。
- **当时那段"推理依据"作废**:形态(GBK 中文目录名、双层 zip、只发 fbx)只能说明 **包内没带许可文本**,不能当许可证据 —— 现在有了页面与 API 两条硬依据,不再靠推测。
- **本项目当前的处置**:**已解除"不要带进发布构建"那条限制**(CC BY 4.0 允许商用),只要构建里带着署名 —— 主菜单「致谢」页 + `Assets/Resources/Text/Credits.txt`(两者同一份,txt 在 `Resources/` 下才进得了包)。
- **网格重量(2026-10-07 实测;`python devtools/fbxstats.py <fbx>` 读 FBX 二进制,不靠 Inspector —— Unity 2022 的 Model 页面上没有这行统计)**:
  **顶点 62,106 / 多边形 51,744 / 三角 86,064**。对比 `spear.fbx` 1,202 三角、`campfire.fbx` 704、`can.fbx` 428、`fox.fbx` 1,848 ⇒
  **这一件 ≈ 全项目其他 46 件模型加起来的十几倍**,而且它无材质无贴图(= 高模 + 白模,细节观感没换来、重量全吃了)。
  ⇒ **简化 / 换一件 / 就这么留着,是用户的一条待决策**(见开发文档 v0.67【③】与 §11-112)。⚠ 换文件即生效,代码不动;
  删掉 `net.fbx` 则 `World.Net()` 那张几何体拼的网自动回来。

## 音乐 —— **CC BY 4.0,需要署名**(2026-10-01 起,本项目第一批非 CC0 素材)

| 项 | 内容 |
|---|---|
| 来源 | Incompetech — Kevin MacLeod,`https://incompetech.com` |
| 目录数据 | `music/royalty-free/pieces.json`(机器可读曲库,1443 首,含 `feel / length / filename`) |
| 音频直链 | `music/royalty-free/mp3-royaltyfree/<文件名>`(6 首已下到 **`Assets/Resources/Audio/`**,共 22.7MB,逐个验过文件头是 `ID3`;运行时按 `Resources.Load<AudioClip>("Audio/<文件名>")` 取,所以文件名里的空格换成了下划线) |
| 许可 | **CC BY 4.0** —— 可商用,**必须署名**(站方 `llms.txt` 原文:"Nearly all music is free to use under Creative Commons Attribution 4.0") |
| 明细 | 逐首的曲名 / 用途 / feel 见 **`Assets/Audio/CREDITS.txt`**(台账,随文件走);**游戏内那份署名文本只有一个出处:`Assets/Resources/Text/Credits.txt`**(致谢页读它)。 |

⚠ **署名义务的落点(2026-10-07 起已做)**:游戏主菜单第四颗 **「致谢」** → `TitlePhase.OpenCredits()` → 按 `## 小节名` 分条渲染 `Assets/Resources/Text/Credits.txt`。
**本节不再抄那段英文署名串**(以前抄过一份,改了 txt 而台账留着旧文案就是两本账 —— 自检 `V067()` ③ 现在盯着这件事)。
新增/替换素材时:**只改 `Resources/Text/Credits.txt` 一处**,台账里补一条许可依据即可。

## 字体 —— **SIL OFL 1.1(2026-10-06 起;条件比 CC BY 简单,但有一条不能省)**

| 项 | 内容 |
|---|---|
| 字体 | **Noto Sans SC Regular**(思源黑体简体中文),8.3MB,`Assets/Resources/Fonts/NotoSansSC-Regular.otf` |
| 来源 | `github.com/notofonts/noto-cjk` → `Sans/SubsetOTF/SC/NotoSansSC-Regular.otf`;许可证文本抄自 `github.com/google/fonts` 的 `ofl/notosanssc/OFL.txt` |
| 版权行 | `Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'` |
| 许可 | **SIL Open Font License 1.1** —— 免费商用、**可嵌入产品/构建**、不要求界面署名 |
| ⚠ 唯一硬条件 | **发布时必须连同许可证文本一起带上** ⇒ 全文存在 **`Assets/Art/FONT-OFL-NotoSansSC.txt`**,**不要删、不要移出仓库**;字体自身改名受"Reserved Font Name"约束(我们没改名) |

**为什么换**:用户 2026-10-06 原话「**字体换一个辨识度高的(黑体?),下载并替换可以吗**」—— 覆盖他自己 2026-09-28 定的"全项目 华文行楷"。旧那支 **STXINGKA.TTF 仍留在 `Assets/Scenes/`**(他说文件留着不删),它不是我们的资产,只是不再被加载,所以本节不涉及它的许可。

⚠ **CC0 素材不需要任何声明,OFL 需要"带许可文本",CC BY 需要"署名"——这三类的义务不一样,别混着记。** 加载路径只有一条(`Ui.BootFont()` → `Resources.Load<Font>("Fonts/NotoSansSC-Regular")`),细节见开发文档 §11-87。

## 排除项

- **`Spider`(Easy Animated Enemy Pack 内)一律不用** —— 用户明确要求不要蜘蛛。
- CC-BY 4.0 素材的署名文本 **只写在一处:`Assets/Resources/Text/Credits.txt`**(游戏内致谢页的唯一出处)。新增一首/一件就在那份 txt 里加一个 `## 小节`;**本台账只记许可依据、URL 与取证方式,不再抄署名串**(v0.67 起的口径,自检 `V067()` ③ 盯着)。
