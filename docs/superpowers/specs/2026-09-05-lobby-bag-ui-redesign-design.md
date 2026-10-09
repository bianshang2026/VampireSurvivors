# 大厅背包界面重建设计（方案 A）

日期：2026-09-05
状态：已获用户批准
场景：Assets/Scenes/大厅.scene（Canvas 1920×1080，CanvasScaler ScaleWithScreenSize）

## 1. 背景与问题

用户反馈：大厅 UI 三块内容互相脱节——"大背景整体的结构"和"边线边框"都不行，背景与顶部格子区、底部导航栏都不契合。

排查确认的具体问题：

1. **顶部浮空格子行是损坏副本**：Canvas 下存在 3 份 BagSlotsPreview（y=60/-66/-193）+ 嵌套脏对象（BagSlotsPreview (1) @x=783、Slot_4 (1) @x=1238），由上会话编辑器脚本僵尸执行（重投递）堆积产生，直接浮在黑底上无任何背板。
2. **现有 InventoryBG 比例失当**：源图 2056×530 横条，九宫格边框 571×224（左右边框占半幅、中部纸面仅 82px 高被硬拉伸）——边框过厚是"结构不行"的根源。
3. **导航栏风格不搭**：NavBg_Brown（绿色卷轴重上色）的卷轴造型+云朵装饰与整体不合。
4. **功能性 bug**：NavBg 是 Canvas 第一个子物体，渲染在 InventoryPanel 之下；打开背包后纸面（2263×1016）覆盖卡片区域，GraphicRaycaster 使纸面拦截点击 → **储物卡无法再点击关闭**，且场景无 Close 按钮（SectUIController.FindCloseButton 找不到任何名字含 Close 的按钮）→ 打开即卡死。

已验证：Assets 下无任何脚本引用 BagSlotsPreview / ItemGrid（仅 Temp/ExecutedCSharpScripts 历史脚本提及），删除安全。

## 2. 设计决策（用户逐条确认）

- 基础大厅 = 场景 + 底部导航栏（顶部不再常驻浮空格子）
- 点储物卡 → 展开完整背包界面：大纸面背景 + 顶部角色面板 + 13列×4行网格，一起出现/消失
- 三块设计语言统一：木框+米黄纸+金饰边（背景/格框）｜素色深棕+细金边（导航栏）
- 背景 AI 重生成（细边框根治）；导航栏 **AI 生成**（不用代码绘制，风格与背景完全同源、贴合）；格框复用 BagSlot.png
- 绿色卡片（宗门/悟性/秘境/功法/储物）一概不动

## 3. 布局结构

```
Canvas (1920×1080)
├── InventoryPanel（点储物卡开关，SectUIController 逻辑不改）
│   ├── InventoryBackground   ← 新 AI 背景 InventoryBG_v2，九宫格 Sliced
│   ├── CharacterPanel        ← 现有内容整体保留（立绘+6装备栏+名字属性）
│   └── BagGridPanel          ← 新建：13×4 = 52 格
└── NavBg（常驻；移到最后 = 最上层渲染，修复卡片被纸面遮挡不可点击的 bug）
    └── Card_Storage ×5（不动）
```

垂直预算（Canvas 坐标，+540 顶 / -540 底）：

- **InventoryPanel**：调整为 1920×1080 全屏居中（纯开关根节点，当前 1920×657 废弃）；InventoryBackground 从拉伸锚点改为**居中固定尺寸锚点**（不再用 stretch anchor）

- 导航栏：transform 不动（2083×320，中心 y≈-431），仅换 sprite 和切片方式
- 纸面：约 1900×920 居中，顶贴屏幕上缘，底端伸入导航栏后方（被栏体自然遮住）
- CharacterPanel：1280×340，纸面顶部（约 y +355 中心）
- BagGridPanel：格子 120×120、间距 8 → 网格 1656 宽 × 504 高，居中在角色面板下方（约 y -90 中心）
- 格子严格网格数学：`x = (col - 6) × 128`，`y = (row - 1.5) × 128`（col 0..12，row 0..3），杜绝错位

具体数值实现时可微调（±20px 级），但"纸面底端藏于导航栏后、网格完整可见、角色面板与网格不重叠"三条硬约束不变。

## 4. 视觉资产规格

### 4.1 背包大背景 InventoryBG_v2.png（AI 生成 1-2 次）

- 生成器：huoshan_seedream（沿上会话 InventoryBG 验证过的同源路线）
- 风格参照图：现有 InventoryBG.png + BagSlot.png（与导航栏共用同一组参照 → 画风一致）
- 生成内容：近全屏横向面板，**细边框（边框宽度 <5% 幅宽）**，纯米黄纸面中心，木框 + 内沿细金线，暖胡桃木色系；**明确要求无四角云朵/无额外装饰**（吸取上会话 AI 加云朵再去云朵的教训）；贴纸风粗描边语言与现有 UI 资产一致
- 处理管线（上会话已验证 + memory 教训）：
  1. 生成 2560×1440 → 下载
  2. 纸面纯色化（flood-fill/erode）
  3. 缩放至精确显示尺寸（1px=1unit，如 1900×920）；**Blit 前必须先 SetPixels32+Apply 写回**（避免旧纹理缩放 bug）
  4. 颜色判定法测边框（奶黄纸面 run ≥150 处 = 内线，内线外装饰色最远点 +12 = 边框），不能只用 alpha
  5. importer：textureType=Sprite、spriteImportMode=Single、PPU=100、spriteBorder=测得像素值；**注意 maxTextureSize=2048 静默压缩坑**（若目标超 2048 需关压缩上限）
  6. Image.type=Sliced

### 4.2 导航栏 NavBg_Ai.png（AI 生成 1-2 次）

- 生成器：huoshan_seedream，**与 4.1 共用同一组风格参照图与调色锚点**（深棕取自木框同色系、金线取自 BagSlot 金云同色系）
- 生成内容：素色深棕横向长条 + 细金描边 + 简洁圆角端头；**无云朵、无卷轴造型、无任何装饰**；扁平贴纸风
- 生成尺寸 ~2048×512 → 缩放为 1px=1unit 精确尺寸
- NavBg 的 Image 改 **Simple → Sliced**：spriteBorder 保留左右端头 + 上下薄边，中间纯深棕段拉伸——任意宽度下边线干净贴合不变形
- **NavBg transform 一概不动**，卡片（含其子物体文字/图标）零改动

### 4.3 格子框

- 复用现有 Assets/Resources/UI/BagSlot.png（已去红底透明版），52 格全部用它，不生成新资产

### 4.4 资产管理

- 新纹理走新文件名（_v2 / _Ai），旧资产（InventoryBG.png、NavBg_Brown.png）不删除不覆盖，仅取消引用 → 随时可回退
- 处理前 Temp 备份源文件；**绝不从已处理图二次加工**（累积误删教训）

## 5. 清理清单

- 删除 Canvas 下 3 份 BagSlotsPreview 像素副本（含其全部嵌套脏对象）
- 删除空 ItemGrid（Canvas/InventoryPanel/InventoryBackground/ItemGrid，0 子物体，无代码引用）
- NavBg 旧 SpriteRenderer 版 UINavBar 遗留（若仍在场景根，保持不动——上会话已确认是无内容空壳，不在本次范围）

## 6. 验证计划

1. **Edit 模式资产检查**：importer 设置（Sprite/Single/PPU=100/spriteBorder 值）、新 sprite 赋值状态
2. **Play 模式结构化验证**（exec_runtime_script）：
   - SectUIController.ToggleInventory() 驱动开关
   - 读结构化数据：InventoryPanel 激活状态、BagGridPanel 子物体数=52、抽样格子位置（网格数学校验）、NavBg 渲染顺序=最后兄弟、卡片 RaycastTarget 不被遮挡
3. **最终视觉门（MP4）**：录制"关闭态→点储物展开→完整界面→再点收起"全流程一段（record_game_view），编码成功即通过
4. **防僵尸执行**：所有创建/删除脚本写成幂等形式（先查存在再动手）；场景操作后检查 Canvas 子物体数（应为 2：InventoryPanel + NavBg）

## 7. 回退方案

- 资产级：旧 InventoryBG.png / NavBg_Brown.png 完好，重新赋值 sprite 即回退
- 场景级：修改前确认 dirty=false；每阶段保存（ensure_scene_saved）
- 脚本级：SectUIController / CharacterPanelController 均不改

## 8. 范围外（本次不做）

- 格子与装备栏的功能绑定（CharacterPanelController.SetEquip/ClearEquip API 预留；装备/背包数据系统未实现）
- 储物卡以外的卡片功能（宗门/悟性/秘境/功法目前仅 Debug.Log）
- 非严格 16:9 的适配优化（Sliced 素色中心拉伸无痕，可接受）
- 绿色卡片与棕色导航栏的配色再调整（用户已确认卡片不动）
