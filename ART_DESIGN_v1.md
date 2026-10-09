# 技能美术资产设计 v1（2026-08-17）

> 画风标准（所有资产通用，与 GhostRabbit_v2.png / ThunderOrb_v2 严格一致）：
> **粗黑均匀描边（手绘感、粗细不变）+ 纯色平涂（NO渐变/NO阴影/NO发光/NO半透明）+ 极简几何造型 + Q版贴纸风**
> 生成 prompt 关键词：`flat solid color fill, NO gradients, NO shading, NO glow, thick uniform black outline, minimalist sticker art, kawaii flat vector, transparent background`

---

## 一、缺失清单总览

| 优先级 | 资产 | 现状 | 用途 |
|--------|------|------|------|
| P0 | EarthVFX_Frames ×16 | **缺失** | 土属性投射物跟随粒子（土灵根激活后无特效） |
| P0 | FireVFX_Frames ×16 | 用静态BurnSpark代替 | 火属性统一16帧粒子 |
| P1 | FlameWindOrb（炎球） | 只有tint | 火+风融合球体 |
| P1 | FrostOrb（冰球） | 只有tint | 水+风融合球体 |
| P1 | WoodOrb（草球） | 只有tint | 水+土融合球体 |
| P1 | MetalFusionOrb（金球） | 只有tint | 火+土融合球体 |
| P1 | 融合VFX ×4套×16帧 | 缺失 | 融合属性投射物跟随粒子 |
| P1 | 灵石/功法碎片/词缀碎片 | 经验球贴图染色 | 掉落物专属造型 |
| P2 | 灵气刃元素形态变体 ×5 | tint | 各属性刃形（可选） |
| P2 | 符箓/灵阵元素变体 | tint | 可选优化 |
| 复用 | 道进化/神通 | — | 不需要新贴图，组合规则见文末 |

---

## 二、P0：属性 VFX 序列帧

### 1. EarthVFX_Frames（土之砂尘）
- **规格**：4×4 网格 sheet，1024×1024（每帧256×256），16帧，12fps循环
- **外观**：暖棕色（#8B6F47）小石粒+砂尘团，从中心向外抛洒又落回的抛物线运动。石粒是 3-4 个不规则小多边形（深棕2-3色阶），砂尘为半透明浅土色团雾。**无圆环、无光晕**，纯颗粒。
- **参考**：WindVFX 的线条密度感 + RockOrb 的棕色系
- **prompt**: `top-down dust burst particle effect, small brown rock chunks flying outward in parabolic arcs, sandy dust puffs, flat solid colors, thick black outlines on rocks only, NO glow, NO ring, dark green background, 4x4 grid sprite sheet, 16 frames`

### 2. FireVFX_Frames（火之飞焰）
- **规格**：同上 16帧
- **外观**：橙红（#FF6B2B）+明黄（#FFC93C）双色小火苗，围绕中心顺时针卷动，火苗形态为泪滴形/锯齿三角，边缘有 2-3 个独立小火星。替换现在不统一的静态 BurnSpark。
- **参考**：Sprite_20260807_213848.png（火球火花风）
- **prompt**: `orbiting small flame particles, orange and yellow teardrop fire sprites circling center clockwise, few detached sparks, flat solid color, NO glow, NO gradient, dark green background, 4x4 grid sprite sheet, 16 frames`

### 融合VFX（与单属性同规格，跟随投射物）
| 资产 | 外观 | 色板 |
|------|------|------|
| **FlameWindVFX（炎）** | 螺旋上升的火风旋涡：橙红火苗被青色风线拉成倾斜流线，整体像小型火焰龙卷 | 炽橙#FF4D26 + 风青#66E0D9 |
| **FrostVFX（冰）** | 环绕的六棱冰晶碎片+细小雪点，冰晶缓慢旋转闪烁白色高光边 | 冰蓝#80D9FF + 白 |
| **WoodVFX（草）** | 环绕的嫩绿叶片（椭圆叶形+叶脉线）+ 少量上升的绿色孢光点 | 翠绿#66CC4D + 嫩黄绿#A8E063 |
| **MetalFusionVFX（金）** | 环绕的小型金属锋刃碎片（三角形小刀片）+ 金色闪光十字星，锋刃朝外旋转 | 亮金#FFCC4D + 银白#E5E5EA |

---

## 三、P1：融合属性球体（4张，规格对齐 WindOrb_v2/ThunderOrb_v2）

统一规格：2048×2048 单张，透明背景，正交视角，动态造型（非正圆），flood-fill去白边（threshold 245）。

### 1. FlameWindOrb 炎球
- **造型**：火焰与旋风结合的**上升火螺旋**——底部是圆形火球核心（橙红平涂+黑描边），顶部拉出 2-3 股顺时针旋扭的火焰尖尾（尾尖渐变为风青色块，纯色不渐变，用分色块表现）。表面点缀 2 个白色小高光块。
- **色彩**：主体炽橙#FF4D26，火焰尾外缘橙黄#FFA640，风尖青#66E0D9，黑色描边
- **prompt**: `fire wind fusion orb, circular flame core with 3 spiral fire tails twisting clockwise, tail tips in teal color blocks, flat solid colors, thick uniform black outline, sticker style, transparent background`

### 2. FrostOrb 冰球
- **造型**：正圆冰晶球体，表面有 4-5 条多边形裂纹线（黑描边细线），顶部一个六角冰晶小尖角（像戴了小冰帽），环绕 3 个小三角冰碎片贴在球面。
- **色彩**：主体冰蓝#80D9FF，深部钴蓝#4DA6E8（分色平涂），冰晶尖白#F0F9FF
- **prompt**: `frost ice orb, round crystal ball with polygonal crack lines, small hexagon ice crystal spike on top, 3 small triangle ice shards stuck on surface, ice blue flat colors, thick black outline, sticker style`

### 3. WoodOrb 草球
- **造型**：圆球主体上缠绕 1-2 根**嫩藤蔓**（S形粗线带小叶片），顶部冒出 2 牊对称的嫩叶芽（像发芽的种子），球面有 3 个浅色叶形斑纹。
- **色彩**：主体翠绿#66CC4D，藤蔓深绿#3D8B37，嫩芽黄绿#A8E063，斑纹浅绿#8FD97A
- **prompt**: `nature wood orb, round seed ball wrapped with vine with small leaves, two sprout leaves growing from top, leaf shaped patches on surface, green flat colors, thick black outline, sticker style`

### 4. MetalFusionOrb 金球
- **造型**：多面体金属球（用 5-6 个平涂色块切面表现立体感，**非渐变而是分色平涂**），表面嵌 2-3 个小三角锋刃朝外突出（像刺球但刃少而锐），留 1 个白色十字星闪光。
- **色彩**：切面亮金#FFCC4D/中金#E6A832/暗金#BF8C1F 三阶，锋刃银白#E5E5EA
- **prompt**: `metal fusion orb, faceted golden sphere made of flat color polygons, 3 sharp triangular blades sticking outward, one white cross sparkle, gold flat colors, thick black outline, sticker style`

---

## 四、P1：掉落物专属贴图（3张，256-512px）

| 资产 | 造型 | 色彩 |
|------|------|------|
| **SpiritStone 灵石** | 中国古铜钱造型：圆形石币+中间方孔，币面刻小篆"灵"字（简化笔画），下缘 2 个缺口表现原石感 | 青白玉色#B8E0D2+深青绿#5F9E8C+方孔镂空 |
| **MethodFragment 功法碎片** | 残缺的卷轴/书页：竖向撕边的米色纸卷，露出 2-3 行模糊竖排"符文"短线，顶部卷轴轴头（深棕小圆柱） | 米黄#F0E6C8+符文青#5F9EA0+轴头棕#8B6F47 |
| **AffixFragment 词缀碎片** | 六角星形玉石碎片：不规则多边形晶片（像符文晶石），中央刻一个发光感的几何符号（空心菱形），断口边缘锯齿状 | 淡紫#C9A8E8+深紫#8B5FBF+符号白 |

---

## 五、P2：元素形态变体（可选优化，当前 tint 已可用）

### 灵气刃元素变体（5张，弧形刃规格同 Sprite_20260812_200505）
- **火刃**：刃身改为锯齿火焰边（内侧直线+外侧三段锯齿）
- **冰刃**：刃身笔直+尖端分叉成冰晶尖刺，表面裂纹线
- **土刃**：厚重石质刃（更宽），表面 2 条裂纹，边缘粗糙
- **雷刃**：刃身呈锯齿闪电状（Z形折线刃口），周身 2 个小电弧点
- **风刃**：双层弧线（外弧+内弧之间露出白色间隙），尾端散成 2 股流线

### 符箓元素变体（可选）：中央符号按属性替换——火=火焰符、水=三波纹、土=山形符、风=三横流线、雷=雷纹
### 灵阵元素变体（可选）：内圈图形按属性替换——火=三角炎阵、水=六波环、土=方形回纹、风=螺旋纹、雷=闪电辐条

---

## 六、道进化/神通视觉（零新资产·复用规则）

企划明确：所有道和神通**复用现有投射物贴图**，通过参数组合制造新视觉：

| 道类型 | 复用组合 |
|--------|----------|
| 火·火雨道 | Fireball 贴图 ×数量8-12 ×scale0.5 ×随机落点（雨状） |
| 火·赤阳道 | Fireball 贴图 ×scale2.5 ×数量1 ×慢速旋转（巨日） |
| 水·潮汐道 | WaterOrb ×数量6 ×scale0.8 ×环形排布旋转 |
| 雷·雷狱道 | ThunderOrbFrames ×数量16 ×全屏随机闪现 ×scale1.2 |
| 金·万剑道 | FlyingSword剑贴图 ×数量12 ×环形剑阵 ×自转+公转 |
| 神通 | 对应道视觉 ×scale2-3 + SpawnCircle法阵扩大版 ×屏幕震动 |

---

## 七、生成参数速查

- **球体/掉落物**：huoshan_seedream + image_urls=ThunderOrb_v2.png（造型参考）→ flood-fill threshold 240 → 2048×2048 → TextureImporter Sprite/Single
- **VFX序列帧**：frontier-game-design 深绿背景 sheet → 切割256×256 → greenBg 全局 threshold（g>0.25&&g>r+0.08&&g>b+0.08）→ 16帧 → Resources/{Name}VFX_Frames/
- **新VFX接入点**：SpiritOrbSpell/QiBladeSpell/TalismanSpell 的 folder 映射加 Earth/Fire/FlameWind/Frost/Wood/MetalFusion 分支
