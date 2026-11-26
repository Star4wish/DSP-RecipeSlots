# Changelog

Bilingual: each version lists the English summary first, then the Chinese details.
双语说明：每个版本先列英文摘要，再列中文详情。

## 1.2.2


### English
- **"Cancel recipe" (X) button restored in the multi-row layout**: the vanilla button (`UIAssemblerWindow.resetButton`, prefab `produce/circle-back/stop-btn`) is hidden together with the product group that the multi-row layout replaced. A clone is now placed at the top-left corner of the ingredient area, its normally alpha-0 idle look is forced visible, and its click still calls the original `OnRecipeResetClick` (refund ingredients + clear recipe). Single-row recipes keep the vanilla button and hide the clone, so there is no duplicate.
- **Shutdown `NullReferenceException` guard**: `GameLogic.Update()` dereferences `mainPlayer` on its first line; when a mod's load/quit flow ticks it before the player exists, the game showed a red `NullReferenceException ... GameLogic.Update (IL_0006)` error report. A first-priority prefix now skips only those not-ready frames (`data`/`mainPlayer`/`packageUtility` null); normal frames are untouched. Verified: no such error during runtime or shutdown.
- Dev builds: `newgame` is refused while a load is in progress (prevents a `GameLoader.FixedUpdate` race); new diagnostics `uireset`, `resetclick`, `uihit`, `uiclick`, `recipscan`, `tpto`.

### 中文
- **多行布局恢复「取消配方 X」按钮**：原版按钮（`UIAssemblerWindow.resetButton`，预制体路径 `produce/circle-back/stop-btn`）会随被替换的产物容器一起隐藏。现在克隆一份放在**原料区左上角**，把原版「平时透明、悬停淡入」改为常显，点击仍调用原版 `OnRecipeResetClick`（退回原料 + 清空配方）；单行配方仍用原版按钮并隐藏克隆，不会重复。
- **退出时 `NullReferenceException` 防御**：`GameLogic.Update()` 首行解引用 `mainPlayer`；某些 MOD 的读档/退出流程会在玩家尚未创建时调用一帧，导致红色错误框 `NullReferenceException ... GameLogic.Update (IL_0006)`。现加最高优先级前缀，仅在 `data`/`mainPlayer`/`packageUtility` 为空时跳过该帧，正常帧行为不变。已验证运行期与退出均无此报错。
- 开发构建：加载过程中 `newgame` 会被拒绝（避免 `GameLoader.FixedUpdate` 竞争）；新增诊断命令 `uireset`、`resetclick`、`uihit`、`uiclick`、`recipscan`、`tpto`。


## 1.2.1

### English
- **Multi-row layout reflowed**: ingredients and products now wrap only after 7 per row (was 6 / 4). Recipe slots shrink from 64 px to 56 px with a 58 px row pitch, and the gaps between the ingredient rows, the speed area, the product rows and the inventory are tightened, so a multi-row window is much shorter: 8-in/7-out 681 → 570 px, 12-in/12-out 681 → 628 px (vanilla single-row window stays 420 px).
- **Window height follows the player inventory again**: height is computed as content bottom + inventory title + the actual `rowCount * 50`, so every inventory row stays inside the window (the old code assumed a fixed 204 px grid and clipped rows beyond 4).
- A 7-ingredient / ≤4-product recipe stays in the vanilla single-row layout — 7 ingredients no longer wrap.
- **Third-party UI coexistence**: foreign `(Clone)` child slots that ProjectOrbitalRing injects into the vanilla product-group template are stripped when cloning our own slots; AssemblerUI's per-item speed labels are hidden in multi-row mode (they are positioned for the vanilla 2-slot layout and floated outside the window) and restored in single-row mode.
- Dev-channel additions (dev builds only): `uiscreen`, `uipatches`, `uikids` diagnostics.

### 中文
- **多行布局重排**：原料/产物改为满 7 个才换行（原 6 / 4）；配方槽 64 → 56 px、行距 58 px，原料行/速度区/产物行/物品栏之间间距收紧，多行窗口明显变矮：8 原料 7 产物 681 → 570 px，12 原料 12 产物 681 → 628 px（原版单行窗口仍为 420 px）。
- **窗口高度重新跟随物品栏行数**：按「内容底 + 物品清单标题 + 实际 rowCount×50」计算，物品栏超过 4 行也不会被窗口底边裁掉（旧实现写死 204 px 网格高度）。
- 7 原料 / ≤4 产物的配方继续走原版单行布局，7 个原料不再换行。
- **第三方 UI 共存**：克隆自建槽位时剔除 ProjectOrbitalRing 注入到产物槽模板里的 `(Clone)` 子物体；多行模式隐藏 AssemblerUI 的逐物品速度标签（按原版 2 槽位定位，会飘到窗口外），回到单行时恢复。
- 开发通道（仅 dev 构建）新增 `uiscreen`、`uipatches`、`uikids` 排查命令。

## 1.2.0

### English
- **M2 – full looping of the input needs chain**: the `needs` array is now filled to the recipe's ingredient count (no more 6-slot rotation), so there is no advertising duty-cycle loss. All hard-coded 6-item matches were rewritten to length-based loops: `CargoPath.TryPickItem` / `TryPickItemAtRear` / `CanPickItemFromRear`, `StorageComponent.TakeTailItems`, `StationComponent.TakeItem`, and the assembler/lab branches of `PlanetFactory.PickFrom(int/uint)`. Lab normal recipes got the same treatment. `Core.EnableNeedsLoop=false` falls back to the 1.1 rotation.
- Verified: a 12-ingredient recipe receives all 12 ingredients from belts / storage / machines and runs 10 consecutive production cycles (12 products each); 8-ingredient and vanilla recipes still work.
- **Release build and packaging**: new `SHIP` compile symbol. `build.ps1 -Release` produces a release DLL with test content, debug channel, test power patch and `Test.*` configs stripped. `build.ps1 -Release -Package` produces a r2modman / Thunderstore compatible `dist/DSP_RecipeSlots-<version>.zip` plus `.sha256`. Dev builds (`bin/Dev`) keep everything for in-game verification.

### 中文
- **M2：输入需求「全循环化」，去掉 6 格轮转的占空比损失**：`needs` 按配方原料数全长填充，取料匹配全部按长度循环（传送带 3 处、箱子、物流站、机器直送 2 个重载），研究站普通配方同步处理；`Core.EnableNeedsLoop=false` 可回退 1.1 轮转。
- 实测：12 原料配方从传送带/箱子/机器四种来源全部取到第 7–12 种原料，连续生产 10 轮（每轮 12 产物）；8 输入与原版配方回归正常。
- **发布构建与打包**：新增 `SHIP` 编译开关；`build.ps1 -Release` 产出剔除测试内容/调试通道/测试供电/`Test.*` 的发布版 DLL；`build.ps1 -Release -Package` 一键产出 r2modman / Thunderstore 兼容 zip 与 `.sha256`。开发构建（`bin/Dev`）保留全部调试能力。

## 1.1.4

### English
- Product row capacity changed from 4 to 6; the multi-row layout still triggers when ingredients > 6 or products > 4.
- Added 12-ingredient / 12-product stress-test recipes (7007–7009) and verified them in game.

### 中文
- 产物每行容量 4 → 6；多行 UI 触发条件保持「原料 > 6 或 产物 > 4」。
- 新增 12 原料 / 12 产物压力测试配方（7007–7009）并实机验证。

## 1.1.3

### English
- Production window: ingredients now on top (6/row), products below (4/row at the time), speed display centered in between with downward arrows; counts moved to the bottom-right inside each circle; fixed slot coordinate offset (`produce` container offset -100) that overlapped the inventory panel.
- Lab window: 3×3 matrix grid is now auto-scaled and centered inside the left area of the (unchanged) vanilla window, so the right-side hints no longer overlap.

### 中文
- 生产窗口：原料在上（6/行）、产物在下、速度居中且箭头朝下；数量统一到圆圈右下角内侧；修正槽位坐标偏移（`produce` 容器 -100）导致的清单重叠。
- 研究站：3×3 网格在窗口左侧可用区域内自动缩放居中，右侧提示不再重叠。

## 1.1.1 / 1.1.2

### English
- 1.1.1: first version of the production-window multi-row layout (ingredients 6/row, products 4/row, auto window height).
- 1.1.2: lab matrix slots fixed to a 3×3 grid (9 cells, 2 reserved for future 9-matrix tech); fixed crashes when a lab produced an extra matrix, non-contiguous extra matrix ID mapping, and tech `Level/MaxLevel` convention.

### 中文
- 1.1.1：生产窗口多行布局初版（原料 6/行、产物 4/行、窗口自动加高）。
- 1.1.2：研究站矩阵槽固定 3×3 九格（预留 2 格）；修复研究站产出额外矩阵越界崩溃、非连续矩阵 ID 映射、科技 `Level/MaxLevel` 约定等问题。

## 1.1.0

### English
- Added multi-row layout for production windows and a GenesisBook-inspired 3×3 lab matrix grid.
- Added dev test content: pink native matrix item 6010, leaf item 6101, recipes 7003–7006 and test tech 7100.

### 中文
- 新增生产窗口多行布局、参考创世之书的研究站 3×3 九宫格。
- 新增开发测试内容：粉色原生矩阵 6010、树叶 6101、配方 7003–7006、测试科技 7100。

## 1.0.0

### English
- First usable release: >6 ingredients, >6 products, labs with >6 matrices, tech-tree/Tooltip support for >6 costs, vanilla rollback verified, Nebula load-level coexistence verified.

### 中文
- 首个可用版本：>6 输入、>6 产物、研究站 >6 矩阵、科技树/tooltip >6 消耗、原版回退与 Nebula 加载级共存均已验证。
