# 美术方向

这份文档定义 Agar.Unity 样例的美术标准。它的目标不是把样例改造成重资产项目，而是让它具备完整游戏项目应有的视觉一致性、可读性和交互反馈。后续生成、替换或接入美术资产时，应先对照本文档判断风格是否成立。

## 目标定位

Agar.Unity 的视觉定位是：

> 轻量、清晰、带果冻质感的多人吞噬竞技游戏。

项目仍然首先服务于 ULinkGame / ULinkRPC 框架演示，因此美术方案必须保持轻量、易维护、易替换。视觉表现要让玩家感到这是一个完整可玩的游戏，而不是只用占位图验证网络流程的技术样例。

## 风格关键词

- 深色竞技场。
- 发光细胞。
- 半透明果冻质感。
- 清爽街机 UI。
- 友好但有竞技感。
- 高可读性。
- 少量科技感，不做厚重科幻。

英文提示词中可以使用以下稳定风格描述：

```text
clean neon cellular arena style, dark sci-fi arcade palette, soft glow, jelly-like translucent material, high readability, friendly competitive mood
```

## 视觉原则

### 可读性优先

吞噬类玩法的核心判断是位置、体型、速度、可吞噬关系和危险边界。所有美术资产都必须服务这些判断：

- 玩家、敌人、食物、边界和 UI 信息必须在移动中可快速识别。
- 小尺寸物体不能依赖复杂细节表达身份。
- 玩家主体不能被背景、粒子或 UI 遮挡。
- 特效持续时间应短，不能长期覆盖核心视野。
- 敌我关系应通过颜色、描边、名称和 HUD 信息共同表达。

### 统一性优先

所有资产应属于同一个视觉系统。允许有皮肤变化，但不允许风格混杂：

- 玩家皮肤可以有不同颜色和内部纹理，但轮廓语言应统一。
- 食物、拾取物和特效应共享发光、圆润、轻量的视觉语法。
- UI 面板、按钮、图标和文本应共享同一套边框、透明度和强调色。
- 背景应衬托主体，不能成为视觉中心。

### 样例友好

美术资产不应让样例项目变得难以理解或难以维护：

- 优先使用 2D PNG、Sprite、材质参数和少量 Shader。
- 避免大型资源包、复杂骨骼动画和过度依赖第三方美术插件。
- 资产命名、目录和用途必须清楚。
- 每类资产至少有一个可替换的基准版本，便于未来迭代。

## 调色板

当前代码已经采用深色背景、明亮玩家色、金色强调色和青色拾取物。后续美术应在此基础上扩展，而不是重做成完全不同的色系。

| 用途 | 推荐方向 | 说明 |
|------|----------|------|
| 背景 | 深蓝黑、低饱和蓝灰 | 提供空间感和对比度，不抢主体 |
| 棋盘/地面 | 暗蓝灰、轻微网格 | 表达运动参照和场地边界 |
| 玩家 | 高饱和果冻色 | 青、绿、红、黄、蓝、紫等变体 |
| 本地玩家 | 更明亮的青蓝或自选皮肤色 | 需要最强识别度 |
| 敌方玩家 | 与本地玩家区分明显的高饱和色 | 颜色哈希可继续保留 |
| 食物/质量点 | 青色、薄荷色、淡蓝色 | 小而亮，但不能像玩家 |
| 危险提示 | 红色低透明发光 | 只做警示，不做大面积压迫 |
| 边界强调 | 暖金色或琥珀色 | 和深色场地形成边缘提示 |
| UI 背景 | 深蓝黑半透明 | 保持信息密度和可读性 |
| UI 强调 | 柔金、青色 | 分别用于标题/奖励与在线/行动状态 |

禁止把界面做成单一紫色、单一蓝色或大面积米色调。玩家皮肤可以多彩，但场景主视觉应保持深色竞技场基调。

## 核心资产类型

### 玩家球

玩家球是游戏中最重要的资产。

标准：

- 圆形或近圆形轮廓。
- 半透明果冻质感。
- 有柔和高光、内发光或轻微渐变。
- 边缘干净，透明背景无白边或黑边。
- 缩小到实际游戏尺寸后仍然可辨。
- 同一皮肤在不同缩放下不能出现过多噪点。

建议首批皮肤：

- `Skin_Jelly_Cyan`
- `Skin_Jelly_Mint`
- `Skin_Jelly_Crimson`
- `Skin_Jelly_Sunburst`
- `Skin_Jelly_Glacier`
- `Skin_Jelly_Violet`

### 食物与拾取物

食物需要大量出现在场上，因此必须轻量、统一、低干扰。

标准：

- 小型发光颗粒或圆润碎片。
- 不使用复杂图案。
- 不使用与玩家完全相同的尺寸和质感。
- 多个食物聚集时不能形成刺眼噪声。

当前玩法中 `PickupType` 只保留 `ScorePoint`，因此第一阶段只需要质量/得分拾取物，不需要旧版速度、击退、护盾等强化图标。

### 场地与边界

场地应强化空间感和移动参照。

标准：

- 深色地面。
- 低透明网格或细胞纹理。
- 边界应明确但不刺眼。
- 安全区、危险区和场地外区域可以通过透明覆盖层表达。
- 背景不能影响玩家球颜色判断。

### UI

UI 应像完整游戏界面，而不是调试面板。

标准：

- 深色半透明面板。
- 细边框或柔和外发光。
- 文本高对比、字号克制。
- 主按钮、次按钮、危险按钮有明确层级。
- 登录、匹配、排行榜、结算、任务、商店等入口风格统一。
- 不在界面中使用解释性装饰文案；说明文字只服务实际状态和操作。

### 图标

图标用于大厅、任务、商店、记录、排行榜和设置。

标准：

- 线性或半实心图标。
- 圆润、发光、透明背景。
- 在 32x32 显示时仍可识别。
- 不包含文字。
- 不使用写实物体贴图。

### 特效

特效用于补足游戏感，而不是炫技。

首批需要：

- 吸收食物：短促收缩、粒子汇入、亮点闪烁。
- 吞噬玩家：环形波纹、柔和爆散或溶解。
- 复活/生成：从透明到实体，带一圈扩散波纹。
- 成长：球体轻微弹性缩放和高光闪动。
- 匹配/等待：轻量循环动效。

标准：

- 单次反馈建议控制在 0.15 到 0.45 秒。
- 不遮挡玩家当前位置判断。
- 不使用血腥或攻击性过强的表现。
- 粒子数量控制在移动端和编辑器中都可接受的范围。

## 动画标准

动画应使用少量关键动效提升完整度：

- 玩家移动时可以有轻微果冻形变或呼吸缩放。
- 玩家成长时应有短促弹性反馈。
- 食物被吸收时应向玩家方向收拢或淡出。
- 玩家死亡/被吞噬时应快速消散，避免长时间残留。
- UI 按钮需要 hover、press、disabled 状态差异。
- 匹配和结算面板切换应有短过渡，但不能拖慢操作。

后续如果实现运行时动画，应优先使用已有 Sprite、材质参数、ParticleSystem 或简单脚本控制，不优先引入复杂 Animator 状态机。

## 资产目录

建议使用以下目录组织新增资产：

```txt
Client/Assets/Art
 ├─ Backgrounds
 ├─ FX
 ├─ Icons
 ├─ Materials
 ├─ Sprites
 │  ├─ Pickups
 │  └─ Skins
 └─ UI
```

当前已有 `Art/Materials` 和 `Art/Sprites`，后续可以在此基础上扩展。不要把正式资产放到 `Textures` 根目录下，除非它是代码或 Unity 导入历史已经依赖的通用贴图。

## 命名规则

资产命名应表达类型、用途和状态：

```txt
Skin_Jelly_Cyan.png
Skin_Jelly_Crimson.png
Pickup_Mass_Teal_01.png
FX_Absorb_Ring_01.png
FX_Spawn_Wave_01.png
BG_Arena_Grid_Dark_01.png
UI_Panel_Dark_01.png
UI_Button_Primary_Normal.png
UI_Button_Primary_Pressed.png
Icon_Leaderboard_01.png
Icon_Shop_01.png
```

规则：

- 使用 PascalCase 或下划线分段，不使用空格。
- 后缀数字用于同类型变体。
- 不使用 `final`、`new`、`copy`、`test` 作为正式文件名。
- 占位资产继续保留 `Placeholder`，正式资产不要使用该后缀。

## 规格建议

| 类型 | 推荐尺寸 | 背景 | 说明 |
|------|----------|------|------|
| 玩家皮肤 | 512x512 | 透明 | 可缩放到不同体型 |
| 食物/拾取物 | 128x128 或 256x256 | 透明 | 小尺寸仍需清晰 |
| 图标 | 256x256 | 透明 | UI 中常用 24-48 像素显示 |
| UI 面板 | 512x512 或 1024x1024 | 透明 | 适合九宫格或 Sprite 切片 |
| 按钮 | 512x192 或 768x256 | 透明 | 需要 normal / pressed / disabled |
| 背景/地面 | 2048x2048 | 不透明或透明 | 可平铺优先 |
| 特效帧 | 256x256 或 512x512 | 透明 | 可做序列帧或单张 Sprite |

PNG 是默认格式。除非 Unity 工作流明确需要，不优先生成 PSD、TIFF 或大体积源文件。

## 生成提示词模板

基础模板：

```text
2D game asset, clean neon cellular arena style, dark sci-fi arcade palette, soft glow, jelly-like translucent material, high readability, friendly competitive mood, centered object, transparent background, no text, no watermark, suitable for Unity sprite
```

玩家皮肤模板：

```text
2D game asset, circular jelly blob player skin, translucent [color] gel, glossy highlight, subtle inner glow, clean silhouette, transparent background, no text, no watermark, suitable for Unity sprite
```

拾取物模板：

```text
2D game asset, tiny glowing mass pickup, [color] energy pellet, simple rounded shape, soft bloom, readable at small size, transparent background, no text, no watermark, suitable for Unity sprite
```

UI 图标模板：

```text
2D UI icon, clean rounded line icon, neon arcade style, [subject], cyan and gold accent, transparent background, no text, no watermark, readable at 32 pixels
```

背景模板：

```text
2D game arena background texture, dark blue black cellular grid, subtle microscopic pattern, low contrast, seamless or tileable feel, does not distract from bright player sprites, no text, no watermark
```

## 质量验收

生成或接入资产前，需要通过以下检查：

- 在 Unity 中以实际显示尺寸查看，主体仍然可读。
- 透明边缘干净，没有明显白边、黑边或脏边。
- 颜色与现有深色竞技场 UI 协调。
- 不遮挡玩家名、分数、HUD 和排行榜信息。
- 同一批资产风格一致。
- 文件命名和目录符合本文档。
- 没有文字、水印、签名或明显生成瑕疵。
- 没有引入与当前玩法不符的旧概念，例如冲刺、护盾、击退强化。

## 首批资产计划

第一轮不要直接生成完整资产库，应先做小型基准包并在 Unity 中验证：

1. 竞技场背景或地面纹理 1 张。
2. 玩家果冻皮肤 3 张。
3. 质量拾取物 2 张。
4. UI 面板或按钮风格 1 套。
5. 吸收特效和复活波纹原型各 1 个。

基准包通过游戏视图验证后，再扩展到完整资产列表。这样可以避免资产单独看起来不错，但进入实际玩法后破坏可读性或风格统一。

## 暂不追求

- 写实细胞或医学显微镜风格。
- 厚重机甲、硬科幻或赛博城市风格。
- 大量角色插画。
- 复杂 3D 场景。
- 骨骼动画角色。
- 血腥、尖刺、病毒等当前玩法没有定义的视觉元素。
- 与框架演示无关的营销型首页美术。
