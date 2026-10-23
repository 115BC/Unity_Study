# 美术资产出处与许可

本项目所有 3D 模型、音乐、字体都来自公开免费素材。取证方式:**逐个 zip 读包内 `License.txt` 原文**,读不到的记在「无许可文件」一节,并写明推理依据。
本文件是法律口径的唯一出处 —— 代码注释、开发文档里不要再各自写一遍。

## 结论速览

| 来源 | 许可 | 证据强度 |
|---|---|---|
| KayKit(Kay Lousberg)| **CC0 1.0** | 逐包 `LICENSE.txt` 原文,10 个仓库/包全部命中 |
| Quaternius(Justin, @Quaternius) | **CC0 1.0**(我们手上这批) | 17 个 zip 里 13 个带 CC0 原文;**Pirate Kit 2026-10-22 从包页补证 CC0**;其余 5 个见下 |
| Quaternius **Easy Enemy Pack(Jan 2019)** | **CC0 1.0** | 包页 `quaternius.com/packs/easyenemy.html` 原文 `License CC0` + 正文 `free to use in personal and commercial projects`;与本地 zip 的 2019-01 时间戳、5 种敌人完全对上 |
| acornbringer **Simplistic Low Poly Nature** | **自定义:可免费用,但不得再分发素材本身** | 商品页无 License 字段;作者在页面评论区逐字回复 `free to use them as you please. However, you are not permitted to re-distribute the assets themselves.` |
| Incompetech(Kevin MacLeod)音乐 6 首 | **CC BY 4.0 ⇒ 必须署名** | 站方许可页原文;游戏内文本在 **`Assets/Resources/Text/Credits.txt`**(唯一一份,致谢页读它) |
| Noto Sans SC 字体(思源黑体) | **SIL OFL 1.1 ⇒ 必须随字体带许可文本** | 许可证全文已存 `Assets/Art/FONT-OFL-NotoSansSC.txt`,见「字体」一节 |
| **用户提供:渔网(2026-10-06 下载,2026-10-07 给出来源页)** | **CC BY 4.0 ⇒ 必须署名** | Sketchfab 公开 API 逐字核到 `"CC Attribution"` / `creativecommons.org/licenses/by/4.0/`,作者 **cozee4sure** —— 见「渔网」一节 |
| **用户提供:直升飞机(2026-10-22 下载)** | **CC BY 4.0 ⇒ 必须署名** | zip 内无许可文件;Sketchfab 公开 API 取回 `"CC Attribution"` / `creativecommons.org/licenses/by/4.0/`,作者 **antonmoek** —— 见「直升飞机」一节 |

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

### 运行时副本(v0.79 新增,`Assets/Resources/Models/`)

A/H 结局过场要按 `World.PlaceModel` 的 key 从 `Resources` 取模型,所以从既有包里复制了三份(**同一字节,只换 GUID**):

| 运行时文件 | 来源 | 大小 | 许可 |
|---|---|---|---|
| `Resources/Models/cruiseship.fbx` | `Art/quaternius/ships/CruiseShip.fbx`(Ships 包,0930T175219) | 29,468 B | **CC0 1.0**(包内 `LICENSE.txt`) |
| `Resources/Models/npc_man.fbx` | `Art/quaternius/men/Male_Suit.fbx`(Animated Men 包,1001T012726) | 1,993,452 B | **CC0 1.0** |
| `Resources/Models/npc_woman.fbx` | `Art/quaternius/women/Female_Dress.fbx`(Animated Women 包,1001T012654) | 1,971,500 B | **CC0 1.0** |

v0.83 又加了一份(**海鸥结局里被拖走的那具人类骸骨**):

| 运行时文件 | 来源 | 大小 | 许可 |
|---|---|---|---|
| `Resources/Models/skeleton.fbx` | `Art/quaternius/pirate/Characters_Skeleton.fbx`(Pirate Kit,0930T175411) | 3,826,380 B(6,975 顶点 / 13,766 三角) | **CC0 1.0** —— 包内没有 `License.txt`,但 **2026-10-22 已从包页 `https://quaternius.com/packs/piratekit.html` 逐字核到 `License: CC0. It allows redistribution as it is public domain.`** |

v0.91 又加了两份(**影怪 / 小影怪的身体**,2026-10-23):

| 运行时文件 | 来源 | 大小 | 许可 |
|---|---|---|---|
| `Resources/Models/ghost.fbx` | `Art/quaternius/monsters/Ghost.fbx`(Ultimate Monsters,0930T175252) | 1,784,076 B | **CC0 1.0**(包内 `LICENSE.txt`) |
| `Resources/Models/ghostskull.fbx` | `Art/quaternius/monsters/Ghost_Skull.fbx`(Ultimate Monsters,0930T175252) | 1,786,252 B | **CC0 1.0**(包内 `LICENSE.txt`) |

两份都与源文件 **逐字节一致**(MD5 同值,只换 GUID);用户原话里的 `Ghost_Skull.fbx` 在 Unity 导入后落成小写 `ghostskull.fbx`,代码里的 key 也是后者。

六份都是 **CC0 ⇒ 可商用、可再分发、不要求署名**(原包许可见上;Pirate Kit 的补证见本节表格)。`Art/` 下的源文件保持不动 —— 运行时副本只是"按 key 取"的落地位置,删掉任何一边前先按 GUID 反查引用。

### 包内漏放 License.txt 的(5 个)

| zip 尾号 | 包 | FBX | 推理依据 |
|---|---|---|---|
| 0930T173706 | Simple Nature | 13 | 同作者、同 Drive 分发方式,其余 13 包一致 CC0 |
| 0930T173715 | Animated Guns | 6 | 同上 |
| 0930T173728 | Animated Zombie | 2 | 同上 |
| 1001T012631 | RPG Essentials | 13 | 同上;Drive ID 逐字等于 `quaternius.com/packs/rpg.html` 页面里的那条 |
| 1001T021032 | Junk Food | 16 | 同上;Drive ID 来自 `quaternius.com/packs/junkfood.html` |

⚠ **Pirate Kit(0930T175411,71 个 FBX)已于 2026-10-22 补证,不再靠"同作者推理"**:包页
`https://quaternius.com/packs/piratekit.html` 逐字写着 `License: CC0. It allows redistribution as it is public domain.`
⇒ **CC0 1.0,可再分发、不要求署名**。海鸥结局用的 `Characters_Skeleton.fbx` 就来自这个包(见「运行时副本」)。

## ✅ 已补证(2026-10-22:用户给出两个商品页 URL,两包都落地)

| 包 | 内容 | 结论与证据 |
|---|---|---|
| `Simplistic Low Poly Nature.zip`(25) | 鸟×2、蝴蝶、鱼、松鼠、龟、荷叶、蘑菇、树枝、羽毛、草、苔石 | 出处 = `https://acornbringer.itch.io/assets-simplistic-low-poly-nature`(用户 2026-10-22 给出)。**商品页没有 `License:` 字段**,但作者 acornbringer 在页面评论区对"是不是免费/什么许可"的提问 **逐字回复**:`Yes! This particular asset is "Name your own price" which could be free indeed! And once you download the assets, you are free to use them as you please. However, you are not permitted to re-distribute the assets themselves.` ⇒ **可免费用(本项目/课程项目没问题),但不得再分发素材本身;这不是 CC0**。 |
| `Easy Animated Enemy Pack - Jan 2019.zip`(6) | **`Snake` `Snake_angry`**、`Rat`、`Wasp`、`Frog`、`Spider` | **实际来源 = Quaternius 的 Easy Enemy Pack**,不是 acornbringer。页面 `https://quaternius.com/packs/easyenemy.html` 逐字:`Easy Enemy Pack January 2019` / `This pack includes 5 different animated enemies … FBX, OBJ and Blend formats, free to use in personal and commercial projects` / `License CC0`。⚠ **CC0 ⇒ 可商用、可再分发、不要求署名**。此前台账把它记在 acornbringer 名下是错的,现纠正。 |

⚠ **一处命名遗留(不动文件)**:`Assets/Art/itch/animated_enemies/` 这个目录名里的 `itch` 对 Quaternius 那包已不准确 —— 但它只是历史命名,改名会连带 `.meta` 与 GUID,而内容 0 引用,没必要动;按本节口径记来源即可。

⚠ **2026-10-22 决定与执行(用户答"1" = 走 ①)**:

- v0.70 的"全传"已经把 **acorn 那包的 26 个素材 + `.meta`(共 52 个文件)** 提交并推到公开仓库(commit `4a906c1`;当前 `origin/main` 与本地 HEAD 相同,仍含它)⇒ 这正是作者禁止的 **"再分发素材本身"**。
- **已执行 ①「只往前删」**:`git rm -r --cached -- Assets/Art/itch/acorn_stylized_nature Assets/Art/itch/acorn_stylized_nature.meta`(53 条跟踪记录 = 52 个包内文件 + 1 个目录 `.meta`);`.gitignore` 增加 `/Assets/Art/itch/acorn_stylized_nature/` 与 `/Assets/Art/itch/acorn_stylized_nature.meta`。**工作区文件一个没删**(盘上仍 52 个),本地工程照常可用;只是不再进版本库、今后不再随克隆/推送分发。
- ⚠ **历史没有改**:`4a906c1` 与 `origin/main` 里那份仍在,所以"公开仓库曾分发过"这件事在历史里仍可查到;这条路只保证 **今后** 不再分发。清历史(②)要重写 + **force-push**,不可逆,需单独点头;③(向作者取书面许可)未做。
- **Quaternius 那包不受此冲突影响**:CC0 允许再分发(`Assets/Art/itch/animated_enemies/` 12 条跟踪记录留在仓库里);但要记住他官网 2026-08-28 起新下的东西可能改成 **QAL**(不得转售/再分发素材本身),这个 2019 包页明写 CC0,按 CC0 走。
- **玩法零影响**:`Assets/Art/itch` 两个目录合计 **64 个文件(含 `.meta`)/ 6.7MB**,按 GUID 反查场景与材质 **0 引用**;`Assets/Resources/Models/` 里没有任何一件来自它们(海鸥 = Quaternius `Birb`、狐狸 = Quaternius `Fox`)。

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

## 直升飞机 —— **许可已确认:CC BY 4.0,须署名**(2026-10-22 用户提供)

- **来源页**:`https://sketchfab.com/3d-models/low-poly-helicopter-5bbed2de9baa4a13a7faa72653b78b64`,UID `5bbed2de9baa4a13a7faa72653b78b64`。
- **取证方式**:Sketchfab 公开 API 按 UID 取回,逐字为 `name = "Low Poly Helicopter"`、`user.displayName = "antonmoek"`、
  `license.label = "CC Attribution"`、`license.slug = "by"`、`license.url = http://creativecommons.org/licenses/by/4.0/`、`isDownloadable = true`
  ⇒ **可商用、可再分发,条件是署名**。
- **怎么进来的**:用户从 `D:\Edge_Download\low-poly-helicopter.zip` 提供(2026-10-22)。包内 **只有两个文件,没有 License.txt / README**:
  `source/Helicopter.fbx`(162,624 B,2018-08-14)+ `textures/Palette.png`(30,459 B)。
- **网格重量**(`python devtools/fbxstats.py`):**顶点 180 / 三角 324**(全项目最轻的一档,远轻于渔网 86,064)。
- **落地文件**:`Assets/Art/user_provided/helicopter.fbx` + `Assets/Art/user_provided/Palette.png`(存底、贴图按材质名配),
  `Assets/Resources/Models/helicopter.fbx`(运行时按 key 取,与存底同一字节 md5 `cb4218e1…`)。
- 署名文本只在 **`Assets/Resources/Text/Credits.txt`** 的「模型 · 直升飞机」一节(致谢页唯一出处)。

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

- **`Spider`(Quaternius Easy Enemy Pack 内)一律不用** —— 用户明确要求不要蜘蛛。
- CC-BY 4.0 素材的署名文本 **只写在一处:`Assets/Resources/Text/Credits.txt`**(游戏内致谢页的唯一出处)。新增一首/一件就在那份 txt 里加一个 `## 小节`;**本台账只记许可依据、URL 与取证方式,不再抄署名串**(v0.67 起的口径,自检 `V067()` ③ 盯着)。
