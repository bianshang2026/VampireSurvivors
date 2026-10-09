# 大厅背包界面重建实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 按 `docs/superpowers/specs/2026-09-05-lobby-bag-ui-redesign-design.md` 重建大厅背包界面：AI 生成同风格大背景+导航栏、13×4 网格、修复卡片点击 bug。

**Architecture:** 两张 AI 纹理（同一组风格参照图）→ 后处理管线（flood-fill 去白底/裁切/缩放/纸面纯色化/测边框/九宫格）→ 场景清理（删 3 份僵尸执行脏副本）→ 布局重建（InventoryPanel 全屏化 + BagGridPanel 52 格 + NavBg 移到最后修点击 bug）→ Play 验证 + MP4 视觉门。

**Tech Stack:** Unity 2022.3.62t11 团结 / UGUI / huoshan_seedream AI 生成（TJGenerators 扩展，MCP 为后备）/ exec_editor_script + exec_runtime_script 工具链。

**重要约束（执行时必读）：**
- 项目**无 git 仓库**：所有 "Commit" 步骤替换为场景保存检查点（`unity_scene ensure_scene_saved`），每 Task 结束确认场景已保存。
- 生成积分敏感：本计划共 2 次 AI 生成（背景 1 + 导航栏 1），重试需先问用户。
- 所有 exec_editor_script 写成幂等形式（先查存在再动手），防僵尸执行重投递造成重复创建。
- 批量纹理导入避免 SaveAndReimport 循环（单文件一次可以）。
- 僵尸执行教训：操作后检查 Canvas 子物体数（最终应为 2：InventoryPanel + NavBg）。
- 每次写新 PNG 后调一次 `unity_editor.refresh`（每批次一次，不要循环调）。
- Console 纪律：风险操作前 clear → 操作后 get。
- 参考资产路径：`Assets/Resources/UI/InventoryBG.png`（旧背景，风格参照）、`Assets/Resources/UI/BagSlot.png`（格框，风格参照+复用）。

---

### Task 1: AI 生成背包大背景并后处理为 InventoryBG_v2.png

**Files:**
- Create: `Assets/Resources/UI/InventoryBG_v2.png`（九宫格，目标 1900×920）
- 源: `Assets/TJGenerators/History/`（生成输出）

- [ ] **Step 1: 预检**

`unity_editor get_state`：确认 isPlaying=false、scene dirty=false、无编译错误。若 dirty=true 先 `unity_scene save`。`unity_console clear` 建立干净边界。

- [ ] **Step 2: 提交 AI 生成（huoshan_seedream）**

优先用 TJGenerators 扩展（`activate_skill unity-image-generation` 后按其流程用扩展的 generate_image 工具）；扩展不可用则用 MCP `generate_image`（provider=seedream）。

- 风格参照图（两张都传）：`Assets/Resources/UI/InventoryBG.png` + `Assets/Resources/UI/BagSlot.png`
- 尺寸：2560×1440（landscape_16_9）
- Prompt（逐字使用）：

```
横版游戏UI大背景面板，古代修仙主题。暖胡桃木色木框边饰，边框很细，只占画面宽度很小的比例（约5%）。
木框内部为纯米黄宣纸纸面，木框内沿有一圈很细的金色线装饰。
四个角完全干净，无云朵、无如意纹、无任何装饰图案。
扁平贴纸风格，粗细均匀的深色描边，纯色平涂，无纹理噪点，无立体感，无阴影，无发光。
整体是一张完整的不透明矩形面板，水平放置，占满画面。
```

- 等待生成完成，记录输出文件的完整路径（History 目录下）。
- **生成后立刻做一次视觉检查**（`unity_screenshot capture_asset` 渲染该图或读回缩略图 + 目视）：边框必须细、四角无云朵、纸面干净。**不合格直接重试一次（先告知用户）**，不要进入后处理。

- [ ] **Step 3: 后处理管线（exec_editor_script，完整代码）**

脚本自动取 History 下 30 分钟内最新的图片文件。当前时间点只有本 Task 生成的文件，无歧义。

```csharp
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// ========== 配置 ==========
string outPath = "Assets/Resources/UI/InventoryBG_v2.png";
string outAbs = Path.Combine(Application.dataPath, "Resources/UI/InventoryBG_v2.png");
int targetW = 1900, targetH = 920;

// 1. 找最新源图（30分钟内）
var histDir = Path.Combine(Application.dataPath, "TJGenerators/History");
var srcFile = new DirectoryInfo(histDir).GetFiles()
    .Where(f => f.Extension.ToLower() == ".png" || f.Extension.ToLower() == ".jpeg" || f.Extension.ToLower() == ".jpg")
    .OrderByDescending(f => f.LastWriteTime)
    .First(f => (System.DateTime.Now - f.LastWriteTime).TotalMinutes < 30);
Debug.Log($"[BGv2] source: {srcFile.FullName} ({srcFile.LastWriteTime})");

// 2. 加载 → 必须新建 RGBA32（JPEG 源是 RGB24，直接写 alpha 会被静默丢弃）
var loader = new Texture2D(2, 2);
loader.LoadImage(File.ReadAllBytes(srcFile.FullName));
int sw = loader.width, sh = loader.height;
var src = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
src.SetPixels32(loader.GetPixels32());
src.Apply();
var px = src.GetPixels32();

// 3. flood-fill 从四边去白底（阈值225，8连通）——纸面被木框包住，不会被触及
bool IsWhite(Color32 c) => c.r > 225 && c.g > 225 && c.b > 225;
var bg = new bool[sw * sh];
var stack = new System.Collections.Generic.Stack<int>();
for (int x = 0; x < sw; x++) {
    int t = x, b = (sh - 1) * sw + x;
    if (IsWhite(px[t])) { bg[t] = true; stack.Push(t); }
    if (IsWhite(px[b])) { bg[b] = true; stack.Push(b); }
}
for (int y = 0; y < sh; y++) {
    int l = y * sw, r = y * sw + sw - 1;
    if (IsWhite(px[l])) { bg[l] = true; stack.Push(l); }
    if (IsWhite(px[r])) { bg[r] = true; stack.Push(r); }
}
int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 }, dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
while (stack.Count > 0) {
    int i = stack.Pop(); int x = i % sw, y = i / sw;
    for (int d = 0; d < 8; d++) {
        int nx = x + dx8[d], ny = y + dy8[d];
        if (nx < 0 || ny < 0 || nx >= sw || ny >= sh) continue;
        int ni = ny * sw + nx;
        if (!bg[ni] && IsWhite(px[ni])) { bg[ni] = true; stack.Push(ni); }
    }
}
// 3b. 3轮白边侵蚀（>205 且 8邻接透明区 → 透明）
for (int round = 0; round < 3; round++) {
    var toClear = new System.Collections.Generic.List<int>();
    for (int y = 1; y < sh - 1; y++) for (int x = 1; x < sw - 1; x++) {
        int i = y * sw + x;
        if (bg[i] || px[i].a == 0) continue;
        if (px[i].r > 205 && px[i].g > 205 && px[i].b > 205) {
            for (int d = 0; d < 8; d++) {
                int j = (y + dy8[d]) * sw + (x + dx8[d]);
                if (bg[j] || px[j].a == 0) { toClear.Add(i); break; }
            }
        }
    }
    foreach (var i in toClear) bg[i] = true;
}
int removed = 0;
for (int i = 0; i < px.Length; i++) if (bg[i]) { var c = px[i]; c.a = 0; px[i] = c; removed++; }
Debug.Log($"[BGv2] flood-fill removed {removed}/{px.Length} px ({100.0 * removed / px.Length:F1}%)，opaque={(px.Length - removed) * 100.0 / px.Length:F1}%");
if ((px.Length - removed) * 100.0 / px.Length < 40) { Debug.LogError("[BGv2] 内容保留 <40%，疑似误删，中止（检查源图）"); return; }

// 4. 写回新纹理再缩放（Blit 用的是传入纹理对象，不写回=缩放旧图）
src.SetPixels32(px); src.Apply();
var rt = new RenderTexture(targetW, targetH, 0, RenderTextureFormat.ARGB32);
Graphics.Blit(src, rt);
RenderTexture.active = rt;
var scaled = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
scaled.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
scaled.Apply();
RenderTexture.active = null;
Object.DestroyImmediate(src); Object.DestroyImmediate(loader); rt.Release();

// 5. 测边框：从四边找首个 ≥50 连续"米黄纸面"run（颜色判定法，纹理坐标 y=0 在底）
var spx = scaled.GetPixels32();
int W = targetW, H = targetH;
bool IsCream(Color32 c) => c.r > 215 && c.g > 200 && c.b > 175;
int leftBorder = -1, rightBorder = -1, topBorder = -1, bottomBorder = -1;
{ int y = H / 2, run = 0;
  for (int x = 0; x < W; x++) { if (IsCream(spx[y * W + x])) { run++; if (run >= 50) { leftBorder = x - 49; break; } } else run = 0; } }
{ int y = H / 2, run = 0;
  for (int x = W - 1; x >= 0; x--) { if (IsCream(spx[y * W + x])) { run++; if (run >= 50) { rightBorder = (W - 1) - x - 49; break; } } else run = 0; } }
{ int x = W / 2, run = 0;
  for (int y = H - 1; y >= 0; y--) { if (IsCream(spx[y * W + x])) { run++; if (run >= 50) { topBorder = (H - 1) - y - 49; break; } } else run = 0; } }
{ int x = W / 2, run = 0;
  for (int y = 0; y < H; y++) { if (IsCream(spx[y * W + x])) { run++; if (run >= 50) { bottomBorder = y - 49; break; } } else run = 0; } }
// 兜底与钳制
int fbL = (int)(W * 0.05f), fbT = (int)(H * 0.05f);
if (leftBorder < 0) leftBorder = fbL; if (rightBorder < 0) rightBorder = fbL;
if (topBorder < 0) topBorder = fbT; if (bottomBorder < 0) bottomBorder = fbT;
leftBorder = Mathf.Clamp(leftBorder, 10, W / 3); rightBorder = Mathf.Clamp(rightBorder, 10, W / 3);
topBorder = Mathf.Clamp(topBorder, 10, H / 3); bottomBorder = Mathf.Clamp(bottomBorder, 10, H / 3);
Debug.Log($"[BGv2] borders: L={leftBorder} R={rightBorder} T={topBorder} B={bottomBorder} (expect thin, <{W / 20})");

// 6. 纸面纯色化：中心取色 → 填充整个内部矩形（九宫格中心拉伸无痕）
Color32 acc = new Color32(); int n = 0;
for (int y = H / 2 - 50; y < H / 2 + 50; y++) for (int x = W / 2 - 50; x < W / 2 + 50; x++) {
    var c = spx[y * W + x]; acc.r += c.r; acc.g += c.g; acc.b += c.b; n++;
}
var flat = new Color32((byte)(acc.r / n), (byte)(acc.g / n), (byte)(acc.b / n), 255);
Debug.Log($"[BGv2] flat cream = #{ColorUtility.ToHtmlStringRGB(flat)}");
for (int y = bottomBorder; y < H - topBorder; y++)
    for (int x = leftBorder; x < W - rightBorder; x++)
        spx[y * W + x] = flat;
scaled.SetPixels32(spx); scaled.Apply();

// 7. 写 PNG + 导入设置（Sprite/Single/PPU=100/spriteBorder/NPOT 不缩放）
File.WriteAllBytes(outAbs, scaled.EncodeToPNG());
Object.DestroyImmediate(scaled);
AssetDatabase.ImportAsset(outPath);
var ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
ti.textureType = TextureImporterType.Sprite;
ti.spriteImportMode = SpriteImportMode.Single;
ti.spritePixelsPerUnit = 100;
ti.spriteBorder = new Vector4(leftBorder, bottomBorder, rightBorder, topBorder);
ti.alphaIsTransparency = true;
ti.mipmapEnabled = false;
ti.npotScale = TextureImporterNPOTScale.None;
ti.maxTextureSize = 2048;
ti.SaveAndReimport();
var imported = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
Debug.Log($"[BGv2] DONE: {outPath} sprite={imported != null} tex={imported?.texture.width}x{imported?.texture.height} border={ti.spriteBorder}");
return new { ok = imported != null, w = imported?.texture.width, h = imported?.texture.height, border = ti.spriteBorder.ToString(), flatCream = "#" + ColorUtility.ToHtmlStringRGB(flat) };
```

- [ ] **Step 4: 验证资产**

`unity_console get`：无 error。日志应显示：opaque ≥ 40%、border 四值均在 10~W/3 之间且期望 < 95（细边框）。若 border 值 > W/10（边框仍过厚），**不重跑管线**——回 Step 2 重生成（告知用户后）。
再 `unity_editor refresh`（本批次唯一一次），确认无新编译错误。

---

### Task 2: AI 生成导航栏并后处理为 NavBg_Ai.png

**Files:**
- Create: `Assets/Resources/UI/NavBg_Ai.png`（九宫格，目标 2083×320）

- [ ] **Step 1: 提交 AI 生成**

同 Task 1 通道（扩展优先，MCP `generate_image` provider=seedream 后备）。

- 风格参照图（与 Task 1 完全相同）：`Assets/Resources/UI/InventoryBG.png` + `Assets/Resources/UI/BagSlot.png`
- 尺寸：2560×1440（landscape_16_9）
- Prompt（逐字使用）：

```
横版游戏UI底部导航栏长条，古代修仙主题。素色深棕色实心横条，外沿一圈很细的金色描边，
左右两端为简洁的圆角端头。无云朵、无卷轴造型、无如意纹、无任何装饰图案。
扁平贴纸风格，纯色平涂，无纹理噪点，无立体感，无阴影，无发光。
超宽横条构图，长条占满画面宽度，长宽比约6:1，上下留白。
```

- 生成后目视检查：素色深棕、细金边、圆角端头、无装饰。不合格重试一次（先告知用户）。

- [ ] **Step 2: 后处理管线（exec_editor_script，完整代码）**

与 Task 1 Step 3 同一时间执行（此刻 History 最新文件=本 Task 生成图）。

```csharp
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

string outPath = "Assets/Resources/UI/NavBg_Ai.png";
string outAbs = Path.Combine(Application.dataPath, "Resources/UI/NavBg_Ai.png");
int targetW = 2083, targetH = 320;

// 1. 最新源图
var histDir = Path.Combine(Application.dataPath, "TJGenerators/History");
var srcFile = new DirectoryInfo(histDir).GetFiles()
    .Where(f => f.Extension.ToLower() == ".png" || f.Extension.ToLower() == ".jpeg" || f.Extension.ToLower() == ".jpg")
    .OrderByDescending(f => f.LastWriteTime)
    .First(f => (System.DateTime.Now - f.LastWriteTime).TotalMinutes < 30);
Debug.Log($"[NAV] source: {srcFile.FullName}");

// 2. RGBA32 加载
var loader = new Texture2D(2, 2);
loader.LoadImage(File.ReadAllBytes(srcFile.FullName));
int sw = loader.width, sh = loader.height;
var src = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
src.SetPixels32(loader.GetPixels32());
src.Apply();
var px = src.GetPixels32();

// 3. flood-fill 去白底（225）+ 3轮侵蚀（205）——与背景管线同法
bool IsWhite(Color32 c) => c.r > 225 && c.g > 225 && c.b > 225;
var bg = new bool[sw * sh];
var stack = new System.Collections.Generic.Stack<int>();
for (int x = 0; x < sw; x++) {
    int t = x, b = (sh - 1) * sw + x;
    if (IsWhite(px[t])) { bg[t] = true; stack.Push(t); }
    if (IsWhite(px[b])) { bg[b] = true; stack.Push(b); }
}
for (int y = 0; y < sh; y++) {
    int l = y * sw, r = y * sw + sw - 1;
    if (IsWhite(px[l])) { bg[l] = true; stack.Push(l); }
    if (IsWhite(px[r])) { bg[r] = true; stack.Push(r); }
}
int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 }, dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
while (stack.Count > 0) {
    int i = stack.Pop(); int x = i % sw, y = i / sw;
    for (int d = 0; d < 8; d++) {
        int nx = x + dx8[d], ny = y + dy8[d];
        if (nx < 0 || ny < 0 || nx >= sw || ny >= sh) continue;
        int ni = ny * sw + nx;
        if (!bg[ni] && IsWhite(px[ni])) { bg[ni] = true; stack.Push(ni); }
    }
}
for (int round = 0; round < 3; round++) {
    var toClear = new System.Collections.Generic.List<int>();
    for (int y = 1; y < sh - 1; y++) for (int x = 1; x < sw - 1; x++) {
        int i = y * sw + x;
        if (bg[i] || px[i].a == 0) continue;
        if (px[i].r > 205 && px[i].g > 205 && px[i].b > 205) {
            for (int d = 0; d < 8; d++) {
                int j = (y + dy8[d]) * sw + (x + dx8[d]);
                if (bg[j] || px[j].a == 0) { toClear.Add(i); break; }
            }
        }
    }
    foreach (var i in toClear) bg[i] = true;
}
int removed = 0;
for (int i = 0; i < px.Length; i++) if (bg[i]) { var c = px[i]; c.a = 0; px[i] = c; removed++; }
Debug.Log($"[NAV] flood removed {100.0 * removed / px.Length:F1}%");
if ((px.Length - removed) * 100.0 / px.Length < 10) { Debug.LogError("[NAV] 内容保留 <10%，疑似误删，中止"); return; }

// 4. 内容包围盒裁切
int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
for (int y = 0; y < sh; y++) for (int x = 0; x < sw; x++) {
    if (px[y * sw + x].a > 128) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
}
int cw = maxX - minX + 1, ch = maxY - minY + 1;
Debug.Log($"[NAV] content bbox {cw}x{ch} (aspect {cw / (float)ch:F2}, target 6.51)");
if (cw / (float)ch < 2.5f) { Debug.LogError("[NAV] 长条长宽比 <2.5，生成图不是横条，中止回退重生成"); return; }
var cropped = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
var cpx = new Color32[cw * ch];
for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++) cpx[y * cw + x] = px[(minY + y) * sw + (minX + x)];
cropped.SetPixels32(cpx); cropped.Apply();
Object.DestroyImmediate(src); Object.DestroyImmediate(loader);

// 5. 非等比缩放到精确 2083×320（1px=1unit，显示尺寸=源尺寸 → Sliced 渲染零变形）
var rt = new RenderTexture(targetW, targetH, 0, RenderTextureFormat.ARGB32);
Graphics.Blit(cropped, rt);
RenderTexture.active = rt;
var scaled = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
scaled.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
scaled.Apply();
RenderTexture.active = null;
Object.DestroyImmediate(cropped); rt.Release();
var spx = scaled.GetPixels32();
int W = targetW, H = targetH;

// 6. 测边框
// 深棕检测（内芯）：暗且暖
bool IsBrownCore(Color32 c) => c.a > 200 && c.r < 150 && c.r >= c.g && c.g >= c.b;
int topBorder = 0, bottomBorder = 0;
{ int x = W / 2; for (int y = H - 1; y >= 0; y--) { if (IsBrownCore(spx[y * W + x])) { topBorder = (H - 1 - y) + 6; break; } } }
{ int x = W / 2; for (int y = 0; y < H; y++) { if (IsBrownCore(spx[y * W + x])) { bottomBorder = y + 6; break; } } }
int FirstOpaqueX(int y) { for (int x = 0; x < W; x++) if (spx[y * W + x].a > 128) return x; return W; }
int LastOpaqueX(int y) { for (int x = W - 1; x >= 0; x--) if (spx[y * W + x].a > 128) return x; return -1; }
int midFirst = FirstOpaqueX(H / 2), midLast = LastOpaqueX(H / 2);
int leftBorder = Mathf.Max(0, FirstOpaqueX(8) - midFirst) + 6;
int rightBorder = Mathf.Max(0, midLast - LastOpaqueX(H - 9)) + 6;
leftBorder = Mathf.Clamp(leftBorder, 4, W / 3); rightBorder = Mathf.Clamp(rightBorder, 4, W / 3);
topBorder = Mathf.Clamp(topBorder, 4, H / 3); bottomBorder = Mathf.Clamp(bottomBorder, 4, H / 3);
Debug.Log($"[NAV] borders: L={leftBorder} R={rightBorder} T={topBorder} B={bottomBorder}");

// 7. 中心纯色化（中段纯深棕，横向拉伸无痕）
Color32 flat = spx[(H / 2) * W + (W / 2)];
Debug.Log($"[NAV] flat brown = #{ColorUtility.ToHtmlStringRGB(flat)}");
for (int y = bottomBorder; y < H - topBorder; y++)
    for (int x = leftBorder; x < W - rightBorder; x++)
        spx[y * W + x] = flat;
scaled.SetPixels32(spx); scaled.Apply();

// 8. 写 PNG + 导入设置
// 注意 maxTextureSize 必须 4096：目标宽 2083 > 2048，2048 会被静默降采样破坏 1px=1unit（质量审查发现）
File.WriteAllBytes(outAbs, scaled.EncodeToPNG());
Object.DestroyImmediate(scaled);
AssetDatabase.ImportAsset(outPath);
var ti = (TextureImporter)AssetImporter.GetAtPath(outPath);
ti.textureType = TextureImporterType.Sprite;
ti.spriteImportMode = SpriteImportMode.Single;
ti.spritePixelsPerUnit = 100;
ti.spriteBorder = new Vector4(leftBorder, bottomBorder, rightBorder, topBorder);
ti.alphaIsTransparency = true;
ti.mipmapEnabled = false;
ti.npotScale = TextureImporterNPOTScale.None;
ti.maxTextureSize = 4096;
ti.SaveAndReimport();
var imported = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
Debug.Log($"[NAV] DONE: {outPath} sprite={imported != null} border={ti.spriteBorder}");
return new { ok = imported != null, border = ti.spriteBorder.ToString() };
```

- [ ] **Step 3: 验证资产**

`unity_console get` 无 error；日志显示 aspect ≥ 2.5、border 值合理（T/B 很小 ~10-20、L/R 为端头宽度）。`unity_editor refresh`。

---

### Task 3: 场景清理（删脏副本）

**Files:**
- Modify: `Assets/Scenes/大厅.scene`（删除对象）

- [ ] **Step 1: 清理脚本（exec_editor_script，幂等）**

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

var canvas = GameObject.Find("Canvas");
if (canvas == null) { Debug.LogError("[CLEAN] Canvas not found"); return; }
int deleted = 0;
// 删除 Canvas 下所有 BagSlotsPreview*（含 3 份损坏副本及其嵌套脏对象）
for (int i = canvas.transform.childCount - 1; i >= 0; i--) {
    var c = canvas.transform.GetChild(i);
    if (c.name.StartsWith("BagSlotsPreview")) {
        Debug.Log($"[CLEAN] delete Canvas/{c.name} (children={c.childCount})");
        Object.DestroyImmediate(c.gameObject);
        deleted++;
    }
}
// 删除空 ItemGrid（已确认无 Assets 代码引用）
var itemGrid = GameObject.Find("Canvas/InventoryPanel/InventoryBackground/ItemGrid");
if (itemGrid != null) { Debug.Log("[CLEAN] delete ItemGrid"); Object.DestroyImmediate(itemGrid); deleted++; }
Debug.Log($"[CLEAN] deleted {deleted}, Canvas children now = {canvas.transform.childCount} (expect 2: InventoryPanel + NavBg)");
EditorSceneManager.MarkSceneDirty(canvas.scene);
return new { deleted, canvasChildren = canvas.transform.childCount };
```

- [ ] **Step 2: 验证**

日志：deleted ≥ 4（3 份 BagSlotsPreview + 1 ItemGrid），Canvas children = 2。
**此时先不保存场景**（布局重建后再一起保存，减少保存次数）。

---

### Task 4: 布局重建

**Files:**
- Modify: `Assets/Scenes/大厅.scene`

- [ ] **Step 1: 布局重建脚本（exec_editor_script，幂等，完整代码）**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

var canvas = GameObject.Find("Canvas");
if (canvas == null) { Debug.LogError("[BUILD] Canvas not found"); return; }

// A. InventoryPanel → 1920×1080 全屏居中（纯开关根节点）
var panelGo = GameObject.Find("Canvas/InventoryPanel");
var panel = (RectTransform)panelGo.transform;
panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
panel.anchoredPosition = Vector2.zero;
panel.sizeDelta = new Vector2(1920, 1080);

// B. InventoryBackground → 居中固定 1900×920 + InventoryBG_v2 + Sliced
var bgGo = GameObject.Find("Canvas/InventoryPanel/InventoryBackground");
var bgRt = (RectTransform)bgGo.transform;
bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
bgRt.anchoredPosition = new Vector2(0, 80);   // 纸面 +540..-380
bgRt.sizeDelta = new Vector2(1900, 920);
var bgImg = bgGo.GetComponent<Image>();
bgImg.type = Image.Type.Sliced;
bgImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/InventoryBG_v2.png");
if (bgImg.sprite == null) { Debug.LogError("[BUILD] InventoryBG_v2 sprite 加载失败，先完成 Task 1"); return; }

// C. CharacterPanel → 居中 (0,+355) 1280×340（纸面顶部，内容与子物体不动）
var charGo = GameObject.Find("Canvas/InventoryPanel/CharacterPanel");
var charRt = (RectTransform)charGo.transform;
charRt.anchorMin = charRt.anchorMax = charRt.pivot = new Vector2(0.5f, 0.5f);
charRt.anchoredPosition = new Vector2(0, 355);
charRt.sizeDelta = new Vector2(1280, 340);

// D. BagGridPanel → 新建 13×4 = 52 格（幂等：先删旧）
var oldGrid = GameObject.Find("Canvas/InventoryPanel/BagGridPanel");
if (oldGrid != null) Object.DestroyImmediate(oldGrid);
var grid = new GameObject("BagGridPanel", typeof(RectTransform));
grid.transform.SetParent(panelGo.transform, false);
var gridRt = (RectTransform)grid.transform;
gridRt.anchorMin = gridRt.anchorMax = gridRt.pivot = new Vector2(0.5f, 0.5f);
gridRt.anchoredPosition = new Vector2(0, -90);   // 网格 +162..-342
gridRt.sizeDelta = new Vector2(1656, 504);
var slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/BagSlot.png");
if (slotSprite == null) { Debug.LogError("[BUILD] BagSlot sprite 加载失败"); return; }
for (int r = 0; r < 4; r++) for (int c = 0; c < 13; c++) {
    var slot = new GameObject($"Slot_r{r}_c{c}", typeof(RectTransform), typeof(Image));
    slot.transform.SetParent(grid.transform, false);
    var srt = (RectTransform)slot.transform;
    srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
    srt.anchoredPosition = new Vector2((c - 6) * 128, (r - 1.5f) * 128);   // 严格网格数学
    srt.sizeDelta = new Vector2(120, 120);
    var img = slot.GetComponent<Image>();
    img.sprite = slotSprite;
    img.preserveAspect = true;
    img.raycastTarget = false;   // 纯展示，不挡点击
}
Debug.Log($"[BUILD] BagGridPanel created, slots = {grid.transform.childCount}");

// E. NavBg → 换 NavBg_Ai + Sliced + 移到最后（修卡片被纸面遮挡不可点击的 bug）
//    transform/卡片零改动
var nav = GameObject.Find("Canvas/NavBg");
var navImg = nav.GetComponent<Image>();
navImg.type = Image.Type.Sliced;
navImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/NavBg_Ai.png");
if (navImg.sprite == null) { Debug.LogError("[BUILD] NavBg_Ai sprite 加载失败，先完成 Task 2"); return; }
nav.transform.SetAsLastSibling();

// F. 汇报
Debug.Log($"[BUILD] Canvas children = {canvas.transform.childCount}: " +
    string.Join(", ", Enumerable(canvas.transform)));
System.Collections.Generic.IEnumerable<string> Enumerable(UnityEngine.Transform t) {
    for (int i = 0; i < t.childCount; i++) yield return t.GetChild(i).name;
}
EditorSceneManager.MarkSceneDirty(canvas.scene);
```

> 注意：若 Roslyn 对局部函数在表达式中的写法报错，把 F 段换成普通 for 循环拼接字符串（语义相同）。

- [ ] **Step 2: 场景保存（检查点，代替 git commit）**

`unity_scene action=ensure_scene_saved`。然后 `unity_console get` 确认无 error。

---

### Task 5: Edit 模式结构验证

- [ ] **Step 1: 读回验证（exec_editor_script）**

```csharp
using UnityEngine;
using UnityEngine.UI;

var sb = new System.Text.StringBuilder();
var canvas = GameObject.Find("Canvas");
sb.AppendLine($"Canvas children = {canvas.transform.childCount} (expect 2)");
for (int i = 0; i < canvas.transform.childCount; i++) sb.AppendLine($"  [{i}] {canvas.transform.GetChild(i).name}");
var panel = GameObject.Find("Canvas/InventoryPanel").GetComponent<RectTransform>();
sb.AppendLine($"InventoryPanel size={panel.sizeDelta} pos={panel.anchoredPosition} (expect 1920x1080 @(0,0))");
var bg = GameObject.Find("Canvas/InventoryPanel/InventoryBackground").GetComponent<Image>();
var bgRt = (RectTransform)bg.transform;
sb.AppendLine($"InventoryBG sprite={bg.sprite?.name} type={bg.type} size={bgRt.sizeDelta} pos={bgRt.anchoredPosition}");
sb.AppendLine($"  border L{bg.sprite?.border.x} B{bg.sprite?.border.y} R{bg.sprite?.border.z} T{bg.sprite?.border.w} (expect thin)");
var grid = GameObject.Find("Canvas/InventoryPanel/BagGridPanel");
sb.AppendLine($"BagGridPanel slots={grid.transform.childCount} (expect 52)");
var s00 = (RectTransform)grid.transform.Find("Slot_r0_c0");
var s3c12 = (RectTransform)grid.transform.Find("Slot_r3_c12");
sb.AppendLine($"s00 pos=({s00.anchoredPosition.x},{s00.anchoredPosition.y}) (expect -768,192)");
sb.AppendLine($"s3c12 pos=({s3c12.anchoredPosition.x},{s3c12.anchoredPosition.y}) (expect 768,-192)");
var nav = GameObject.Find("Canvas/NavBg");
var navImg = nav.GetComponent<Image>();
sb.AppendLine($"NavBg sprite={navImg.sprite?.name} type={navImg.type} siblingIndex={nav.transform.GetSiblingIndex()} of {canvas.transform.childCount} (expect LAST)");
var navRt = (RectTransform)nav.transform;
sb.AppendLine($"NavBg pos=({navRt.anchoredPosition.x},{navRt.anchoredPosition.y}) size={navRt.sizeDelta} (expect UNCHANGED 0,109 / 2083x320)");
var cp = GameObject.Find("Canvas/InventoryPanel/CharacterPanel").GetComponent<RectTransform>();
sb.AppendLine($"CharacterPanel pos={cp.anchoredPosition} size={cp.sizeDelta} children={cp.childCount} (expect 9: CharDisplay+6slots+Txt_Name+EmptyLabels)");
Debug.Log(sb.ToString());
return sb.ToString();
```

- [ ] **Step 2: 通过标准**

- Canvas children = 2，顺序 [InventoryPanel, NavBg]
- InventoryBG sprite=InventoryBG_v2、type=Sliced、border 四值 < 100
- 52 格、两个抽样位置精确匹配 (-768,192) / (768,-192)
- NavBg sprite=NavBg_Ai、type=Sliced、siblingIndex=1（最后）、pos/size 与改动前一致（(0,109)/(2083×320)，注意 anchoredPosition）
- CharacterPanel (0,355)、1280×340、9 子物体

全部通过 → `unity_scene ensure_scene_saved`。任何一项不过 → 修对应 Task 后重跑本验证。

---

### Task 6: Play 模式验证 + MP4 最终视觉门

- [ ] **Step 1: 结构化运行时验证（exec_runtime_script）**

```csharp
using System.Collections;
using UnityEngine;

var canvas = GameObject.Find("Canvas");
var panelGo = GameObject.Find("Canvas/InventoryPanel");
var gridGo = GameObject.Find("Canvas/InventoryPanel/BagGridPanel");
var nav = GameObject.Find("Canvas/NavBg");
var sb = new System.Text.StringBuilder();

// 反射拿 SectUIController（避免编译期类型解析问题）
Component sec = null;
foreach (var c in canvas.GetComponents<Component>())
    if (c != null && c.GetType().Name == "SectUIController") { sec = c; break; }
if (sec == null) { Debug.LogError("[PLAY] SectUIController not found"); return "FAIL: no SectUIController"; }
var toggle = sec.GetType().GetMethod("ToggleInventory");

sb.AppendLine($"initial panel active={panelGo.activeInHierarchy} (expect False, Start hides it)");
toggle.Invoke(sec, null);
sb.AppendLine($"after open   active={panelGo.activeInHierarchy} (expect True)");
sb.AppendLine($"grid slots={gridGo.transform.childCount} (expect 52)");
sb.AppendLine($"NavBg last={nav.transform.GetSiblingIndex() == canvas.transform.childCount - 1}");
var bgImg = GameObject.Find("Canvas/InventoryPanel/InventoryBackground").GetComponent<UnityEngine.UI.Image>();
sb.AppendLine($"bg visible={bgImg.enabled} sprite={bgImg.sprite?.name}");
toggle.Invoke(sec, null);
sb.AppendLine($"after close active={panelGo.activeInHierarchy} (expect False)");
Debug.Log(sb.ToString());
return sb.ToString();
```

通过标准：五行全部符合 expect。注意此脚本会进/出 Play Mode（工具自动），若 `initial active=True` 说明 Start 时序问题——等待一帧后再读（在脚本开头 `yield return null` 需用 IEnumerator 版本）。

- [ ] **Step 2: MP4 最终视觉门（exec_runtime_script + record_game_view）**

同一次调用内驱动"关闭态 → 展开 → 完整界面 → 收起"，`record_game_view`：durationSeconds=6, fps=15, scale=0.75。

```csharp
using System.Collections;
using UnityEngine;

IEnumerator Run()
{
    var canvas = GameObject.Find("Canvas");
    Component sec = null;
    foreach (var c in canvas.GetComponents<Component>())
        if (c != null && c.GetType().Name == "SectUIController") { sec = c; break; }
    var toggle = sec.GetType().GetMethod("ToggleInventory");

    yield return new WaitForSeconds(1.0f);   // 关闭态（大厅场景+导航栏）
    toggle.Invoke(sec, null);               // 展开背包界面
    yield return new WaitForSeconds(2.5f);  // 纸面+角色面板+52格完整可见
    toggle.Invoke(sec, null);               // 收起
    yield return new WaitForSeconds(1.5f);
}
return Run();
```

- [ ] **Step 3: 通过标准**

- MP4 编码成功（返回路径、frames > 0）即视觉门通过
- 录像中确认：关闭态顶部无浮空格子；展开态纸面完整、角色面板在顶部、13×4 网格整齐、导航栏在最上层且卡片可见；收起恢复
- 若编码失败（技术性失败）→ 重录一次；画面主观不理想 → 向用户展示录像再定，不擅自迭代

- [ ] **Step 4: 收尾**

- 确认场景保存（`unity_scene ensure_scene_saved`）
- `unity_console get` 最终检查无遗留 error
- 向用户汇报 + 展示 MP4，请用户确认最终观感

---

## Self-Review 记录

- **Spec 覆盖**：spec §2 全部决策 → Task 1/2（AI 资产）、Task 4（布局+NavBg 上移修 bug）、Task 3（清理）；§3 布局数字 → Task 4 代码常量；§4 资产规格 → Task 1/2 prompt+管线；§5 清理 → Task 3；§6 验证 → Task 5/6；§7 回退 → 新文件名+旧资产不动（隐含于 Task 1/2 输出路径）；§8 范围外 → 无对应任务（正确）。无缺口。
- **占位符**：无 TBD/TODO；两处"不合格回退"均给了明确路径（重生成需先告知用户）。
- **类型/命名一致性**：InventoryBG_v2.png / NavBg_Ai.png / BagGridPanel / Slot_r{r}_c{c} 在 Task 1/2/4/5/6 中一致；网格数学 (c-6)×128 / (r-1.5)×128 与验证抽样值 (-768,192)/(768,-192) 自洽。
